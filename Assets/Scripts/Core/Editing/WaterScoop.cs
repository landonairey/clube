using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Takes water out of the voxel data and puts it back (GL319, the bucket). Volume is kept: a full water sample is one
    /// unit, so what a scoop takes is what the bucket gains and a pour puts back exactly what it gives. A scoop takes the
    /// samples nearest the point first; a pour fills the open samples round the point from the lowest up, as water would
    /// settle. Every sample changed is listed, so the caller can wake the flow there (<see cref="LiveWater.Wake"/>).
    /// </summary>
    public static class WaterScoop
    {
        private static readonly List<(Vector3Int sample, float distance)> Found = new List<(Vector3Int, float)>();

        /// <summary>
        /// Takes up to <paramref name="most"/> units of water from the samples within <paramref name="radius"/> metres of
        /// <paramref name="centre"/>, nearest first. Returns the units taken (rounded down; any fraction stays in the water).
        /// </summary>
        public static int Take(World world, Vector3 centre, float radius, int most, List<Vector3Int> changed)
        {
            if (most <= 0)
            {
                return 0;
            }
            Gather(world, centre, radius, sample => world.GetWater(sample) > 0f);
            float taken = 0f;
            foreach ((Vector3Int sample, float _) in Found)
            {
                float room = most - taken;
                if (room < 1e-3f)
                {
                    break;
                }
                float there = world.GetWater(sample);
                float take = Mathf.Min(there, room);
                world.SetWater(sample, there - take);
                taken += take;
                changed?.Add(sample);
            }
            int whole = Mathf.FloorToInt(taken + 1e-3f);
            // Gives the fraction back to the first sample it can, so no water is lost.
            float rest = taken - whole;
            for (int i = 0; i < Found.Count && rest > 1e-4f; i++)
            {
                float there = world.GetWater(Found[i].sample);
                float put = Mathf.Min(1f - there, rest);
                world.SetWater(Found[i].sample, there + put);
                rest -= put;
            }
            return whole;
        }

        /// <summary>
        /// Pours <paramref name="units"/> of water into the open (air) samples within <paramref name="radius"/> metres of
        /// <paramref name="centre"/>, lowest first. Returns the units poured (less when there's no room).
        /// </summary>
        public static int Pour(World world, Vector3 centre, float radius, int units, float iso, List<Vector3Int> changed)
        {
            if (units <= 0)
            {
                return 0;
            }
            Gather(world, centre, radius, sample =>
            {
                float? density = world.GetDensity(sample);
                return density.HasValue && density.Value < iso && world.GetWater(sample) < 1f;
            });
            Found.Sort((a, b) => a.sample.y != b.sample.y ? a.sample.y.CompareTo(b.sample.y) : a.distance.CompareTo(b.distance));
            float left = units;
            foreach ((Vector3Int sample, float _) in Found)
            {
                if (left < 1e-3f)
                {
                    break;
                }
                float there = world.GetWater(sample);
                float put = Mathf.Min(1f - there, left);
                world.SetWater(sample, there + put);
                left -= put;
                changed?.Add(sample);
            }
            return units - Mathf.CeilToInt(left - 1e-3f);
        }

        private static void Gather(World world, Vector3 centre, float radius, System.Func<Vector3Int, bool> keep)
        {
            Found.Clear();
            WorldGrid grid = world.Grid;
            int reach = Mathf.CeilToInt(radius / grid.VoxelSize);
            Vector3Int middle = grid.WorldToNearestSample(centre);
            for (int z = -reach; z <= reach; z++)
            {
                for (int y = -reach; y <= reach; y++)
                {
                    for (int x = -reach; x <= reach; x++)
                    {
                        Vector3Int sample = middle + new Vector3Int(x, y, z);
                        float distance = Vector3.Distance(grid.SampleToWorld(sample), centre);
                        if (distance <= radius && keep(sample))
                        {
                            Found.Add((sample, distance));
                        }
                    }
                }
            }
            Found.Sort((a, b) => a.distance.CompareTo(b.distance));
        }
    }
}
