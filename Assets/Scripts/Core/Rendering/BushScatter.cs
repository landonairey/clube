using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Core
{
    /// <summary>One bush (GL236): its grid cell (which names it), size, height and where its foot stands, in world space.</summary>
    public readonly struct Bush
    {
        public Bush(Vector2Int cell, BushSize size, float height, Vector3 position)
        {
            Cell = cell;
            Size = size;
            Height = height;
            Position = position;
        }

        public Vector2Int Cell { get; }
        public BushSize Size { get; }
        public float Height { get; }
        public Vector3 Position { get; }

        /// <summary>The middle of the clump, for hit tests.</summary>
        public Vector3 Centre => Position + Vector3.up * (Height * 0.5f);

        /// <summary>The clump's rough radius, for hit tests.</summary>
        public float Radius => Height * 0.5f * BushMeshes.WidthOf(Size);
    }

    /// <summary>
    /// Bushes on the grass near the world's focus (GL236), drawn instanced: small bulbs, boulder-sized bushes and
    /// bushes as tall as the player, where <see cref="BushGrowth"/> grows them. They aren't voxels: like the grass
    /// (<see cref="GrassScatter"/>) they stand on the built meshes' grassy triangles, on a grid across the world, so
    /// the same ground always grows the same bushes.
    /// </summary>
    /// <remarks>
    /// When a chunk is rebuilt (an edit), its bushes are found again; one whose ground has gone, or moved, is broken
    /// (<see cref="Broken"/>, GL237). <see cref="Raycast"/> finds the bush a ray hits and <see cref="Pick"/> takes one
    /// away (GL238). A picked or broken bush stays gone (<see cref="RemovedCells"/>, kept in saves).
    /// </remarks>
    [RequireComponent(typeof(WorldView))]
    public class BushScatter : MonoBehaviour
    {
        // Kept chunks' bushes are dropped once the focus is this many radii away across the ground.
        private const float KeepRadii = 2f;

        // A bush whose ground moves up or down more than this (m) when its chunk is rebuilt is broken.
        private const float BreakingRise = 0.12f;

        private static readonly int FadeStart = Shader.PropertyToID("_FadeStart");
        private static readonly int FadeEnd = Shader.PropertyToID("_FadeEnd");
        private static readonly BushSize[] Sizes = { BushSize.Small, BushSize.Medium, BushSize.Large };

        [Tooltip("The material bushes grow on.")]
        [SerializeField]
        private VoxelMaterial grass;

        [Tooltip("Draws the bushes (Clube/Grass with no straw, GPU instancing on).")]
        [SerializeField]
        private Material material;

        [Tooltip("Bushes grow within this many metres of the focus.")]
        [SerializeField, Min(8f)]
        private float radius = 60f;

        [Tooltip("How upward the ground must face for a bush (the y of its normal).")]
        [SerializeField, Range(0f, 1f)]
        private float minUp = 0.8f;

        [Tooltip("Where bushes grow and how big (GL236).")]
        [SerializeField]
        private BushGrowth growth = new BushGrowth();

        [Tooltip("Milliseconds a frame may spend finding bushes on chunks.")]
        [SerializeField, Min(0.1f)]
        private float scatterMilliseconds = 0.5f;

        private readonly Dictionary<Vector3Int, Scattered> bushes = new Dictionary<Vector3Int, Scattered>();
        private readonly Stack<Scattered> spare = new Stack<Scattered>();
        private readonly HashSet<Vector2Int> removed = new HashSet<Vector2Int>();
        private readonly List<Vector3Int> dropped = new List<Vector3Int>();
        private readonly List<Bush> before = new List<Bush>();
        private readonly InstanceBatches[] batches = { new InstanceBatches(), new InstanceBatches(), new InstanceBatches() };
        private readonly Mesh[] meshes = new Mesh[3];
        private readonly GrassyGround ground = new GrassyGround();
        private readonly List<GroundSpot> spots = new List<GroundSpot>();
        private readonly System.Diagnostics.Stopwatch clock = new System.Diagnostics.Stopwatch();

        private WorldView worldView;
        private TreeCover cover;
        private TreeField coverOf;
        private GrassScatter grassScatter;
        private MaterialPropertyBlock properties;
        private bool batchesDirty;
        private bool scanPending = true;
        private Vector3 scannedFrom;

        private sealed class Scattered
        {
            public readonly List<Bush> Bushes = new List<Bush>();
            public bool Drawn;
        }

        /// <summary>A bush whose ground was dug away or built over (GL237): it's gone, and drops what it's made of.</summary>
        public event Action<Bush> Broken;

        /// <summary>Where bushes grow, how big, and what they drop.</summary>
        public BushGrowth Growth => growth;

        /// <summary>Bushes drawn last frame, for the lab readouts.</summary>
        public int BushCount { get; private set; }

        /// <summary>The cells whose bushes were picked or broken: they don't grow again.</summary>
        public IReadOnlyCollection<Vector2Int> RemovedCells => removed;

        /// <summary>The seed the bushes grow from.</summary>
        public int Seed => worldView != null ? worldView.Config.Terrain.Seed : 0;

        /// <summary>Puts back a save's picked and broken bushes (they stay gone), before the world streams.</summary>
        public void Restore(IEnumerable<Vector2Int> cells)
        {
            removed.Clear();
            if (cells != null)
            {
                removed.UnionWith(cells);
            }
            Regrow();
        }

        /// <summary>Finds every chunk's bushes again (after a change to <see cref="Growth"/>).</summary>
        public void Regrow()
        {
            foreach (Vector3Int coord in new List<Vector3Int>(bushes.Keys))
            {
                Drop(coord);
            }
            scanPending = true;
        }

        /// <summary>The nearest drawn bush the ray passes through within <paramref name="maxDistance"/> metres.</summary>
        public bool Raycast(Ray ray, float maxDistance, out Bush hit, out float distance)
        {
            hit = default;
            distance = float.PositiveInfinity;
            bool found = false;
            Vector3 direction = ray.direction.normalized;
            foreach (Scattered scattered in bushes.Values)
            {
                if (!scattered.Drawn)
                {
                    continue;
                }
                foreach (Bush bush in scattered.Bushes)
                {
                    Vector3 toCentre = bush.Centre - ray.origin;
                    float along = Vector3.Dot(toCentre, direction);
                    float r = bush.Radius;
                    if (along < -r || along - r > maxDistance)
                    {
                        continue;
                    }
                    float missBy = (toCentre - direction * along).sqrMagnitude;
                    if (missBy > r * r)
                    {
                        continue;
                    }
                    float entry = Mathf.Max(0f, along - Mathf.Sqrt(r * r - missBy));
                    if (entry <= maxDistance && entry < distance)
                    {
                        distance = entry;
                        hit = bush;
                        found = true;
                    }
                }
            }
            return found;
        }

        /// <summary>Takes a bush away for good (picked by hand, GL238). False when it isn't there.</summary>
        public bool Pick(Bush bush)
        {
            foreach (Scattered scattered in bushes.Values)
            {
                int index = scattered.Bushes.FindIndex(b => b.Cell == bush.Cell);
                if (index < 0)
                {
                    continue;
                }
                scattered.Bushes.RemoveAt(index);
                removed.Add(bush.Cell);
                batchesDirty |= scattered.Drawn;
                return true;
            }
            return false;
        }

        private void Awake()
        {
            worldView = GetComponent<WorldView>();
            grassScatter = GetComponent<GrassScatter>();
            properties = new MaterialPropertyBlock();
            for (int i = 0; i < Sizes.Length; i++)
            {
                meshes[i] = BushMeshes.Build(Sizes[i]);
            }
        }

        private void OnEnable()
        {
            worldView.ChunkMeshed += OnChunkMeshed;
            worldView.ChunkUnloaded += OnChunkUnloaded;
        }

        private void OnDisable()
        {
            worldView.ChunkMeshed -= OnChunkMeshed;
            worldView.ChunkUnloaded -= OnChunkUnloaded;
            foreach (Scattered scattered in bushes.Values)
            {
                Recycle(scattered);
            }
            bushes.Clear();
            batchesDirty = true;
            scanPending = true;
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in meshes)
            {
                if (mesh != null)
                {
                    Destroy(mesh);
                }
            }
        }

        private void OnChunkMeshed(Vector3Int coord, ChunkRenderer chunkRenderer)
        {
            scanPending = true;
            if (!bushes.TryGetValue(coord, out Scattered scattered))
            {
                return;
            }
            if (!scattered.Drawn || chunkRenderer == null)
            {
                Drop(coord);
                return;
            }

            // An edit rebuilt it: find its bushes again, and break the ones whose ground went or moved.
            before.Clear();
            before.AddRange(scattered.Bushes);
            Scatter(chunkRenderer, scattered);
            foreach (Bush old in before)
            {
                int index = scattered.Bushes.FindIndex(b => b.Cell == old.Cell);
                if (index >= 0 && Mathf.Abs(scattered.Bushes[index].Position.y - old.Position.y) <= BreakingRise)
                {
                    continue;
                }
                if (index >= 0)
                {
                    scattered.Bushes.RemoveAt(index);
                }
                removed.Add(old.Cell);
                Broken?.Invoke(old);
            }
            batchesDirty = true;
        }

        private void OnChunkUnloaded(Vector3Int coord)
        {
            Drop(coord);
        }

        private void LateUpdate()
        {
            if (!worldView.IsReady || worldView.Focus == null || grass == null || material == null)
            {
                return;
            }
            Vector3 focus = worldView.Focus.position;
            if (scanPending || (focus - scannedFrom).sqrMagnitude > 4f)
            {
                Scan(focus);
            }
            Draw();
        }

        private void Scan(Vector3 focus)
        {
            scannedFrom = focus;
            scanPending = false;
            float near = radius * radius;
            float far = (radius + 8f) * (radius + 8f);
            float keep = radius * KeepRadii * radius * KeepRadii;

            dropped.Clear();
            foreach (KeyValuePair<Vector3Int, Scattered> pair in bushes)
            {
                if (!worldView.Renderers.TryGetValue(pair.Key, out ChunkRenderer chunkRenderer) || GroundDistance(chunkRenderer, focus) > keep)
                {
                    dropped.Add(pair.Key);
                    continue;
                }
                bool drawn = Distance(chunkRenderer, focus) <= far;
                if (drawn != pair.Value.Drawn)
                {
                    pair.Value.Drawn = drawn;
                    batchesDirty = true;
                }
            }
            foreach (Vector3Int coord in dropped)
            {
                Drop(coord);
            }

            // New chunks in reach, nearest first, within the time a frame allows.
            clock.Restart();
            World world = worldView.World;
            Vector3 chunkSize = world.Grid.ChunkWorldSize;
            Vector3Int centre = world.Grid.WorldToChunk(worldView.transform.InverseTransformPoint(focus));
            var reach = new Vector3Int(Mathf.CeilToInt(radius / chunkSize.x), Mathf.CeilToInt(radius / chunkSize.y), Mathf.CeilToInt(radius / chunkSize.z));
            for (int ring = 0; ring <= Mathf.Max(reach.x, reach.z); ring++)
            {
                for (int dz = -ring; dz <= ring; dz++)
                {
                    for (int dx = -ring; dx <= ring; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != ring)
                        {
                            continue;
                        }
                        for (int dy = -reach.y; dy <= reach.y; dy++)
                        {
                            var coord = new Vector3Int(centre.x + dx, centre.y + dy, centre.z + dz);
                            if (bushes.ContainsKey(coord)
                                || !worldView.Renderers.TryGetValue(coord, out ChunkRenderer chunkRenderer)
                                || Distance(chunkRenderer, focus) > near)
                            {
                                continue;
                            }
                            if (clock.Elapsed.TotalMilliseconds > scatterMilliseconds)
                            {
                                scanPending = true;
                                return;
                            }
                            Scattered scattered = spare.Count > 0 ? spare.Pop() : new Scattered();
                            Scatter(chunkRenderer, scattered);
                            scattered.Drawn = true;
                            bushes.Add(coord, scattered);
                            batchesDirty = true;
                        }
                    }
                }
            }
        }

        private void Scatter(ChunkRenderer chunkRenderer, Scattered scattered)
        {
            scattered.Bushes.Clear();
            spots.Clear();
            int seed = Seed;
            ground.Find(chunkRenderer, grass, minUp, growth.CellSize, seed, BushGrowth.SpotSaltX, BushGrowth.SpotSaltZ, BushGrowth.SpotMargin, spots);
            TreeCover trees = CoverOf(worldView.Trees);
            foreach (GroundSpot spot in spots)
            {
                if (removed.Contains(spot.Cell))
                {
                    continue;
                }
                Vector3 local = worldView.transform.InverseTransformPoint(spot.Position);
                float forest = trees.At(local.x, local.z);
                // No bushes in a wheat field.
                if (grassScatter != null && grassScatter.Growth.WheatAt(seed, local.x, local.z, forest) > 0.1f)
                {
                    continue;
                }
                if (growth.TryGrow(seed, spot.Cell, forest, out BushSize size, out float tall))
                {
                    // Sunk a little, so the clump sits in the grass rather than on it.
                    scattered.Bushes.Add(new Bush(spot.Cell, size, tall, spot.Position - Vector3.up * (0.05f * tall)));
                }
            }
        }

        // How thick the trees stand, from the world's tree field (made again if the world gets a new one).
        private TreeCover CoverOf(TreeField trees)
        {
            if (cover == null || coverOf != trees)
            {
                cover = new TreeCover(trees);
                coverOf = trees;
            }
            return cover;
        }

        private void Draw()
        {
            properties.SetFloat(FadeStart, radius - 8f);
            properties.SetFloat(FadeEnd, radius);
            var parameters = new RenderParams(material)
            {
                matProps = properties,
                shadowCastingMode = ShadowCastingMode.On,
                receiveShadows = true,
                layer = gameObject.layer,
            };
            if (batchesDirty)
            {
                RebuildBatches();
            }
            for (int i = 0; i < batches.Length; i++)
            {
                batches[i].Draw(parameters, meshes[i]);
            }
        }

        private void RebuildBatches()
        {
            batchesDirty = false;
            foreach (InstanceBatches batch in batches)
            {
                batch.Clear();
            }
            int seed = Seed;
            int total = 0;
            foreach (Scattered scattered in bushes.Values)
            {
                if (!scattered.Drawn)
                {
                    continue;
                }
                foreach (Bush bush in scattered.Bushes)
                {
                    Quaternion turn = Quaternion.Euler(0f, growth.YawOf(seed, bush.Cell), 0f);
                    batches[(int)bush.Size].Add(Matrix4x4.TRS(bush.Position, turn, Vector3.one * bush.Height));
                    total++;
                }
            }
            BushCount = total;
        }

        private void Drop(Vector3Int coord)
        {
            if (bushes.TryGetValue(coord, out Scattered scattered))
            {
                bushes.Remove(coord);
                batchesDirty |= scattered.Drawn;
                Recycle(scattered);
            }
        }

        private void Recycle(Scattered scattered)
        {
            scattered.Bushes.Clear();
            scattered.Drawn = false;
            spare.Push(scattered);
        }

        private float Distance(ChunkRenderer chunkRenderer, Vector3 focus)
        {
            Vector3 centre = chunkRenderer.Transform.position + worldView.World.Grid.ChunkWorldSize * 0.5f;
            return (centre - focus).sqrMagnitude;
        }

        private float GroundDistance(ChunkRenderer chunkRenderer, Vector3 focus)
        {
            Vector3 centre = chunkRenderer.Transform.position + worldView.World.Grid.ChunkWorldSize * 0.5f;
            float dx = centre.x - focus.x, dz = centre.z - focus.z;
            return dx * dx + dz * dz;
        }
    }
}
