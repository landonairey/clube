using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Core
{
    /// <summary>
    /// A chunk mesh's streams as flora reads them (GL775): positions, normals, material ids and weights (UV channels 2 and 3)
    /// and the first sub-mesh's indices, copied out of a <see cref="Mesh.MeshData"/> inside a job, so no managed copy of the
    /// mesh is made on the main thread. The Burst side of <see cref="GrassyGround"/>.
    /// </summary>
    public struct GroundMesh : IDisposable
    {
        public NativeArray<Vector3> Vertices;
        public NativeArray<Vector3> Normals;
        public NativeArray<Vector3> Ids;
        public NativeArray<Vector3> Weights;
        public NativeArray<int> Indices;

        /// <summary>False when the mesh is empty or lacks a stream flora needs: it then has no spots.</summary>
        public bool IsValid;

        /// <summary>Reads the streams of <paramref name="data"/> into new arrays (dispose them, or use <see cref="Allocator.Temp"/>).</summary>
        public static GroundMesh Read(in Mesh.MeshData data, Allocator allocator)
        {
            var mesh = new GroundMesh();
            int count = data.vertexCount;
            if (count == 0 || data.subMeshCount == 0 || !data.HasVertexAttribute(VertexAttribute.Normal)
                || !data.HasVertexAttribute(VertexAttribute.TexCoord2) || !data.HasVertexAttribute(VertexAttribute.TexCoord3))
            {
                return mesh;
            }
            mesh.Vertices = new NativeArray<Vector3>(count, allocator, NativeArrayOptions.UninitializedMemory);
            mesh.Normals = new NativeArray<Vector3>(count, allocator, NativeArrayOptions.UninitializedMemory);
            mesh.Ids = new NativeArray<Vector3>(count, allocator, NativeArrayOptions.UninitializedMemory);
            mesh.Weights = new NativeArray<Vector3>(count, allocator, NativeArrayOptions.UninitializedMemory);
            mesh.Indices = new NativeArray<int>(data.GetSubMesh(0).indexCount, allocator, NativeArrayOptions.UninitializedMemory);
            data.GetVertices(mesh.Vertices);
            data.GetNormals(mesh.Normals);
            data.GetUVs(2, mesh.Ids);
            data.GetUVs(3, mesh.Weights);
            data.GetIndices(mesh.Indices, 0);
            mesh.IsValid = true;
            return mesh;
        }

        public void Dispose()
        {
            if (Vertices.IsCreated)
            {
                Vertices.Dispose();
                Normals.Dispose();
                Ids.Dispose();
                Weights.Dispose();
                Indices.Dispose();
            }
            IsValid = false;
        }
    }

    /// <summary>
    /// <see cref="GrassyGround"/>'s search for Burst jobs (GL775): the same spots from a <see cref="GroundMesh"/>. <c>GroundSpotSearchTests</c> checks the two agree.
    /// </summary>
    public static class GroundSpotSearch
    {
        /// <summary>Most ground materials one search can take together.</summary>
        public const int MaxGrounds = 7;

        /// <summary>Adds the mesh's spots to <paramref name="spots"/>, as <see cref="GrassyGround"/> does.</summary>
        public static void Find(in GroundMesh mesh, float4x4 toWorld, in FixedList32Bytes<float> grounds, float minUp, float cell, int seed,
            int saltX, int saltZ, float margin, ref NativeList<GroundSpot> spots)
        {
            if (!mesh.IsValid || cell <= 0f || grounds.Length == 0)
            {
                return;
            }
            float span = 1f - 2f * margin;
            NativeArray<int> triangles = mesh.Indices;
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                float3 na = mesh.Normals[a], nb = mesh.Normals[b], nc = mesh.Normals[c];
                float3 sum = na + nb + nc;
                // Vector3.normalized: zero below 1e-5.
                float length = math.length(sum);
                float3 normal = length > 1e-5f ? sum / length : float3.zero;
                if (normal.y < minUp || Share(mesh, grounds, a) + Share(mesh, grounds, b) + Share(mesh, grounds, c) < 1.8f)
                {
                    continue;
                }
                float3 pa = math.transform(toWorld, mesh.Vertices[a]);
                float3 pb = math.transform(toWorld, mesh.Vertices[b]);
                float3 pc = math.transform(toWorld, mesh.Vertices[c]);

                float denominator = (pb.z - pc.z) * (pa.x - pc.x) + (pc.x - pb.x) * (pa.z - pc.z);
                if (math.abs(denominator) < 1e-9f)
                {
                    continue;
                }
                float inverse = 1f / denominator;
                int x0 = (int)math.floor(math.min(pa.x, math.min(pb.x, pc.x)) / cell);
                int x1 = (int)math.floor(math.max(pa.x, math.max(pb.x, pc.x)) / cell);
                int z0 = (int)math.floor(math.min(pa.z, math.min(pb.z, pc.z)) / cell);
                int z1 = (int)math.floor(math.max(pa.z, math.max(pb.z, pc.z)) / cell);
                for (int iz = z0; iz <= z1; iz++)
                {
                    for (int ix = x0; ix <= x1; ix++)
                    {
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
        private static float Share(in GroundMesh mesh, in FixedList32Bytes<float> grounds, int vertex)
        {
            Vector3 id = mesh.Ids[vertex];
            Vector3 weight = mesh.Weights[vertex];
            float total = weight.x + weight.y + weight.z;
            if (total <= 0f)
            {
                return 0f;
            }
            float share = (IsGround(grounds, id.x) ? weight.x : 0f) + (IsGround(grounds, id.y) ? weight.y : 0f) + (IsGround(grounds, id.z) ? weight.z : 0f);
            return share / total;
        }

        private static bool IsGround(in FixedList32Bytes<float> grounds, float id)
        {
            for (int i = 0; i < grounds.Length; i++)
            {
                if (GrassyGround.SameId(id, grounds[i]))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
