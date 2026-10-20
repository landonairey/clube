using Unity.Collections;
using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// The carved half of a chunk's mixed mesh (GL92, first pass of MX2): a voxel the chisel has
    /// carved out (<see cref="CarveFlags"/>) is left empty by marching cubes
    /// (<see cref="SurfaceExtraction"/>), and each solid neighbour shows a flat wall on the face it
    /// shares with it. The wall is that face's cross-section of the neighbour's ground (marching
    /// squares over its four corner samples), with its edge points placed as marching cubes places
    /// them, so the neighbour's rounded surface meets the sharp wall without a gap. Burst-compatible.
    /// </summary>
    /// <remarks>
    /// A chunk draws the walls of its own carved voxels. It knows the carved voxels just outside it
    /// from the copies of its border samples, so walls between two carved voxels are never drawn.
    /// </remarks>
    public static class CarvedVoxels
    {
        /// <summary>
        /// True when a voxel (by its lowest corner) is carved; voxels one step outside the chunk can be
        /// asked about too, through the flag on their highest corner.
        /// </summary>
        public static bool IsCarved(NativeArray<byte> carves, int3 count, int3 voxel)
        {
            if (carves.Length == 0)
            {
                return false;
            }
            if (math.all(voxel >= 0) && math.all(voxel < count))
            {
                return (carves[Index(voxel, count)] & CarveFlags.Above) != 0;
            }
            int3 highest = voxel + 1;
            return math.all(highest >= 0) && math.all(highest < count) && (carves[Index(highest, count)] & CarveFlags.Below) != 0;
        }

        /// <summary>Adds the walls round every carved voxel of the chunk, each as its own vertices (flat-shaded).</summary>
        public static void AddWalls<TPlacement>(
            NativeArray<float> densities, NativeArray<byte> carves, NativeArray<byte> materials, byte uniformMaterial,
            int3 count, float isoLevel, float voxelSize, TPlacement placement,
            ref NativeList<float3> positions, ref NativeList<byte> vertexMaterials, ref NativeList<int> indices)
            where TPlacement : struct, IEdgePlacement
        {
            if (carves.Length == 0)
            {
                return;
            }
            var wall = new NativeList<float3>(8, Allocator.Temp);
            var wallMaterials = new NativeList<byte>(8, Allocator.Temp);
            for (int z = 0; z < count.z - 1; z++)
            {
                for (int y = 0; y < count.y - 1; y++)
                {
                    for (int x = 0; x < count.x - 1; x++)
                    {
                        var voxel = new int3(x, y, z);
                        if ((carves[Index(voxel, count)] & CarveFlags.Above) == 0)
                        {
                            continue;
                        }
                        for (int side = 0; side < 6; side++)
                        {
                            Face(side, out int3 direction, out int3 a, out int3 b);
                            if (IsCarved(carves, count, voxel + direction))
                            {
                                continue;
                            }
                            wall.Clear();
                            wallMaterials.Clear();
                            Outline(voxel, direction, a, b, densities, materials, uniformMaterial, count, isoLevel, voxelSize, placement, ref wall, ref wallMaterials);
                            for (int i = 1; i + 1 < wall.Length; i++)
                            {
                                AddTriangle(wall, wallMaterials, 0, i, i + 1, ref positions, ref vertexMaterials, ref indices);
                            }
                        }
                    }
                }
            }
        }

        // The solid part of the face between a carved voxel and its neighbour in one direction, as a
        // polygon wound to face back into the carved voxel: each solid corner in turn, with the point
        // where the ground crosses the iso level on each edge between a solid and an empty corner.
        private static void Outline<TPlacement>(
            int3 voxel, int3 direction, int3 a, int3 b,
            NativeArray<float> densities, NativeArray<byte> materials, byte uniformMaterial,
            int3 count, float isoLevel, float voxelSize, TPlacement placement,
            ref NativeList<float3> wall, ref NativeList<byte> wallMaterials)
            where TPlacement : struct, IEdgePlacement
        {
            for (int i = 0; i < 4; i++)
            {
                int3 corner = FaceCorner(voxel, direction, a, b, i);
                int3 next = FaceCorner(voxel, direction, a, b, (i + 1) & 3);
                int cornerIndex = Index(corner, count);
                int nextIndex = Index(next, count);
                float here = densities[cornerIndex];
                float there = densities[nextIndex];
                bool solid = here >= isoLevel;
                if (solid)
                {
                    wall.Add((float3)corner * voxelSize);
                    wallMaterials.Add(materials.Length > 0 ? materials[cornerIndex] : uniformMaterial);
                }
                if (solid != (there >= isoLevel))
                {
                    float t = placement.Along(here, there, isoLevel);
                    wall.Add(math.lerp((float3)corner, (float3)next, t) * voxelSize);
                    int solidEnd = solid ? cornerIndex : nextIndex;
                    wallMaterials.Add(materials.Length > 0 ? materials[solidEnd] : uniformMaterial);
                }
            }
        }

        // The face's corners in turn: (-a, -b), (+a, -b), (+a, +b), (-a, +b), as a sample.
        private static int3 FaceCorner(int3 voxel, int3 direction, int3 a, int3 b, int i)
        {
            int sa = i == 1 || i == 2 ? 1 : 0;
            int sb = i >= 2 ? 1 : 0;
            // The face's lowest corner: the voxel's own for a - side, one along for a + side.
            int3 low = voxel + math.max(direction, 0);
            return low + a * sa + b * sb;
        }

        // A side's direction and two axes along the face with cross(a, b) = -direction, so the corners in
        // FaceCorner order go clockwise seen from the carved voxel (Unity's front face).
        private static void Face(int side, out int3 direction, out int3 a, out int3 b)
        {
            switch (side)
            {
                case 0:
                    direction = new int3(1, 0, 0);
                    a = new int3(0, 0, 1);
                    b = new int3(0, 1, 0);
                    break;
                case 1:
                    direction = new int3(-1, 0, 0);
                    a = new int3(0, 1, 0);
                    b = new int3(0, 0, 1);
                    break;
                case 2:
                    direction = new int3(0, 1, 0);
                    a = new int3(1, 0, 0);
                    b = new int3(0, 0, 1);
                    break;
                case 3:
                    direction = new int3(0, -1, 0);
                    a = new int3(0, 0, 1);
                    b = new int3(1, 0, 0);
                    break;
                case 4:
                    direction = new int3(0, 0, 1);
                    a = new int3(0, 1, 0);
                    b = new int3(1, 0, 0);
                    break;
                default:
                    direction = new int3(0, 0, -1);
                    a = new int3(1, 0, 0);
                    b = new int3(0, 1, 0);
                    break;
            }
        }

        private static void AddTriangle(
            NativeList<float3> wall, NativeList<byte> wallMaterials, int i0, int i1, int i2,
            ref NativeList<float3> positions, ref NativeList<byte> vertexMaterials, ref NativeList<int> indices)
        {
            int first = positions.Length;
            positions.Add(wall[i0]);
            positions.Add(wall[i1]);
            positions.Add(wall[i2]);
            vertexMaterials.Add(wallMaterials[i0]);
            vertexMaterials.Add(wallMaterials[i1]);
            vertexMaterials.Add(wallMaterials[i2]);
            indices.Add(first);
            indices.Add(first + 1);
            indices.Add(first + 2);
        }

        private static int Index(int3 sample, int3 count)
        {
            return sample.x + count.x * (sample.y + count.y * sample.z);
        }
    }
}
