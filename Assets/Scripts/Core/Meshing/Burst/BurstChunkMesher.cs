using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// The chunk mesher as a Burst-compiled job over native arrays (K12). It runs the same
    /// loop as <see cref="ChunkMesher"/> (Z layers, shared corners, early skip, the same
    /// edge placement and vertex sharing), so both give the same mesh, but without strategy
    /// objects or step recording. Jobs don't share state, so many chunks can mesh at once
    /// on worker threads (the start of Chapter 4's threading).
    /// </summary>
    /// <remarks>
    /// The densities are copied into the job a Z layer at a time through
    /// <see cref="IVoxelStorage.ReadLayer"/> (A12), and the job frees its inputs when it
    /// completes. The output lists belong to the caller.
    /// </remarks>
    public static class BurstChunkMesher
    {
        private const int MaxTriangleIndices = 16;

        // The Marching Cubes tables flattened to one dimension, made once.
        private static int[] edgeMasks;
        private static int[] triangleTable;
        private static int[] edgeCorners;
        private static int[] cornerOffsets;

        /// <summary>Builds the chunk's mesh on this thread and returns when it's done.</summary>
        public static void Build(Chunk chunk, ChunkMeshSettings settings, NativeList<float3> vertices, NativeList<int> triangles)
        {
            Schedule(chunk, settings, vertices, triangles).Complete();
        }

        /// <summary>
        /// Schedules the chunk's mesh build on a worker thread. Complete the handle before
        /// reading the lists. The densities are copied now, so the chunk may change straight away.
        /// </summary>
        public static JobHandle Schedule(
            Chunk chunk, ChunkMeshSettings settings, NativeList<float3> vertices, NativeList<int> triangles,
            JobHandle dependsOn = default)
        {
            EnsureTables();

            UnityEngine.Vector3Int samples = chunk.SampleCount;
            int layerSize = samples.x * samples.y;
            var densities = new NativeArray<float>(layerSize * samples.z, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            for (int z = 0; z < samples.z; z++)
            {
                chunk.ReadLayer(z, densities.AsSpan().Slice(z * layerSize, layerSize));
            }

            var job = new MeshChunkJob
            {
                Densities = densities,
                SampleCount = new int3(samples.x, samples.y, samples.z),
                IsoLevel = settings.IsoLevel,
                VoxelSize = settings.VoxelSize,
                Interpolate = settings.EdgePlacement == EdgePlacement.Interpolated,
                ShareVertices = settings.Shading == Shading.Smooth,
                EdgeMasks = new NativeArray<int>(edgeMasks, Allocator.TempJob),
                TriangleTable = new NativeArray<int>(triangleTable, Allocator.TempJob),
                EdgeCorners = new NativeArray<int>(edgeCorners, Allocator.TempJob),
                CornerOffsets = new NativeArray<int>(cornerOffsets, Allocator.TempJob),
                Vertices = vertices,
                Triangles = triangles,
            };
            return job.Schedule(dependsOn);
        }

        private static void EnsureTables()
        {
            if (edgeMasks != null)
            {
                return;
            }

            var masks = new int[MarchingCubes.CaseCount];
            var table = new int[MarchingCubes.CaseCount * MaxTriangleIndices];
            for (int caseIndex = 0; caseIndex < MarchingCubes.CaseCount; caseIndex++)
            {
                masks[caseIndex] = MarchingCubes.GetCrossedEdgeMask(caseIndex);
                for (int i = 0; i < MaxTriangleIndices; i++)
                {
                    table[caseIndex * MaxTriangleIndices + i] = MarchingCubesTables.Triangles[caseIndex, i];
                }
            }

            var edges = new int[MarchingCubes.EdgeCount * 2];
            for (int edge = 0; edge < MarchingCubes.EdgeCount; edge++)
            {
                edges[edge * 2] = MarchingCubesTables.EdgeCorners[edge, 0];
                edges[edge * 2 + 1] = MarchingCubesTables.EdgeCorners[edge, 1];
            }

            var offsets = new int[MarchingCubes.CornerCount * 3];
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    offsets[corner * 3 + axis] = MarchingCubesTables.CornerOffsets[corner, axis];
                }
            }

            triangleTable = table;
            edgeCorners = edges;
            cornerOffsets = offsets;
            edgeMasks = masks;
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct MeshChunkJob : IJob
        {
            [ReadOnly, DeallocateOnJobCompletion] public NativeArray<float> Densities;
            [ReadOnly, DeallocateOnJobCompletion] public NativeArray<int> EdgeMasks;
            [ReadOnly, DeallocateOnJobCompletion] public NativeArray<int> TriangleTable;
            [ReadOnly, DeallocateOnJobCompletion] public NativeArray<int> EdgeCorners;
            [ReadOnly, DeallocateOnJobCompletion] public NativeArray<int> CornerOffsets;

            public int3 SampleCount;
            public float IsoLevel;
            public float VoxelSize;
            public bool Interpolate;
            public bool ShareVertices;

            public NativeList<float3> Vertices;
            public NativeList<int> Triangles;

            public void Execute()
            {
                Vertices.Clear();
                Triangles.Clear();

                // Corners in MarchingCubesTables order, as in ChunkMesher: 0, 1, 4, 5 on the
                // near Z layer, 3, 2, 7, 6 on the far one; 1, 2, 5, 6 on the +X side.
                var corners = new NativeArray<float>(MarchingCubes.CornerCount, Allocator.Temp);
                var edgeVertices = new NativeArray<float3>(MarchingCubes.EdgeCount, Allocator.Temp);
                var shared = ShareVertices ? new NativeHashMap<int, int>(1024, Allocator.Temp) : default;

                int rowLength = SampleCount.x;
                int layerSize = SampleCount.x * SampleCount.y;
                int3 voxels = SampleCount - 1;

                for (int z = 0; z < voxels.z; z++)
                {
                    int near = z * layerSize;
                    int far = near + layerSize;
                    for (int y = 0; y < voxels.y; y++)
                    {
                        int row = y * rowLength;
                        int rowAbove = row + rowLength;

                        corners[1] = Densities[near + row];
                        corners[2] = Densities[far + row];
                        corners[5] = Densities[near + rowAbove];
                        corners[6] = Densities[far + rowAbove];
                        int plusXBits = SolidBits(corners);

                        for (int x = 0; x < voxels.x; x++)
                        {
                            corners[0] = corners[1];
                            corners[3] = corners[2];
                            corners[4] = corners[5];
                            corners[7] = corners[6];
                            int minusXBits = ((plusXBits & 0b0000_0010) >> 1) | ((plusXBits & 0b0000_0100) << 1)
                                           | ((plusXBits & 0b0010_0000) >> 1) | ((plusXBits & 0b0100_0000) << 1);

                            corners[1] = Densities[near + row + x + 1];
                            corners[2] = Densities[far + row + x + 1];
                            corners[5] = Densities[near + rowAbove + x + 1];
                            corners[6] = Densities[far + rowAbove + x + 1];
                            plusXBits = SolidBits(corners);
                            int caseIndex = minusXBits | plusXBits;

                            int edgeMask = EdgeMasks[caseIndex];
                            if (edgeMask == 0)
                            {
                                continue;
                            }

                            var voxel = new int3(x, y, z);
                            PlaceEdgeVertices(corners, edgeMask, (float3)voxel * VoxelSize, edgeVertices);
                            WriteTriangles(caseIndex, voxel, edgeVertices, shared);
                        }
                    }
                }
            }

            private int SolidBits(NativeArray<float> corners)
            {
                return (corners[1] >= IsoLevel ? 1 << 1 : 0)
                     | (corners[2] >= IsoLevel ? 1 << 2 : 0)
                     | (corners[5] >= IsoLevel ? 1 << 5 : 0)
                     | (corners[6] >= IsoLevel ? 1 << 6 : 0);
            }

            // The same arithmetic as InterpolatedEdgePlacer and MidpointEdgePlacer, then
            // origin + size * local as in MarchingCubes.Polygonise.
            private void PlaceEdgeVertices(NativeArray<float> corners, int edgeMask, float3 origin, NativeArray<float3> edgeVertices)
            {
                for (int edge = 0; edge < MarchingCubes.EdgeCount; edge++)
                {
                    if ((edgeMask & (1 << edge)) == 0)
                    {
                        continue;
                    }

                    int a = EdgeCorners[edge * 2];
                    int b = EdgeCorners[edge * 2 + 1];
                    float3 cornerA = CornerOffset(a);
                    float3 cornerB = CornerOffset(b);
                    float3 local;
                    if (Interpolate)
                    {
                        // Mathf.InverseLerp: 0 when the values are equal.
                        float valueA = corners[a];
                        float valueB = corners[b];
                        float t = valueA != valueB ? math.saturate((IsoLevel - valueA) / (valueB - valueA)) : 0f;
                        local = cornerA + (cornerB - cornerA) * t;
                    }
                    else
                    {
                        local = (cornerA + cornerB) * 0.5f;
                    }
                    edgeVertices[edge] = origin + VoxelSize * local;
                }
            }

            private void WriteTriangles(int caseIndex, int3 voxel, NativeArray<float3> edgeVertices, NativeHashMap<int, int> shared)
            {
                int start = caseIndex * MaxTriangleIndices;
                for (int i = start; TriangleTable[i] != -1; i++)
                {
                    int edge = TriangleTable[i];
                    if (!ShareVertices)
                    {
                        Triangles.Add(Vertices.Length);
                        Vertices.Add(edgeVertices[edge]);
                        continue;
                    }

                    // One vertex per grid edge, keyed by its lower sample and axis (SharedVertexWriter).
                    int key = EdgeKey(voxel, edge);
                    if (!shared.TryGetValue(key, out int index))
                    {
                        index = Vertices.Length;
                        Vertices.Add(edgeVertices[edge]);
                        shared.Add(key, index);
                    }
                    Triangles.Add(index);
                }
            }

            private int EdgeKey(int3 voxel, int edge)
            {
                int3 a = voxel + (int3)CornerOffset(EdgeCorners[edge * 2]);
                int3 b = voxel + (int3)CornerOffset(EdgeCorners[edge * 2 + 1]);
                int3 lower = math.min(a, b);
                int axis = a.x != b.x ? 0 : a.y != b.y ? 1 : 2;
                int sample = lower.x + SampleCount.x * (lower.y + SampleCount.y * lower.z);
                return sample * 3 + axis;
            }

            private float3 CornerOffset(int corner)
            {
                return new float3(CornerOffsets[corner * 3], CornerOffsets[corner * 3 + 1], CornerOffsets[corner * 3 + 2]);
            }
        }
    }
}
