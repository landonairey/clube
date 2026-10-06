using Unity.Collections;
using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>A mesh vertex with material ids and weights (M13): position, normal, UV2 (ids), UV3 (weights).</summary>
    public struct MaterialVertex
    {
        public float3 Position;
        public float3 Normal;
        public float3 Ids;
        public float3 Weights;
    }

    /// <summary>A mesh vertex without materials: position and normal.</summary>
    public struct PlainVertex
    {
        public float3 Position;
        public float3 Normal;
    }

    /// <summary>
    /// The steps after surface extraction, as Burst-compatible code for the mesh job (K35):
    /// normals, and the material split for hard seams or blending (M13, M15). Each matches its
    /// managed counterpart (<see cref="MeshNormals"/>, <see cref="HardSeamSplitter"/>,
    /// <see cref="BlendedSplitter"/>), which the labs' managed path still uses.
    /// </summary>
    public static class MeshKernels
    {
        /// <summary>
        /// Vertex normals as <c>Mesh.RecalculateNormals</c> makes them: each triangle adds its
        /// area-weighted face normal to its vertices (<see cref="MeshNormals.Compute"/>).
        /// </summary>
        public static void Normals(NativeList<float3> positions, NativeList<int> indices, NativeArray<float3> normals)
        {
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = float3.zero;
            }
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a = indices[i];
                int b = indices[i + 1];
                int c = indices[i + 2];

                // Unnormalized, so larger triangles count for more.
                float3 face = math.cross(positions[b] - positions[a], positions[c] - positions[a]);
                normals[a] += face;
                normals[b] += face;
                normals[c] += face;
            }
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = math.normalizesafe(normals[i]);
            }
        }

        /// <summary>
        /// Hard seams (M15): each triangle shows the material most of its corners have (the
        /// first corner's when all three differ); a vertex shared by triangles of different
        /// materials is copied once per material, so nothing bleeds.
        /// </summary>
        public static void SplitHardSeams(
            NativeList<float3> positions, NativeArray<float3> normals, NativeList<int> indices, NativeList<byte> materials,
            ref NativeList<MaterialVertex> vertices, ref NativeList<int> outIndices)
        {
            var copies = new NativeHashMap<long, int>(math.max(16, positions.Length), Allocator.Temp);
            var whole = new float3(1f, 0f, 0f);
            for (int i = 0; i < indices.Length; i += 3)
            {
                byte material = Dominant(materials[indices[i]], materials[indices[i + 1]], materials[indices[i + 2]]);
                var ids = new float3((float)material);
                for (int corner = 0; corner < 3; corner++)
                {
                    int source = indices[i + corner];
                    long key = ((long)material << 32) | (uint)source;
                    if (!copies.TryGetValue(key, out int index))
                    {
                        index = vertices.Length;
                        vertices.Add(new MaterialVertex { Position = positions[source], Normal = normals[source], Ids = ids, Weights = whole });
                        copies.Add(key, index);
                    }
                    outIndices.Add(index);
                }
            }
        }

        /// <summary>
        /// Blended (M13): each triangle's vertices carry its three material ids, sorted, with a
        /// weight of 1 on their own; vertices are copied only where neighbouring triangles hold
        /// different sets of materials, so uniform ground stays shared.
        /// </summary>
        public static void SplitBlended(
            NativeList<float3> positions, NativeArray<float3> normals, NativeList<int> indices, NativeList<byte> materials,
            ref NativeList<MaterialVertex> vertices, ref NativeList<int> outIndices)
        {
            var copies = new NativeHashMap<long, int>(math.max(16, positions.Length), Allocator.Temp);
            for (int i = 0; i < indices.Length; i += 3)
            {
                Sort(materials[indices[i]], materials[indices[i + 1]], materials[indices[i + 2]], out byte low, out byte mid, out byte high);
                var ids = new float3(low, mid, high);
                long set = ((long)low << 32) | ((long)mid << 40) | ((long)high << 48);
                for (int corner = 0; corner < 3; corner++)
                {
                    int source = indices[i + corner];
                    long key = set | (uint)source;
                    if (!copies.TryGetValue(key, out int index))
                    {
                        byte own = materials[source];
                        float3 weights = own == low ? new float3(1f, 0f, 0f) : own == mid ? new float3(0f, 1f, 0f) : new float3(0f, 0f, 1f);
                        index = vertices.Length;
                        vertices.Add(new MaterialVertex { Position = positions[source], Normal = normals[source], Ids = ids, Weights = weights });
                        copies.Add(key, index);
                    }
                    outIndices.Add(index);
                }
            }
        }

        /// <summary>The material at least two corners share, or the first corner's (<see cref="HardSeamSplitter.Dominant"/>).</summary>
        public static byte Dominant(byte a, byte b, byte c)
        {
            return a == b || a == c ? a : b == c ? b : a;
        }

        private static void Sort(byte a, byte b, byte c, out byte low, out byte mid, out byte high)
        {
            // Three compare-and-swaps, with plain temporaries (no tuples in Burst code).
            byte swap;
            if (a > b)
            {
                swap = a;
                a = b;
                b = swap;
            }
            if (b > c)
            {
                swap = b;
                b = c;
                c = swap;
            }
            if (a > b)
            {
                swap = a;
                a = b;
                b = swap;
            }
            low = a;
            mid = b;
            high = c;
        }
    }
}
