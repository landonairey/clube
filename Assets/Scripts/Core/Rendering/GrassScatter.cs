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

        private static readonly int FadeStart = Shader.PropertyToID("_FadeStart");
        private static readonly int FadeEnd = Shader.PropertyToID("_FadeEnd");

        [Tooltip("The material grass grows on.")]
        [SerializeField]
        private VoxelMaterial grass;

        [Tooltip("Draws the tufts (Clube/Grass, with GPU instancing on).")]
        [SerializeField]
        private Material material;

        [Tooltip("Grass grows within this many metres of the focus.")]
        [SerializeField, Min(4f)]
        private float radius = 28f;

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

        [Tooltip("Milliseconds a frame may spend scattering chunks.")]
        [SerializeField, Min(0.1f)]
        private float scatterMilliseconds = 1f;

        // Each scattered chunk's tufts, and whether they're in the drawn batches.
        private readonly Dictionary<Vector3Int, Scattered> tufts = new Dictionary<Vector3Int, Scattered>();
        private readonly Stack<Scattered> spare = new Stack<Scattered>();
        private readonly List<Vector3Int> dropped = new List<Vector3Int>();
        private readonly InstanceBatches grassBatches = new InstanceBatches();
        private readonly InstanceBatches wheatBatches = new InstanceBatches();
        private readonly GrassyGround ground = new GrassyGround();
        private readonly List<GroundSpot> spots = new List<GroundSpot>();
        private readonly System.Diagnostics.Stopwatch clock = new System.Diagnostics.Stopwatch();

        private WorldView worldView;
        private TreeCover cover;
        private TreeField coverOf;
        private Mesh tuft;
        private Mesh wheat;
        private MaterialPropertyBlock properties;
        private bool batchesDirty;
        private bool scanPending = true;
        private Vector3 scannedFrom;
        private Vector3Int[] reachOffsets;
        private float reachOffsetsRadius;
        private Vector3 reachOffsetsChunk;

        private sealed class Scattered
        {
            public readonly List<Matrix4x4> Grass = new List<Matrix4x4>();
            public readonly List<Matrix4x4> Wheat = new List<Matrix4x4>();
            public bool Drawn;

            public void Clear()
            {
                Grass.Clear();
                Wheat.Clear();
            }
        }

        /// <summary>Tufts drawn last frame, grass and wheat, for the lab readouts.</summary>
        public int TuftCount { get; private set; }

        /// <summary>Wheat stalks among them.</summary>
        public int WheatCount { get; private set; }

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
        }

        private void Awake()
        {
            worldView = GetComponent<WorldView>();
            tuft = BuildTuft();
            wheat = BuildWheat();
            properties = new MaterialPropertyBlock();
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
        }

        private void OnDestroy()
        {
            if (tuft != null)
            {
                Destroy(tuft);
            }
            if (wheat != null)
            {
                Destroy(wheat);
            }
        }

        private void OnChunkMeshed(Vector3Int coord, ChunkRenderer chunkRenderer)
        {
            scanPending = true;
            if (!tufts.TryGetValue(coord, out Scattered scattered))
            {
                // Not scattered yet: the scan grows it once it's in reach.
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
            // The chunks are looked over only when something changed: a new mesh, the focus moving a
            // metre, or the last look running out of time.
            Vector3 focus = worldView.Focus.position;
            if (scanPending || (focus - scannedFrom).sqrMagnitude > 1f)
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
            foreach (Vector3Int offset in ReachOffsets(world.Grid.ChunkWorldSize))
            {
                Vector3Int coord = centre + offset;
                if (tufts.ContainsKey(coord)
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
                tufts.Add(coord, scattered);
                batchesDirty = true;
            }
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
                        offsets.Add(new Vector3Int(dx, dy, dz));
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

            TreeCover trees = CoverOf(worldView.Trees);
            int seed = worldView.Config.Terrain.Seed;
            foreach (GroundSpot spot in spots)
            {
                int ix = spot.Cell.x, iz = spot.Cell.y;
                Vector3 local = worldView.transform.InverseTransformPoint(spot.Position);
                float forest = trees.At(local.x, local.z);
                float yaw = VoxelHash.Uniform(0, ix, 0, iz, SaltYaw) * 360f;
                float roll = VoxelHash.Uniform(0, ix, 0, iz, SaltHeight);
                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, Vector3.Lerp(Vector3.up, spot.Normal, 0.5f)) * Quaternion.Euler(0f, yaw, 0f);

                float wheatHere = growth.WheatAt(seed, local.x, local.z, forest);
                if (wheatHere > 0f && VoxelHash.Uniform(0, ix, 0, iz, SaltWheat) < wheatHere)
                {
                    float tall = growth.WheatHeightFor(roll);
                    scattered.Wheat.Add(Matrix4x4.TRS(spot.Position, rotation, new Vector3(tall * 0.8f, tall, tall * 0.8f)));
                    continue;
                }
                float baseSize = Mathf.Lerp(height.x, height.y, roll);
                float scale = growth.HeightScale(forest, local.y);
                // Taller grass is a little wider, but mostly it reaches up.
                float width = baseSize * Mathf.Sqrt(scale);
                scattered.Grass.Add(Matrix4x4.TRS(spot.Position, rotation, new Vector3(width, baseSize * scale, width)));
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
            // The fade is set per draw, not on the shared material asset.
            properties.SetFloat(FadeStart, radius - 8f);
            properties.SetFloat(FadeEnd, radius - 1f);
            var parameters = new RenderParams(material)
            {
                matProps = properties,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                layer = gameObject.layer,
            };
            if (batchesDirty)
            {
                RebuildBatches();
            }
            grassBatches.Draw(parameters, tuft);
            wheatBatches.Draw(parameters, wheat);
        }

        // Packs every drawn chunk's tufts into the batches, kept until the tufts change.
        private void RebuildBatches()
        {
            batchesDirty = false;
            grassBatches.Clear();
            wheatBatches.Clear();
            foreach (Scattered scattered in tufts.Values)
            {
                if (scattered.Drawn)
                {
                    grassBatches.AddRange(scattered.Grass);
                    wheatBatches.AddRange(scattered.Wheat);
                }
            }
            TuftCount = grassBatches.Count + wheatBatches.Count;
            WheatCount = wheatBatches.Count;
        }

        private void Recycle(Scattered scattered)
        {
            scattered.Clear();
            scattered.Drawn = false;
            spare.Push(scattered);
        }

        // One tuft, 1 m tall before scaling: seven thin blades leaning out from the middle,
        // dark at the root and light at the tip (alpha: how far up, for the wind).
        private static Mesh BuildTuft()
        {
            const int blades = 7;
            var positions = new List<Vector3>();
            var colors = new List<Color>();
            var normalsOut = new List<Vector3>();
            var indices = new List<int>();
            var root = new Color(0.1f, 0.22f, 0.04f, 0f);
            var tip = new Color(0.34f, 0.52f, 0.12f, 1f);
            var random = new System.Random(7);
            for (int i = 0; i < blades; i++)
            {
                float angle = (i + (float)random.NextDouble() * 0.6f) / blades * Mathf.PI * 2f;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var side = new Vector3(-outward.z, 0f, outward.x);
                float lean = 0.15f + (float)random.NextDouble() * 0.3f;
                float tall = 0.7f + (float)random.NextDouble() * 0.3f;
                Blade(outward * 0.06f, side * 0.07f, outward * lean + Vector3.up * tall, outward, root, tip, positions, colors, normalsOut, indices);
            }
            return Make("Grass tuft", positions, colors, normalsOut, indices);
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
            var root = new Color(0.32f, 0.3f, 0.12f, 0f);
            var tip = new Color(0.8f, 0.68f, 0.34f, 1f);
            var ear = new Color(0.86f, 0.7f, 0.3f, 1f);
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
