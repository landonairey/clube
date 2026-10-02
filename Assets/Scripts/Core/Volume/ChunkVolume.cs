using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The solid volume inside a whole chunk, in world units³, by summing the
    /// per-voxel measures of <see cref="VoxelVolume"/> (V11, V12) over every voxel.
    /// Bounded by the chunk: solid reaching its border is cut off there.
    /// </summary>
    public static class ChunkVolume
    {
        /// <summary>Sum of each voxel's mean corner density (V11). Ignores the iso level.</summary>
        public static float Approximate(Chunk chunk, float voxelSize)
        {
            var corners = new float[MarchingCubes.CornerCount];
            float total = 0f;
            foreach (Vector3Int voxel in Voxels(chunk))
            {
                ReadCorners(chunk, voxel, corners);
                total += VoxelVolume.Approximate(corners);
            }
            return total * Cube(voxelSize);
        }

        /// <summary>
        /// Sum of each voxel's exact Marching Cubes solid (V12). Voxels entirely solid
        /// or empty count as 1 or 0 without building any tetrahedra.
        /// </summary>
        public static float Exact(Chunk chunk, float isoLevel, IEdgeVertexPlacer placer, float voxelSize)
        {
            var corners = new float[MarchingCubes.CornerCount];
            float total = 0f;
            foreach (Vector3Int voxel in Voxels(chunk))
            {
                ReadCorners(chunk, voxel, corners);
                int caseIndex = MarchingCubes.GetCaseIndex(corners, isoLevel);
                if (caseIndex == MarchingCubes.CaseCount - 1)
                {
                    total += 1f;
                }
                else if (caseIndex != 0)
                {
                    total += VoxelVolume.Exact(corners, isoLevel, placer);
                }
            }
            return total * Cube(voxelSize);
        }

        private static IEnumerable<Vector3Int> Voxels(Chunk chunk)
        {
            Vector3Int count = chunk.VoxelCount;
            for (int z = 0; z < count.z; z++)
            {
                for (int y = 0; y < count.y; y++)
                {
                    for (int x = 0; x < count.x; x++)
                    {
                        yield return new Vector3Int(x, y, z);
                    }
                }
            }
        }

        private static void ReadCorners(Chunk chunk, Vector3Int voxel, float[] corners)
        {
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                corners[corner] = chunk.GetDensity(voxel + MarchingCubes.CornerOffset(corner));
            }
        }

        private static float Cube(float value)
        {
            return value * value * value;
        }
    }
}
