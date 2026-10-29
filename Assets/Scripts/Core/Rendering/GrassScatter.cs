using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Core
{
    /// <summary>
    /// Low-poly grass on the terrain (GL67): small tufts of blades stand on the upward-facing
    /// triangles of a chunk's mesh that show grass, in the chunks near the world's focus, drawn
    /// instanced. Tufts stand on a fixed grid across the world (one per cell, jittered by a hash of
    /// the cell), each at the height of the triangle under it, so the same ground always grows the
    /// same grass and a rebuilt mesh keeps every tuft whose ground is still there (GL171).
    /// </summary>
    /// <remarks>
    /// Reads the meshes the pipeline built (positions, normals, and the material ids and weights
    /// in UV channels 2 and 3), so it costs nothing in generation or meshing. A millisecond or so of
    /// chunks is scattered a frame, nearest the focus first. A rebuilt chunk that has grass scatters
    /// again at once, so an edit never leaves a frame without grass or grass floating (GL161, GL171).
    /// A chunk's tufts are kept while the focus is within a few radii of it across the ground, at any
    /// height, and drawn only within the radius, so switching cameras or flying up and back finds the
    /// grass still there (GL167). The tufts shrink into the ground over the last metres of the radius.
    /// </remarks>
    [RequireComponent(typeof(WorldView))]
    public class GrassScatter : MonoBehaviour
    {
        // Instances per draw call: the most RenderMeshInstanced takes from an array.
        private const int BatchSize = 1023;

        // Scattered chunks are kept while the focus is within this many radii of them across the ground.
        private const float KeepRadii = 3f;

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

        [Tooltip("Tuft height in metres, smallest and largest.")]
        [SerializeField]
        private Vector2 height = new Vector2(0.18f, 0.38f);

        [Tooltip("Milliseconds a frame may spend scattering chunks.")]
        [SerializeField, Min(0.1f)]
        private float scatterMilliseconds = 1f;

        // Each scattered chunk's tufts, and whether they're in the drawn batches.
        private readonly Dictionary<Vector3Int, Scattered> tufts = new Dictionary<Vector3Int, Scattered>();
        private readonly Stack<List<Matrix4x4>> spareLists = new Stack<List<Matrix4x4>>();
        private readonly List<Vector3Int> dropped = new List<Vector3Int>();
        private readonly List<Matrix4x4[]> batches = new List<Matrix4x4[]>();
        private readonly List<int> batchCounts = new List<int>();
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector3> ids = new List<Vector3>();
        private readonly List<Vector3> weights = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();
        private readonly System.Diagnostics.Stopwatch clock = new System.Diagnostics.Stopwatch();

        private WorldView worldView;
        private Mesh tuft;
        private MaterialPropertyBlock properties;
        private bool batchesDirty;
        private bool scanPending = true;
        private Vector3 scannedFrom;
        private Vector3Int[] reachOffsets;
        private float reachOffsetsRadius;
        private Vector3 reachOffsetsChunk;

        private sealed class Scattered
        {
            public List<Matrix4x4> Tufts;
            public bool Drawn;
        }

        /// <summary>Tufts drawn last frame, for the lab readouts.</summary>
        public int TuftCount { get; private set; }

        /// <summary>Chunks whose tufts are kept, drawn or not.</summary>
        public int KeptChunkCount => tufts.Count;

        private void Awake()
        {
            worldView = GetComponent<WorldView>();
            tuft = BuildTuft();
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
                Recycle(scattered.Tufts);
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
                Scatter(chunkRenderer, scattered.Tufts);
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
                List<Matrix4x4> list = spareLists.Count > 0 ? spareLists.Pop() : new List<Matrix4x4>();
                Scatter(chunkRenderer, list);
                tufts.Add(coord, new Scattered { Tufts = list, Drawn = true });
                batchesDirty = true;
            }
        }

        private void Drop(Vector3Int coord)
        {
            if (tufts.TryGetValue(coord, out Scattered scattered))
            {
                Recycle(scattered.Tufts);
                tufts.Remove(coord);
                batchesDirty |= scattered.Drawn;
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

        // Fills the list with the chunk's tufts: one per grid cell whose point lies, seen from above, on an
        // upward, grassy triangle, at that triangle's height there.
        private void Scatter(ChunkRenderer chunkRenderer, List<Matrix4x4> list)
        {
            list.Clear();
            Mesh mesh = chunkRenderer.Mesh;
            if (mesh == null || mesh.vertexCount == 0 || density <= 0f)
            {
                return;
            }
            mesh.GetVertices(vertices);
            mesh.GetNormals(normals);
            mesh.GetUVs(2, ids);
            mesh.GetUVs(3, weights);
            mesh.GetTriangles(triangles, 0);
            if (ids.Count != vertices.Count || weights.Count != vertices.Count)
            {
                return;
            }

            Transform chunkTransform = chunkRenderer.Transform;
            float grassId = grass.Id;
            float cell = 1f / Mathf.Sqrt(density);
            for (int t = 0; t < triangles.Count; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                Vector3 normal = (normals[a] + normals[b] + normals[c]).normalized;
                if (normal.y < minUp || GrassShare(a) + GrassShare(b) + GrassShare(c) < 1.8f)
                {
                    continue;
                }
                Vector3 pa = chunkTransform.TransformPoint(vertices[a]);
                Vector3 pb = chunkTransform.TransformPoint(vertices[b]);
                Vector3 pc = chunkTransform.TransformPoint(vertices[c]);

                // The triangle seen from above, as barycentric weights over x and z.
                float denominator = (pb.z - pc.z) * (pa.x - pc.x) + (pc.x - pb.x) * (pa.z - pc.z);
                if (Mathf.Abs(denominator) < 1e-9f)
                {
                    continue;
                }
                float inverse = 1f / denominator;
                int x0 = Mathf.FloorToInt(Mathf.Min(pa.x, Mathf.Min(pb.x, pc.x)) / cell);
                int x1 = Mathf.FloorToInt(Mathf.Max(pa.x, Mathf.Max(pb.x, pc.x)) / cell);
                int z0 = Mathf.FloorToInt(Mathf.Min(pa.z, Mathf.Min(pb.z, pc.z)) / cell);
                int z1 = Mathf.FloorToInt(Mathf.Max(pa.z, Mathf.Max(pb.z, pc.z)) / cell);
                for (int iz = z0; iz <= z1; iz++)
                {
                    for (int ix = x0; ix <= x1; ix++)
                    {
                        // The cell's point, kept when it lies on this triangle.
                        float px = (ix + VoxelHash.Uniform(0, ix, 0, iz, 1)) * cell;
                        float pz = (iz + VoxelHash.Uniform(0, ix, 0, iz, 2)) * cell;
                        float wa = ((pb.z - pc.z) * (px - pc.x) + (pc.x - pb.x) * (pz - pc.z)) * inverse;
                        float wb = ((pc.z - pa.z) * (px - pc.x) + (pa.x - pc.x) * (pz - pc.z)) * inverse;
                        float wc = 1f - wa - wb;
                        if (wa < 0f || wb < 0f || wc < 0f)
                        {
                            continue;
                        }
                        var position = new Vector3(px, pa.y * wa + pb.y * wb + pc.y * wc, pz);
                        float yaw = VoxelHash.Uniform(0, ix, 0, iz, 3) * 360f;
                        float size = Mathf.Lerp(height.x, height.y, VoxelHash.Uniform(0, ix, 0, iz, 4));
                        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, Vector3.Lerp(Vector3.up, normal, 0.5f)) * Quaternion.Euler(0f, yaw, 0f);
                        list.Add(Matrix4x4.TRS(position, rotation, new Vector3(size, size, size)));
                    }
                }
            }

            // How much of a vertex shows grass, 0-1.
            float GrassShare(int vertex)
            {
                Vector3 id = ids[vertex];
                Vector3 weight = weights[vertex];
                float total = weight.x + weight.y + weight.z;
                if (total <= 0f)
                {
                    return 0f;
                }
                float share = (Mathf.Approximately(id.x, grassId) ? weight.x : 0f)
                            + (Mathf.Approximately(id.y, grassId) ? weight.y : 0f)
                            + (Mathf.Approximately(id.z, grassId) ? weight.z : 0f);
                return share / total;
            }
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
            for (int i = 0; i < batchCounts.Count; i++)
            {
                Graphics.RenderMeshInstanced(parameters, tuft, 0, batches[i], batchCounts[i]);
            }
        }

        // Packs every chunk's tufts into arrays of up to BatchSize, kept until the tufts change.
        private void RebuildBatches()
        {
            batchesDirty = false;
            batchCounts.Clear();
            int filled = 0;
            int total = 0;
            foreach (Scattered scattered in tufts.Values)
            {
                if (!scattered.Drawn)
                {
                    continue;
                }
                List<Matrix4x4> list = scattered.Tufts;
                for (int i = 0; i < list.Count; i++)
                {
                    if (filled == 0)
                    {
                        if (batches.Count == batchCounts.Count)
                        {
                            batches.Add(new Matrix4x4[BatchSize]);
                        }
                        batchCounts.Add(0);
                    }
                    batches[batchCounts.Count - 1][filled++] = list[i];
                    batchCounts[batchCounts.Count - 1] = filled;
                    total++;
                    if (filled == BatchSize)
                    {
                        filled = 0;
                    }
                }
            }
            TuftCount = total;
        }

        private void Recycle(List<Matrix4x4> list)
        {
            list.Clear();
            spareLists.Push(list);
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
                float width = 0.07f;
                Vector3 baseCentre = outward * 0.06f;
                int first = positions.Count;
                positions.Add(baseCentre - side * width);
                positions.Add(baseCentre + side * width);
                positions.Add(baseCentre + outward * lean + Vector3.up * tall);
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
            var mesh = new Mesh { name = "Grass tuft" };
            mesh.SetVertices(positions);
            mesh.SetColors(colors);
            mesh.SetNormals(normalsOut);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
