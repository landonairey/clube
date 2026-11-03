using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Puts a <see cref="WaterMap"/>'s rivers and lakes into the voxel data (GL315): every air sample of a wet cell between
    /// its bed and its surface gets water, full below the surface and partly full in the sample the surface crosses, the
    /// same ramp as the ground's densities, so the water's mesh puts its surface at the map's level. These samples are
    /// the generated water: <see cref="LiveWater"/> keeps them topped up (a river or lake is a source without end).
    /// </summary>
    public static class WaterFill
    {
        /// <summary>
        /// How full of generated water a sample at <paramref name="position"/> is, 0-1, whatever the ground there (the
        /// caller checks it's air). 0 on dry land, above the surface, or more than a voxel under the bed (a tunnel under a
        /// lake stays dry until water runs into it).
        /// </summary>
        public static float GeneratedAt(in WaterMap map, Vector3 position, float voxelSize)
        {
            if (!map.IsCreated || !map.TrySurfaceAt(new float2(position.x, position.z), out float surface, out int cell))
            {
                return 0f;
            }
            float bed = map.Surface[cell] - map.Depth[cell];
            if (position.y < bed - voxelSize * 1.5f)
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
            for (int z = 0; z < count.z; z++)
            {
                for (int x = 0; x < count.x; x++)
                {
                    Vector3 column = grid.SampleToWorld(first + new Vector3Int(x, 0, z));
                    if (!map.TrySurfaceAt(new float2(column.x, column.z), out float surface, out int cell))
                    {
                        continue;
                    }
                    // The same as GeneratedAt, with the column's surface worked out once.
                    float lowest = map.Surface[cell] - map.Depth[cell] - voxel * 1.5f;
                    for (int y = 0; y < count.y; y++)
                    {
                        float height = column.y + y * voxel;
                        if (height < lowest || height > surface + voxel * 0.5f)
                        {
                            continue;
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
                    }
                }
            }
            return filled;
        }

        // True when any wet cell under the chunk has its water reaching the chunk's height.
        private static bool Touches(WorldGrid grid, Vector3Int coord, in WaterMap map)
        {
            Vector3 min = grid.ChunkOrigin(coord);
            Vector3 max = min + grid.ChunkWorldSize;
            int2 from = (int2)math.floor((new float2(min.x, min.z) - map.Origin) / map.CellSize) - 1;
            int2 to = (int2)math.floor((new float2(max.x, max.z) - map.Origin) / map.CellSize) + 1;
            from = math.max(from, 0);
            to = math.min(to, map.Size - 1);
            for (int z = from.y; z <= to.y; z++)
            {
                for (int x = from.x; x <= to.x; x++)
                {
                    int i = x + z * map.Size.x;
                    if (map.Kind[i] != 0 && map.Surface[i] + grid.VoxelSize >= min.y && map.Surface[i] - map.Depth[i] - 2f * grid.VoxelSize <= max.y)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
