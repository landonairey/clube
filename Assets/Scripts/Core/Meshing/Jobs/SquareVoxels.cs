using Unity.Collections;
using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// The square half of a chunk's mixed mesh (GL86, first pass of MX2): a voxel whose eight corner
    /// samples are all solid and marked square is a sharp cube filling that voxel, on the same grid
    /// as the marching cubes, with a face on each side whose neighbouring voxel isn't such a cube too.
    /// So chiselling digs in: the cube faces lie on the outer samples, about half a voxel inside where
    /// the marching surface was. <see cref="ChunkMeshJob"/> meshes square samples as air for marching
    /// cubes, except where they touch unchiselled solid ground, so the two meet without a gap. Burst-compatible.
    /// </summary>
    /// <remarks>
    /// A chunk draws the cubes of its own voxels. Faces towards a voxel outside the chunk are always
    /// drawn (the neighbour isn't known here); they are hidden inside the ground when it is solid.
    /// </remarks>
    public static class SquareVoxels
    {
        /// <summary>Adds the cubes' faces as their own vertices (flat-shaded), each in the material of the voxel's lowest corner.</summary>
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
                        var voxel = new int3(x, y, z);
                        if (!IsCube(voxel, densities, squares, count, isoLevel))
                        {
                            continue;
                        }
                        int index = x + count.x * (y + count.y * z);
                        byte material = materials.Length > 0 ? materials[index] : uniformMaterial;
                        float3 centre = ((float3)voxel + 0.5f) * voxelSize;
                        for (int side = 0; side < 6; side++)
                        {
                            Face(side, out int3 direction, out float3 u, out float3 v);
                            if (IsCube(voxel + direction, densities, squares, count, isoLevel))
                            {
                                continue;
                            }
                            AddFace(centre, (float3)direction, u, v, voxelSize * 0.5f, material,
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

        // A voxel (by its lowest corner) is a cube when all eight of its corners are solid and square.
        private static bool IsCube(int3 voxel, NativeArray<float> densities, NativeArray<byte> squares, int3 count, float isoLevel)
        {
            if (math.any(voxel < 0) || math.any(voxel + 1 >= count))
            {
                return false;
            }
            for (int corner = 0; corner < 8; corner++)
            {
                int3 sample = voxel + new int3(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1);
                int index = sample.x + count.x * (sample.y + count.y * sample.z);
                if (squares[index] == 0 || densities[index] < isoLevel)
                {
                    return false;
                }
            }
            return true;
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
