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
        private Color mapleLeaves = new Color(0.6f, 0.26f, 0.1f);

        private DistantTreeBuilder builder;
        private ITerrainGenerator builtFor;
        private float builtRange;
        private Material runtimeMaterial;
        private ChunkCoverageMask coverage;
        private Mesh[] meshes;
        private string builtColors;
        private int gatheredVersion = -1;
        private Vector3 gatheredAt;
        private int nextGatherFrame;
        private int drawnFrame = -1;

        // Per mesh (birch near, birch far, maple near, maple far): the trees to draw.
        private readonly List<Matrix4x4>[] instances = { new List<Matrix4x4>(), new List<Matrix4x4>(), new List<Matrix4x4>(), new List<Matrix4x4>() };
        private Matrix4x4[][] arrays = new Matrix4x4[4][];

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

        /// <summary>Trees drawn (before the shader drops the ones the world grows itself).</summary>
        public int TreeCount { get; private set; }

        /// <summary>Tiles still waiting for their trees.</summary>
        public int PendingTiles => builder?.Pending ?? 0;

        /// <summary>Why nothing is drawn, or null.</summary>
        public string Problem { get; private set; }

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

            if (builder == null || builtFor != worldView.Generator || builtRange != range)
            {
                DropBuilder();
                builder = new DistantTreeBuilder(heightfield, worldView.Trees, range);
                builtFor = worldView.Generator;
                builtRange = range;
            }
            EnsureMeshes();
            runtimeMaterial ??= new Material(material) { name = material.name + " (runtime)", enableInstancing = true };

            Vector3 focus = worldView.transform.InverseTransformPoint(worldView.Focus.position);
            builder.Update(focus);
            // While tiles keep arriving, gather every few frames rather than every one.
            bool moved = (focus - gatheredAt).sqrMagnitude > detailDistance * detailDistance * 0.0625f;
            if (moved || (builder.Version != gatheredVersion && Time.frameCount >= nextGatherFrame))
            {
                Gather(focus);
            }
            UpdateCoverage();
            Draw();
        }

        // Every ready tile's trees, sorted into the four meshes by kind and distance.
        private void Gather(Vector3 focus)
        {
            gatheredVersion = builder.Version;
            gatheredAt = focus;
            nextGatherFrame = Time.frameCount + 10;
            foreach (List<Matrix4x4> list in instances)
            {
                list.Clear();
            }
            Matrix4x4 toWorld = worldView.transform.localToWorldMatrix;
            float detail = detailDistance * detailDistance;
            float reach = range * range;
            foreach (DistantTreeBuilder.Tile tile in builder.Tiles)
            {
                if (!tile.Ready)
                {
                    continue;
                }
                foreach (DistantTree tree in tile.Trees)
                {
                    float distance = new Vector2(tree.Root.x - focus.x, tree.Root.z - focus.z).sqrMagnitude;
                    if (distance > reach)
                    {
                        continue;
                    }
                    int list = (tree.Maple ? 2 : 0) + (distance <= detail ? 0 : 1);
                    float width = Mathf.Lerp(0.85f, 1.2f, tree.Variation);
                    float yaw = tree.Variation * 2520f;
                    instances[list].Add(toWorld * Matrix4x4.TRS(
                        tree.Root, Quaternion.Euler(0f, yaw, 0f), new Vector3(tree.Height * width, tree.Height, tree.Height * width)));
                }
            }
            TreeCount = 0;
            for (int i = 0; i < instances.Length; i++)
            {
                if (arrays[i] == null || arrays[i].Length < instances[i].Count)
                {
                    arrays[i] = new Matrix4x4[Mathf.NextPowerOfTwo(Mathf.Max(64, instances[i].Count))];
                }
                instances[i].CopyTo(arrays[i]);
                TreeCount += instances[i].Count;
            }
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
            var parameters = new RenderParams(runtimeMaterial)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(worldView.Focus.position, Vector3.one * (range * 2.2f)),
            };
            for (int i = 0; i < instances.Length; i++)
            {
                int count = instances[i].Count;
                for (int start = 0; start < count; start += BatchSize)
                {
                    Graphics.RenderMeshInstanced(parameters, meshes[i], 0, arrays[i], Mathf.Min(BatchSize, count - start), start);
                }
            }
        }

        private void EnsureMeshes()
        {
            string colors = $"{birchBark}{birchLeaves}{mapleBark}{mapleLeaves}";
            if (meshes != null && colors == builtColors)
            {
                return;
            }
            DestroyMeshes();
            builtColors = colors;
            // Vertex colours reach the shader as they are, so in a linear project they go in linear.
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Color Convert(Color color) => linear ? color.linear : color;
            meshes = new[]
            {
                DistantTreeMeshes.Build(true, Convert(birchBark), Convert(birchLeaves)),
                DistantTreeMeshes.Build(false, Convert(birchBark), Convert(birchLeaves)),
                DistantTreeMeshes.Build(true, Convert(mapleBark), Convert(mapleLeaves)),
                DistantTreeMeshes.Build(false, Convert(mapleBark), Convert(mapleLeaves)),
            };
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
            gatheredVersion = -1;
            foreach (List<Matrix4x4> list in instances)
            {
                list.Clear();
            }
            TreeCount = 0;
        }
    }
}
