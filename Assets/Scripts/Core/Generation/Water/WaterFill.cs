using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Puts a <see cref="WaterMap"/>'s rivers and lakes into the voxel data (GL315, GL318): every air sample of a wet cell
    /// under its surface gets water, full below the surface and partly full in the sample the surface crosses, the same
    /// ramp as the ground's densities, so the water's mesh puts its surface at the map's level. Air all the way down to the
    /// ground is filled, so the bed holds no dry pockets. Past the wet cells the water runs on over the shore for up to
    /// <see cref="ShoreReach"/> samples, at the same level, wherever the ground under it holds it up, so a bank lower than
    /// the map thought is under water rather than beside a wall of it. Wet-cell samples are the generated water:
    /// <see cref="LiveWater"/> keeps them topped up (a river or lake is a source without end).
    /// </summary>
    public static class WaterFill
    {
        /// <summary>How far, in samples, the water runs on past the wet cells over a low shore.</summary>
        public const int ShoreReach = 12;

        private static readonly Vector3Int[] Sideways =
        {
            Vector3Int.right, Vector3Int.left, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        private static readonly List<Vector3Int> Front = new List<Vector3Int>();
        private static readonly List<Vector3Int> Next = new List<Vector3Int>();

        /// <summary>
        /// How full of generated water a sample at <paramref name="position"/> is, 0-1, whatever the ground there (the
        /// caller checks it's air). 0 on dry land or above the surface.
        /// </summary>
        public static float GeneratedAt(in WaterMap map, Vector3 position, float voxelSize)
        {
            if (!map.IsCreated || !map.TrySurfaceAt(new float2(position.x, position.z), out float surface, out _))
            {
                return 0f;
            }
            return Mathf.Clamp01((surface - position.y) / voxelSize + 0.5f);
        }

        /// <summary>
        /// Fills a newly loaded chunk's air samples with the map's water. Returns the samples filled. Cheap for chunks
        /// nowhere near water: their footprint's cells are checked first.
        /// </summary>
        public static int Fill(World world, Vector3Int coord, in WaterMap map, float iso)
        {
            if (!map.IsCreated || !world.TryGetChunk(coord, out Chunk chunk) || !Touches(world.Grid, coord, map))
            {
                return 0;
            }
            WorldGrid grid = world.Grid;
            Vector3Int first = grid.ChunkFirstSample(coord);
            Vector3Int count = chunk.SampleCount;
            float voxel = grid.VoxelSize;
            int filled = 0;
            Front.Clear();
            for (int z = 0; z < count.z; z++)
            {
                for (int x = 0; x < count.x; x++)
                {
                    Vector3 column = grid.SampleToWorld(first + new Vector3Int(x, 0, z));
                    if (!map.TrySurfaceAt(new float2(column.x, column.z), out float surface, out int cell))
                    {
                        continue;
                    }
                    for (int y = 0; y < count.y; y++)
                    {
                        float height = column.y + y * voxel;
                        if (height > surface + voxel * 0.5f)
                        {
                            break;
                        }
                        var local = new Vector3Int(x, y, z);
                        if (chunk.GetDensity(local) >= iso)
                        {
                            continue;
                        }
                        float amount = Mathf.Clamp01((surface - height) / voxel + 0.5f);
                        if (amount > 0f && chunk.SetWater(local, Mathf.Max(amount, chunk.GetWater(local))))
                        {
                            filled++;
                        }
                        // Only lakes run on over their shores: a river's banks are cut, and its water runs downhill.
                        if (amount > 0f && map.Kind[cell] == (byte)WaterKind.Lake)
                        {
                            Front.Add(local);
                        }
                    }
                }
            }
            filled += SeedFromNeighbours(world, coord, chunk);
            return filled + RunOverShore(world, coord, chunk, map, iso);
        }


        // Only dry cells take the shore's water: a lower lake's cells keep their own level.
        private static bool IsWetCell(World world, Vector3Int coord, Vector3Int local, in WaterMap map)
        {
            Vector3 position = world.Grid.SampleToWorld(world.Grid.ChunkFirstSample(coord) + local);
            return map.TryCell(new float2(position.x, position.z), out int cell) && map.Kind[cell] != 0;
        }

        // Water a loaded neighbour ran over its shore up to the shared face carries on into this chunk: the face samples
        // are copies of each other, so this chunk's copies take the neighbour's water and start the run from there.
        private static int SeedFromNeighbours(World world, Vector3Int coord, Chunk chunk)
        {
            Vector3Int count = chunk.SampleCount;
            Vector3Int size = world.Grid.ChunkSize;
            int filled = 0;
            foreach (Vector3Int step in Sideways)
            {
                if (!world.TryGetChunk(coord + step, out Chunk next) || !next.HasWater)
                {
                    continue;
                }
                // The face this chunk shares with that neighbour, and where those samples sit in the neighbour.
                bool alongX = step.x != 0;
                int mine = (alongX ? step.x : step.z) > 0 ? (alongX ? count.x : count.z) - 1 : 0;
                int theirs = mine == 0 ? (alongX ? size.x : size.z) : 0;
                for (int y = 0; y < count.y; y++)
                {
                    for (int a = 0; a < (alongX ? count.z : count.x); a++)
                    {
                        Vector3Int local = alongX ? new Vector3Int(mine, y, a) : new Vector3Int(a, y, mine);
                        Vector3Int there = alongX ? new Vector3Int(theirs, y, a) : new Vector3Int(a, y, theirs);
                        float water = next.GetWater(there);
                        if (water > chunk.GetWater(local) && chunk.SetWater(local, water))
                        {
                            Front.Add(local);
                            filled++;
                        }
                    }
                }
            }
            return filled;
        }
        // The water runs on sideways from the wet cells, a sample a pass, into air at the same height that stands on
        // ground or on water, taking the level it came from.
        private static int RunOverShore(World world, Vector3Int coord, Chunk chunk, in WaterMap map, float iso)
        {
            Vector3Int count = chunk.SampleCount;
            int filled = 0;
            for (int pass = 0; pass < ShoreReach && Front.Count > 0; pass++)
            {
                Next.Clear();
                foreach (Vector3Int from in Front)
                {
                    float amount = chunk.GetWater(from);
                    foreach (Vector3Int step in Sideways)
                    {
                        Vector3Int to = from + step;
                        if (to.x < 0 || to.z < 0 || to.x >= count.x || to.z >= count.z || chunk.GetWater(to) >= amount
                            || chunk.GetDensity(to) >= iso || !HeldUp(world, coord, chunk, to, iso) || IsWetCell(world, coord, to, map))
                        {
                            continue;
                        }
                        chunk.SetWater(to, amount);
                        if (to.x == 0 || to.z == 0 || to.x == count.x - 1 || to.z == count.z - 1)
                        {
                            // A face sample: the neighbour's copy too, so the two chunks agree.
                            world.SetWater(world.Grid.ChunkFirstSample(coord) + to, amount);
                        }
                        Next.Add(to);
                        filled++;
                        // Water standing on this sample can now be held up too.
                        Vector3Int above = to + Vector3Int.up;
                        if (above.y < count.y && chunk.GetDensity(above) < iso && chunk.GetWater(above) < chunk.GetWater(from + Vector3Int.up))
                        {
                            float over = chunk.GetWater(from + Vector3Int.up);
                            if (over > 0f)
                            {
                                chunk.SetWater(above, over);
                                Next.Add(above);
                                filled++;
                            }
                        }
                    }
                }
                Front.Clear();
                Front.AddRange(Next);
            }
            return filled;
        }

        // Ground or a full sample of water under it; under the chunk.s bottom row, the chunk below says (unloaded: not held).
        private static bool HeldUp(World world, Vector3Int coord, Chunk chunk, Vector3Int sample, float iso)
        {
            if (sample.y == 0)
            {
                Vector3Int under = world.Grid.ChunkFirstSample(coord) + sample + Vector3Int.down;
                float? density = world.GetDensity(under);
                return density.HasValue && (density.Value >= iso || world.GetWater(under) > 0.98f);
            }
            Vector3Int below = sample + Vector3Int.down;
            return chunk.GetDensity(below) >= iso || chunk.GetWater(below) > 0.98f;
        }

        // True when any wet cell under the chunk (or a few metres round it) has its surface at or over the chunk's bottom.
        private static bool Touches(WorldGrid grid, Vector3Int coord, in WaterMap map)
        {
            Vector3 min = grid.ChunkOrigin(coord);
            Vector3 max = min + grid.ChunkWorldSize;
            int margin = Mathf.CeilToInt(ShoreReach * grid.VoxelSize / map.CellSize) + 1;
            int2 from = (int2)math.floor((new float2(min.x, min.z) - map.Origin) / map.CellSize) - margin;
            int2 to = (int2)math.floor((new float2(max.x, max.z) - map.Origin) / map.CellSize) + margin;
            from = math.max(from, 0);
            to = math.min(to, map.Size - 1);
            for (int z = from.y; z <= to.y; z++)
            {
                for (int x = from.x; x <= to.x; x++)
                {
                    int i = x + z * map.Size.x;
                    if (map.Kind[i] != 0 && map.Surface[i] + grid.VoxelSize >= min.y)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
