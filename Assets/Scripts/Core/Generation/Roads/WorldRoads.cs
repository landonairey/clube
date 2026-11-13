using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Finishes a world plan's roads on the real ground (GL631, GL632): the plan is made from the survey's 40 m cells
    /// alone, so this second step needs the map's heights and water. Roads become smoothed routes
    /// (<see cref="RoadRefiner"/>) and the signposts are placed on them (<see cref="SignpostPlanner"/>).
    /// </summary>
    public static class WorldRoads
    {
        /// <summary>Finishes a bounded map's roads: on its ground, over its water, and only where the player walks freely.</summary>
        public static float Finish(WorldPlan plan, BoundedMapGenerator map)
        {
            MapLayout layout = map.Layout;
            return Finish(plan, map.Height, map.Water, (x, z) => !layout.WalksFreely(x, z), points => Heights(map, points));
        }

        /// <param name="heightAt">The ground's height at (x, z).</param>
        /// <param name="water">The map's water, or an uncreated one for a dry map.</param>
        /// <param name="closedAt">True where no road may run; null for nowhere.</param>
        /// <param name="heightsAt">The ground's heights at many points at once, where that is faster; null to ask one by one.</param>
        /// <returns>Seconds it took.</returns>
        public static float Finish(WorldPlan plan, Func<float, float, float> heightAt, WaterMap water, Func<float, float, bool> closedAt = null,
            Func<Vector2[], float[]> heightsAt = null)
        {
            float started = Time.realtimeSinceStartup;
            var refiner = new RoadRefiner(plan.Area, heightAt, water.IsCreated ? WaterOf(water) : null, closedAt) { HeightsAt = heightsAt };
            refiner.Refine(plan);
            plan.Signposts = SignpostPlanner.Plan(plan);
            float seconds = Time.realtimeSinceStartup - started;
            plan.Timings += $", roads on the ground {seconds * 1000f:0} ms ({refiner.Refined} refined, {refiner.Kept} kept, {plan.Signposts.Count} signposts)";
            return seconds;
        }

        // A corridor's heights in one job: asked one by one they are most of the time the roads take.
        private static float[] Heights(HeightfieldGenerator map, Vector2[] at)
        {
            var points = new NativeArray<float2>(at.Length, Allocator.TempJob);
            var heights = new NativeArray<float>(at.Length, Allocator.TempJob);
            try
            {
                for (int i = 0; i < at.Length; i++)
                {
                    points[i] = new float2(at[i].x, at[i].y);
                }
                map.SchedulePointHeights(points, heights).Complete();
                return heights.ToArray();
            }
            finally
            {
                points.Dispose();
                heights.Dispose();
            }
        }

        /// <summary>What a road finds at a point of a water map: a river, a lake, or the sea (still water at the sea's level).</summary>
        public static Func<float, float, RoadWater> WaterOf(WaterMap water)
        {
            if (!water.IsCreated)
            {
                return (x, z) => RoadWater.Dry;
            }
            return (x, z) =>
            {
                if (!water.TrySurfaceAt(new float2(x, z), out float surface, out int cell))
                {
                    return RoadWater.Dry;
                }
                if (water.Kind[cell] == (byte)WaterKind.River)
                {
                    return RoadWater.River;
                }
                return water.HasSea && surface <= water.SeaLevel + 0.05f ? RoadWater.Sea : RoadWater.Lake;
            };
        }
    }
}
