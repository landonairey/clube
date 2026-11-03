using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Core
{
    /// <summary>
    /// Trees past the world's own chunks (GL162), out to a few kilometres over the coarse and far
    /// terrain: the same trees the voxel world grows (<see cref="DistantTreeBuilder"/>), drawn as
    /// low-poly meshes (<see cref="DistantTreeMeshes"/>), instanced, birch or maple in the voxel trees' colours.
    /// Four levels of detail step down with distance (GL258), each made from the same few real trees of each kind
    /// (<see cref="Variants"/>, built by <see cref="TreeShape"/>), so a tree keeps its variant's shape as it steps
    /// down: every branch and leaf ball just past the voxel trees, then a blob per leaf cluster, then a crown of a few
    /// blobs, then one. Neighbouring levels cross-fade over <see cref="fadeWidth"/> metres, dithered per pixel, so no
    /// level ends in a line. The shader drops any tree standing on a chunk column the world draws in full
    /// (<see cref="ChunkCoverageMask"/>), where its voxel tree is. Needs a heightfield generator and trees in the
    /// terrain settings.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DistantTreeView : MonoBehaviour
    {
        public const float MinRange = 500f;
        public const float MaxRange = 8000f;

        /// <summary>Real trees of each kind the meshes are made from; each distant tree takes one by its hash.</summary>
        public const int Variants = 6;

        // Instances per draw call: the most RenderMeshInstanced takes from an array.
        private const int BatchSize = 1023;
        private const int Kinds = 2;
        private const int Levels = DistantTreeMeshes.Levels;

        private static readonly int CoverageMaskId = Shader.PropertyToID("_CoverageMask");
        private static readonly int CoverageId = Shader.PropertyToID("_Coverage");
        private static readonly int FadeInId = Shader.PropertyToID("_FadeIn");
        private static readonly int FadeOutId = Shader.PropertyToID("_FadeOut");

        [Tooltip("The world whose trees these are. Defaults to the WorldView on this object.")]
        [SerializeField]
        private WorldView worldView;

        [Tooltip("The Clube/Distant Trees material (GPU instancing on).")]
        [SerializeField]
        private Material material;

        [Tooltip("Draw the distant trees.")]
        [SerializeField]
        private bool show = true;

        [Tooltip("How far from the focus trees are drawn, in metres.")]
        [SerializeField, Range(MinRange, MaxRange)]
        private float range = 3000f;

        [Tooltip("Where each level of detail ends, in metres from the focus (GL258): every branch and leaf ball (x), a blob per leaf cluster (y), a crown of a few blobs (z); one blob beyond.")]
        [SerializeField]
        private Vector3 levelEnds = new Vector3(220f, 520f, 1100f);

        [Tooltip("Metres over which neighbouring levels cross-fade.")]
        [SerializeField, Range(0f, 200f)]
        private float fadeWidth = 60f;

        [Tooltip("Bark and leaf colours (sRGB): the voxel trees' textures, on average.")]
        [SerializeField]
        private Color birchBark = new Color(0.65f, 0.63f, 0.57f);

        [SerializeField]
        private Color birchLeaves = new Color(0.37f, 0.47f, 0.31f);

        [SerializeField]
        private Color mapleBark = new Color(0.38f, 0.27f, 0.18f);

        [SerializeField]
        private Color mapleLeaves = new Color(0.61f, 0.45f, 0.25f);

        private DistantTreeBuilder builder;
        private ITerrainGenerator builtFor;
        private float builtRange;
        private Material runtimeMaterial;
        private ChunkCoverageMask coverage;
        // Per kind, variant and level: (kind * Variants + variant) * Levels + level.
        private Mesh[] meshes;
        private (Color, Color, Color, Color) builtColors;
        private (ITerrainGenerator, TreeGeneration, int) builtShapes;
        private readonly MaterialPropertyBlock[] levelProperties = new MaterialPropertyBlock[Levels];
        private int syncedVersion = -1;
        private int drawnFrame = -1;
        // What the last collect saw: the camera's pose and the tiles. While they're unchanged its lists are drawn again.
        private Vector3 collectedAt;
        private Quaternion collectedTurn;
        private int collectedVersion = -1;

        // Each ready tile's trees as world matrices, made once when the tile arrives: a frame only
        // copies the tiles in view into the draw arrays.
        private readonly Dictionary<DistantTreeBuilder.Tile, TileDraw> tileDraws = new Dictionary<DistantTreeBuilder.Tile, TileDraw>();
        private readonly HashSet<DistantTreeBuilder.Tile> liveTiles = new HashSet<DistantTreeBuilder.Tile>();
        private readonly List<DistantTreeBuilder.Tile> goneTiles = new List<DistantTreeBuilder.Tile>();
        private readonly Plane[] frustum = new Plane[6];

        // Per mesh (as meshes): this frame's trees, and how many.
        private readonly Matrix4x4[][] arrays = new Matrix4x4[Kinds * Variants * Levels][];
        private readonly int[] counts = new int[Kinds * Variants * Levels];

        private sealed class TileDraw
        {
            // Per kind and variant: kind * Variants + variant.
            public Matrix4x4[][] Trees;
            public int Count;
            public Bounds Bounds;
        }

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

        /// <summary>Where each level of detail ends, metres from the focus: full trees, leaf clusters, crowns (each past the last).</summary>
        public Vector3 LevelEnds
        {
            get => levelEnds;
            set
            {
                float full = Mathf.Clamp(value.x, 50f, 2000f);
                float clusters = Mathf.Clamp(value.y, full + 20f, 4000f);
                levelEnds = new Vector3(full, clusters, Mathf.Clamp(value.z, clusters + 20f, MaxRange));
            }
        }

        /// <summary>Metres over which neighbouring levels cross-fade.</summary>
        public float FadeWidth
        {
            get => fadeWidth;
            set => fadeWidth = Mathf.Clamp(value, 0f, 200f);
        }

        /// <summary>Trees placed in reach, in view or not.</summary>
        public int TreeCount { get; private set; }

        /// <summary>Trees drawn last frame: those in the camera's view (before the shader drops the ones the world grows itself).</summary>
        public int DrawnCount { get; private set; }

        /// <summary>Tiles still waiting for their trees.</summary>
        public int PendingTiles => builder?.Pending ?? 0;

        /// <summary>Why nothing is drawn, or null.</summary>
        public string Problem { get; private set; }

        /// <summary>Main-thread time of the last frame's update, drawing included.</summary>
        public float LastUpdateMilliseconds { get; private set; }

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
            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
            }
            DestroyMeshes();
        }

        private void LateUpdate()
        {
            if (!show || worldView == null || !worldView.IsReady || worldView.Focus == null)
            {
                Problem = material == null ? "No distant tree material" : null;
                DropBuilder();
                return;
            }
            if (material == null)
            {
                Problem = "No distant tree material";
                return;
            }
            if (!(worldView.Generator is HeightfieldGenerator heightfield))
            {
                Problem = "Distant trees need a heightfield generator";
                DropBuilder();
                return;
            }
            if (worldView.Trees == null)
            {
                Problem = "The terrain has no trees";
                DropBuilder();
                return;
            }
            Problem = null;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();

            if (builder == null || builtFor != worldView.Generator || builtRange != range)
            {
                DropBuilder();
                builder = new DistantTreeBuilder(heightfield, worldView.Trees, range);
                builtFor = worldView.Generator;
                builtRange = range;
            }
            EnsureMeshes();
            runtimeMaterial ??= new Material(material) { name = material.name + " (runtime)", enableInstancing = true };

            builder.Update(worldView.transform.InverseTransformPoint(worldView.Focus.position));
            if (builder.Version != syncedVersion)
            {
                SyncTiles();
            }
            UpdateCoverage();
            Draw();
            LastUpdateMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000f / System.Diagnostics.Stopwatch.Frequency;
        }

        // Matrices for the tiles that arrived, and none for the tiles that went.
        private void SyncTiles()
        {
            syncedVersion = builder.Version;
            liveTiles.Clear();
            foreach (DistantTreeBuilder.Tile tile in builder.Tiles)
            {
                if (!tile.Ready)
                {
                    continue;
                }
                liveTiles.Add(tile);
                if (!tileDraws.ContainsKey(tile))
                {
                    tileDraws.Add(tile, Build(tile));
                }
            }
            goneTiles.Clear();
            foreach (DistantTreeBuilder.Tile tile in tileDraws.Keys)
            {
                if (!liveTiles.Contains(tile))
                {
                    goneTiles.Add(tile);
                }
            }
            foreach (DistantTreeBuilder.Tile tile in goneTiles)
            {
                tileDraws.Remove(tile);
            }
            TreeCount = 0;
            foreach (TileDraw draw in tileDraws.Values)
            {
                TreeCount += draw.Count;
            }
        }

        // Which of its kind's variants a tree is drawn as: a hash of its own roll, apart from its turn and width.
        private static int VariantOf(DistantTree tree)
        {
            return Mathf.Min(Variants - 1, (int)(Mathf.Repeat(tree.Variation * 7.31f + 0.13f, 1f) * Variants));
        }

        // A tile's trees as world matrices by kind and variant, and the box round them for culling.
        private TileDraw Build(DistantTreeBuilder.Tile tile)
        {
            Matrix4x4 toWorld = worldView.transform.localToWorldMatrix;
            var sizes = new int[Kinds * Variants];
            foreach (DistantTree tree in tile.Trees)
            {
                sizes[(tree.Maple ? Variants : 0) + VariantOf(tree)]++;
            }
            var draw = new TileDraw { Trees = new Matrix4x4[Kinds * Variants][], Count = tile.Trees.Count };
            for (int i = 0; i < sizes.Length; i++)
            {
                draw.Trees[i] = new Matrix4x4[sizes[i]];
            }
            var filled = new int[Kinds * Variants];
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (DistantTree tree in tile.Trees)
            {
                float width = Mathf.Lerp(0.9f, 1.1f, tree.Variation);
                float yaw = tree.Variation * 2520f;
                Matrix4x4 matrix = toWorld * Matrix4x4.TRS(
                    tree.Root, Quaternion.Euler(0f, yaw, 0f), new Vector3(tree.Height * width, tree.Height, tree.Height * width));
                int list = (tree.Maple ? Variants : 0) + VariantOf(tree);
                draw.Trees[list][filled[list]++] = matrix;
                // The crown spreads about half the height either side and tops out a little over it.
                Vector3 root = matrix.GetColumn(3);
                Vector3 reach = new Vector3(tree.Height * 0.6f, 0f, tree.Height * 0.6f);
                min = Vector3.Min(min, root - reach - Vector3.up * (tree.Height * 0.3f));
                max = Vector3.Max(max, root + reach + Vector3.up * (tree.Height * 1.2f));
            }
            draw.Bounds = tile.Trees.Count > 0 ? new Bounds((min + max) * 0.5f, max - min) : default;
            return draw;
        }

        // Where level <paramref name="level"/> starts and ends, metres from the focus.
        private Vector2 Band(int level)
        {
            float start = level == 0 ? 0f : levelEnds[level - 1];
            float end = level == Levels - 1 ? range + DistantTreeBuilder.TileSize : levelEnds[level];
            return new Vector2(start, end);
        }

        // This frame's trees: the tiles in the camera's view, in every level whose band (with its fades) the tile
        // reaches into; the shader keeps each tree in the levels its own distance puts it in.
        private void Collect()
        {
            Camera camera = Camera.main;
            bool cull = camera != null;
            if (cull)
            {
                GeometryUtility.CalculateFrustumPlanes(camera, frustum);
            }
            Vector3 focus = worldView.Focus.position;
            var flatFocus = new Vector2(focus.x, focus.z);
            float half = fadeWidth * 0.5f;
            System.Array.Clear(counts, 0, counts.Length);
            DrawnCount = 0;
            foreach (TileDraw draw in tileDraws.Values)
            {
                if (draw.Count == 0)
                {
                    continue;
                }
                // The nearest and farthest the tile's trees are across the ground.
                Bounds bounds = draw.Bounds;
                var low = new Vector2(bounds.min.x, bounds.min.z);
                var high = new Vector2(bounds.max.x, bounds.max.z);
                float near = Vector2.Distance(flatFocus, Vector2.Max(low, Vector2.Min(high, flatFocus)));
                float far = Vector2.Distance(flatFocus, new Vector2(
                    Mathf.Abs(low.x - focus.x) > Mathf.Abs(high.x - focus.x) ? low.x : high.x,
                    Mathf.Abs(low.y - focus.z) > Mathf.Abs(high.y - focus.z) ? low.y : high.y));
                if (near > range || (cull && !GeometryUtility.TestPlanesAABB(frustum, bounds)))
                {
                    continue;
                }
                DrawnCount += draw.Count;
                for (int level = 0; level < Levels; level++)
                {
                    Vector2 band = Band(level);
                    if (far < band.x - half || near > band.y + half)
                    {
                        continue;
                    }
                    for (int list = 0; list < Kinds * Variants; list++)
                    {
                        Append(list * Levels + level, draw.Trees[list]);
                    }
                }
            }
        }

        private void Append(int list, Matrix4x4[] trees)
        {
            if (trees.Length == 0)
            {
                return;
            }
            int needed = counts[list] + trees.Length;
            if (arrays[list] == null || arrays[list].Length < needed)
            {
                var grown = new Matrix4x4[Mathf.NextPowerOfTwo(Mathf.Max(1024, needed))];
                if (arrays[list] != null)
                {
                    System.Array.Copy(arrays[list], grown, counts[list]);
                }
                arrays[list] = grown;
            }
            System.Array.Copy(trees, 0, arrays[list], counts[list], trees.Length);
            counts[list] = needed;
        }

        private void UpdateCoverage()
        {
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

        private void Draw()
        {
            // Once a frame: a script pumping LateUpdate by hand (lab tests) would otherwise queue draws whose
            // per-frame GPU buffers are only freed when a real frame ends.
            if (Time.frameCount == drawnFrame)
            {
                return;
            }
            drawnFrame = Time.frameCount;
            Camera view = Camera.main;
            Vector3 at = view != null ? view.transform.position : worldView.Focus.position;
            Quaternion turn = view != null ? view.transform.rotation : Quaternion.identity;
            if (collectedVersion != syncedVersion || at != collectedAt || turn != collectedTurn)
            {
                collectedVersion = syncedVersion;
                collectedAt = at;
                collectedTurn = turn;
                Collect();
            }
            float half = fadeWidth * 0.5f;
            for (int level = 0; level < Levels; level++)
            {
                // Each level fades in over its start and out over its end; the nearest never fades in, the last never out.
                Vector2 band = Band(level);
                levelProperties[level] ??= new MaterialPropertyBlock();
                levelProperties[level].SetVector(FadeInId, level == 0 ? new Vector4(-2f, -1f) : new Vector4(band.x - half, band.x + half));
                levelProperties[level].SetVector(FadeOutId, level == Levels - 1 ? new Vector4(1e7f, 2e7f) : new Vector4(band.y - half, band.y + half));
            }
            for (int i = 0; i < counts.Length; i++)
            {
                int count = counts[i];
                if (count == 0)
                {
                    continue;
                }
                var parameters = new RenderParams(runtimeMaterial)
                {
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                    worldBounds = new Bounds(worldView.Focus.position, Vector3.one * (range * 2.2f)),
                    matProps = levelProperties[i % Levels],
                };
                for (int start = 0; start < count; start += BatchSize)
                {
                    Graphics.RenderMeshInstanced(parameters, meshes[i], 0, arrays[i], Mathf.Min(BatchSize, count - start), start);
                }
            }
        }

        private void EnsureMeshes()
        {
            TreeGeneration trees = worldView.Trees.Settings;
            int seed = worldView.Config.Terrain.Seed;
            if (meshes != null && builtColors == (birchBark, birchLeaves, mapleBark, mapleLeaves) && builtShapes == (worldView.Generator, trees, seed))
            {
                return;
            }
            DestroyMeshes();
            builtColors = (birchBark, birchLeaves, mapleBark, mapleLeaves);
            builtShapes = (worldView.Generator, trees, seed);
            // Vertex colours reach the shader as they are, so in a linear project they go in linear.
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Color Convert(Color color) => linear ? color.linear : color;
            meshes = new Mesh[Kinds * Variants * Levels];
            var parts = new List<TreePart>();
            for (int kind = 0; kind < Kinds; kind++)
            {
                bool maple = kind == 1;
                List<Vector2Int> cells = SampleTrees(seed, trees, maple);
                for (int variant = 0; variant < Variants; variant++)
                {
                    Vector2Int cell = cells[variant % cells.Count];
                    parts.Clear();
                    TreeShape.Build(seed, cell, Unity.Mathematics.float3.zero, trees, parts);
                    float height = TreeShape.HeightOf(seed, cell, trees);
                    Mesh[] levels = DistantTreeMeshes.BuildLevels(parts, Vector3.zero, height,
                        Convert(maple ? mapleBark : birchBark), Convert(maple ? mapleLeaves : birchLeaves), $"{(maple ? "Maple" : "Birch")} {variant + 1}");
                    for (int level = 0; level < Levels; level++)
                    {
                        meshes[(kind * Variants + variant) * Levels + level] = levels[level];
                    }
                }
            }
        }

        // Cells of full-grown trees of a kind, the same for a seed every time: the variants' shapes.
        private static List<Vector2Int> SampleTrees(int seed, TreeGeneration trees, bool maple)
        {
            var cells = new List<Vector2Int>();
            for (int i = 0; i < 4000 && cells.Count < Variants; i++)
            {
                var cell = new Vector2Int(i, 7919);
                if (TreeShape.IsMaple(seed, cell, trees) == maple && TreeForests.SizeOf(seed, cell, trees) >= 0.85f)
                {
                    cells.Add(cell);
                }
            }
            if (cells.Count == 0)
            {
                cells.Add(new Vector2Int(0, 7919));
            }
            return cells;
        }

        private void DestroyMeshes()
        {
            if (meshes == null)
            {
                return;
            }
            foreach (Mesh mesh in meshes)
            {
                Destroy(mesh);
            }
            meshes = null;
        }

        private void DropBuilder()
        {
            builder?.Dispose();
            builder = null;
            builtFor = null;
            syncedVersion = -1;
            collectedVersion = -1;
            tileDraws.Clear();
            System.Array.Clear(counts, 0, counts.Length);
            TreeCount = 0;
            DrawnCount = 0;
        }
    }
}
