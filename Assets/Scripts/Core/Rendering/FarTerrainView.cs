using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Core
{
    /// <summary>
    /// Draws the land beyond the voxel chunks out to tens of kilometres (P7): a geometry clipmap
    /// of the world's heightfield (<see cref="FarTerrainBuilder"/>), one renderer per level, in the
    /// <c>Clube/Far Terrain</c> shader. Over every chunk column the <see cref="WorldView"/> draws in
    /// full (<see cref="ChunkCoverageMask"/>) the shader leaves the far terrain out, so caves and
    /// edits show as the chunks have them, and anywhere chunks are still streaming in the far terrain
    /// fills the gap. It also pushes the camera's far plane out to reach the horizon. Needs a
    /// heightfield generator; with a 3D one it shows nothing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FarTerrainView : MonoBehaviour
    {
        public const float MinRange = 1000f;
        public const float MaxRange = 60000f;

        private static readonly int CoverageMaskId = Shader.PropertyToID("_CoverageMask");
        private static readonly int CoverageId = Shader.PropertyToID("_Coverage");
        private static readonly int LevelTintId = Shader.PropertyToID("_LevelTint");
        private static readonly int NearRectId = Shader.PropertyToID("_NearRect");

        [Tooltip("The world whose terrain continues into the distance. Defaults to the WorldView on this object.")]
        [SerializeField]
        private WorldView worldView;

        [Tooltip("The coarse voxel terrain (P7c) this starts beyond. Defaults to the TerrainLodView on this object; none: this starts at the world's chunks.")]
        [SerializeField]
        private TerrainLodView coarseTerrain;

        [Tooltip("The Clube/Far Terrain material.")]
        [SerializeField]
        private Material material;

        [Tooltip("Draw the far terrain.")]
        [SerializeField]
        private bool show = true;

        [Tooltip("How far it reaches from the focus, in metres. Each doubling adds one level.")]
        [SerializeField, Range(MinRange, MaxRange)]
        private float range = 30000f;

        [Tooltip("Metres between the finest level's vertices; each level out doubles it.")]
        [SerializeField, Range(0.5f, 8f)]
        private float baseSpacing = 2f;

        [Tooltip("Quads per side of each level (a multiple of 4). More: finer at every distance, more vertices.")]
        [SerializeField, Range(32, 252)]
        private int quads = 128;

        [Tooltip("Metres the far terrain sits below the true ground, so the chunks win where both draw.")]
        [SerializeField, Range(0f, 2f)]
        private float sink = 0.3f;

        [Tooltip("Tint each level its own colour, to see the rings.")]
        [SerializeField]
        private bool tintLevels;

        [Tooltip("Push the main camera's far plane out to the far terrain's reach.")]
        [SerializeField]
        private bool extendCameraFarPlane = true;

        private FarTerrainBuilder builder;
        private ITerrainGenerator builtFor;
        private int builtSettings;
        private Mesh[] meshes;
        private MeshRenderer[] renderers;
        private Material runtimeMaterial;
        private MaterialPropertyBlock block;
        private ChunkCoverageMask coverage;

        public bool Show
        {
            get => show;
            set => show = value;
        }

        public float Range
        {
            get => range;
            set => range = Mathf.Clamp(value, MinRange, MaxRange);
        }

        public float BaseSpacing
        {
            get => baseSpacing;
            set => baseSpacing = Mathf.Clamp(value, 0.5f, 8f);
        }

        public int Quads
        {
            get => quads;
            set => quads = Mathf.Clamp(value / 4 * 4, 32, 252);
        }

        public bool TintLevels
        {
            get => tintLevels;
            set => tintLevels = value;
        }

        /// <summary>Levels in use, 0 when nothing is drawn.</summary>
        public int LevelCount => builder?.LevelCount ?? 0;

        /// <summary>How far the coarsest level reaches, in metres.</summary>
        public float Reach => builder?.Reach ?? 0f;

        /// <summary>Vertices across all levels.</summary>
        public int VertexCount => builder == null ? 0 : builder.LevelCount * (builder.Quads + 1) * (builder.Quads + 1);

        /// <summary>Worker time of the last rebuild, start to apply, and how many levels it rebuilt.</summary>
        public float LastBuildMilliseconds => builder?.LastBatchMilliseconds ?? 0f;

        public int LastBuildLevels => builder?.LastBatchLevels ?? 0;

        /// <summary>Why nothing is drawn, or null.</summary>
        public string Problem { get; private set; }

        /// <summary>Main-thread time of the last frame's update.</summary>
        public float LastUpdateMilliseconds { get; private set; }

        private void Awake()
        {
            if (worldView == null)
            {
                worldView = GetComponent<WorldView>();
            }
            if (coarseTerrain == null)
            {
                coarseTerrain = GetComponent<TerrainLodView>();
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
            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
            }
        }

        private void LateUpdate()
        {
            if (!show || worldView == null || !worldView.IsReady || worldView.Focus == null || material == null)
            {
                Problem = material == null ? "No far terrain material" : null;
                DropBuilder();
                return;
            }
            if (!(worldView.Generator is HeightfieldGenerator heightfield))
            {
                Problem = "The far terrain needs a heightfield generator";
                DropBuilder();
                return;
            }
            Problem = null;

            // Past the coarse terrain: its reach with room for the two squares' centres to differ, and sunk by half its coarsest voxel.
            float coarseReach = coarseTerrain != null ? coarseTerrain.PlannedReach : 0f;
            int firstLevel = coarseReach > 0f ? FirstLevelBeyond(coarseReach * 1.25f) : 0;
            float levelSink = coarseReach > 0f ? Mathf.Max(sink, coarseReach / (coarseTerrain.HalfChunks * worldView.Config.ChunkSize.x) * 0.5f) : sink;
            int settings = System.HashCode.Combine(range, baseSpacing, quads, levelSink, firstLevel);
            if (builder == null || builtFor != worldView.Generator || builtSettings != settings)
            {
                DropBuilder();
                builder = new FarTerrainBuilder(heightfield, range, baseSpacing, quads, levelSink, firstLevel);
                builtFor = worldView.Generator;
                builtSettings = settings;
                CreateLevels(builder.LevelCount);
            }

            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            Vector3 focus = worldView.transform.InverseTransformPoint(worldView.Focus.position);
            builder.Update(focus, meshes);
            UpdateLevels();
            UpdateNearArea();
            LastUpdateMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000f / System.Diagnostics.Stopwatch.Frequency;
            if (extendCameraFarPlane && Camera.main != null)
            {
                float far = builder.Reach * 1.1f;
                if (Camera.main.farClipPlane < far)
                {
                    Camera.main.farClipPlane = far;
                }
            }
        }

        // One child per level, drawn only once it has been built.
        private void CreateLevels(int count)
        {
            if (runtimeMaterial == null)
            {
                runtimeMaterial = new Material(material) { name = material.name + " (runtime)" };
            }
            block ??= new MaterialPropertyBlock();
            meshes = new Mesh[count];
            renderers = new MeshRenderer[count];
            for (int i = 0; i < count; i++)
            {
                var level = new GameObject($"Far terrain level {i}");
                level.transform.SetParent(worldView.transform, false);
                meshes[i] = new Mesh { name = level.name };
                meshes[i].MarkDynamic();
                level.AddComponent<MeshFilter>().sharedMesh = meshes[i];
                MeshRenderer meshRenderer = level.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = runtimeMaterial;
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                meshRenderer.lightProbeUsage = LightProbeUsage.Off;
                meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                meshRenderer.enabled = false;
                renderers[i] = meshRenderer;
            }
        }

        private void UpdateLevels()
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = builder.TryGetShown(i, out _);
                block.SetColor(LevelTintId, tintLevels ? Color.HSVToRGB(i / (float)renderers.Length, 0.6f, 1f) : Color.white);
                renderers[i].SetPropertyBlock(block);
            }
        }

        // The finest clipmap level reaching at least that far from its centre.
        private int FirstLevelBeyond(float reach)
        {
            int level = 0;
            while (FarTerrainGrid.Around(Vector2.zero, level, baseSpacing, quads).HalfExtent < reach && level < 30)
            {
                level++;
            }
            return level;
        }

        // Where nearer terrain draws: the coarse terrain's outer square, less one of its voxels so
        // it runs on over this a little; or, without it, the columns the chunks cover in full.
        private void UpdateNearArea()
        {
            Vector3 viewOrigin = worldView.transform.position;
            if (coarseTerrain != null && coarseTerrain.TryGetOuterArea(out Rect area, out float voxelSize))
            {
                runtimeMaterial.SetVector(NearRectId, new Vector4(
                    viewOrigin.x + area.xMin + voxelSize, viewOrigin.z + area.yMin + voxelSize,
                    viewOrigin.x + area.xMax - voxelSize, viewOrigin.z + area.yMax - voxelSize));
                return;
            }
            runtimeMaterial.SetVector(NearRectId, Vector4.zero);

            coverage ??= new ChunkCoverageMask(worldView, WorldView.MaxRenderDistance);
            Vector3Int focusChunk = worldView.FocusChunk;
            coverage.Update(new Vector2Int(focusChunk.x, focusChunk.z));
            WorldConfig config = worldView.Config;
            float chunkWidth = config.ChunkSize.x * config.VoxelSize;
            Vector3 origin = worldView.transform.position;
            runtimeMaterial.SetTexture(CoverageMaskId, coverage.Texture);
            runtimeMaterial.SetVector(CoverageId, new Vector4(
                origin.x + coverage.Origin.x * chunkWidth, origin.z + coverage.Origin.y * chunkWidth, chunkWidth, coverage.Size));
        }

        private void DropBuilder()
        {
            builder?.Dispose();
            builder = null;
            builtFor = null;
            if (renderers != null)
            {
                foreach (MeshRenderer meshRenderer in renderers)
                {
                    if (meshRenderer != null)
                    {
                        Destroy(meshRenderer.gameObject);
                    }
                }
                renderers = null;
            }
            if (meshes != null)
            {
                foreach (Mesh mesh in meshes)
                {
                    if (mesh != null)
                    {
                        Destroy(mesh);
                    }
                }
                meshes = null;
            }
        }
    }
}
