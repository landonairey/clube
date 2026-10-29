using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Core
{
    /// <summary>
    /// Trees past the world's own chunks (GL162), out to a few kilometres over the coarse and far
    /// terrain: the same trees the voxel world grows (<see cref="DistantTreeBuilder"/>), drawn as
    /// low-poly meshes (<see cref="DistantTreeMeshes"/>), instanced, a detailed one near and a plain
    /// one further out, birch or maple in their colours. The shader drops any tree standing on a chunk
    /// column the world draws in full (<see cref="ChunkCoverageMask"/>), where its voxel tree is.
    /// Needs a heightfield generator and trees in the terrain settings.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DistantTreeView : MonoBehaviour
    {
        public const float MinRange = 500f;
        public const float MaxRange = 8000f;

        // Instances per draw call: the most RenderMeshInstanced takes from an array.
        private const int BatchSize = 1023;

        private static readonly int CoverageMaskId = Shader.PropertyToID("_CoverageMask");
        private static readonly int CoverageId = Shader.PropertyToID("_Coverage");

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

        [Tooltip("Within this many metres trees use the detailed mesh, beyond it the plain one.")]
        [SerializeField, Range(50f, 2000f)]
        private float detailDistance = 400f;

        [SerializeField]
        private Color birchBark = new Color(0.66f, 0.65f, 0.63f);

        [SerializeField]
        private Color birchLeaves = new Color(0.27f, 0.36f, 0.19f);

        [SerializeField]
        private Color mapleBark = new Color(0.33f, 0.22f, 0.14f);

        [SerializeField]
        private Color mapleLeaves = new Color(0.54f, 0.34f, 0.17f);

        private DistantTreeBuilder builder;
        private ITerrainGenerator builtFor;
        private float builtRange;
        private Material runtimeMaterial;
        private ChunkCoverageMask coverage;
        private Mesh[] meshes;
        private (Color, Color, Color, Color) builtColors;
        private (float, float) builtTrunks;
        private int syncedVersion = -1;
        private int drawnFrame = -1;

        // Each ready tile's trees as world matrices, made once when the tile arrives: a frame only
        // copies the tiles in view into the draw arrays.
        private readonly Dictionary<DistantTreeBuilder.Tile, TileDraw> tileDraws = new Dictionary<DistantTreeBuilder.Tile, TileDraw>();
        private readonly HashSet<DistantTreeBuilder.Tile> liveTiles = new HashSet<DistantTreeBuilder.Tile>();
        private readonly List<DistantTreeBuilder.Tile> goneTiles = new List<DistantTreeBuilder.Tile>();
        private readonly Plane[] frustum = new Plane[6];

        // Per mesh (birch near, birch far, maple near, maple far): this frame's trees, and how many.
        private readonly Matrix4x4[][] arrays = new Matrix4x4[4][];
        private readonly int[] counts = new int[4];

        private sealed class TileDraw
        {
            public Matrix4x4[] Birch;
            public Matrix4x4[] Maple;
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

        public float DetailDistance
        {
            get => detailDistance;
            set => detailDistance = Mathf.Clamp(value, 50f, 2000f);
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
                TreeCount += draw.Birch.Length + draw.Maple.Length;
            }
        }

        // A tile's trees as world matrices by kind, and the box round them for culling.
        private TileDraw Build(DistantTreeBuilder.Tile tile)
        {
            Matrix4x4 toWorld = worldView.transform.localToWorldMatrix;
            int maples = 0;
            foreach (DistantTree tree in tile.Trees)
            {
                maples += tree.Maple ? 1 : 0;
            }
            var draw = new TileDraw { Birch = new Matrix4x4[tile.Trees.Count - maples], Maple = new Matrix4x4[maples] };
            int birch = 0, maple = 0;
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (DistantTree tree in tile.Trees)
            {
                float width = Mathf.Lerp(0.85f, 1.2f, tree.Variation);
                float yaw = tree.Variation * 2520f;
                Matrix4x4 matrix = toWorld * Matrix4x4.TRS(
                    tree.Root, Quaternion.Euler(0f, yaw, 0f), new Vector3(tree.Height * width, tree.Height, tree.Height * width));
                if (tree.Maple)
                {
                    draw.Maple[maple++] = matrix;
                }
                else
                {
                    draw.Birch[birch++] = matrix;
                }
                // The crown spreads about half the height either side and tops out a little over it.
                Vector3 root = matrix.GetColumn(3);
                Vector3 reach = new Vector3(tree.Height * 0.6f, 0f, tree.Height * 0.6f);
                min = Vector3.Min(min, root - reach - Vector3.up * (tree.Height * 0.3f));
                max = Vector3.Max(max, root + reach + Vector3.up * (tree.Height * 1.2f));
            }
            draw.Bounds = tile.Trees.Count > 0 ? new Bounds((min + max) * 0.5f, max - min) : default;
            return draw;
        }

        // This frame's trees: the tiles in the camera's view, near or far mesh by the tile's distance.
        private void Collect()
        {
            Camera camera = Camera.main;
            bool cull = camera != null;
            if (cull)
            {
                GeometryUtility.CalculateFrustumPlanes(camera, frustum);
            }
            Vector3 focus = worldView.Focus.position;
            float detail = detailDistance * detailDistance;
            float reach = range * range;
            System.Array.Clear(counts, 0, counts.Length);
            foreach (TileDraw draw in tileDraws.Values)
            {
                if (draw.Birch.Length + draw.Maple.Length == 0)
                {
                    continue;
                }
                Vector3 centre = draw.Bounds.center;
                float distance = new Vector2(centre.x - focus.x, centre.z - focus.z).sqrMagnitude;
                if (distance > reach || (cull && !GeometryUtility.TestPlanesAABB(frustum, draw.Bounds)))
                {
                    continue;
                }
                int far = distance <= detail ? 0 : 1;
                Append(far, draw.Birch);
                Append(2 + far, draw.Maple);
            }
            DrawnCount = counts[0] + counts[1] + counts[2] + counts[3];
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
            Collect();
            var parameters = new RenderParams(runtimeMaterial)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(worldView.Focus.position, Vector3.one * (range * 2.2f)),
            };
            for (int i = 0; i < counts.Length; i++)
            {
                int count = counts[i];
                for (int start = 0; start < count; start += BatchSize)
                {
                    Graphics.RenderMeshInstanced(parameters, meshes[i], 0, arrays[i], Mathf.Min(BatchSize, count - start), start);
                }
            }
        }

        private void EnsureMeshes()
        {
            // Each kind's trunk against its height, as the voxel trees have it (GL166): the mesh is scaled by the tree's height.
            TreeGeneration trees = worldView.Trees.Settings;
            float birchTrunk = TrunkShare(trees, false);
            float mapleTrunk = TrunkShare(trees, true);
            if (meshes != null && builtColors == (birchBark, birchLeaves, mapleBark, mapleLeaves) && builtTrunks == (birchTrunk, mapleTrunk))
            {
                return;
            }
            DestroyMeshes();
            builtColors = (birchBark, birchLeaves, mapleBark, mapleLeaves);
            builtTrunks = (birchTrunk, mapleTrunk);
            // Vertex colours reach the shader as they are, so in a linear project they go in linear.
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Color Convert(Color color) => linear ? color.linear : color;
            meshes = new[]
            {
                DistantTreeMeshes.Build(true, Convert(birchBark), Convert(birchLeaves), birchTrunk),
                DistantTreeMeshes.Build(false, Convert(birchBark), Convert(birchLeaves), birchTrunk),
                DistantTreeMeshes.Build(true, Convert(mapleBark), Convert(mapleLeaves), mapleTrunk),
                DistantTreeMeshes.Build(false, Convert(mapleBark), Convert(mapleLeaves), mapleTrunk),
            };
        }

        // The full-grown trunk radius over the middle full-grown height; age scales both alike.
        private static float TrunkShare(TreeGeneration trees, bool maple)
        {
            Vector2 height = trees.HeightFor(maple);
            return trees.TrunkRadiusFor(maple) / ((height.x + height.y) * 0.5f);
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
            tileDraws.Clear();
            System.Array.Clear(counts, 0, counts.Length);
            TreeCount = 0;
            DrawnCount = 0;
        }
    }
}
