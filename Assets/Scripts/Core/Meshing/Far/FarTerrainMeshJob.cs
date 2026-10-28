using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Core
{
    /// <summary>
    /// Builds one far-terrain level's mesh (P7) from its column heights, straight into
    /// <see cref="Mesh.MeshData"/>: a vertex per grid point (position and a normal from its
    /// neighbours' heights), two triangles per quad, none in the <see cref="Hole"/> the finer level
    /// fills. Heights come bordered by one column on every side (<see cref="BorderedBlock"/>), so
    /// normals on the edge see past it.
    /// </summary>
    /// <remarks>
    /// The seam: along the outer edge a finer level has a vertex every metre the coarser level
    /// has one every two, so each odd edge vertex takes the mean of its two neighbours' heights and
    /// lies exactly on the coarser level's edge. The even ones sample the same position as the
    /// coarser level's (spacings are powers of two, so the products are exact) and so match it.
    /// </remarks>
    [BurstCompile]
    public struct FarTerrainMeshJob : IJob
    {
        /// <summary>Heights over <see cref="BorderedBlock"/>: (Quads + 3)² columns, x fastest.</summary>
        [ReadOnly] public NativeArray<float> Heights;

        public int Quads;
        public float Spacing;

        /// <summary>Vertex (0, 0), in vertices of this level from the world origin.</summary>
        public int2 First;

        /// <summary>Quads left out, (x min, z min, x max, z max), max exclusive; empty for the finest level.</summary>
        public int4 Hole;

        /// <summary>True for every level but the coarsest: its outer edge meets a coarser level.</summary>
        public bool StitchEdges;

        /// <summary>Metres every vertex is lowered, so the voxel surface wins where both are drawn.</summary>
        public float Sink;

        public Mesh.MeshData Output;

        /// <summary>The columns a level's heights cover: its vertices and one more all round.</summary>
        public static ColumnBlock BorderedBlock(FarTerrainGrid grid)
        {
            return new ColumnBlock(grid.First - 1, new int2(grid.Quads + 3), grid.Spacing);
        }

        public void Execute()
        {
            int side = Quads + 1;
            int bordered = Quads + 3;

            var attributes = new NativeArray<VertexAttributeDescriptor>(2, Allocator.Temp);
            attributes[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3);
            attributes[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3);
            Output.SetVertexBufferParams(side * side, attributes);
            NativeArray<PlainVertex> vertices = Output.GetVertexData<PlainVertex>();

            float low = float.MaxValue;
            float high = float.MinValue;
            for (int z = 0; z < side; z++)
            {
                for (int x = 0; x < side; x++)
                {
                    float height = HeightAt(x, z, bordered, side);
                    low = math.min(low, height);
                    high = math.max(high, height);

                    float slopeX = (Raw(x + 1, z, bordered) - Raw(x - 1, z, bordered)) / (2f * Spacing);
                    float slopeZ = (Raw(x, z + 1, bordered) - Raw(x, z - 1, bordered)) / (2f * Spacing);
                    var position = new float3((First.x + x) * Spacing, height - Sink, (First.y + z) * Spacing);
                    vertices[x + side * z] = new PlainVertex { Position = position, Normal = math.normalize(new float3(-slopeX, 1f, -slopeZ)) };
                }
            }

            int indexCount = FarTerrainGrid.QuadsDrawn(Quads, Hole) * 6;
            Output.SetIndexBufferParams(indexCount, IndexFormat.UInt16);
            NativeArray<ushort> indices = Output.GetIndexData<ushort>();
            int written = 0;
            for (int z = 0; z < Quads; z++)
            {
                for (int x = 0; x < Quads; x++)
                {
                    if (x >= Hole.x && x < Hole.z && z >= Hole.y && z < Hole.w)
                    {
                        continue;
                    }
                    // Clockwise seen from above, Unity's front face.
                    ushort a = (ushort)(x + side * z);
                    ushort b = (ushort)(x + side * (z + 1));
                    ushort c = (ushort)(x + 1 + side * (z + 1));
                    ushort d = (ushort)(x + 1 + side * z);
                    indices[written++] = a;
                    indices[written++] = b;
                    indices[written++] = c;
                    indices[written++] = a;
                    indices[written++] = c;
                    indices[written++] = d;
                }
            }

            var min = new Vector3(First.x * Spacing, low - Sink, First.y * Spacing);
            var size = new Vector3(Quads * Spacing, math.max(0.01f, high - low), Quads * Spacing);
            var bounds = new Bounds(min + size * 0.5f, size);
            Output.subMeshCount = 1;
            Output.SetSubMesh(
                0,
                new SubMeshDescriptor(0, indexCount) { bounds = bounds, vertexCount = side * side },
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
        }

        // A vertex's height: its column's, or on a stitched edge's odd vertices the mean of the two either side.
        private float HeightAt(int x, int z, int bordered, int side)
        {
            if (StitchEdges)
            {
                bool alongX = (z == 0 || z == side - 1) && ((First.x + x) & 1) != 0;
                if (alongX)
                {
                    return 0.5f * (Raw(x - 1, z, bordered) + Raw(x + 1, z, bordered));
                }
                bool alongZ = (x == 0 || x == side - 1) && ((First.y + z) & 1) != 0;
                if (alongZ)
                {
                    return 0.5f * (Raw(x, z - 1, bordered) + Raw(x, z + 1, bordered));
                }
            }
            return Raw(x, z, bordered);
        }

        // A column's height; x and z may be -1 or Quads + 1 (the border).
        private float Raw(int x, int z, int bordered)
        {
            return Heights[(x + 1) + bordered * (z + 1)];
        }
    }
}
