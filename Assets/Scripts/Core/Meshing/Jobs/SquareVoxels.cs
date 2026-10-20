using Unity.Collections;
using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// The square half of a chunk's mixed mesh (GL86, first pass of MX2): every solid sample
    /// marked square is a sharp cube one voxel across, centred on the sample, with a face on each
    /// side whose neighbour isn't a solid square sample too. Marching cubes leaves square samples
    /// out (<see cref="ChunkMeshJob"/> meshes them as air), so its surface meets a cube at the
    /// half-voxel plane between them, where these faces lie. Burst-compatible.
    /// </summary>
    /// <remarks>
    /// A chunk draws the cubes of the samples it owns, those below its last layer on each axis;
    /// the last layer is the next chunk's first, which draws them. Faces towards a sample outside
    /// the chunk are always drawn (the neighbour isn't known here); they are hidden inside the
    /// ground when it is solid.
    /// </remarks>
    public static class SquareVoxels
    {
        /// <summary>Adds the cubes' faces as their own vertices (flat-shaded), with each vertex's material.</summary>
        public static void AddCubes(
            NativeArray<float> densities, NativeArray<byte> squares, NativeArray<byte> materials, byte uniformMaterial,
            int3 count, float isoLevel, float voxelSize,
            ref NativeList<float3> positions, ref NativeList<byte> vertexMaterials, ref NativeList<int> indices)
        {
            for (int z = 0; z < count.z - 1; z++)
            {
                for (int y = 0; y < count.y - 1; y++)
                {
                    for (int x = 0; x < count.x - 1; x++)
                    {
                        int index = x + count.x * (y + count.y * z);
                        if (squares[index] == 0 || densities[index] < isoLevel)
                        {
                            continue;
                        }
                        byte material = materials.Length > 0 ? materials[index] : uniformMaterial;
                        var sample = new int3(x, y, z);
                        for (int side = 0; side < 6; side++)
                        {
                            Face(side, out int3 direction, out float3 u, out float3 v);
                            if (SolidSquare(sample + direction, densities, squares, count, isoLevel))
                            {
                                continue;
                            }
                            AddFace((float3)sample * voxelSize, (float3)direction, u, v, voxelSize * 0.5f, material,
                                ref positions, ref vertexMaterials, ref indices);
                        }
                    }
                }
            }
        }

        // A side's outward direction and two axes along the face with cross(u, v) = direction,
        // so the winding below is clockwise seen from outside (Unity's front face).
        private static void Face(int side, out int3 direction, out float3 u, out float3 v)
        {
            switch (side)
            {
                case 0:
                    direction = new int3(1, 0, 0);
                    u = new float3(0, 1, 0);
                    v = new float3(0, 0, 1);
                    break;
                case 1:
                    direction = new int3(-1, 0, 0);
                    u = new float3(0, 0, 1);
                    v = new float3(0, 1, 0);
                    break;
                case 2:
                    direction = new int3(0, 1, 0);
                    u = new float3(0, 0, 1);
                    v = new float3(1, 0, 0);
                    break;
                case 3:
                    direction = new int3(0, -1, 0);
                    u = new float3(1, 0, 0);
                    v = new float3(0, 0, 1);
                    break;
                case 4:
                    direction = new int3(0, 0, 1);
                    u = new float3(1, 0, 0);
                    v = new float3(0, 1, 0);
                    break;
                default:
                    direction = new int3(0, 0, -1);
                    u = new float3(0, 1, 0);
                    v = new float3(1, 0, 0);
                    break;
            }
        }

        private static bool SolidSquare(int3 sample, NativeArray<float> densities, NativeArray<byte> squares, int3 count, float isoLevel)
        {
            if (math.any(sample < 0) || math.any(sample >= count))
            {
                return false;
            }
            int index = sample.x + count.x * (sample.y + count.y * sample.z);
            return squares[index] != 0 && densities[index] >= isoLevel;
        }

        private static void AddFace(
            float3 centre, float3 direction, float3 u, float3 v, float half, byte material,
            ref NativeList<float3> positions, ref NativeList<byte> vertexMaterials, ref NativeList<int> indices)
        {
            float3 middle = centre + direction * half;
            int first = positions.Length;
            positions.Add(middle - (u + v) * half);
            positions.Add(middle + (u - v) * half);
            positions.Add(middle + (u + v) * half);
            positions.Add(middle + (v - u) * half);
            for (int i = 0; i < 4; i++)
            {
                vertexMaterials.Add(material);
            }
            indices.Add(first);
            indices.Add(first + 1);
            indices.Add(first + 2);
            indices.Add(first);
            indices.Add(first + 2);
            indices.Add(first + 3);
        }
    }
}
