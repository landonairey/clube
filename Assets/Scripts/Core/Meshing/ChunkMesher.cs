using System;
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
            ChunkMeshSettings settings,
            List<Vector3> vertices,
            List<int> triangles)
        {
            vertices.Clear();
            triangles.Clear();

            // Variants are resolved once here, never per vertex (A6).
            IEdgeVertexPlacer placer = EdgeVertexPlacers.For(settings.EdgePlacement);
            IVertexWriter writer = CreateWriter(settings.Shading, vertices, chunk.SampleCount);

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

                        writer.BeginVoxel(voxel);
                        Vector3 origin = (Vector3)voxel * settings.VoxelSize;
                        MarchingCubes.Polygonise(
                            cornerValues, settings.IsoLevel, origin, settings.VoxelSize, placer, writer, triangles);
                    }
                }
            }
        }

        private static IVertexWriter CreateWriter(Shading shading, List<Vector3> vertices, Vector3Int sampleCount)
        {
            switch (shading)
            {
                case Shading.Flat:
                    return new FlatVertexWriter(vertices);
                case Shading.Smooth:
                    return new SharedVertexWriter(vertices, sampleCount);
                default:
                    throw new ArgumentOutOfRangeException(nameof(shading), shading, null);
            }
        }
    }
}
