using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>A spot on grassy ground (<see cref="GrassyGround"/>): where, the ground's facing there, and its grid cell.</summary>
    public struct GroundSpot
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2Int Cell;
    }

    /// <summary>
    /// Finds the spots on a chunk's built mesh where flora stands (grass, GL67; bushes, GL236): one per cell of a
    /// fixed grid across the world, at a point in the cell rolled from its hash, kept when that point lies, seen from
    /// above, on an upward-facing triangle that shows grass, at the triangle's height there. The same ground always
    /// gives the same spots, so a rebuilt mesh keeps every spot whose ground is still there.
    /// </summary>
    /// <remarks>
    /// Reads the mesh's positions, normals and material ids and weights (UV channels 2 and 3); keeps its buffers. Since GL775
    /// the game finds spots in Burst jobs (<see cref="GroundSpotSearch"/>); this managed copy is the reference they're tested against.
    /// </remarks>
    public sealed class GrassyGround
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector3> ids = new List<Vector3>();
        private readonly List<Vector3> weights = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();
        private IReadOnlyList<float> groundIds;

        /// <summary>
        /// True when a mesh's material id is <paramref name="ground"/>'s (ids are whole numbers carried as floats):
        /// <see cref="Mathf.Approximately"/> written out, so Burst jobs can share it.
        /// </summary>
        public static bool SameId(float id, float ground)
        {
            return System.Math.Abs(ground - id) < System.Math.Max(1e-6f * System.Math.Max(System.Math.Abs(id), System.Math.Abs(ground)), 8f * float.Epsilon);
        }

        private bool IsGround(float id)
        {
            for (int i = 0; i < groundIds.Count; i++)
            {
                if (SameId(id, groundIds[i]))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Adds the mesh's spots to <paramref name="spots"/>: on triangles facing up at least <paramref name="minUp"/>
        /// (the y of the normal) and mostly the materials <paramref name="grounds"/> together, one per <paramref name="cell"/>-metre
        /// grid cell, at a point rolled with the seed and salts, kept <paramref name="margin"/> of the cell off its edges.
        /// <paramref name="toWorld"/> places the mesh in the world.
        /// </summary>
        public void Find(Mesh mesh, Matrix4x4 toWorld, IReadOnlyList<float> grounds, float minUp, float cell, int seed, int saltX, int saltZ, float margin,
            List<GroundSpot> spots)
        {
            groundIds = grounds;
            if (mesh == null || mesh.vertexCount == 0 || cell <= 0f || grounds.Count == 0)
            {
                return;
            }
            mesh.GetVertices(vertices);
            mesh.GetNormals(normals);
            mesh.GetUVs(2, ids);
            mesh.GetUVs(3, weights);
            mesh.GetTriangles(triangles, 0);
            if (ids.Count != vertices.Count || weights.Count != vertices.Count || normals.Count != vertices.Count)
            {
                return;
            }

            float span = 1f - 2f * margin;
            for (int t = 0; t < triangles.Count; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                Vector3 normal = (normals[a] + normals[b] + normals[c]).normalized;
                if (normal.y < minUp || GrassShare(a) + GrassShare(b) + GrassShare(c) < 1.8f)
                {
                    continue;
                }
                Vector3 pa = toWorld.MultiplyPoint3x4(vertices[a]);
                Vector3 pb = toWorld.MultiplyPoint3x4(vertices[b]);
                Vector3 pc = toWorld.MultiplyPoint3x4(vertices[c]);

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
                        float px = (ix + margin + span * VoxelHash.Uniform(seed, ix, 0, iz, saltX)) * cell;
                        float pz = (iz + margin + span * VoxelHash.Uniform(seed, ix, 0, iz, saltZ)) * cell;
                        float wa = ((pb.z - pc.z) * (px - pc.x) + (pc.x - pb.x) * (pz - pc.z)) * inverse;
                        float wb = ((pc.z - pa.z) * (px - pc.x) + (pa.x - pc.x) * (pz - pc.z)) * inverse;
                        float wc = 1f - wa - wb;
                        if (wa < 0f || wb < 0f || wc < 0f)
                        {
                            continue;
                        }
                        spots.Add(new GroundSpot
                        {
                            Position = new Vector3(px, pa.y * wa + pb.y * wb + pc.y * wc, pz),
                            Normal = normal,
                            Cell = new Vector2Int(ix, iz),
                        });
                    }
                }
            }
        }

        // How much of a vertex shows the ground materials, 0-1.
        private float GrassShare(int vertex)
        {
            Vector3 id = ids[vertex];
            Vector3 weight = weights[vertex];
            float total = weight.x + weight.y + weight.z;
            if (total <= 0f)
            {
                return 0f;
            }
            float share = (IsGround(id.x) ? weight.x : 0f) + (IsGround(id.y) ? weight.y : 0f) + (IsGround(id.z) ? weight.z : 0f);
            return share / total;
        }
    }
}
