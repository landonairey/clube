using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Pure Marching Cubes polygoniser: turns the 8 corner values of one cube
    /// into triangles. Holds no state and knows nothing about Unity components,
    /// so it can be reused later for chunks of many cubes.
    /// </summary>
    public static class MarchingCubes
    {
        public const int CornerCount = 8;

        public const int EdgeCount = 12;

        public const int CaseCount = 256;

        /// <summary>
        /// Appends the isosurface triangles for a single cube, with interpolated
        /// edge vertices and no vertex sharing (flat shading).
        /// </summary>
        /// <param name="cornerValues">8 values, ordered as in <see cref="MarchingCubesTables.CornerOffsets"/>.</param>
        /// <param name="isoLevel">Threshold separating solid from empty.</param>
        /// <param name="origin">Position of corner 0.</param>
        /// <param name="size">Edge length of the cube.</param>
        /// <param name="vertices">Vertex list to append to.</param>
        /// <param name="triangles">Index list to append to.</param>
        public static void Polygonise(
            IReadOnlyList<float> cornerValues,
            float isoLevel,
            Vector3 origin,
            float size,
            List<Vector3> vertices,
            List<int> triangles)
        {
            Polygonise(
                cornerValues, isoLevel, origin, size,
                InterpolatedEdgePlacer.Instance, new FlatVertexWriter(vertices), triangles);
        }

        /// <summary>
        /// Appends the isosurface triangles for a single cube. Corners with a value
        /// at or above <paramref name="isoLevel"/> are treated as solid.
        /// </summary>
        /// <param name="placer">Decides where each vertex sits on its edge (V3).</param>
        /// <param name="writer">Decides whether vertices are shared (V4). The caller calls
        /// <see cref="IVertexWriter.BeginVoxel"/> first if the writer needs to know the voxel.</param>
        public static void Polygonise(
            IReadOnlyList<float> cornerValues,
            float isoLevel,
            Vector3 origin,
            float size,
            IEdgeVertexPlacer placer,
            IVertexWriter writer,
            List<int> triangles)
        {
            int cubeIndex = GetCaseIndex(cornerValues, isoLevel);

            // With solid corners setting the index bits, the table's winding gives
            // faces pointing from solid towards empty under Unity's clockwise convention.
            for (int i = 0; MarchingCubesTables.Triangles[cubeIndex, i] != -1; i++)
            {
                int edge = MarchingCubesTables.Triangles[cubeIndex, i];
                int cornerA = MarchingCubesTables.EdgeCorners[edge, 0];
                int cornerB = MarchingCubesTables.EdgeCorners[edge, 1];

                Vector3 local = placer.Place(
                    CornerPosition(cornerA), CornerPosition(cornerB),
                    cornerValues[cornerA], cornerValues[cornerB], isoLevel);
                triangles.Add(writer.Write(edge, origin + size * local));
            }
        }

        /// <summary>
        /// The 8-bit case index (0-255): bit <c>i</c> is set when corner <c>i</c> is solid,
        /// i.e. its value is at or above <paramref name="isoLevel"/>.
        /// </summary>
        public static int GetCaseIndex(IReadOnlyList<float> cornerValues, float isoLevel)
        {
            int caseIndex = 0;
            for (int corner = 0; corner < CornerCount; corner++)
            {
                if (cornerValues[corner] >= isoLevel)
                {
                    caseIndex |= 1 << corner;
                }
            }
            return caseIndex;
        }

        /// <summary>True when bit <paramref name="corner"/> of the case index is set (the corner is solid).</summary>
        public static bool IsCornerSolid(int caseIndex, int corner)
        {
            return (caseIndex & (1 << corner)) != 0;
        }

        /// <summary>
        /// 12-bit mask of the edges the surface crosses for a case (the classic
        /// "edge table"): bit <c>e</c> is set when edge <c>e</c> joins a solid and an
        /// empty corner. Computed rather than stored, since it follows directly
        /// from the case index.
        /// </summary>
        public static int GetCrossedEdgeMask(int caseIndex)
        {
            int mask = 0;
            for (int edge = 0; edge < EdgeCount; edge++)
            {
                bool solidA = IsCornerSolid(caseIndex, MarchingCubesTables.EdgeCorners[edge, 0]);
                bool solidB = IsCornerSolid(caseIndex, MarchingCubesTables.EdgeCorners[edge, 1]);
                if (solidA != solidB)
                {
                    mask |= 1 << edge;
                }
            }
            return mask;
        }

        /// <summary>Offset of a corner from corner 0, in whole voxels.</summary>
        public static Vector3Int CornerOffset(int corner)
        {
            return new Vector3Int(
                MarchingCubesTables.CornerOffsets[corner, 0],
                MarchingCubesTables.CornerOffsets[corner, 1],
                MarchingCubesTables.CornerOffsets[corner, 2]);
        }

        /// <summary>Position of a corner in unit-cube space.</summary>
        public static Vector3 CornerPosition(int corner)
        {
            return CornerOffset(corner);
        }
    }
}
