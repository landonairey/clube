using System;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// The core mesher's flat-shaded loop with the output storage swapped for a type
    /// parameter (K11). It reuses the core's case lookup, tables and edge placer, so
    /// the storage variants differ only in how they keep the mesh, and their output
    /// can be checked against <see cref="ChunkMesher"/>.
    /// </summary>
    internal static class StorageMesher
    {
        // Single-threaded benchmark: one scratch array, so builds allocate nothing themselves.
        private static readonly float[] CornerValues = new float[MarchingCubes.CornerCount];

        public static void BuildFlat<TStorage>(Chunk chunk, ChunkMeshSettings settings, ref TStorage storage)
            where TStorage : struct, IMeshStorage
        {
            storage.Clear();
            IEdgeVertexPlacer placer = EdgeVertexPlacers.For(settings.EdgePlacement);
            Span<Vector3> edgeVertices = stackalloc Vector3[MarchingCubes.EdgeCount];
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
                            CornerValues[corner] = chunk.GetDensity(voxel + MarchingCubes.CornerOffset(corner));
                        }

                        int caseIndex = MarchingCubes.GetCaseIndex(CornerValues, settings.IsoLevel);
                        int edgeMask = MarchingCubes.GetCrossedEdgeMask(caseIndex);
                        if (edgeMask == 0)
                        {
                            continue;
                        }

                        Vector3 origin = (Vector3)voxel * settings.VoxelSize;
                        for (int edge = 0; edge < MarchingCubes.EdgeCount; edge++)
                        {
                            if ((edgeMask & (1 << edge)) == 0)
                            {
                                continue;
                            }

                            int cornerA = MarchingCubesTables.EdgeCorners[edge, 0];
                            int cornerB = MarchingCubesTables.EdgeCorners[edge, 1];
                            Vector3 local = placer.Place(
                                MarchingCubes.CornerPosition(cornerA), MarchingCubes.CornerPosition(cornerB),
                                CornerValues[cornerA], CornerValues[cornerB], settings.IsoLevel);
                            edgeVertices[edge] = origin + settings.VoxelSize * local;
                        }

                        // Flat shading: a new vertex for every triangle corner.
                        for (int i = 0; MarchingCubesTables.Triangles[caseIndex, i] != -1; i++)
                        {
                            storage.AddIndex(storage.AddVertex(edgeVertices[MarchingCubesTables.Triangles[caseIndex, i]]));
                        }
                    }
                }
            }
        }
    }
}
