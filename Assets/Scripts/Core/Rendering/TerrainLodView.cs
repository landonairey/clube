using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Core
{
    /// <summary>
    /// The coarse voxel terrain (P7c): rings of chunks past the world's own, each level's voxels
    /// twice the size of the one inside it, out to kilometres (<see cref="TerrainLodBuilder"/>). They
    /// are generated and meshed like the world's chunks and drawn in the same material, so the land
    /// looks the same all the way out and only its detail steps down. Each level leaves out, per
    /// pixel, what the level inside it draws (the first level: every column the world's chunks cover,
    /// <see cref="ChunkCoverageMask"/>), and sits a fraction of its voxel lower, so a finer level
    /// always wins where both draw and its edge never shows a gap. Needs a heightfield generator.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TerrainLodView : MonoBehaviour, TerrainLodBuilder.ISink
    {
        public const int MaxLevels = 10;

        // Frames between copies of the surface material's properties onto the levels' own.
        private const int MaterialCopyFrames = 30;

        private static readonly int LodClipId = Shader.PropertyToID("_LodClip");
        private static readonly int LodHoleId = Shader.PropertyToID("_LodHole");
        private static readonly int LodHoleMarginId = Shader.PropertyToID("_LodHoleMargin");
        private static readonly int LodCoverageId = Shader.PropertyToID("_LodCoverage");
        private static readonly int LodCoverageMaskId = Shader.PropertyToID("_LodCoverageMask");
        private static readonly int LodTintId = Shader.PropertyToID("_LodTint");

        [Tooltip("The world whose terrain this continues. Defaults to the WorldView on this object.")]
        [SerializeField]
        private WorldView worldView;

        [Tooltip("Draw the coarse terrain.")]
        [SerializeField]
        private bool show = true;

        [Tooltip("Levels of coarser voxels: each doubles the voxel size and the reach.")]
        [SerializeField, Range(1, MaxLevels)]
        private int levels = 8;

        [Tooltip("Chunks each way from a level's centre. More: each step of detail further out, more chunks.")]
        [SerializeField, Range(4, 16)]
        private int halfChunks = 8;

        [Tooltip("How far each level sits below the true ground, in its own voxels, so the finer level wins where both draw.")]
        [SerializeField, Range(0f, 0.5f)]
        private float sink = 0.15f;

        [Tooltip("Metres the first level runs on under the world's own chunks, so no gap shows at their edge.")]
        [SerializeField, Range(0f, 2f)]
        private float underlap = 0.6f;

        [Tooltip("Tint each level its own colour, to see the rings.")]
        [SerializeField]
        private bool tintLevels;

        private TerrainLodBuilder builder;
        private ITerrainGenerator builtFor;
        private string builtSettings;
        private ChunkCoverageMask coverage;
        private Transform[] levelRoots;
        private Material[] levelMaterials;
        private Material copiedFrom;
        private int nextCopyFrame;
        private ClipState[] lastClips;
        private readonly Dictionary<(int, Vector3Int), MeshRenderer> shown = new Dictionary<(int, Vector3Int), MeshRenderer>();
        private readonly Stack<MeshRenderer> pool = new Stack<MeshRenderer>();

        public bool Show
        {
            get => show;
            set => show = value;
        }

        public int Levels
        {
            get => levels;
            set => levels = Mathf.Clamp(value, 1, MaxLevels);
        }

        public int HalfChunks
        {
            get => halfChunks;
            set => halfChunks = Mathf.Clamp(value, 4, 16);
        }

        public bool TintLevels
        {
            get => tintLevels;
            set => tintLevels = value;
        }

        /// <summary>Levels in use, 0 when nothing is drawn.</summary>
        public int LevelCount => builder?.LevelCount ?? 0;

        /// <summary>Main-thread milliseconds of the last frame's update (taking finished jobs, starting new ones, showing chunks).</summary>
        public float LastUpdateMilliseconds { get; private set; }

        /// <summary>Why nothing is drawn, or null.</summary>
        public string Problem { get; private set; }

        /// <summary>The level's voxel size in metres (level 0 = the first coarse level).</summary>
        public float VoxelSizeOf(int i)
        {
            return builder != null ? builder.GridOf(i).VoxelSize : 0f;
        }

        /// <summary>Chunks the level draws and chunks it still waits for.</summary>
        public void CountOf(int i, out int drawn, out int waiting)
        {
            drawn = waiting = 0;
            builder?.CountOf(i, out drawn, out waiting);
        }

        /// <summary>
        /// How far the coarse terrain reaches from the focus when it settles, in metres: what the
        /// far terrain starts beyond. 0 when it draws nothing.
        /// </summary>
        public float PlannedReach
        {
            get
            {
                if (!isActiveAndEnabled || !show || worldView == null || !worldView.IsReady || !(worldView.Generator is HeightfieldGenerator))
                {
                    return 0f;
                }
                float chunkWidth = worldView.Config.ChunkSize.x * worldView.Config.VoxelSize;
                return halfChunks * chunkWidth * TerrainLodRing.ScaleOf(levels);
            }
        }

        /// <summary>The outermost square shown so far (x, z in world-view space), the far terrain's inner edge.</summary>
        public bool TryGetOuterArea(out Rect area, out float voxelSize)
        {
            area = default;
            voxelSize = 0f;
            if (builder == null)
            {
                return false;
            }
            for (int i = builder.LevelCount - 1; i >= 0; i--)
            {
                if (builder.TryGetShown(i, out TerrainLodRing ring))
                {
                    area = ring.Area;
                    voxelSize = builder.GridOf(i).VoxelSize;
                    return true;
                }
            }
            return false;
        }

        private void Awake()
        {
            if (worldView == null)
            {
                worldView = GetComponent<WorldView>();
            }
        }

        private void OnDisable()
        {
            DropBuilder();
            coverage?.Dispose();
            coverage = null;
        }

        private void OnDestroy()
        {
            DropBuilder();
            coverage?.Dispose();
            coverage = null;
        }

        private void LateUpdate()
        {
            if (!show || worldView == null || !worldView.IsReady || worldView.Focus == null)
            {
                Problem = null;
                DropBuilder();
                return;
            }
            if (!(worldView.Generator is HeightfieldGenerator heightfield))
            {
                Problem = "The coarse terrain needs a heightfield generator";
                DropBuilder();
                return;
            }
            if (worldView.SurfaceMaterial == null)
            {
                Problem = "The world has no chunk material";
                DropBuilder();
                return;
            }
            Problem = null;

            WorldConfig config = worldView.Config;
            string settings = $"{levels}|{halfChunks}|{config.ChunkSize}|{config.VoxelSize}|{config.MeshSettings.MaterialDisplay}";
            if (builder == null || builtFor != worldView.Generator || builtSettings != settings)
            {
                DropBuilder();
                CreateBuilder(heightfield, config);
                builtFor = worldView.Generator;
                builtSettings = settings;
            }

            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            builder.Update(worldView.transform.InverseTransformPoint(worldView.Focus.position));
            UpdateClips();
            LastUpdateMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000f / System.Diagnostics.Stopwatch.Frequency;
        }

        void TerrainLodBuilder.ISink.Show(int level, Vector3Int coord, Mesh mesh)
        {
            int i = level - builder.FirstLevel;
            MeshRenderer chunk = pool.Count > 0 ? pool.Pop() : CreateRenderer();
            chunk.transform.SetParent(levelRoots[i], false);
            chunk.transform.localPosition = builder.GridOf(i).ChunkOrigin(coord);
            chunk.GetComponent<MeshFilter>().sharedMesh = mesh;
            chunk.sharedMaterial = levelMaterials[i];
            chunk.gameObject.SetActive(true);
            shown[(level, coord)] = chunk;
        }

        void TerrainLodBuilder.ISink.Hide(int level, Vector3Int coord)
        {
            if (shown.TryGetValue((level, coord), out MeshRenderer chunk))
            {
                shown.Remove((level, coord));
                chunk.GetComponent<MeshFilter>().sharedMesh = null;
                chunk.gameObject.SetActive(false);
                pool.Push(chunk);
            }
        }

        private void CreateBuilder(HeightfieldGenerator heightfield, WorldConfig config)
        {
            World world = worldView.World;
            ChunkFillSettings fill = ChunkGenerator.SettingsFor(config.Terrain.Layers, worldView.Ores, world.PreferredFormat, config.Terrain.StartArea);
            // No voxel trees in the coarse levels: past the world's chunks trees are meshes (DistantTreeView, GL162).
            builder = new TerrainLodBuilder(
                heightfield, world.Grid, fill, config.MeshSettings, world.StorageFactory,
                1, levels, halfChunks, this);

            levelRoots = new Transform[builder.LevelCount];
            lastClips = new ClipState[builder.LevelCount];
            levelMaterials = new Material[builder.LevelCount];
            copiedFrom = null;
            for (int i = 0; i < builder.LevelCount; i++)
            {
                var root = new GameObject($"Coarse terrain level {builder.FirstLevel + i}").transform;
                root.SetParent(worldView.transform, false);
                // A fraction of a voxel low, so the finer level inside wins where both draw.
                root.localPosition = new Vector3(0f, -sink * builder.GridOf(i).VoxelSize, 0f);
                levelRoots[i] = root;
                levelMaterials[i] = new Material(worldView.SurfaceMaterial) { name = $"Coarse terrain level {builder.FirstLevel + i}" };
            }
        }

        // Each level leaves out what the level inside it draws, as of this frame, through its own
        // copy of the surface material (one change per level, and the SRP batcher still batches).
        private void UpdateClips()
        {
            coverage ??= new ChunkCoverageMask(worldView, WorldView.MaxRenderDistance);
            Vector3Int focusChunk = worldView.FocusChunk;
            coverage.Update(new Vector2Int(focusChunk.x, focusChunk.z));
            WorldConfig config = worldView.Config;
            float chunkWidth = config.ChunkSize.x * config.VoxelSize;
            Vector3 origin = worldView.transform.position;
            var coverageArea = new Vector4(
                origin.x + coverage.Origin.x * chunkWidth, origin.z + coverage.Origin.y * chunkWidth, chunkWidth, coverage.Size);

            // The surface material's own properties (texture array, the registry's hidden-ore remap)
            // can change at run time: copy them over now and then, and whenever it's another material.
            Material surface = worldView.SurfaceMaterial;
            bool recopy = surface != copiedFrom || Time.frameCount >= nextCopyFrame;
            if (recopy)
            {
                copiedFrom = surface;
                nextCopyFrame = Time.frameCount + MaterialCopyFrames;
            }

            for (int i = 0; i < builder.LevelCount; i++)
            {
                // Always the world's own chunks, wherever they cover their columns; past the first
                // level also the finer level's square, less one of its voxels, so it runs on over this
                // level that far.
                Vector4 hole = Vector4.zero;
                float mode = 2f;
                if (i > 0 && builder.TryGetShown(i - 1, out TerrainLodRing finer))
                {
                    float overlap = builder.GridOf(i - 1).VoxelSize;
                    Rect area = finer.Area;
                    mode = 3f;
                    hole = new Vector4(
                        origin.x + area.xMin + overlap, origin.z + area.yMin + overlap,
                        origin.x + area.xMax - overlap, origin.z + area.yMax - overlap);
                }
                Color tint = tintLevels ? Color.HSVToRGB(i / (float)builder.LevelCount, 0.6f, 1f) : Color.white;
                var clipState = new ClipState { Mode = mode, Hole = hole, Coverage = coverageArea, Tint = tint };
                if (!recopy && clipState.Equals(lastClips[i]))
                {
                    continue;
                }
                lastClips[i] = clipState;

                Material material = levelMaterials[i];
                if (recopy)
                {
                    material.CopyPropertiesFromMaterial(surface);
                }
                material.SetFloat(LodClipId, mode);
                material.SetVector(LodHoleId, hole);
                material.SetFloat(LodHoleMarginId, underlap);
                material.SetTexture(LodCoverageMaskId, coverage.Texture);
                material.SetVector(LodCoverageId, coverageArea);
                material.SetColor(LodTintId, tint);
            }
        }

        // What a level's material was last given, to set it only when it changes.
        private struct ClipState
        {
            public float Mode;
            public Vector4 Hole;
            public Vector4 Coverage;
            public Color Tint;

            public bool Equals(ClipState other)
            {
                return Mode == other.Mode && Hole == other.Hole && Coverage == other.Coverage && Tint == other.Tint;
            }
        }

        private MeshRenderer CreateRenderer()
        {
            var chunk = new GameObject("Coarse chunk", typeof(MeshFilter), typeof(MeshRenderer));
            MeshRenderer meshRenderer = chunk.GetComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = true;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return meshRenderer;
        }

        private void DropBuilder()
        {
            builder?.Dispose();
            builder = null;
            builtFor = null;
            foreach (MeshRenderer chunk in shown.Values)
            {
                if (chunk != null)
                {
                    Destroy(chunk.gameObject);
                }
            }
            shown.Clear();
            while (pool.Count > 0)
            {
                MeshRenderer chunk = pool.Pop();
                if (chunk != null)
                {
                    Destroy(chunk.gameObject);
                }
            }
            if (levelRoots != null)
            {
                foreach (Transform root in levelRoots)
                {
                    if (root != null)
                    {
                        Destroy(root.gameObject);
                    }
                }
                levelRoots = null;
            }
            if (levelMaterials != null)
            {
                foreach (Material material in levelMaterials)
                {
                    Destroy(material);
                }
                levelMaterials = null;
            }
        }
    }
}
