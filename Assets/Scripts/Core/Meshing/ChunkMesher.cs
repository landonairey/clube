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
        /// <param name="recorder">Optional step log for lab playback (A11), cleared and refilled.
        /// Leave null in the game: nothing is recorded and nothing extra is computed.</param>
        public static void Build(
            Chunk chunk,
            ChunkMeshSettings settings,
            List<Vector3> vertices,
            List<int> triangles,
            MeshingRecorder recorder = null)
        {
            vertices.Clear();
            triangles.Clear();
            if (recorder != null)
            {
                recorder.Begin(settings, chunk);
            }

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

                        // Most voxels are all solid or all empty: no surface, nothing to do.
                        // Step-through still logs them, so only skip when not recording.
                        int caseIndex = MarchingCubes.GetCaseIndex(cornerValues, settings.IsoLevel);
                        if (recorder == null && MarchingCubes.GetCrossedEdgeMask(caseIndex) == 0)
                        {
                            continue;
                        }

                        writer.BeginVoxel(voxel);
                        Vector3 origin = (Vector3)voxel * settings.VoxelSize;
                        if (recorder != null)
                        {
                            recorder.BeginVoxel(voxel, origin, cornerValues);
                        }

                        MarchingCubes.Polygonise(
                            cornerValues, caseIndex, settings.IsoLevel, origin, settings.VoxelSize,
                            placer, writer, triangles, recorder);
                    }
                }
            }

            if (recorder != null)
            {
                recorder.End();
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
