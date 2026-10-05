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

            // Corners in MarchingCubesTables order: 0, 1, 4, 5 lie on the voxel's near
            // Z layer, 3, 2, 7, 6 on its far one; 1, 2, 5, 6 are on its +X side.
            var corners = new float[MarchingCubes.CornerCount];
            float iso = settings.IsoLevel;
            Vector3Int voxelCount = chunk.VoxelCount;
            int rowLength = chunk.SampleCount.x;
            int layerSize = rowLength * chunk.SampleCount.y;

            // Densities are read a whole Z layer at a time (K32); each voxel spans two.
            float[] near = LayerBuffer(ref nearLayer, layerSize);
            float[] far = LayerBuffer(ref farLayer, layerSize);
            chunk.ReadLayer(0, far);

            for (int z = 0; z < voxelCount.z; z++)
            {
                (near, far) = (far, near);
                chunk.ReadLayer(z + 1, far);

                for (int y = 0; y < voxelCount.y; y++)
                {
                    int row = y * rowLength;
                    int rowAbove = row + rowLength;

                    // Seed the +X side with x = 0, which the first voxel shifts to its -X side.
                    corners[1] = near[row];
                    corners[2] = far[row];
                    corners[5] = near[rowAbove];
                    corners[6] = far[rowAbove];
                    int plusXBits = SolidBits(corners, iso);

                    for (int x = 0; x < voxelCount.x; x++)
                    {
                        // A voxel's -X corners are the previous voxel's +X corners (K32).
                        corners[0] = corners[1];
                        corners[3] = corners[2];
                        corners[4] = corners[5];
                        corners[7] = corners[6];
                        int minusXBits = ((plusXBits & 0b0000_0010) >> 1) | ((plusXBits & 0b0000_0100) << 1)
                                       | ((plusXBits & 0b0010_0000) >> 1) | ((plusXBits & 0b0100_0000) << 1);

                        corners[1] = near[row + x + 1];
                        corners[2] = far[row + x + 1];
                        corners[5] = near[rowAbove + x + 1];
                        corners[6] = far[rowAbove + x + 1];
                        plusXBits = SolidBits(corners, iso);
                        int caseIndex = minusXBits | plusXBits;

                        // Most voxels are all solid or all empty: no surface, nothing to do.
                        // Step-through still logs them, so only skip when not recording.
                        if (recorder == null && MarchingCubes.GetCrossedEdgeMask(caseIndex) == 0)
                        {
                            continue;
                        }

                        var voxel = new Vector3Int(x, y, z);
                        writer.BeginVoxel(voxel);
                        Vector3 origin = (Vector3)voxel * settings.VoxelSize;
                        if (recorder != null)
                        {
                            recorder.BeginVoxel(voxel, origin, corners);
                        }

                        MarchingCubes.Polygonise(
                            corners, caseIndex, settings.IsoLevel, origin, settings.VoxelSize,
                            placer, writer, triangles, recorder);
                    }
                }
            }

            if (recorder != null)
            {
                recorder.End();
            }
        }

        // Layer buffers kept between builds so meshing allocates nothing per build;
        // one pair per thread, ready for meshing off the main thread (Chapter 4).
        [ThreadStatic] private static float[] nearLayer;
        [ThreadStatic] private static float[] farLayer;

        private static float[] LayerBuffer(ref float[] buffer, int size)
        {
            if (buffer == null || buffer.Length < size)
            {
                buffer = new float[size];
            }
            return buffer;
        }

        /// <summary>Case index bits of the +X corners (1, 2, 5, 6) that are solid.</summary>
        private static int SolidBits(float[] corners, float isoLevel)
        {
            return (corners[1] >= isoLevel ? 1 << 1 : 0)
                 | (corners[2] >= isoLevel ? 1 << 2 : 0)
                 | (corners[5] >= isoLevel ? 1 << 5 : 0)
                 | (corners[6] >= isoLevel ? 1 << 6 : 0);
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
