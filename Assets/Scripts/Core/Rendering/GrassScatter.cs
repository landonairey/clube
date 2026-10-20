using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Core
{
    /// <summary>
    /// Low-poly grass on the terrain (GL67): small tufts of blades stand on the upward-facing
    /// triangles of a chunk's mesh that show grass, in the chunks near the world's focus, drawn
    /// instanced. Where a tuft goes is hashed from the triangle's position, so the same ground
    /// always grows the same grass; a rebuilt mesh (an edit) scatters again, so dug-away grass
    /// goes with its ground.
    /// </summary>
    /// <remarks>
    /// Reads the meshes the pipeline built (positions, normals, and the material ids and weights
    /// in UV channels 2 and 3), so it costs nothing in generation or meshing. A millisecond or so of
    /// chunks is scattered a frame; the radius is small, so their order doesn't matter. The tufts
    /// shrink into the ground over the last metres of the radius.
    /// </remarks>
    [RequireComponent(typeof(WorldView))]
    public class GrassScatter : MonoBehaviour
    {
        // Instances per draw call: the most RenderMeshInstanced takes from an array.
        private const int BatchSize = 1023;

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

        private readonly Dictionary<Vector3Int, List<Matrix4x4>> tufts = new Dictionary<Vector3Int, List<Matrix4x4>>();
        private readonly HashSet<Vector3Int> stale = new HashSet<Vector3Int>();
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

        /// <summary>Tufts drawn last frame, for the lab readouts.</summary>
        public int TuftCount { get; private set; }

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
            foreach (List<Matrix4x4> list in tufts.Values)
            {
                Recycle(list);
            }
            tufts.Clear();
            stale.Clear();
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
            stale.Add(coord);
            scanPending = true;
        }

        private void OnChunkUnloaded(Vector3Int coord)
        {
            stale.Remove(coord);
            if (tufts.TryGetValue(coord, out List<Matrix4x4> list))
            {
                Recycle(list);
                tufts.Remove(coord);
                batchesDirty = true;
            }
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

            // Grass leaves the chunks that have gone out of reach.
            dropped.Clear();
            foreach (Vector3Int coord in tufts.Keys)
            {
                if (!worldView.Renderers.TryGetValue(coord, out ChunkRenderer chunkRenderer) || Distance(chunkRenderer, focus) > far)
                {
                    dropped.Add(coord);
                }
            }
            foreach (Vector3Int coord in dropped)
            {
                Recycle(tufts[coord]);
                tufts.Remove(coord);
                batchesDirty = true;
            }

            // And grows on a few chunks in reach that have none yet, or a new mesh.
            clock.Restart();
            foreach (KeyValuePair<Vector3Int, ChunkRenderer> entry in worldView.Renderers)
            {
                if (clock.Elapsed.TotalMilliseconds > scatterMilliseconds)
                {
                    scanPending = true;
                    break;
                }
                if ((tufts.ContainsKey(entry.Key) && !stale.Contains(entry.Key)) || Distance(entry.Value, focus) > near)
                {
                    continue;
                }
                Scatter(entry.Key, entry.Value);
                stale.Remove(entry.Key);
                batchesDirty = true;
            }
        }

        // Squared distance from the focus to the chunk's centre.
        private float Distance(ChunkRenderer chunkRenderer, Vector3 focus)
        {
            Vector3 centre = chunkRenderer.Transform.position + worldView.World.Grid.ChunkWorldSize * 0.5f;
            return (centre - focus).sqrMagnitude;
        }

        private void Scatter(Vector3Int coord, ChunkRenderer chunkRenderer)
        {
            if (!tufts.TryGetValue(coord, out List<Matrix4x4> list))
            {
                list = spareLists.Count > 0 ? spareLists.Pop() : new List<Matrix4x4>();
                tufts.Add(coord, list);
            }
            list.Clear();
            Mesh mesh = chunkRenderer.Mesh;
            if (mesh == null || mesh.vertexCount == 0)
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
                float area = Vector3.Cross(pb - pa, pc - pa).magnitude * 0.5f;

                // Tufts for this triangle: its share of the density, the fraction settled by a hash of where it is.
                Vector3 centre = (pa + pb + pc) / 3f;
                int hx = Mathf.FloorToInt(centre.x * 16f), hy = Mathf.FloorToInt(centre.y * 16f), hz = Mathf.FloorToInt(centre.z * 16f);
                float wanted = area * density;
                int count = (int)wanted + (VoxelHash.Uniform(0, hx, hy, hz, 1) < wanted - (int)wanted ? 1 : 0);
                for (int i = 0; i < count; i++)
                {
                    // A point inside the triangle, folded into it when it lands outside.
                    float u = VoxelHash.Uniform(0, hx, hy, hz, 10 + i * 4);
                    float v = VoxelHash.Uniform(0, hx, hy, hz, 11 + i * 4);
                    if (u + v > 1f)
                    {
                        u = 1f - u;
                        v = 1f - v;
                    }
                    Vector3 position = pa + (pb - pa) * u + (pc - pa) * v;
                    float yaw = VoxelHash.Uniform(0, hx, hy, hz, 12 + i * 4) * 360f;
                    float size = Mathf.Lerp(height.x, height.y, VoxelHash.Uniform(0, hx, hy, hz, 13 + i * 4));
                    Quaternion rotation = Quaternion.FromToRotation(Vector3.up, Vector3.Lerp(Vector3.up, normal, 0.5f)) * Quaternion.Euler(0f, yaw, 0f);
                    list.Add(Matrix4x4.TRS(position, rotation, new Vector3(size, size, size)));
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
            foreach (List<Matrix4x4> list in tufts.Values)
            {
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
