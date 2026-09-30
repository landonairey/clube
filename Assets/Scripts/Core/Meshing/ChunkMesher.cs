using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Builds a chunk's isosurface by running <see cref="MarchingCubes"/> over
    /// every voxel. Pure function of the chunk's densities and the meshing
    /// settings; knows nothing about Unity components.
    /// </summary>
    public static class ChunkMesher
    {
        /// <summary>
        /// Replaces the contents of <paramref name="vertices"/> and <paramref name="triangles"/>
        /// with the chunk's surface, in chunk-local space with sample (0,0,0) at the origin.
        /// </summary>
        public static void Build(
            Chunk chunk,
            float isoLevel,
            float voxelSize,
            List<Vector3> vertices,
            List<int> triangles)
        {
            vertices.Clear();
            triangles.Clear();

            var cornerValues = new float[MarchingCubes.CornerCount];
            Vector3Int voxelCount = chunk.VoxelCount;

            for (int z = 0; z < voxelCount.z; z++)
            {
                for (int y = 0; y < voxelCount.y; y++)
                {
                    for (int x = 0; x < voxelCount.x; x++)
                    {
                        var voxel = new Vector3Int(x, y, z);
                        for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
                        {
                            cornerValues[corner] = chunk.GetDensity(voxel + MarchingCubes.CornerOffset(corner));
                        }

                        Vector3 origin = (Vector3)voxel * voxelSize;
                        MarchingCubes.Polygonise(cornerValues, isoLevel, origin, voxelSize, vertices, triangles);
                    }
                }
            }
        }
    }
}
