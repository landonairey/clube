using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// How thick the trees stand round a place (GL234): 0 in the open, 1 as thick as deep forest, counted from the
    /// trees that actually grow (<see cref="TreeField.TryGetTree"/>, which knows the flat-ground rule and the start
    /// area's trees), not the forest noise alone, which calls a slope with no trees on it forest. Nearer trees count
    /// more. Grass grows taller and wheat grows where the cover is thin; bushes grow thicker under trees.
    /// </summary>
    /// <remarks>Keeps each looked-at cell's tree root, so the many tufts of a chunk ask the tree field once per cell.</remarks>
    public sealed class TreeCover
    {
        // Cells kept before the cache is cleared.
        private const int MaxCachedCells = 16384;

        private readonly TreeField trees;
        private readonly float radius;
        private readonly Dictionary<Vector2Int, Vector2?> roots = new Dictionary<Vector2Int, Vector2?>();
        private readonly float dense;

        /// <summary>Cover counted from the trees within <paramref name="radius"/> metres.</summary>
        public TreeCover(TreeField trees, float radius = 10f)
        {
            this.trees = trees;
            this.radius = Mathf.Max(1f, radius);
            if (trees != null)
            {
                // What deep forest adds up to: its trees per cell over the circle, each weighted 1/3 on average
                // (a weight falling straight from 1 at the middle to 0 at the edge).
                float cell = trees.CellSize;
                dense = Mathf.Max(0.01f, trees.Settings.Chance * Mathf.PI * this.radius * this.radius / (cell * cell) / 3f);
            }
        }

        /// <summary>The cover at (x, z) in the world's own space, 0-1.</summary>
        public float At(float x, float z)
        {
            if (trees == null)
            {
                return 0f;
            }
            float cell = trees.CellSize;
            int fromX = Mathf.FloorToInt((x - radius) / cell), toX = Mathf.FloorToInt((x + radius) / cell);
            int fromZ = Mathf.FloorToInt((z - radius) / cell), toZ = Mathf.FloorToInt((z + radius) / cell);
            float sum = 0f;
            for (int cz = fromZ; cz <= toZ; cz++)
            {
                for (int cx = fromX; cx <= toX; cx++)
                {
                    Vector2? root = RootIn(new Vector2Int(cx, cz));
                    if (!root.HasValue)
                    {
                        continue;
                    }
                    float distance = Vector2.Distance(root.Value, new Vector2(x, z));
                    if (distance < radius)
                    {
                        sum += 1f - distance / radius;
                    }
                }
            }
            return Mathf.Clamp01(sum / dense);
        }

        /// <summary>
        /// The tree roots a job needs to work out the cover anywhere from (<paramref name="minX"/>, <paramref name="minZ"/>) to
        /// (<paramref name="maxX"/>, <paramref name="maxZ"/>) (GL775), into <paramref name="roots"/>, and the grid that reads them.
        /// </summary>
        public TreeCoverGrid Fill(float minX, float minZ, float maxX, float maxZ, NativeList<float3> roots)
        {
            roots.Clear();
            if (trees == null)
            {
                return new TreeCoverGrid { HasTrees = false, Cell = 1f, Radius = radius, Dense = 1f };
            }
            float cell = trees.CellSize;
            var grid = new TreeCoverGrid
            {
                FromX = Mathf.FloorToInt((minX - radius) / cell),
                FromZ = Mathf.FloorToInt((minZ - radius) / cell),
                Cell = cell,
                Radius = radius,
                Dense = dense,
                HasTrees = true,
            };
            grid.Width = Mathf.FloorToInt((maxX + radius) / cell) - grid.FromX + 1;
            grid.Depth = Mathf.FloorToInt((maxZ + radius) / cell) - grid.FromZ + 1;
            for (int z = 0; z < grid.Depth; z++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    Vector2? root = RootIn(new Vector2Int(grid.FromX + x, grid.FromZ + z));
                    roots.Add(root.HasValue ? new float3(root.Value.x, root.Value.y, 1f) : float3.zero);
                }
            }
            return grid;
        }

        private Vector2? RootIn(Vector2Int cell)
        {
            if (roots.TryGetValue(cell, out Vector2? root))
            {
                return root;
            }
            if (roots.Count >= MaxCachedCells)
            {
                roots.Clear();
            }
            root = trees.TryGetTree(cell, out Vector3 at, out _) ? new Vector2(at.x, at.z) : (Vector2?)null;
            roots.Add(cell, root);
            return root;
        }
    }
}
