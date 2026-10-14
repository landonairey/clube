using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Places trees through a world (GL30), the way <see cref="OreField"/> places ore: the ground is
    /// cut into square cells, and whether a cell has a tree, where in the cell it stands and its
    /// shape (<see cref="TreeShape"/>) come from the seed and the cell alone. So a tree doesn't
    /// depend on which chunks are loaded, and every chunk it reaches gets the same parts.
    /// </summary>
    /// <remarks>
    /// <para>A tree grows only where the terrain's surface under it is flat enough
    /// (<see cref="TreeGeneration.MaxSlope"/>), found from the generator's depth, which is a pure
    /// function of position. Placing runs on the main thread and is cached per cell; generation
    /// jobs stamp the parts (<see cref="TreeStamp"/>).</para>
    /// </remarks>
    public sealed class TreeField
    {
        /// <summary>
        /// How far around a part a chunk gathers it, in metres: the density ramp's width, more than
        /// a voxel at every voxel size, so both samples of an edge the surface crosses see it.
        /// </summary>
        public const float GatherMargin = TerrainDensity.RampHalfWidth;

        // Cells kept before the cache is cleared; enough for a large loaded area.
        private const int MaxCachedCells = 16384;

        // Trees keep this far inside their cell, as a fraction of it, so neighbours don't grow into each other's trunks.
        private const float CellMargin = 0.2f;

        // How far from the root the ground is checked for flatness, in metres.
        private const float SlopeProbe = 1f;

        // Salt for the per-cell rolls, apart from the shapes' (TreeShape) and the rocks'.
        private const int CellSalt = 0x43454C4C;

        private readonly TreeGeneration settings;
        private readonly ITerrainGenerator generator;
        private readonly int seed;
        private readonly float maxGradient;
        private readonly Dictionary<Vector2Int, Tree> cells = new Dictionary<Vector2Int, Tree>();
        private readonly List<TreePart> scratch = new List<TreePart>();
        private readonly List<TreePart> gathered = new List<TreePart>();

        // Spots tried in a forced cell (StartArea) before giving up on it.
        private const int ForcedAttempts = 12;

        private readonly StartArea start;

        // Cells given a tree to make up the start area's count; null until worked out.
        private HashSet<Vector2Int> forced;

        public TreeField(TreeGeneration settings, int seed, ITerrainGenerator generator, StartArea start = null)
        {
            this.settings = settings;
            this.start = start;
            this.seed = seed;
            this.generator = generator;
            maxGradient = Mathf.Tan(settings.MaxSlope * Mathf.Deg2Rad);
        }

        /// <summary>A tree field for the terrain's settings, or null when it has no trees.</summary>
        public static TreeField Create(TerrainSettings terrain, ITerrainGenerator generator)
        {
            return generator != null && terrain.Trees.HasTrees ? new TreeField(terrain.Trees, terrain.Seed, generator, terrain.StartArea) : null;
        }

        public float CellSize => settings.CellSize;

        /// <summary>The tree growing in a cell, if any: its root and parts. The same every time.</summary>
        public bool TryGetTree(Vector2Int cell, out Vector3 root, out IReadOnlyList<TreePart> parts)
        {
            StockStartArea();
            Tree tree = TreeIn(cell);
            root = tree.Root;
            parts = tree.Parts;
            return tree.Parts != null;
        }

        /// <summary>
        /// Every tree part within <see cref="GatherMargin"/> of the box, in cell order, then part
        /// order. <paramref name="results"/> is cleared first.
        /// </summary>
        public void CollectParts(Bounds box, List<TreePart> results)
        {
            results.Clear();
            StockStartArea();
            float size = CellSize;
            float reach = settings.MaxSpread + GatherMargin;
            int fromX = Mathf.FloorToInt((box.min.x - reach) / size);
            int fromZ = Mathf.FloorToInt((box.min.z - reach) / size);
            int toX = Mathf.FloorToInt((box.max.x + reach) / size);
            int toZ = Mathf.FloorToInt((box.max.z + reach) / size);
            for (int z = fromZ; z <= toZ; z++)
            {
                for (int x = fromX; x <= toX; x++)
                {
                    Tree tree = TreeIn(new Vector2Int(x, z));
                    if (tree.Parts == null || !tree.Bounds.Intersects(box))
                    {
                        continue;
                    }
                    foreach (TreePart part in tree.Parts)
                    {
                        if (part.Bounds(GatherMargin).Intersects(box))
                        {
                            results.Add(part);
                        }
                    }
                }
            }
        }

        /// <summary>The tree parts reaching a chunk, as generation jobs read them (empty when none).</summary>
        public NativeArray<TreePart> CollectJobParts(Bounds box, Allocator allocator)
        {
            CollectParts(box, gathered);
            var parts = new NativeArray<TreePart>(gathered.Count, allocator, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < gathered.Count; i++)
            {
                parts[i] = gathered[i];
            }
            return parts;
        }

        // The start area gets at least its minimum of trees (StartArea): the seed's trees rooted in it
        // count, and the area's other cells are given a tree, on the first flat spot found, until
        // there are enough. Worked out once, before any tree is handed out.
        private void StockStartArea()
        {
            if (forced != null)
            {
                return;
            }
            forced = new HashSet<Vector2Int>();
            if (start == null || start.MinTrees <= 0)
            {
                return;
            }
            Rect area = start.Area;
            float size = CellSize;
            var empty = new List<Vector2Int>();
            int count = 0;
            for (int z = Mathf.FloorToInt(area.yMin / size); z <= Mathf.FloorToInt((area.yMax - 0.01f) / size); z++)
            {
                for (int x = Mathf.FloorToInt(area.xMin / size); x <= Mathf.FloorToInt((area.xMax - 0.01f) / size); x++)
                {
                    var cell = new Vector2Int(x, z);
                    Tree tree = TreeIn(cell);
                    if (tree.Parts != null && area.Contains(new Vector2(tree.Root.x, tree.Root.z)))
                    {
                        count++;
                    }
                    else
                    {
                        empty.Add(cell);
                    }
                }
            }
            foreach (Vector2Int cell in empty)
            {
                if (count >= start.MinTrees)
                {
                    break;
                }
                forced.Add(cell);
                cells.Remove(cell);
                if (TreeIn(cell).Parts != null)
                {
                    count++;
                }
                else
                {
                    // No flat spot: the cell keeps whatever the seed gave it.
                    forced.Remove(cell);
                    cells.Remove(cell);
                }
            }
        }

        private Tree TreeIn(Vector2Int cell)
        {
            if (cells.TryGetValue(cell, out Tree tree))
            {
                return tree;
            }
            if (cells.Count >= MaxCachedCells)
            {
                cells.Clear();
            }
            tree = Grow(cell);
            cells.Add(cell, tree);
            return tree;
        }

        // The cell's tree, or none: a roll, a spot inside the cell, and flat ground there.
        private Tree Grow(Vector2Int cell)
        {
            if (forced != null && forced.Contains(cell))
            {
                return GrowForced(cell);
            }
            if (VoxelHash.Uniform(seed, cell.x, 0, cell.y, CellSalt) >= settings.Chance)
            {
                return default;
            }

            float size = CellSize;
            float x = (cell.x + Mathf.Lerp(CellMargin, 1f - CellMargin, VoxelHash.Uniform(seed, cell.x, 0, cell.y, CellSalt + 1))) * size;
            float z = (cell.y + Mathf.Lerp(CellMargin, 1f - CellMargin, VoxelHash.Uniform(seed, cell.x, 0, cell.y, CellSalt + 2))) * size;
            if (!TryFindGround(x, z, out float ground) || !IsFlat(x, ground, z))
            {
                return default;
            }
            return Build(cell, new Vector3(x, ground, z));
        }

        // A tree the start area needs: hashed spots in the part of the cell inside the area, the
        // first on flat ground; none when the cell has no flat spot.
        private Tree GrowForced(Vector2Int cell)
        {
            float size = CellSize;
            Rect area = start.Area;
            float fromX = Mathf.Max(cell.x * size, area.xMin) + 1f;
            float toX = Mathf.Min((cell.x + 1) * size, area.xMax) - 1f;
            float fromZ = Mathf.Max(cell.y * size, area.yMin) + 1f;
            float toZ = Mathf.Min((cell.y + 1) * size, area.yMax) - 1f;
            if (fromX > toX || fromZ > toZ)
            {
                return default;
            }
            for (int attempt = 0; attempt < ForcedAttempts; attempt++)
            {
                float x = Mathf.Lerp(fromX, toX, VoxelHash.Uniform(seed, cell.x, attempt, cell.y, CellSalt + 3));
                float z = Mathf.Lerp(fromZ, toZ, VoxelHash.Uniform(seed, cell.x, attempt, cell.y, CellSalt + 4));
                if (TryFindGround(x, z, out float ground) && IsFlat(x, ground, z))
                {
                    return Build(cell, new Vector3(x, ground, z));
                }
            }
            return default;
        }

        private Tree Build(Vector2Int cell, Vector3 root)
        {
            scratch.Clear();
            TreeShape.Build(seed, cell, root, settings, scratch);
            Bounds bounds = scratch[0].Bounds(GatherMargin);
            foreach (TreePart part in scratch)
            {
                bounds.Encapsulate(part.Bounds(GatherMargin));
            }
            return new Tree { Root = root, Parts = scratch.ToArray(), Bounds = bounds };
        }

        // The surface height over (x, z): exact in one step for a heightfield (its depth is height
        // minus y), and a few more for a 3D terrain. False where no surface is found near.
        private bool TryFindGround(float x, float z, out float ground)
        {
            ground = 0f;
            for (int step = 0; step < 4; step++)
            {
                float depth = generator.Depth(new Vector3(x, ground, z));
                if (Mathf.Abs(depth) < 0.01f)
                {
                    return true;
                }
                ground += depth;
            }
            return Mathf.Abs(generator.Depth(new Vector3(x, ground, z))) < 0.05f;
        }

        // The ground rises or falls no more than the steepest allowed slope around the root.
        private bool IsFlat(float x, float ground, float z)
        {
            float limit = maxGradient * SlopeProbe;
            return Mathf.Abs(generator.Depth(new Vector3(x + SlopeProbe, ground, z))) <= limit
                   && Mathf.Abs(generator.Depth(new Vector3(x - SlopeProbe, ground, z))) <= limit
                   && Mathf.Abs(generator.Depth(new Vector3(x, ground, z + SlopeProbe))) <= limit
                   && Mathf.Abs(generator.Depth(new Vector3(x, ground, z - SlopeProbe))) <= limit;
        }

        // One cell's tree; Parts is null when the cell has none.
        private struct Tree
        {
            public Vector3 Root;
            public TreePart[] Parts;
            public Bounds Bounds;
        }
    }
}
