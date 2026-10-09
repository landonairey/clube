using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>How a <see cref="TerrainPile"/> grows: its shape, slope and the cells blocks fill (GL17).</summary>
    public readonly struct PileSettings
    {
        public PileSettings(PileShape shape, float reposeAngle, BuildGrid grid)
        {
            Shape = shape;
            ReposeAngle = Mathf.Clamp(reposeAngle, 10f, 80f);
            Grid = grid;
        }

        /// <summary>A 40° cone: soft ground, and what tests use.</summary>
        public static PileSettings Cone => new PileSettings(PileShape.Cone, 40f, new BuildGrid(1f));

        /// <summary>The settings for a material, on a world's build grid.</summary>
        public static PileSettings For(VoxelMaterial material, BuildGrid grid)
        {
            return material != null ? new PileSettings(material.PileShape, material.ReposeAngle, grid) : new PileSettings(PileShape.Cone, 40f, grid);
        }

        public PileShape Shape { get; }

        /// <summary>Steepest slope of a cone, in degrees.</summary>
        public float ReposeAngle { get; }

        public BuildGrid Grid { get; }
    }

    /// <summary>
    /// Puts material back into the ground as a pile (dropping items, I4/I6, GL17): fills air
    /// samples one at a time, each resting on something solid. Soft ground grows as a cone at
    /// its angle of repose around the drop point; hard material stacks as a level-topped block
    /// inside the build cell it lands in, one cell high, then fills the next nearest cell. One
    /// sample per item, the same amount a tool removed to collect it (GL5). Writes go through
    /// the world's edit paths (A7), so border copies stay equal.
    /// </summary>
    /// <remarks>
    /// Piles don't slump once made; settling over time is SM1.
    /// </remarks>
    /// <summary>
    /// A pour in progress (GL27): material dropped a batch at a time onto one pile, which keeps
    /// growing from where it began. Start each pour from <c>default</c> and pass it to every
    /// <see cref="TerrainPile.Pour"/> of that pour.
    /// </summary>
    public struct PilePour
    {
        /// <summary>False until the first batch is placed.</summary>
        public bool Started;

        /// <summary>Where the pour was aimed first, relative to the world origin.</summary>
        public Vector3 Point;

        /// <summary>A cone pour's base: the first air sample on the ground, which every batch's cone is measured from.</summary>
        public Vector3Int Base;

        /// <summary>Samples placed so far.</summary>
        public int Total;
    }

    public static class TerrainPile
    {
        private static readonly Vector3Int[] Faces =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down,
            new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        // How many cells around the first one a block pile may spread to.
        private const int BlockCellRadius = 4;

        // Reused between calls; edits only run on the main thread.
        private static readonly List<Vector3Int> Candidates = new List<Vector3Int>();
        private static readonly HashSet<Vector3Int> Seen = new HashSet<Vector3Int>();
        private static readonly List<Vector2Int> CellOrder = new List<Vector2Int>();
        private static readonly HashSet<Vector2Int> Exhausted = new HashSet<Vector2Int>();
        private static readonly List<Vector3Int> Filled = new List<Vector3Int>();

        /// <summary>
        /// The global samples the last <see cref="Place(World, Vector3, byte, int, float, PileSettings)"/>
        /// or <see cref="Pour"/> filled, e.g. to start clay drying (GL34). Overwritten by the next call.
        /// </summary>
        public static IReadOnlyList<Vector3Int> LastFilled => Filled;

        /// <summary>Places a 40° cone (soft ground). See <see cref="Place(World, Vector3, byte, int, float, PileSettings)"/>.</summary>
        public static int Place(World world, Vector3 point, byte material, int count, float isoLevel)
        {
            return Place(world, point, material, count, isoLevel, PileSettings.Cone);
        }

        /// <summary>
        /// Fills up to <paramref name="count"/> samples with <paramref name="material"/> around
        /// <paramref name="point"/> (relative to the world origin), shaped by
        /// <paramref name="settings"/>. Returns how many it filled: fewer when there's no
        /// supported air nearby or the samples aren't loaded.
        /// </summary>
        public static int Place(World world, Vector3 point, byte material, int count, float isoLevel, PileSettings settings)
        {
            Filled.Clear();
            return settings.Shape == PileShape.Block
                ? PlaceBlocks(world, point, material, count, isoLevel, settings.Grid)
                : PlaceCone(world, point, material, count, isoLevel, settings.ReposeAngle);
        }

        /// <summary>
        /// Places one batch of a pour (GL27): like <see cref="Place(World, Vector3, byte, int, float, PileSettings)"/>,
        /// but every batch of the pour builds on the same pile (a cone around the pour's first
        /// base, or blocks from its first point), so the pile's size follows the total poured.
        /// Returns how many it filled this batch.
        /// </summary>
        public static int Pour(World world, ref PilePour pour, Vector3 point, byte material, int count, float isoLevel, PileSettings settings)
        {
            Filled.Clear();
            if (!pour.Started)
            {
                pour.Point = point;
            }
            int placed;
            if (settings.Shape == PileShape.Block)
            {
                placed = PlaceBlocks(world, pour.Point, material, count, isoLevel, settings.Grid);
            }
            else
            {
                Vector3Int? coneBase = pour.Started ? pour.Base : (Vector3Int?)null;
                placed = PlaceCone(world, pour.Point, material, count, isoLevel, settings.ReposeAngle, coneBase, pour.Total + count, out Vector3Int start);
                pour.Base = start;
            }
            pour.Started = true;
            pour.Total += placed;
            return placed;
        }

        private static int PlaceCone(World world, Vector3 point, byte material, int count, float isoLevel, float reposeAngle)
        {
            return PlaceCone(world, point, material, count, isoLevel, reposeAngle, null, count, out _);
        }

        // Nearest the start first, where height counts as distance / tan(angle): every sample
        // filled lies under a cone of that slope, which grows outwards as it fills. A pour's later
        // batches keep its first base and start from the surface of the pile so far, out to the
        // radius a cone of the whole amount needs.
        private static int PlaceCone(
            World world, Vector3 point, byte material, int count, float isoLevel, float reposeAngle,
            Vector3Int? coneBase, int total, out Vector3Int start)
        {
            Candidates.Clear();
            Seen.Clear();
            float slope = Mathf.Tan(reposeAngle * Mathf.Deg2Rad);
            if (coneBase.HasValue)
            {
                start = coneBase.Value;
                SeedPileSurface(world, start, total, slope, isoLevel);
            }
            else
            {
                start = SettledStart(world, point, isoLevel);
            }
            float climb = 1f / slope;
            int filled = 0;
            while (filled < count && Candidates.Count > 0)
            {
                int best = 0;
                float bestScore = float.PositiveInfinity;
                for (int i = 0; i < Candidates.Count; i++)
                {
                    Vector3Int d = Candidates[i] - start;
                    float score = Mathf.Sqrt(d.x * d.x + d.z * d.z) + Mathf.Max(0, d.y) * climb;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = i;
                    }
                }
                if (Fill(world, best, material, isoLevel, null))
                {
                    filled++;
                }
            }
            return filled;
        }

        // The supported air on top of every column within the radius of a cone of total samples
        // (V = pi r^2 h / 3, h = r tan(angle)), searched down from above its peak: where a pour's
        // next batch can go.
        private static void SeedPileSurface(World world, Vector3Int start, int total, float slope, float isoLevel)
        {
            int radius = Mathf.CeilToInt(Mathf.Pow(3f * total / (Mathf.PI * slope), 1f / 3f)) + 2;
            int above = Mathf.CeilToInt(radius * slope) + 2;
            for (int dz = -radius; dz <= radius; dz++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dz * dz > radius * radius)
                    {
                        continue;
                    }
                    var top = new Vector3Int(start.x + dx, start.y + above, start.z + dz);
                    if (TrySurface(world, top, above + radius + 4, isoLevel, out Vector3Int air))
                    {
                        Consider(world, air, isoLevel);
                    }
                }
            }
        }

        // A cell at a time, filling the grid cube its lowest supported air is in (cells are
        // cubes: the build grid in height too), lowest layer first so each block comes out level. The next cell is the one lowest under a stepped cone: its
        // height in cell layers above the drop plus its distance in cells (the lower on a tie),
        // so a full cell sends the next drop to its sides, then its corners, then up a layer.
        private static int PlaceBlocks(World world, Vector3 point, byte material, int count, float isoLevel, BuildGrid grid)
        {
            float voxel = world.Grid.VoxelSize;
            int cellSamples = Mathf.Max(1, Mathf.RoundToInt(grid.CellSize / voxel));
            Vector3Int start = NearestAir(world, point, isoLevel);
            Vector2Int first = grid.Cell(point);
            OrderCells(grid, first, point);
            Exhausted.Clear();

            int filled = 0;
            while (filled < count)
            {
                // The lowest-scoring cell that still has room.
                Vector2Int best = default;
                int bestFloor = 0;
                float bestScore = float.PositiveInfinity;
                foreach (Vector2Int cell in CellOrder)
                {
                    if (Exhausted.Contains(cell) || !TryFloor(world, grid, cell, start.y + cellSamples * 2, cellSamples * 8, isoLevel, out int floor))
                    {
                        continue;
                    }
                    float score = (floor - start.y) / (float)cellSamples + Vector2Int.Distance(cell, first);
                    if (score < bestScore - 1e-4f || (score < bestScore + 1e-4f && floor < bestFloor))
                    {
                        best = cell;
                        bestFloor = floor;
                        bestScore = score;
                    }
                }
                if (float.IsPositiveInfinity(bestScore))
                {
                    break;
                }

                // Up to the top of the grid cube the floor is in, so blocks line up in height too
                // and a half-filled cell finishes its own cube.
                int top = (Mathf.FloorToInt((float)bestFloor / cellSamples) + 1) * cellSamples - 1;
                int placed = FillCell(world, grid, best, bestFloor, top, material, count - filled, isoLevel);
                if (placed == 0)
                {
                    Exhausted.Add(best);
                }
                filled += placed;
            }
            return filled;
        }

        // A cell's lowest supported air (over its columns), searched down from a height.
        private static bool TryFloor(World world, BuildGrid grid, Vector2Int cell, int fromY, int distance, float isoLevel, out int floor)
        {
            floor = int.MaxValue;
            grid.SampleColumns(cell, world.Grid.VoxelSize, out Vector2Int min, out Vector2Int max);
            for (int z = min.y; z <= max.y; z++)
            {
                for (int x = min.x; x <= max.x; x++)
                {
                    if (TrySurface(world, new Vector3Int(x, fromY, z), distance, isoLevel, out Vector3Int air))
                    {
                        floor = Mathf.Min(floor, air.y);
                    }
                }
            }
            return floor != int.MaxValue;
        }

        // Fills a cell's supported air from its floor up to a top, lowest layer first, at most
        // a count; returns how many it filled.
        private static int FillCell(World world, BuildGrid grid, Vector2Int cell, int floor, int top, byte material, int count, float isoLevel)
        {
            grid.SampleColumns(cell, world.Grid.VoxelSize, out Vector2Int min, out Vector2Int max);
            Candidates.Clear();
            Seen.Clear();
            for (int z = min.y; z <= max.y; z++)
            {
                for (int x = min.x; x <= max.x; x++)
                {
                    if (TrySurface(world, new Vector3Int(x, top + 1, z), top + 1 - floor + 1, isoLevel, out Vector3Int air))
                    {
                        Consider(world, air, isoLevel);
                    }
                }
            }

            var bounds = new RectInt(min.x, min.y, max.x - min.x + 1, max.y - min.y + 1);
            int filled = 0;
            while (filled < count && Candidates.Count > 0)
            {
                int best = 0;
                for (int i = 1; i < Candidates.Count; i++)
                {
                    if (Candidates[i].y < Candidates[best].y)
                    {
                        best = i;
                    }
                }
                if (Candidates[best].y > top)
                {
                    Candidates.RemoveAt(best);
                    continue;
                }
                if (Fill(world, best, material, isoLevel, bounds))
                {
                    filled++;
                }
            }
            return filled;
        }

        // Fills the candidate at an index if it's still supported air, then offers its neighbours
        // (only those inside the columns, when given). True when it filled.
        private static bool Fill(World world, int index, byte material, float isoLevel, RectInt? columns)
        {
            Vector3Int sample = Candidates[index];
            Candidates.RemoveAt(index);
            if (!IsSupportedAir(world, sample, isoLevel))
            {
                return false;
            }
            world.SetMaterial(sample, material);
            world.SetDensity(sample, 1f);
            Filled.Add(sample);
            foreach (Vector3Int face in Faces)
            {
                Vector3Int next = sample + face;
                if (columns == null || columns.Value.Contains(new Vector2Int(next.x, next.z)))
                {
                    Consider(world, next, isoLevel);
                }
            }
            return true;
        }

        // The cells a block pile may use, nearest the drop point first.
        private static void OrderCells(BuildGrid grid, Vector2Int first, Vector3 point)
        {
            CellOrder.Clear();
            for (int z = -BlockCellRadius; z <= BlockCellRadius; z++)
            {
                for (int x = -BlockCellRadius; x <= BlockCellRadius; x++)
                {
                    CellOrder.Add(first + new Vector2Int(x, z));
                }
            }
            var target = new Vector2(point.x, point.z);
            CellOrder.Sort((a, b) => (grid.Centre(a) - target).sqrMagnitude.CompareTo((grid.Centre(b) - target).sqrMagnitude));
        }

        // The start of a cone: the air sample nearest the point, settled onto the first support below.
        private static Vector3Int SettledStart(World world, Vector3 point, float isoLevel)
        {
            Vector3Int start = NearestAir(world, point, isoLevel);
            Consider(world, start, isoLevel);
            for (int down = 0; Candidates.Count == 0 && down < 64; down++)
            {
                start += Vector3Int.down;
                Consider(world, start, isoLevel);
            }
            return start;
        }

        // Walking down from a sample: the first loaded air with solid under it, within a distance.
        private static bool TrySurface(World world, Vector3Int from, int distance, float isoLevel, out Vector3Int air)
        {
            for (int i = 0; i < distance; i++)
            {
                Vector3Int sample = from + Vector3Int.down * i;
                if (IsSupportedAir(world, sample, isoLevel))
                {
                    air = sample;
                    return true;
                }
            }
            air = default;
            return false;
        }

        // The air sample nearest the point: the point's own when it's air, else the nearest
        // within two samples (a point on a wall or slope sits just inside it), else straight
        // up out of the ground.
        private static Vector3Int NearestAir(World world, Vector3 point, float isoLevel)
        {
            Vector3Int centre = world.Grid.WorldToNearestSample(point);
            if (IsAir(world, centre, isoLevel))
            {
                return centre;
            }

            const int Radius = 2;
            Vector3Int best = centre;
            float bestDistance = float.PositiveInfinity;
            for (int z = -Radius; z <= Radius; z++)
            {
                for (int y = -Radius; y <= Radius; y++)
                {
                    for (int x = -Radius; x <= Radius; x++)
                    {
                        Vector3Int sample = centre + new Vector3Int(x, y, z);
                        float distance = (world.Grid.SampleToWorld(sample) - point).sqrMagnitude;
                        if (distance < bestDistance && IsAir(world, sample, isoLevel))
                        {
                            bestDistance = distance;
                            best = sample;
                        }
                    }
                }
            }
            if (!float.IsPositiveInfinity(bestDistance))
            {
                return best;
            }

            for (int up = 0; up < 64 && !IsAir(world, best, isoLevel); up++)
            {
                best += Vector3Int.up;
            }
            return best;
        }

        private static bool IsAir(World world, Vector3Int sample, float isoLevel)
        {
            float? density = world.GetDensity(sample);
            return density.HasValue && density.Value < isoLevel;
        }

        // Only samples that are candidates count as seen: an unsupported one can become a
        // candidate later, once the sample under it is filled.
        private static void Consider(World world, Vector3Int sample, float isoLevel)
        {
            if (!Seen.Contains(sample) && IsSupportedAir(world, sample, isoLevel))
            {
                Seen.Add(sample);
                Candidates.Add(sample);
            }
        }

        // Loaded air with solid directly below it.
        private static bool IsSupportedAir(World world, Vector3Int sample, float isoLevel)
        {
            float? density = world.GetDensity(sample);
            float? below = world.GetDensity(sample + Vector3Int.down);
            return density.HasValue && density.Value < isoLevel && below.HasValue && below.Value >= isoLevel;
        }
    }
}
