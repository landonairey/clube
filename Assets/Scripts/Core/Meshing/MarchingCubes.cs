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

        /// <summary>
        /// Appends the isosurface triangles for a single cube to the given lists.
        /// Corners with a value at or above <paramref name="isoLevel"/> are treated as solid.
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
            int cubeIndex = GetCubeIndex(cornerValues, isoLevel);

            // With solid corners setting the index bits, the table's winding gives
            // faces pointing from solid towards empty under Unity's clockwise convention.
            for (int i = 0; MarchingCubesTables.Triangles[cubeIndex, i] != -1; i++)
            {
                int edge = MarchingCubesTables.Triangles[cubeIndex, i];
                triangles.Add(vertices.Count);
                vertices.Add(origin + size * InterpolateEdge(edge, cornerValues, isoLevel));
            }
        }

        private static int GetCubeIndex(IReadOnlyList<float> cornerValues, float isoLevel)
        {
            int cubeIndex = 0;
            for (int corner = 0; corner < CornerCount; corner++)
            {
                if (cornerValues[corner] >= isoLevel)
                {
                    cubeIndex |= 1 << corner;
                }
            }
            return cubeIndex;
        }

        /// <summary>
        /// Returns the point (in unit-cube space) where the isosurface crosses the edge.
        /// </summary>
        private static Vector3 InterpolateEdge(int edge, IReadOnlyList<float> cornerValues, float isoLevel)
        {
            int cornerA = MarchingCubesTables.EdgeCorners[edge, 0];
            int cornerB = MarchingCubesTables.EdgeCorners[edge, 1];
            float valueA = cornerValues[cornerA];
            float valueB = cornerValues[cornerB];

            // InverseLerp returns 0 when the values are equal, which is a safe fallback.
            float t = Mathf.InverseLerp(valueA, valueB, isoLevel);
            return Vector3.Lerp(CornerPosition(cornerA), CornerPosition(cornerB), t);
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
