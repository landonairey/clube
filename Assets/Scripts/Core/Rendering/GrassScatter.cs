using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Core
{
    /// <summary>
    /// Low-poly grass on the terrain (GL67): small tufts of blades stand on the upward-facing
    /// triangles of a chunk's mesh that show grass, in the chunks near the world's focus, drawn
    /// instanced. Tufts stand on a fixed grid across the world (one per cell, jittered by a hash of
    /// the cell, <see cref="GrassyGround"/>), each at the height of the triangle under it, so the same
    /// ground always grows the same grass and a rebuilt mesh keeps every tuft whose ground is still there (GL171).
    /// </summary>
    /// <remarks>
    /// <para>How tall a tuft grows is <see cref="GrassGrowth"/>'s (GL234): up to twice the base height in open
    /// meadows, the base height under dense trees and high up; in large patches of open land it's wheat, about
    /// half a player tall (GL235). The shader colours taller grass towards straw, and sways it in the wind.</para>
    /// <para>Reads the meshes the pipeline built (positions, normals, and the material ids and weights
    /// in UV channels 2 and 3), so it costs nothing in generation or meshing. A millisecond or so of
    /// chunks is scattered a frame, nearest the focus first. A rebuilt chunk that has grass scatters
    /// again at once, so an edit never leaves a frame without grass or grass floating (GL161, GL171):
    /// grass on dug-up ground is simply gone, and drops nothing.
    /// A chunk's tufts are kept while the focus is within a few radii of it across the ground, at any
    /// height, and drawn only within the radius, so switching cameras or flying up and back finds the
    /// grass still there (GL167). The tufts shrink into the ground over the last metres of the radius.</para>
    /// <para>Dahlias (GL265): rarely, a few among the grass straight above a gold node, densest over its centre
    /// (<see cref="DahliaGrowth"/>, the nodes from the world's <see cref="OreField"/>); each patch mostly one colour.</para>
    /// <para>Far grass (GL257): past <see cref="thinFrom"/> metres fewer tufts stand (the shader drops them by a hash of
    /// their place) and those left are wider, so a 64 m reach costs little more than a short one. What grass looks like
    /// from afar at each place (<see cref="GrassLawn"/>: green, straw in tall meadows, wheat; worked out from these tufts'
    /// own colours and the material's straw, mapped round the focus by <see cref="GrassLawnMap"/>) is what grass ground
    /// shows in <c>Clube/Terrain Materials</c> and <c>Clube/Far Terrain</c>, a little near the focus and fully by the
    /// edge, and what far tufts turn to, so the 3D grass gives way to the ground without a line.</para>
    /// </remarks>
    [RequireComponent(typeof(WorldView))]
    public class GrassScatter : MonoBehaviour
    {
        // Scattered chunks are kept while the focus is within this many radii of them across the ground.
        private const float KeepRadii = 3f;

        // The grid's rolls: x and z of the point in a cell, its turn, its height, and whether it's wheat.
        private const int SaltX = 1;
        private const int SaltZ = 2;
        private const int SaltYaw = 3;
        private const int SaltHeight = 4;
        private const int SaltWheat = 5;
        private const int SaltDahlia = 6;
        private const int SaltDahliaStray = 7;
        private const int SaltDahliaColour = 8;
        private const int SaltThin = 9;

        // Past thinFrom each instance is a clump of this many small tufts (GL380): a quarter as many instances for the same
        // grass, since many tiny instances cost more than their few triangles.
        private const int FarClumpTufts = 4;

        // Repack the drawn tufts once the focus has moved this far, in metres, so each chunk's thinned share follows it (GL379).
        private const float RebatchDistance = 2f;

        private static readonly int StrawStrengthProperty = Shader.PropertyToID("_StrawStrength");

        // The dahlias' petal colours (linear): crimson, magenta, orange, golden yellow, blush pink.
        private static readonly Color[] DahliaColours =
        {
            new Color(0.5f, 0.02f, 0.05f, 1f),
            new Color(0.55f, 0.04f, 0.3f, 1f),
            new Color(0.9f, 0.24f, 0.03f, 1f),
            new Color(0.88f, 0.6f, 0.04f, 1f),
            new Color(0.82f, 0.45f, 0.55f, 1f),
        };

        private static readonly int FadeStart = Shader.PropertyToID("_FadeStart");
        private static readonly int FadeEnd = Shader.PropertyToID("_FadeEnd");
        private static readonly int ThinStart = Shader.PropertyToID("_ThinStart");
        private static readonly int ThinMin = Shader.PropertyToID("_ThinMin");
        private static readonly int LawnBlend = Shader.PropertyToID("_LawnBlend");
        private static readonly int LawnColor = Shader.PropertyToID("_GrassLawnColor");
        private static readonly int StrawColorId = Shader.PropertyToID("_StrawColor");
        private static readonly int StrawFromId = Shader.PropertyToID("_StrawFrom");
        private static readonly int StrawAtId = Shader.PropertyToID("_StrawAt");
        private static readonly int StrawStrengthId = Shader.PropertyToID("_StrawStrength");

        // The tufts' and wheat's vertex colours (linear), root to tip; the far grass colour is worked out from them.
        private static readonly Color TuftRoot = new Color(0.1f, 0.22f, 0.04f, 0f);
        private static readonly Color TuftTip = new Color(0.34f, 0.52f, 0.12f, 1f);
        private static readonly Color WheatRoot = new Color(0.32f, 0.3f, 0.12f, 0f);
        private static readonly Color WheatTip = new Color(0.8f, 0.68f, 0.34f, 1f);
        private static readonly Color WheatEar = new Color(0.86f, 0.7f, 0.3f, 1f);

        private static readonly int LawnLayer = Shader.PropertyToID("_GrassLawnLayer");
        private static readonly int LawnFrom = Shader.PropertyToID("_GrassLawnFrom");
        private static readonly int LawnTo = Shader.PropertyToID("_GrassLawnTo");
        private static readonly int LawnNear = Shader.PropertyToID("_GrassLawnNear");

        [Tooltip("The material grass grows on.")]
        [SerializeField]
        private VoxelMaterial grass;

        [Tooltip("Draws the tufts (Clube/Grass, with GPU instancing on).")]
        [SerializeField]
        private Material material;

        [Tooltip("Grass grows within this many metres of the focus (unless it reaches the drawn terrain).")]
        [SerializeField, Min(4f)]
        private float radius = 64f;

        [Tooltip("Grass reaches as far as the voxel terrain is drawn, where the surface rocks end too (GL340), instead of the radius.")]
        [SerializeField]
        private bool reachDrawnTerrain = true;

        // The radius as set, kept while radius follows the drawn terrain; NaN until first read.
        private float configuredRadius = float.NaN;

        [Tooltip("Past this many metres fewer tufts stand, the ones left wider and taller, so far grass costs less (GL257, GL378).")]
        [SerializeField, Min(0f)]
        private float thinFrom = 40f;

        [Tooltip("The share of tufts still standing where they start to shrink into the ground at the edge.")]
        [SerializeField, Range(0.02f, 1f)]
        private float thinTo = 0.35f;

        [Tooltip("Metres over which the tufts shrink into the ground at the edge of the radius.")]
        [SerializeField, Min(1f)]
        private float fadeLength = 8f;

        [Tooltip("How light grass looks from afar against its blades' colours (GL257): under 1 for the shade between them.")]
        [SerializeField, Range(0.3f, 1.5f)]
        private float lawnShade = 0.6f;

        [Tooltip("How much of the far grass colour grass ground shows right by the focus, under the tufts.")]
        [SerializeField, Range(0f, 1f)]
        private float lawnNear = 0.55f;

        [Tooltip("Tufts per square metre of flat grass.")]
        [SerializeField, Range(0f, 30f)]
        private float density = 6f;

        [Tooltip("How upward a triangle must face to grow grass (the y of its normal).")]
        [SerializeField, Range(0f, 1f)]
        private float minUp = 0.7f;

        [Tooltip("Tuft height in metres under dense trees, smallest and largest; meadows grow it taller.")]
        [SerializeField]
        private Vector2 height = new Vector2(0.18f, 0.38f);

        [Tooltip("How tall the grass grows where, and where it's wheat (GL234, GL235).")]
        [SerializeField]
        private GrassGrowth growth = new GrassGrowth();

        [Tooltip("Where dahlias grow among the grass: rarely, over gold nodes (GL265).")]
        [SerializeField]
        private DahliaGrowth dahlias = new DahliaGrowth();

        [Tooltip("Milliseconds a frame may spend scattering chunks.")]
        [SerializeField, Min(0.1f)]
        private float scatterMilliseconds = 1f;

        // Each scattered chunk's tufts, and whether they're in the drawn batches.
        private readonly Dictionary<Vector3Int, Scattered> tufts = new Dictionary<Vector3Int, Scattered>();
        private readonly Stack<Scattered> spare = new Stack<Scattered>();
        private readonly List<Vector3Int> dropped = new List<Vector3Int>();
        private readonly InstanceBatches grassBatches = new InstanceBatches();
        private readonly InstanceBatches farGrassBatches = new InstanceBatches();
        private readonly InstanceBatches wheatBatches = new InstanceBatches();
        private readonly InstanceBatches[] dahliaBatches = new InstanceBatches[DahliaGrowth.Colours];
        private readonly List<OreNode> dahliaNodes = new List<OreNode>();
        private readonly List<OreNode> nodeScratch = new List<OreNode>();
        private readonly GrassyGround ground = new GrassyGround();
        private readonly List<GroundSpot> spots = new List<GroundSpot>();
        private readonly System.Diagnostics.Stopwatch clock = new System.Diagnostics.Stopwatch();
        private readonly GrassLawn lawn = new GrassLawn();
        private readonly GrassLawnMap lawnMap = new GrassLawnMap();
        private System.Func<float, float, Color> lawnAt;
        private float lawnTop;

        private WorldView worldView;
        private TreeCover cover;
        private TreeField coverOf;
        private Mesh tuft;
        private Mesh farTuft;
        private Material farMaterial;
        private Material farMaterialFrom;
        private Mesh wheat;
        private readonly Mesh[] dahliaMeshes = new Mesh[DahliaGrowth.Colours];
        private MaterialPropertyBlock dahliaProperties;
        private MaterialPropertyBlock properties;
        private bool batchesDirty;
        private Vector3 batchedFrom;
        private Vector3Int sweepCentre = new Vector3Int(int.MinValue, 0, 0);
        private int sweepNext;
        private bool scanPending = true;
        private Vector3 scannedFrom;
        private Vector3Int[] reachOffsets;
        private float reachOffsetsRadius;
        private Vector3 reachOffsetsChunk;

        private sealed class Scattered
        {
            public readonly List<Matrix4x4> Grass = new List<Matrix4x4>();
            public readonly List<Matrix4x4> Wheat = new List<Matrix4x4>();

            // Each tuft's thinning pick (0-1) while scattering; the tufts are then sorted by it, so a far chunk draws the
            // first share of its list and the same tufts stay as the share grows or shrinks (GL379).
            public readonly List<(float Pick, Matrix4x4 Matrix)> GrassPicks = new List<(float, Matrix4x4)>();
            public readonly List<(float Pick, Matrix4x4 Matrix)> WheatPicks = new List<(float, Matrix4x4)>();
            public readonly List<Matrix4x4>[] Dahlias = NewLists();

            private static List<Matrix4x4>[] NewLists()
            {
                var lists = new List<Matrix4x4>[DahliaGrowth.Colours];
                for (int i = 0; i < lists.Length; i++)
                {
                    lists[i] = new List<Matrix4x4>();
                }
                return lists;
            }
            public bool Drawn;

            // The chunk's centre in the world, for its distance when the batches are packed.
            public Vector3 Centre;

            // Sorts the tufts by their thinning pick into the drawn lists (GL379).
            public void Order()
            {
                Sort(GrassPicks, Grass);
                Sort(WheatPicks, Wheat);
            }

            private static void Sort(List<(float Pick, Matrix4x4 Matrix)> picks, List<Matrix4x4> into)
            {
                picks.Sort(ByPick);
                into.Clear();
                foreach ((float _, Matrix4x4 matrix) in picks)
                {
                    into.Add(matrix);
                }
                picks.Clear();
            }

            private static readonly System.Comparison<(float Pick, Matrix4x4 Matrix)> ByPick = (a, b) => a.Pick.CompareTo(b.Pick);

            public void Clear()
            {
                Grass.Clear();
                Wheat.Clear();
                GrassPicks.Clear();
                WheatPicks.Clear();
                foreach (List<Matrix4x4> dahlias in Dahlias)
                {
                    dahlias.Clear();
                }
            }
        }

        /// <summary>Tufts drawn last frame, grass and wheat, for the lab readouts.</summary>
        public int TuftCount { get; private set; }

        /// <summary>Milliseconds the last LateUpdate took, and its parts: looking over the chunks, the far grass colours, the draw (GL379).</summary>
        public float LastUpdateMilliseconds { get; private set; }

        public float LastScanMilliseconds { get; private set; }

        public float LastLawnMilliseconds { get; private set; }

        public float LastDrawMilliseconds { get; private set; }

        /// <summary>Wheat stalks among them.</summary>
        public int WheatCount { get; private set; }

        /// <summary>Dahlias drawn last frame (GL265).</summary>
        public int DahliaCount { get; private set; }

        /// <summary>Where dahlias grow; call <see cref="Regrow"/> after changing it.</summary>
        public DahliaGrowth Dahlias => dahlias;

        /// <summary>Chunks whose tufts are kept, drawn or not.</summary>
        public int KeptChunkCount => tufts.Count;

        /// <summary>How tall the grass grows where; call <see cref="Regrow"/> after changing it.</summary>
        public GrassGrowth Growth => growth;

        /// <summary>Scatters every chunk again (after a change to <see cref="Growth"/>).</summary>
        public void Regrow()
        {
            foreach (Vector3Int coord in new List<Vector3Int>(tufts.Keys))
            {
                Drop(coord);
            }
            scanPending = true;
            lawnMap.Rebuild();
        }

        private void Awake()
        {
            worldView = GetComponent<WorldView>();
            tuft = BuildTuft();
            farTuft = BuildClump();
            wheat = BuildWheat();
            dahliaProperties = new MaterialPropertyBlock();
            for (int i = 0; i < DahliaGrowth.Colours; i++)
            {
                dahliaBatches[i] = new InstanceBatches();
                dahliaMeshes[i] = BuildDahlia(DahliaColours[i]);
            }
            properties = new MaterialPropertyBlock();
            lawnAt = LawnColourAt;
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
            foreach (Scattered scattered in tufts.Values)
            {
                Recycle(scattered);
            }
            tufts.Clear();
            batchesDirty = true;
            scanPending = true;
            // No grass now: the terrain shows its own grass texture again.
            Shader.SetGlobalFloat(LawnTo, 0f);
            lawnMap.Hide();
            lawnMap.Rebuild();
        }

        private void OnDestroy()
        {
            lawnMap.Dispose();
            if (tuft != null)
            {
                Destroy(tuft);
            }
            if (wheat != null)
            {
                Destroy(wheat);
            }
            if (farTuft != null)
            {
                Destroy(farTuft);
            }
            if (farMaterial != null)
            {
                Destroy(farMaterial);
            }
            foreach (Mesh mesh in dahliaMeshes)
            {
                if (mesh != null)
                {
                    Destroy(mesh);
                }
            }
        }

        private void OnChunkMeshed(Vector3Int coord, ChunkRenderer chunkRenderer)
        {
            if (!tufts.TryGetValue(coord, out Scattered scattered))
            {
                // Not scattered yet: the scan grows it once it's in reach. Only a chunk within reach calls for a scan: the
                // world meshes chunks far off all the time, and each scan cost about 4 ms (GL379).
                if (chunkRenderer != null && worldView.Focus != null)
                {
                    scanPending |= Distance(chunkRenderer, worldView.Focus.position) <= radius * radius;
                }
                return;
            }

            // Its grass stood on the old mesh (an edit changed it). Grow it again now, so there's no frame
            // without grass and none left floating; the grid keeps the tufts on unchanged ground in place.
            if (scattered.Drawn && chunkRenderer != null)
            {
                Scatter(chunkRenderer, scattered);
                batchesDirty = true;
            }
            else
            {
                Drop(coord);
            }
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
            // Out to where the chunks (and their rocks) are drawn; the coarse terrain past it shows the lawn colour (GL340).
            if (float.IsNaN(configuredRadius))
            {
                configuredRadius = radius;
            }
            float reach = reachDrawnTerrain ? Mathf.Max(16f, worldView.DrawnDistance) : configuredRadius;
            if (!Mathf.Approximately(reach, radius))
            {
                radius = reach;
                scanPending = true;
            }
            // The chunks are looked over only when something changed: a new mesh, the focus moving a
            // metre, or the last look running out of time.
            Vector3 focus = worldView.Focus.position;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            if (scanPending || (focus - scannedFrom).sqrMagnitude > 1f)
            {
                Scan(focus);
            }
            long scanned = System.Diagnostics.Stopwatch.GetTimestamp();
            UpdateLawn(focus);
            long lawned = System.Diagnostics.Stopwatch.GetTimestamp();
            Draw(focus);
            long drawn = System.Diagnostics.Stopwatch.GetTimestamp();
            float toMs = 1000f / System.Diagnostics.Stopwatch.Frequency;
            LastScanMilliseconds = (scanned - started) * toMs;
            LastLawnMilliseconds = (lawned - scanned) * toMs;
            LastDrawMilliseconds = (drawn - lawned) * toMs;
            LastUpdateMilliseconds = (drawn - started) * toMs;
        }

        // The far grass colours (GL257): worked out from the tufts' own colours and the material's straw, and mapped
        // round the focus half a millisecond a frame.
        private void UpdateLawn(Vector3 focus)
        {
            if (worldView.Generator == null)
            {
                return;
            }
            Color tint = material.HasProperty("_Tint") ? material.GetColor("_Tint").linear : Color.white;
            lawn.Green = Color.Lerp(TuftRoot, TuftTip, 0.6f) * tint * lawnShade;
            Color straw = material.HasProperty(StrawColorId) ? material.GetColor(StrawColorId).linear : lawn.Straw;
            lawn.Straw = straw * Mathf.Lerp(0.45f, 1f, 0.6f) * tint * lawnShade;
            lawn.Wheat = Color.Lerp(Color.Lerp(WheatRoot, WheatTip, 0.65f), WheatEar, 0.3f) * tint * lawnShade;
            lawn.BaseHeight = height;
            lawn.StrawFrom = material.HasProperty(StrawFromId) ? material.GetFloat(StrawFromId) : lawn.StrawFrom;
            lawn.StrawAt = material.HasProperty(StrawAtId) ? material.GetFloat(StrawAtId) : lawn.StrawAt;
            lawn.StrawStrength = material.HasProperty(StrawStrengthId) ? material.GetFloat(StrawStrengthId) : lawn.StrawStrength;
            if (!lawnMap.IsShown)
            {
                // Until the map's done, the green of grass under the trees.
                Shader.SetGlobalVector(LawnColor, (Vector4)lawn.Green);
            }
            lawnTop = worldView.World.Grid.ChunkWorldSize.y * worldView.Config.WorldHeightInChunks;
            lawnMap.Update(new Vector2(focus.x, focus.z), lawnAt, 0.5f);
        }

        private Color LawnColourAt(float x, float z)
        {
            Vector3 local = worldView.transform.InverseTransformPoint(new Vector3(x, 0f, z));
            float altitude = TerrainSurface.HeightAt(worldView.Generator, local.x, local.z, 0f, lawnTop);
            return lawn.ColourAt(growth, CoverOf(worldView.Trees), worldView.Config.Terrain.Seed, local.x, local.z, altitude);
        }

        private void Scan(Vector3 focus)
        {
            scannedFrom = focus;
            scanPending = false;
            float near = radius * radius;
            float far = (radius + 8f) * (radius + 8f);
            float keep = radius * KeepRadii * radius * KeepRadii;

            // Kept chunks far across the ground (or without a surface now) let their grass go; the rest are
            // drawn while they're within the radius.
            dropped.Clear();
            foreach (KeyValuePair<Vector3Int, Scattered> pair in tufts)
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

            // And grows on the chunks round the focus that have none yet, nearest first (GL163). Only the
            // chunks round the focus are looked at (GL108): walking every loaded renderer ran out of time on
            // the old, far ones in a streamed world before reaching the new ones by the player.
            clock.Restart();
            World world = worldView.World;
            Vector3Int centre = world.Grid.WorldToChunk(worldView.transform.InverseTransformPoint(focus));
            Vector3Int[] offsets = ReachOffsets(world.Grid.ChunkWorldSize);
            // A look that ran out of time goes on from where it stopped while the focus stays in the same chunk (GL379): starting
            // over, it spent its time on the near chunks already done and never reached the far ones, so it ran every frame.
            int first = centre == sweepCentre ? Mathf.Clamp(sweepNext, 0, offsets.Length) : 0;
            sweepCentre = centre;
            bool grewOne = false;
            for (int i = first; i < offsets.Length; i++)
            {
                Vector3Int coord = centre + offsets[i];
                if (tufts.ContainsKey(coord)
                    || !worldView.Renderers.TryGetValue(coord, out ChunkRenderer chunkRenderer)
                    || Distance(chunkRenderer, focus) > near)
                {
                    continue;
                }
                // At least one chunk grows each look, so a look always gets somewhere.
                if (grewOne && clock.Elapsed.TotalMilliseconds > scatterMilliseconds)
                {
                    sweepNext = i;
                    scanPending = true;
                    return;
                }
                Scattered scattered = spare.Count > 0 ? spare.Pop() : new Scattered();
                Scatter(chunkRenderer, scattered);
                scattered.Drawn = true;
                tufts.Add(coord, scattered);
                batchesDirty = true;
                grewOne = true;
            }
            sweepNext = 0;
        }

        private void Drop(Vector3Int coord)
        {
            if (tufts.TryGetValue(coord, out Scattered scattered))
            {
                tufts.Remove(coord);
                batchesDirty |= scattered.Drawn;
                Recycle(scattered);
            }
        }

        // The chunk offsets within the radius, nearest first, kept until the radius or chunk size changes.
        private Vector3Int[] ReachOffsets(Vector3 chunkSize)
        {
            if (reachOffsets != null && reachOffsetsRadius == radius && reachOffsetsChunk == chunkSize)
            {
                return reachOffsets;
            }
            var reach = new Vector3Int(
                Mathf.CeilToInt(radius / chunkSize.x), Mathf.CeilToInt(radius / chunkSize.y), Mathf.CeilToInt(radius / chunkSize.z));
            var offsets = new List<Vector3Int>();
            for (int dy = -reach.y; dy <= reach.y; dy++)
            {
                for (int dz = -reach.z; dz <= reach.z; dz++)
                {
                    for (int dx = -reach.x; dx <= reach.x; dx++)
                    {
                        // Only the sphere the scan can use (a chunk centre within the radius): the cube had twice as many.
                        if (Vector3.Scale(new Vector3(dx, dy, dz), chunkSize).magnitude <= radius + chunkSize.magnitude)
                        {
                            offsets.Add(new Vector3Int(dx, dy, dz));
                        }
                    }
                }
            }
            offsets.Sort((a, b) => Vector3.Scale(a, chunkSize).sqrMagnitude.CompareTo(Vector3.Scale(b, chunkSize).sqrMagnitude));
            reachOffsets = offsets.ToArray();
            reachOffsetsRadius = radius;
            reachOffsetsChunk = chunkSize;
            return reachOffsets;
        }

        // Squared distance from the focus to the chunk's centre.
        private float Distance(ChunkRenderer chunkRenderer, Vector3 focus)
        {
            Vector3 centre = chunkRenderer.Transform.position + worldView.World.Grid.ChunkWorldSize * 0.5f;
            return (centre - focus).sqrMagnitude;
        }

        // The same across the ground only (x and z).
        private float GroundDistance(ChunkRenderer chunkRenderer, Vector3 focus)
        {
            Vector3 centre = chunkRenderer.Transform.position + worldView.World.Grid.ChunkWorldSize * 0.5f;
            float dx = centre.x - focus.x, dz = centre.z - focus.z;
            return dx * dx + dz * dz;
        }

        // Fills the chunk's tufts: one per grid cell on grassy ground, grass or wheat, as tall as the place grows it.
        private void Scatter(ChunkRenderer chunkRenderer, Scattered scattered)
        {
            scattered.Clear();
            if (density <= 0f)
            {
                return;
            }
            spots.Clear();
            ground.Find(chunkRenderer, grass, minUp, 1f / Mathf.Sqrt(density), 0, SaltX, SaltZ, 0f, spots);
            scattered.Centre = chunkRenderer.Transform.position + worldView.World.Grid.ChunkWorldSize * 0.5f;

            TreeCover trees = CoverOf(worldView.Trees);
            int seed = worldView.Config.Terrain.Seed;
            // The gold nodes under this chunk's ground, if any: dahlias grow over them (GL265).
            Vector3 chunkMin = worldView.transform.InverseTransformPoint(chunkRenderer.Transform.position);
            Vector3 chunkSize = worldView.World.Grid.ChunkWorldSize;
            dahlias.NodesUnder(worldView.Ores, new Bounds(chunkMin + chunkSize * 0.5f, chunkSize), dahliaNodes, nodeScratch);
            foreach (GroundSpot spot in spots)
            {
                int ix = spot.Cell.x, iz = spot.Cell.y;
                Vector3 local = worldView.transform.InverseTransformPoint(spot.Position);
                float forest = trees.At(local.x, local.z);
                float yaw = VoxelHash.Uniform(0, ix, 0, iz, SaltYaw) * 360f;
                float roll = VoxelHash.Uniform(0, ix, 0, iz, SaltHeight);
                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, Vector3.Lerp(Vector3.up, spot.Normal, 0.5f)) * Quaternion.Euler(0f, yaw, 0f);

                if (dahliaNodes.Count > 0)
                {
                    float share = dahlias.ShareAt(dahliaNodes, local, out int node);
                    if (share > 0f && VoxelHash.Uniform(0, ix, 0, iz, SaltDahlia) < share)
                    {
                        int colour = DahliaGrowth.ColourOf(dahliaNodes[node], VoxelHash.Uniform(0, ix, 0, iz, SaltDahliaStray), VoxelHash.Uniform(0, ix, 0, iz, SaltDahliaColour));
                        float tall = dahlias.HeightFor(roll);
                        // Upright: flowers stand straight whatever the slope.
                        scattered.Dahlias[colour].Add(Matrix4x4.TRS(spot.Position, Quaternion.Euler(0f, yaw, 0f), Vector3.one * tall));
                        continue;
                    }
                }

                float wheatHere = growth.WheatAt(seed, local.x, local.z, forest);
                if (wheatHere > 0f && VoxelHash.Uniform(0, ix, 0, iz, SaltWheat) < wheatHere)
                {
                    float tall = growth.WheatHeightFor(roll);
                    scattered.WheatPicks.Add((VoxelHash.Uniform(0, ix, 0, iz, SaltThin), Matrix4x4.TRS(spot.Position, rotation, new Vector3(tall * 0.8f, tall, tall * 0.8f))));
                    continue;
                }
                float baseSize = Mathf.Lerp(height.x, height.y, roll);
                float scale = growth.HeightScale(forest, local.y);
                // Taller grass is a little wider, but mostly it reaches up.
                float width = baseSize * Mathf.Sqrt(scale);
                scattered.GrassPicks.Add((VoxelHash.Uniform(0, ix, 0, iz, SaltThin), Matrix4x4.TRS(spot.Position, rotation, new Vector3(width, baseSize * scale, width))));
            }
            scattered.Order();
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

        private void Draw(Vector3 focus)
        {
            // The fade is set per draw, not on the shared material asset.
            float fadeStart = Mathf.Max(radius - fadeLength, thinFrom + 1f);
            properties.SetFloat(FadeStart, fadeStart);
            properties.SetFloat(FadeEnd, radius - 1f);
            properties.SetFloat(ThinStart, thinFrom);
            properties.SetFloat(ThinMin, thinTo);
            properties.SetFloat(LawnBlend, 1f);
            // How far out the far grass colour takes over, for the terrain shaders and the tufts alike.
            Shader.SetGlobalFloat(LawnLayer, grass.Id);
            Shader.SetGlobalFloat(LawnFrom, thinFrom * 0.5f);
            Shader.SetGlobalFloat(LawnTo, radius - 1f);
            Shader.SetGlobalFloat(LawnNear, lawnNear);
            var parameters = new RenderParams(material)
            {
                matProps = properties,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                layer = gameObject.layer,
            };
            if (batchesDirty || (focus - batchedFrom).sqrMagnitude > RebatchDistance * RebatchDistance)
            {
                RebuildBatches(focus);
            }
            grassBatches.Draw(parameters, tuft);
            // Far grass draws without its depth passes (GL380): at that range it adds nothing to the ambient occlusion.
            RenderParams farParameters = parameters;
            farParameters.material = FarMaterial();
            farGrassBatches.Draw(farParameters, farTuft);
            wheatBatches.Draw(parameters, wheat);

            // Dahlias: their own colours (no drying to straw, no far grass colour) and never thinned out.
            dahliaProperties.SetFloat(FadeStart, fadeStart);
            dahliaProperties.SetFloat(FadeEnd, radius - 1f);
            dahliaProperties.SetFloat(ThinStart, 1e6f);
            dahliaProperties.SetFloat(ThinMin, 1f);
            dahliaProperties.SetFloat(LawnBlend, 0f);
            dahliaProperties.SetFloat(StrawStrengthProperty, 0f);
            parameters.matProps = dahliaProperties;
            for (int i = 0; i < DahliaGrowth.Colours; i++)
            {
                dahliaBatches[i].Draw(parameters, dahliaMeshes[i]);
            }
        }

        // Packs every drawn chunk's tufts into the batches, kept until the tufts change or the focus moves a couple of metres.
        // A far chunk sends only the share of its tufts the thinning keeps at its nearest edge (GL379): the tufts are sorted by
        // their pick, so that's the start of its list. Sending every tuft and dropping most in the shader cost about 9 ms a
        // frame in the Editor.
        private void RebuildBatches(Vector3 focus)
        {
            batchesDirty = false;
            batchedFrom = focus;
            grassBatches.Clear();
            farGrassBatches.Clear();
            wheatBatches.Clear();
            foreach (InstanceBatches batch in dahliaBatches)
            {
                batch.Clear();
            }
            float halfDiagonal = worldView.World.Grid.ChunkWorldSize.magnitude * 0.5f;
            float fadeStart = Mathf.Max(radius - fadeLength, thinFrom + 1f);
            foreach (Scattered scattered in tufts.Values)
            {
                if (scattered.Drawn)
                {
                    float nearest = Mathf.Max(0f, Vector3.Distance(scattered.Centre, focus) - halfDiagonal);
                    float keep = Mathf.Lerp(1f, thinTo, Mathf.Clamp01((nearest - thinFrom) / Mathf.Max(fadeStart - thinFrom, 0.01f)));
                    if (nearest > thinFrom)
                    {
                        farGrassBatches.AddRange(scattered.Grass, Mathf.CeilToInt(scattered.Grass.Count * keep / FarClumpTufts));
                    }
                    else
                    {
                        grassBatches.AddRange(scattered.Grass, Mathf.CeilToInt(scattered.Grass.Count * keep));
                    }
                    wheatBatches.AddRange(scattered.Wheat, Mathf.CeilToInt(scattered.Wheat.Count * keep));
                    for (int i = 0; i < DahliaGrowth.Colours; i++)
                    {
                        dahliaBatches[i].AddRange(scattered.Dahlias[i]);
                    }
                }
            }
            DahliaCount = 0;
            foreach (InstanceBatches batch in dahliaBatches)
            {
                DahliaCount += batch.Count;
            }
            TuftCount = grassBatches.Count + farGrassBatches.Count * FarClumpTufts + wheatBatches.Count + DahliaCount;
            WheatCount = wheatBatches.Count;
        }

        private void Recycle(Scattered scattered)
        {
            scattered.Clear();
            scattered.Drawn = false;
            spare.Push(scattered);
        }

        // One tuft, 1 m tall before scaling: seven thin blades leaning out from the middle (three wider ones far off, GL379),
        // dark at the root and light at the tip (alpha: how far up, for the wind).
        private static Mesh BuildTuft(int blades = 7, float width = 0.07f, string name = "Grass tuft")
        {
            var positions = new List<Vector3>();
            var colors = new List<Color>();
            var normalsOut = new List<Vector3>();
            var indices = new List<int>();
            Color root = TuftRoot;
            Color tip = TuftTip;
            var random = new System.Random(7);
            for (int i = 0; i < blades; i++)
            {
                float angle = (i + (float)random.NextDouble() * 0.6f) / blades * Mathf.PI * 2f;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var side = new Vector3(-outward.z, 0f, outward.x);
                float lean = 0.15f + (float)random.NextDouble() * 0.3f;
                float tall = 0.7f + (float)random.NextDouble() * 0.3f;
                Blade(outward * 0.06f, side * width, outward * lean + Vector3.up * tall, outward, root, tip, positions, colors, normalsOut, indices);
            }
            return Make(name, positions, colors, normalsOut, indices);
        }

        // The far grass instance (GL380): four small three-blade tufts spread over about a metre, as one mesh.
        private static Mesh BuildClump()
        {
            var positions = new List<Vector3>();
            var colors = new List<Color>();
            var normalsOut = new List<Vector3>();
            var indices = new List<int>();
            var random = new System.Random(13);
            Vector3[] spots = { new Vector3(0f, 0f, 0f), new Vector3(0.42f, 0f, 0.12f), new Vector3(-0.18f, 0f, 0.4f), new Vector3(-0.3f, 0f, -0.33f) };
            foreach (Vector3 spot in spots)
            {
                float size = 0.8f + (float)random.NextDouble() * 0.25f;
                for (int i = 0; i < 3; i++)
                {
                    float angle = (i + (float)random.NextDouble() * 0.6f) / 3f * Mathf.PI * 2f;
                    var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    var side = new Vector3(-outward.z, 0f, outward.x);
                    float lean = 0.15f + (float)random.NextDouble() * 0.3f;
                    float tall = (0.7f + (float)random.NextDouble() * 0.3f) * size;
                    Blade(spot + outward * 0.05f, side * 0.09f, spot + outward * lean + Vector3.up * tall, outward, TuftRoot, TuftTip, positions, colors, normalsOut, indices);
                }
            }
            return Make("Far grass clump", positions, colors, normalsOut, indices);
        }

        // A copy of the grass material without its depth passes, for the far clumps (GL380).
        private Material FarMaterial()
        {
            if (farMaterial == null || farMaterialFrom != material)
            {
                if (farMaterial != null)
                {
                    Destroy(farMaterial);
                }
                farMaterial = new Material(material) { name = material.name + " (far)" };
                farMaterial.SetShaderPassEnabled("DepthOnly", false);
                farMaterial.SetShaderPassEnabled("DepthNormals", false);
                farMaterialFrom = material;
            }
            return farMaterial;
        }

        // Wheat, 1 m tall before scaling: a sheaf of thin stalks, each with a grain head of two
        // crossed diamonds at its top, straw-coloured from root to ear.
        private static Mesh BuildWheat()
        {
            const int stalks = 6;
            var positions = new List<Vector3>();
            var colors = new List<Color>();
            var normalsOut = new List<Vector3>();
            var indices = new List<int>();
            Color root = WheatRoot;
            Color tip = WheatTip;
            Color ear = WheatEar;
            var random = new System.Random(11);
            for (int i = 0; i < stalks; i++)
            {
                float angle = (i + (float)random.NextDouble() * 0.7f) / stalks * Mathf.PI * 2f;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var side = new Vector3(-outward.z, 0f, outward.x);
                float lean = 0.04f + (float)random.NextDouble() * 0.1f;
                float tall = 0.8f + (float)random.NextDouble() * 0.2f;
                Vector3 baseCentre = outward * (0.03f + 0.05f * (float)random.NextDouble());
                Vector3 top = baseCentre + outward * lean + Vector3.up * tall;
                Blade(baseCentre, side * 0.012f, top, outward, root, tip, positions, colors, normalsOut, indices);
                // The ear: two diamonds crossed at right angles, from just under the top of the stalk.
                Vector3 earBottom = Vector3.Lerp(baseCentre, top, 0.82f);
                Vector3 earTop = top + (top - earBottom) * 0.25f;
                Vector3 middle = Vector3.Lerp(earBottom, earTop, 0.5f);
                Diamond(earBottom, earTop, middle, side * 0.028f, outward, ear, positions, colors, normalsOut, indices);
                Diamond(earBottom, earTop, middle, outward * 0.028f, side, ear, positions, colors, normalsOut, indices);
            }
            return Make("Wheat", positions, colors, normalsOut, indices);
        }

        // A dahlia, 1 m tall before scaling (GL265): a stem with three leaves, and a full flower head tilted a little
        // forward, two rings of pointed petals, darker towards the middle, round a small centre.
        private static Mesh BuildDahlia(Color petals)
        {
            var positions = new List<Vector3>();
            var colors = new List<Color>();
            var normalsOut = new List<Vector3>();
            var indices = new List<int>();
            var stemRoot = new Color(0.08f, 0.2f, 0.04f, 0f);
            var stemTop = new Color(0.16f, 0.36f, 0.08f, 0.9f);
            var head = new Vector3(0f, 0.86f, 0.03f);

            // The stem, as two crossed blades so it shows from every side.
            Blade(Vector3.zero, Vector3.right * 0.014f, head, Vector3.forward, stemRoot, stemTop, positions, colors, normalsOut, indices);
            Blade(Vector3.zero, Vector3.forward * 0.014f, head, Vector3.right, stemRoot, stemTop, positions, colors, normalsOut, indices);
            // Three leaves up the stem, turning round it.
            for (int i = 0; i < 3; i++)
            {
                float angle = i * 2.3f + 0.5f;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var side = new Vector3(-outward.z, 0f, outward.x);
                var at = new Vector3(0f, 0.18f + i * 0.14f, 0f);
                Blade(at, side * 0.045f, at + outward * 0.19f + Vector3.up * 0.07f, outward, stemRoot, stemTop, positions, colors, normalsOut, indices);
            }

            // The head faces up and a little forward.
            Vector3 facing = new Vector3(0f, 1f, 0.35f).normalized;
            Vector3 u = Vector3.Cross(facing, Vector3.right).normalized;
            Vector3 v = Vector3.Cross(facing, u);
            Ring(head, facing, u, v, 10, 0f, 0.2f, 0.052f, petals, positions, colors, normalsOut, indices);
            Ring(head + facing * 0.015f, facing, u, v, 8, 0.5f, 0.13f, 0.044f, petals * 0.78f, positions, colors, normalsOut, indices);
            Ring(head + facing * 0.028f, facing, u, v, 6, 0.25f, 0.055f, 0.034f, petals * 0.5f + new Color(0.25f, 0.18f, 0f) * 0.5f, positions, colors, normalsOut, indices);
            return Make("Dahlia", positions, colors, normalsOut, indices);
        }

        // A ring of pointed petals round a centre, in the plane across <paramref name="facing"/>, cupped a little.
        private static void Ring(Vector3 centre, Vector3 facing, Vector3 u, Vector3 v, int count, float turn, float length, float width,
            Color color, List<Vector3> positions, List<Color> colors, List<Vector3> normalsOut, List<int> indices)
        {
            color.a = 1f;
            for (int i = 0; i < count; i++)
            {
                float angle = (i + turn) * Mathf.PI * 2f / count;
                Vector3 along = u * Mathf.Cos(angle) + v * Mathf.Sin(angle);
                Vector3 tip = centre + along * length + facing * (length * 0.18f);
                Vector3 middle = centre + along * (length * 0.5f) + facing * (length * 0.06f);
                Vector3 across = Vector3.Cross(facing, along).normalized * width;
                Diamond(centre, tip, middle, across, facing, color, positions, colors, normalsOut, indices);
            }
        }

        private static void Blade(Vector3 baseCentre, Vector3 halfWidth, Vector3 top, Vector3 outward, Color root, Color tip,
            List<Vector3> positions, List<Color> colors, List<Vector3> normalsOut, List<int> indices)
        {
            int first = positions.Count;
            positions.Add(baseCentre - halfWidth);
            positions.Add(baseCentre + halfWidth);
            positions.Add(top);
            colors.Add(root);
            colors.Add(root);
            colors.Add(tip);
            // Soft, mostly upward normals, so the tuft is lit like the ground under it.
            Vector3 normal = (Vector3.up * 2f + outward).normalized;
            normalsOut.Add(normal);
            normalsOut.Add(normal);
            normalsOut.Add(normal);
            indices.Add(first);
            indices.Add(first + 2);
            indices.Add(first + 1);
        }

        private static void Diamond(Vector3 bottom, Vector3 top, Vector3 middle, Vector3 halfWidth, Vector3 facing, Color color,
            List<Vector3> positions, List<Color> colors, List<Vector3> normalsOut, List<int> indices)
        {
            int first = positions.Count;
            positions.Add(bottom);
            positions.Add(middle - halfWidth);
            positions.Add(top);
            positions.Add(middle + halfWidth);
            Vector3 normal = (Vector3.up + facing).normalized;
            for (int i = 0; i < 4; i++)
            {
                colors.Add(color);
                normalsOut.Add(normal);
            }
            indices.Add(first);
            indices.Add(first + 1);
            indices.Add(first + 2);
            indices.Add(first);
            indices.Add(first + 2);
            indices.Add(first + 3);
        }

        private static Mesh Make(string name, List<Vector3> positions, List<Color> colors, List<Vector3> normalsOut, List<int> indices)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(positions);
            mesh.SetColors(colors);
            mesh.SetNormals(normalsOut);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
