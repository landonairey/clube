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
        /// <paramref name="centre"/>, nearest first, emptying each sample it takes from (GL335). Returns the units taken (rounded
        /// down: the fraction goes back into the furthest sample taken from, so the middle of the scoop is left empty).
        /// </summary>
        public static int Take(World world, Vector3 centre, float radius, int most, List<Vector3Int> changed)
        {
            if (most <= 0)
            {
                return 0;
            }
            Gather(world, centre, radius, sample => world.GetWater(sample) > 0f);
            float taken = 0f;
            int used = 0;
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
                used++;
                changed?.Add(sample);
            }
            int whole = Mathf.FloorToInt(taken + 1e-3f);
            if (whole == 0 && taken > 1e-3f)
            {
                // A speck under one unit (GL396) comes up whole, as one unit, or it could never be scooped.
                return 1;
            }
            // Gives the fraction back, furthest out first, so no water is lost.
            float rest = taken - whole;
            for (int i = used - 1; i >= 0 && rest > 1e-4f; i--)
            {
                float there = world.GetWater(Found[i].sample);
                float put = Mathf.Min(1f - there, rest);
                world.SetWater(Found[i].sample, there + put);
                rest -= put;
            }
            return whole;
        }

        /// <summary>
        /// Pours <paramref name="units"/> of water where <paramref name="centre"/> is, the way water settles (GL323): it
        /// drops to the ground under the point, then fills the lowest open samples it can reach from there, out to
        /// <paramref name="radius"/> metres across and no more than a sample above where it landed, so it pools into the
        /// hollows and spreads as a puddle instead of standing as a ball or a wall. Returns the units poured (less when
        /// there's no room).
        /// </summary>
        public static int Pour(World world, Vector3 centre, float radius, int units, float iso, List<Vector3Int> changed)
        {
            if (units <= 0)
            {
                return 0;
            }
            WorldGrid grid = world.Grid;
            Vector3Int start = grid.WorldToNearestSample(centre);
            if (!IsOpen(world, start, iso))
            {
                return 0;
            }
            // Down through air and water to the ground (GL324): stopping on water stacked each small pour of a held
            // pour on the last, into a column.
            for (int i = 0; i < 256 && IsOpen(world, start + Vector3Int.down, iso); i++)
            {
                start += Vector3Int.down;
            }
            // Then up to the water's surface there: the pour fills no higher than a sample over it.
            Vector3Int surface = start;
            for (int i = 0; i < 256 && IsOpen(world, surface, iso) && world.GetWater(surface) >= 1f; i++)
            {
                surface += Vector3Int.up;
            }
            int top = surface.y + 1;
            Queue.Clear();
            Seen.Clear();
            Queue.Add(start);
            Seen.Add(start);
            float reach = radius / grid.VoxelSize;
            float left = units;
            while (left > 1e-3f && Queue.Count > 0 && Seen.Count < MaxLooked)
            {
                // The lowest open sample found so far, nearest the start among equals.
                int best = 0;
                for (int i = 1; i < Queue.Count; i++)
                {
                    Vector3Int a = Queue[i], b = Queue[best];
                    if (a.y < b.y || (a.y == b.y && Flat(a - start) < Flat(b - start)))
                    {
                        best = i;
                    }
                }
                Vector3Int sample = Queue[best];
                Queue.RemoveAt(best);
                float there = world.GetWater(sample);
                float put = Mathf.Min(1f - there, left);
                if (put > 0f)
                {
                    world.SetWater(sample, there + put);
                    left -= put;
                    changed?.Add(sample);
                }
                foreach (Vector3Int step in Around)
                {
                    Vector3Int next = sample + step;
                    // Through the water already there, and no higher than a sample over its surface (GL324): on a slope
                    // the lowest samples in reach are at its downhill rim, where the water stood as a wall; the flow
                    // carries it on from a low puddle instead.
                    if (next.y <= top && Flat(next - start) <= reach * reach && Seen.Add(next) && IsOpen(world, next, iso))
                    {
                        Queue.Add(next);
                    }
                }
            }
            return units - Mathf.CeilToInt(left - 1e-3f);
        }

        private static readonly Vector3Int[] Around =
        {
            Vector3Int.down, Vector3Int.right, Vector3Int.left, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1), Vector3Int.up,
        };

        // Most samples one pour looks through (a pour into a lake would otherwise search all of it in reach).
        private const int MaxLooked = 4000;

        private static readonly List<Vector3Int> Queue = new List<Vector3Int>();
        private static readonly HashSet<Vector3Int> Seen = new HashSet<Vector3Int>();

        private static int Flat(Vector3Int d)
        {
            return d.x * d.x + d.z * d.z;
        }

        private static bool IsOpen(World world, Vector3Int sample, float iso)
        {
            float? density = world.GetDensity(sample);
            return density.HasValue && density.Value < iso;
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
