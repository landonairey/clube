using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Core
{
    /// <summary>
    /// One bush (GL236): its grid cell (which names it), size, height and where its foot stands, in world space; or a clump of
    /// reeds by the water (GL723, <see cref="IsReeds"/>), on the same grid, picked and kept gone the same way.
    /// </summary>
    public readonly struct Bush
    {
        public Bush(Vector2Int cell, BushSize size, float height, Vector3 position, bool isReeds = false)
        {
            Cell = cell;
            Size = size;
            Height = height;
            Position = position;
            IsReeds = isReeds;
        }

        /// <summary>A clump of reeds (GL723) rather than a bush: it gives reeds when picked.</summary>
        public bool IsReeds { get; }

        public Vector2Int Cell { get; }
        public BushSize Size { get; }
        public float Height { get; }
        public Vector3 Position { get; }

        /// <summary>The middle of the clump, for hit tests.</summary>
        public Vector3 Centre => Position + Vector3.up * (Height * 0.5f);

        /// <summary>The clump's rough radius, for hit tests.</summary>
        public float Radius => Height * 0.5f * (IsReeds ? ReedMeshes.Width * 1.6f : BushMeshes.WidthOf(Size));
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

        // Reeds stand on banks steeper than a bush's ground (GL723): the y of the ground's normal.
        private const float ReedMinUp = 0.6f;

        // GL770: how far the focus moves between looks over the chunks, and how often bushes on new chunks join the draw.
        private const float ScanMoveMetres = 4f;
        private const float GrowBatchSeconds = 0.25f;

        private static readonly int InstanceMatricesId = Shader.PropertyToID("_InstanceMatrices");
        private static readonly int FadeStart = Shader.PropertyToID("_FadeStart");
        private static readonly int FadeEnd = Shader.PropertyToID("_FadeEnd");
        private static readonly BushSize[] Sizes = { BushSize.Small, BushSize.Medium, BushSize.Large };
        private const int ReedBatch = 3;

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

        [Tooltip("Where reeds grow by lakes and rivers (GL723), on the bushes' grid.")]
        [SerializeField]
        private ReedGrowth reeds = new ReedGrowth();

        [Tooltip("Milliseconds a frame may spend finding bushes on chunks.")]
        [SerializeField, Min(0.1f)]
        private float scatterMilliseconds = 0.5f;

        private readonly Dictionary<Vector3Int, Scattered> bushes = new Dictionary<Vector3Int, Scattered>();
        private readonly Stack<Scattered> spare = new Stack<Scattered>();
        private readonly HashSet<Vector2Int> removed = new HashSet<Vector2Int>();
        private readonly List<Vector3Int> dropped = new List<Vector3Int>();
        private readonly List<Bush> before = new List<Bush>();
        // One per bush size, then the reeds (GL723).
        private readonly InstanceBatches[] batches = { new InstanceBatches(), new InstanceBatches(), new InstanceBatches(), new InstanceBatches() };
        private readonly Mesh[] meshes = new Mesh[4];
        private readonly GrassyGround ground = new GrassyGround();
        private readonly List<GroundSpot> spots = new List<GroundSpot>();
        private readonly List<GroundSpot> reedSpots = new List<GroundSpot>();
        private readonly HashSet<Vector2Int> reedCells = new HashSet<Vector2Int>();
        private readonly VoxelMaterial[] reedGround = new VoxelMaterial[3];
        private readonly System.Diagnostics.Stopwatch clock = new System.Diagnostics.Stopwatch();

        private WorldView worldView;
        private TreeCover cover;
        private TreeField coverOf;
        private GrassScatter grassScatter;
        private MaterialPropertyBlock properties;
        private bool batchesDirty;
        private bool grownDirty;
        private float batchedAt;
        private bool scanPending = true;
        private Vector3 scannedFrom;
        // Chunks in reach with no bushes yet, nearest first once sorted (GL770).
        private readonly List<(float Distance, Vector3Int Coord, ChunkRenderer Renderer)> waiting = new List<(float, Vector3Int, ChunkRenderer)>();
        private int waitingNext;
        private bool waitingUnsorted;

        private sealed class Scattered
        {
            public readonly List<Bush> Bushes = new List<Bush>();
            // The bushes' matrices by batch, worked out once until the bushes change (GL770).
            public readonly List<Matrix4x4>[] Matrices = { new List<Matrix4x4>(), new List<Matrix4x4>(), new List<Matrix4x4>(), new List<Matrix4x4>() };
            public bool Packed;
            public bool Drawn;
        }

        /// <summary>A bush whose ground was dug away or built over (GL237): it's gone, and drops what it's made of.</summary>
        public event Action<Bush> Broken;

        /// <summary>Where bushes grow, how big, and what they drop.</summary>
        public BushGrowth Growth => growth;

        /// <summary>Where reeds grow, and what they give (GL723).</summary>
        public ReedGrowth Reeds => reeds;

        /// <summary>Reed clumps drawn last frame, for the lab readouts (GL723).</summary>
        public int ReedCount { get; private set; }

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
                scattered.Packed = false;
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
            meshes[ReedBatch] = ReedMeshes.Build();
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
            waiting.Clear();
            waitingNext = 0;
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
            foreach (InstanceBatches batch in batches)
            {
                batch.Dispose();
            }
        }

        private void OnChunkMeshed(Vector3Int coord, ChunkRenderer chunkRenderer)
        {
            if (!bushes.TryGetValue(coord, out Scattered scattered))
            {
                Await(coord, chunkRenderer);
                return;
            }
            if (!scattered.Drawn || chunkRenderer == null)
            {
                Drop(coord);
                Await(coord, chunkRenderer);
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
            scattered.Packed = false;
            batchesDirty = true;
        }

        // A newly meshed chunk in reach joins the chunks waiting for bushes, without a full look (GL770: while the world
        // streams a chunk meshes nearly every frame, and looking over every cell in reach each time cost 2.4 ms a frame).
        private void Await(Vector3Int coord, ChunkRenderer chunkRenderer)
        {
            if (chunkRenderer == null || worldView.Focus == null)
            {
                return;
            }
            float distance = Distance(chunkRenderer, worldView.Focus.position);
            if (distance <= radius * radius)
            {
                waiting.Add((distance, coord, chunkRenderer));
                waitingUnsorted = true;
            }
        }

        private void OnChunkUnloaded(Vector3Int coord)
        {
            Drop(coord);
        }

        private void LateUpdate()
        {
            using FrameCost.Scope cost = FrameCost.Of("Bushes");
            if (!worldView.IsReady || worldView.Focus == null || grass == null || material == null)
            {
                return;
            }
            Vector3 focus = worldView.Focus.position;
            if (scanPending || (focus - scannedFrom).sqrMagnitude > ScanMoveMetres * ScanMoveMetres)
            {
                Scan(focus);
            }
            Grow();
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

            // The meshed chunks in reach that have no bushes yet wait their turn.
            waiting.Clear();
            waitingNext = 0;
            foreach (KeyValuePair<Vector3Int, ChunkRenderer> pair in worldView.Renderers)
            {
                if (bushes.ContainsKey(pair.Key))
                {
                    continue;
                }
                float distance = Distance(pair.Value, focus);
                if (distance <= near)
                {
                    waiting.Add((distance, pair.Key, pair.Value));
                }
            }
            waitingUnsorted = true;
        }

        // Grows bushes on the waiting chunks, nearest first, within the time a frame allows and at least one a frame.
        private void Grow()
        {
            if (waitingNext >= waiting.Count)
            {
                return;
            }
            if (waitingUnsorted)
            {
                waiting.RemoveRange(0, waitingNext);
                waitingNext = 0;
                waiting.Sort((a, b) => a.Distance.CompareTo(b.Distance));
                waitingUnsorted = false;
            }
            clock.Restart();
            bool grewOne = false;
            float allowed = worldView.Hurry ? scatterMilliseconds : FrameBudget.Limit(scatterMilliseconds);
            while (waitingNext < waiting.Count)
            {
                if (grewOne && clock.Elapsed.TotalMilliseconds > allowed)
                {
                    return;
                }
                (float _, Vector3Int coord, ChunkRenderer chunkRenderer) = waiting[waitingNext++];
                // Grown already (listed twice), or its renderer has gone back to the pool since.
                if (bushes.ContainsKey(coord) || !worldView.Renderers.TryGetValue(coord, out ChunkRenderer current) || current != chunkRenderer)
                {
                    continue;
                }
                Scattered scattered = spare.Count > 0 ? spare.Pop() : new Scattered();
                Scatter(chunkRenderer, scattered);
                scattered.Drawn = true;
                bushes.Add(coord, scattered);
                grownDirty = true;
                grewOne = true;
            }
            waiting.Clear();
            waitingNext = 0;
        }

        private void Scatter(ChunkRenderer chunkRenderer, Scattered scattered)
        {
            scattered.Bushes.Clear();
            scattered.Packed = false;
            spots.Clear();
            int seed = Seed;
            TreeCover trees = CoverOf(worldView.Trees);
            ScatterReeds(chunkRenderer, scattered, seed);
            ground.Find(chunkRenderer, grass, minUp, growth.CellSize, seed, BushGrowth.SpotSaltX, BushGrowth.SpotSaltZ, BushGrowth.SpotMargin, spots);
            foreach (GroundSpot spot in spots)
            {
                if (removed.Contains(spot.Cell) || reedCells.Contains(spot.Cell))
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

        // The chunk's reed clumps (GL723): on grass, sand or clay by fresh water, in the water's shallows too, one per bush cell;
        // a cell that grows reeds grows no bush.
        private void ScatterReeds(ChunkRenderer chunkRenderer, Scattered scattered, int seed)
        {
            reedCells.Clear();
            reedSpots.Clear();
            WaterMap water = worldView.Generator is HeightfieldGenerator heightfield ? heightfield.Water : WaterMap.None;
            if (!water.IsCreated)
            {
                return;
            }
            TerrainSettings terrain = worldView.Config.Terrain;
            reedGround[0] = grass;
            reedGround[1] = terrain.Water.Sand;
            reedGround[2] = terrain.Layers.Clay;
            ground.Find(chunkRenderer, reedGround, ReedMinUp, growth.CellSize, seed, BushGrowth.SpotSaltX, BushGrowth.SpotSaltZ, BushGrowth.SpotMargin, reedSpots);
            foreach (GroundSpot spot in reedSpots)
            {
                if (removed.Contains(spot.Cell))
                {
                    // Picked reeds stay gone; the bush loop mustn't grow a bush there either.
                    if (reeds.ShoreAt(water, Local(spot).x, Local(spot).z) > 0f)
                    {
                        reedCells.Add(spot.Cell);
                    }
                    continue;
                }
                Vector3 local = Local(spot);
                if (reeds.TryGrow(seed, spot.Cell, water, local.x, local.z, out float tall))
                {
                    scattered.Bushes.Add(new Bush(spot.Cell, BushSize.Large, tall, spot.Position - Vector3.up * 0.05f, true));
                    reedCells.Add(spot.Cell);
                }
            }
        }

        private Vector3 Local(GroundSpot spot)
        {
            return worldView.transform.InverseTransformPoint(spot.Position);
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
            if (batchesDirty || (grownDirty && Time.unscaledTime - batchedAt >= GrowBatchSeconds))
            {
                RebuildBatches();
            }
            // One draw per mesh from a buffer uploaded when the bushes change (GL770), as the grass does (GL416).
            for (int i = 0; i < batches.Length; i++)
            {
                batches[i].DrawFromBuffer(parameters, meshes[i], InstanceMatricesId);
            }
        }

        private void RebuildBatches()
        {
            batchesDirty = false;
            grownDirty = false;
            batchedAt = Time.unscaledTime;
            foreach (InstanceBatches batch in batches)
            {
                batch.Clear();
            }
            foreach (Scattered scattered in bushes.Values)
            {
                if (!scattered.Drawn)
                {
                    continue;
                }
                if (!scattered.Packed)
                {
                    Pack(scattered);
                }
                for (int i = 0; i < batches.Length; i++)
                {
                    batches[i].AddRange(scattered.Matrices[i]);
                }
            }
            ReedCount = batches[ReedBatch].Count;
            BushCount = batches[0].Count + batches[1].Count + batches[2].Count;
        }

        // Works out the chunk's bushes' matrices, by batch.
        private void Pack(Scattered scattered)
        {
            foreach (List<Matrix4x4> matrices in scattered.Matrices)
            {
                matrices.Clear();
            }
            int seed = Seed;
            foreach (Bush bush in scattered.Bushes)
            {
                float yaw = bush.IsReeds ? reeds.YawOf(seed, bush.Cell) : growth.YawOf(seed, bush.Cell);
                int batch = bush.IsReeds ? ReedBatch : (int)bush.Size;
                scattered.Matrices[batch].Add(Matrix4x4.TRS(bush.Position, Quaternion.Euler(0f, yaw, 0f), Vector3.one * bush.Height));
            }
            scattered.Packed = true;
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
            scattered.Packed = false;
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
