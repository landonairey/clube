using System;
using System.Collections.Generic;
using Clube.Core;

namespace Clube.Debug
{
    /// <summary>
    /// What a case index means, as text and lists (V7, V9): its binary, the solid
    /// corners, the crossed edges and the triangle count. Shared by the Inspector's
    /// case panel and the in-game lab panel so they always say the same thing.
    /// </summary>
    public static class VoxelCaseText
    {
        /// <summary>e.g. 41 → "0010 1001", most significant bit (corner 7) first.</summary>
        public static string Binary(int caseIndex)
        {
            string bits = Convert.ToString(caseIndex, 2).PadLeft(8, '0');
            return $"{bits.Substring(0, 4)} {bits.Substring(4)}";
        }

        /// <summary>e.g. "0, 3, 5", or "none".</summary>
        public static string List(IEnumerable<int> values)
        {
            string text = string.Join(", ", values);
            return text.Length > 0 ? text : "none";
        }

        public static IEnumerable<int> SolidCorners(int caseIndex)
        {
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                if (MarchingCubes.IsCornerSolid(caseIndex, corner))
                {
                    yield return corner;
                }
            }
        }

        public static IEnumerable<int> CrossedEdges(int caseIndex)
        {
            int mask = MarchingCubes.GetCrossedEdgeMask(caseIndex);
            for (int edge = 0; edge < MarchingCubes.EdgeCount; edge++)
            {
                if ((mask & (1 << edge)) != 0)
                {
                    yield return edge;
                }
            }
        }

        public static int TriangleCount(int caseIndex)
        {
            int indices = 0;
            while (MarchingCubesTables.Triangles[caseIndex, indices] != -1)
            {
                indices++;
            }
            return indices / 3;
        }
    }
}
