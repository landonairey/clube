using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Gives each surface vertex a material (M13): every marching cubes vertex sits on a grid
    /// edge between a solid sample and an empty one, and takes the solid sample's material.
    /// Works from vertex positions alone, so it runs after either mesher (K12) and needs
    /// nothing from the meshing loop.
    /// </summary>
    public static class VertexMaterialSampler
    {
        // Closer than this to a whole sample coordinate counts as on it.
        private const float OnSample = 1e-4f;

        /// <param name="vertices">Chunk-local vertex positions, as the mesher wrote them.</param>
        /// <param name="materials">Cleared, then one material id per vertex.</param>
        public static void Assign(
            Chunk chunk, float isoLevel, float voxelSize, IReadOnlyList<Vector3> vertices, List<byte> materials, MeshSeal seal = MeshSeal.None)
        {
            materials.Clear();
            Vector3Int last = chunk.SampleCount - Vector3Int.one;
            var count = new Unity.Mathematics.int3(chunk.SampleCount.x, chunk.SampleCount.y, chunk.SampleCount.z);
            foreach (Vector3 vertex in vertices)
            {
                materials.Add(chunk.GetMaterial(SolidEnd(chunk, isoLevel, vertex / voxelSize, last, seal, count)));
            }
        }

        // The sample at the solid end of the edge the vertex lies on, in sample coordinates.
        private static Vector3Int SolidEnd(Chunk chunk, float isoLevel, Vector3 grid, Vector3Int last, MeshSeal seal, Unity.Mathematics.int3 count)
        {
            // The edge runs along the axis whose coordinate is furthest from a whole number.
            int axis = 0;
            float furthest = -1f;
            for (int i = 0; i < 3; i++)
            {
                float offWhole = Mathf.Abs(grid[i] - Mathf.Round(grid[i]));
                if (offWhole > furthest)
                {
                    furthest = offWhole;
                    axis = i;
                }
            }

            Vector3Int nearest = Clamp(Vector3Int.RoundToInt(grid), last);
            if (furthest < OnSample)
            {
                // Right on a sample: the surface passes through it.
                return nearest;
            }

            Vector3Int low = nearest;
            low[axis] = Mathf.FloorToInt(grid[axis]);
            low = Clamp(low, last);
            Vector3Int high = low;
            high[axis] = Mathf.Min(low[axis] + 1, last[axis]);

            // A sealed sample was meshed as air (MeshSeal), so the wall's solid end is the other one.
            bool lowSolid = chunk.GetDensity(low) >= isoLevel && !MeshSeals.IsSealed(seal, low.x, low.y, low.z, count);
            bool highSolid = chunk.GetDensity(high) >= isoLevel && !MeshSeals.IsSealed(seal, high.x, high.y, high.z, count);
            if (lowSolid != highSolid)
            {
                return lowSolid ? low : high;
            }

            // Not a crossed edge (rounding at the iso level): take the nearer end.
            return grid[axis] - low[axis] <= 0.5f ? low : high;
        }

        private static Vector3Int Clamp(Vector3Int sample, Vector3Int last)
        {
            return Vector3Int.Max(Vector3Int.zero, Vector3Int.Min(sample, last));
        }
    }
}
