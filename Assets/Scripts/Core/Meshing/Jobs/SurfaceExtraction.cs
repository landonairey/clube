using Unity.Collections;
using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>Where a surface vertex sits on a crossed edge (V3), as a Burst strategy (A6): 0 at corner a, 1 at corner b.</summary>
    public interface IEdgePlacement
    {
        float Along(float valueA, float valueB, float isoLevel);
    }

    /// <summary>Where the density crosses the iso level (<see cref="InterpolatedEdgePlacer"/>).</summary>
    public struct InterpolatedPlacement : IEdgePlacement
    {
        public float Along(float valueA, float valueB, float isoLevel)
        {
            // Mathf.InverseLerp: 0 when the values are equal.
            return valueA != valueB ? math.saturate((isoLevel - valueA) / (valueB - valueA)) : 0f;
        }
    }

    /// <summary>Always the edge midpoint (<see cref="MidpointEdgePlacer"/>).</summary>
    public struct MidpointPlacement : IEdgePlacement
    {
        public float Along(float valueA, float valueB, float isoLevel)
        {
            return 0.5f;
        }
    }

    /// <summary>Whether triangles share vertices (V4), as a Burst strategy (A6).</summary>
    public interface IVertexSharing
    {
        /// <summary>The index of the vertex for a grid edge: a new one, or the edge's existing one.</summary>
        int Add(int edgeKey, float3 position, byte material, ref NativeList<float3> positions, ref NativeList<byte> materials);
    }

    /// <summary>Every triangle gets its own vertices: flat shading (<see cref="FlatVertexWriter"/>).</summary>
    public struct SeparateVertices : IVertexSharing
    {
        public int Add(int edgeKey, float3 position, byte material, ref NativeList<float3> positions, ref NativeList<byte> materials)
        {
            positions.Add(position);
            materials.Add(material);
            return positions.Length - 1;
        }
    }

    /// <summary>One vertex per grid edge, shared by every triangle on it: smooth shading (<see cref="SharedVertexWriter"/>).</summary>
    public struct SharedVertices : IVertexSharing
    {
        public NativeHashMap<int, int> ByEdge;

        public int Add(int edgeKey, float3 position, byte material, ref NativeList<float3> positions, ref NativeList<byte> materials)
        {
            if (ByEdge.TryGetValue(edgeKey, out int index))
            {
                return index;
            }
            index = positions.Length;
            positions.Add(position);
            materials.Add(material);
            ByEdge.Add(edgeKey, index);
            return index;
        }
    }

    /// <summary>
    /// The Marching Cubes loop over a whole chunk inside a job (K12, K35): the same walk as
    /// <see cref="ChunkMesher"/> (Z layers, the four corners shared with the previous voxel
    /// reused, voxels with no crossed edge skipped), so both give the same surface. Each vertex
    /// also records the material of its edge's solid end (M13), read straight from the loop,
    /// where the managed path recovers it from positions afterwards (<see cref="VertexMaterialSampler"/>).
    /// </summary>
    /// <remarks>Generic over the two A6 variants, so each combination compiles to its own loop with nothing checked per vertex.</remarks>
    public static class SurfaceExtraction
    {
        /// <param name="densities">Every sample, X fastest, then Y, then Z.</param>
        /// <param name="materials">Every sample's material id, or empty when all are <paramref name="uniformMaterial"/>.</param>
        public static void Extract<TPlacement, TSharing>(
            NativeArray<float> densities, NativeArray<byte> materials, byte uniformMaterial,
            int3 sampleCount, float isoLevel, float voxelSize,
            TPlacement placement, ref TSharing sharing,
            ref NativeList<float3> positions, ref NativeList<byte> vertexMaterials, ref NativeList<int> indices)
            where TPlacement : struct, IEdgePlacement
            where TSharing : struct, IVertexSharing
        {
            int rowLength = sampleCount.x;
            int layerSize = sampleCount.x * sampleCount.y;
            int3 voxels = sampleCount - 1;

            // Per corner: its offset as a position and as an index into the sample arrays.
            var cornerPositions = new NativeArray<float3>(MarchingCubes.CornerCount, Allocator.Temp);
            var cornerIndices = new NativeArray<int>(MarchingCubes.CornerCount, Allocator.Temp);
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                var offset = new int3(
                    MarchingCubesJobTables.CornerOffsets[corner * 3],
                    MarchingCubesJobTables.CornerOffsets[corner * 3 + 1],
                    MarchingCubesJobTables.CornerOffsets[corner * 3 + 2]);
                cornerPositions[corner] = offset;
                cornerIndices[corner] = offset.x + rowLength * (offset.y + sampleCount.y * offset.z);
            }

            // Per edge: the sample index of its lower end and its axis, which key a shared vertex
            // (as SharedVertexWriter keys it: lower sample × 3 + axis).
            var edgeKeyOffsets = new NativeArray<int>(MarchingCubes.EdgeCount, Allocator.Temp);
            for (int edge = 0; edge < MarchingCubes.EdgeCount; edge++)
            {
                int a = MarchingCubesJobTables.EdgeCorners[edge * 2];
                int b = MarchingCubesJobTables.EdgeCorners[edge * 2 + 1];
                float3 delta = cornerPositions[b] - cornerPositions[a];
                int axis = delta.x != 0f ? 0 : delta.y != 0f ? 1 : 2;
                int lower = math.csum(delta) > 0f ? a : b;
                edgeKeyOffsets[edge] = cornerIndices[lower] * 3 + axis;
            }

            // Corners in MarchingCubesTables order: 0, 1, 4, 5 lie on the voxel's near Z layer,
            // 3, 2, 7, 6 on its far one; 1, 2, 5, 6 are on its +X side.
            var corners = new NativeArray<float>(MarchingCubes.CornerCount, Allocator.Temp);
            var edgeVertices = new NativeArray<float3>(MarchingCubes.EdgeCount, Allocator.Temp);
            var edgeMaterials = new NativeArray<byte>(MarchingCubes.EdgeCount, Allocator.Temp);
            bool uniform = materials.Length == 0;

            for (int z = 0; z < voxels.z; z++)
            {
                int near = z * layerSize;
                int far = near + layerSize;
                for (int y = 0; y < voxels.y; y++)
                {
                    int row = y * rowLength;
                    int rowAbove = row + rowLength;

                    // Seed the +X side with x = 0, which the first voxel shifts to its -X side.
                    corners[1] = densities[near + row];
                    corners[2] = densities[far + row];
                    corners[5] = densities[near + rowAbove];
                    corners[6] = densities[far + rowAbove];
                    int plusXBits = PlusXSolidBits(corners, isoLevel);

                    for (int x = 0; x < voxels.x; x++)
                    {
                        // A voxel's -X corners are the previous voxel's +X corners (K32).
                        corners[0] = corners[1];
                        corners[3] = corners[2];
                        corners[4] = corners[5];
                        corners[7] = corners[6];
                        int minusXBits = ((plusXBits & 0b0000_0010) >> 1) | ((plusXBits & 0b0000_0100) << 1)
                                       | ((plusXBits & 0b0010_0000) >> 1) | ((plusXBits & 0b0100_0000) << 1);

                        corners[1] = densities[near + row + x + 1];
                        corners[2] = densities[far + row + x + 1];
                        corners[5] = densities[near + rowAbove + x + 1];
                        corners[6] = densities[far + rowAbove + x + 1];
                        plusXBits = PlusXSolidBits(corners, isoLevel);
                        int caseIndex = minusXBits | plusXBits;

                        // Most voxels are all solid or all empty: no surface, nothing to do.
                        int edgeMask = MarchingCubesJobTables.EdgeMasks[caseIndex];
                        if (edgeMask == 0)
                        {
                            continue;
                        }

                        int voxelIndex = near + row + x;
                        float3 origin = new float3(x, y, z) * voxelSize;
                        for (int edge = 0; edge < MarchingCubes.EdgeCount; edge++)
                        {
                            if ((edgeMask & (1 << edge)) == 0)
                            {
                                continue;
                            }
                            int a = MarchingCubesJobTables.EdgeCorners[edge * 2];
                            int b = MarchingCubesJobTables.EdgeCorners[edge * 2 + 1];
                            float t = placement.Along(corners[a], corners[b], isoLevel);
                            float3 local = cornerPositions[a] + (cornerPositions[b] - cornerPositions[a]) * t;
                            edgeVertices[edge] = origin + voxelSize * local;

                            // The vertex takes the material of its edge's solid end (M13).
                            int solidEnd = voxelIndex + cornerIndices[corners[a] >= isoLevel ? a : b];
                            edgeMaterials[edge] = uniform ? uniformMaterial : materials[solidEnd];
                        }

                        int start = caseIndex * MarchingCubesJobTables.TriangleStride;
                        for (int i = start; MarchingCubesJobTables.Triangles[i] != -1; i++)
                        {
                            int edge = MarchingCubesJobTables.Triangles[i];
                            int key = (voxelIndex * 3) + edgeKeyOffsets[edge];
                            indices.Add(sharing.Add(key, edgeVertices[edge], edgeMaterials[edge], ref positions, ref vertexMaterials));
                        }
                    }
                }
            }
        }

        /// <summary>Case index bits of the +X corners (1, 2, 5, 6) that are solid.</summary>
        private static int PlusXSolidBits(NativeArray<float> corners, float isoLevel)
        {
            return (corners[1] >= isoLevel ? 1 << 1 : 0)
                 | (corners[2] >= isoLevel ? 1 << 2 : 0)
                 | (corners[5] >= isoLevel ? 1 << 5 : 0)
                 | (corners[6] >= isoLevel ? 1 << 6 : 0);
        }
    }
}
