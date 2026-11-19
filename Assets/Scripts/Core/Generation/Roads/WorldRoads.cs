using System;
using System.Collections;
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
            return Finish(plan, map.Height, map.Water, (x, z) => !layout.WalksFreely(x, z), HeightsOf(map));
        }

        /// <param name="heightAt">The ground's height at (x, z).</param>
        /// <param name="water">The map's water, or an uncreated one for a dry map.</param>
        /// <param name="closedAt">True where no road may run; null for nowhere.</param>
        /// <param name="heightsAt">The ground's heights at many points at once, where that is faster; null to ask one by one.</param>
        /// <returns>Seconds it took.</returns>
        public static float Finish(WorldPlan plan, Func<float, float, float> heightAt, WaterMap water, Func<float, float, bool> closedAt = null,
            Func<Vector2[], float[]> heightsAt = null)
        {
            float seconds = 0f;
            SlicedWork.RunToEnd(FinishSteps(plan, heightAt, water, closedAt, heightsAt, taken => seconds = taken));
            return seconds;
        }

        /// <summary>A bounded map's roads finished a road at a time (GL762, <see cref="SlicedWork"/>).</summary>
        public static IEnumerator FinishSteps(WorldPlan plan, BoundedMapGenerator map)
        {
            MapLayout layout = map.Layout;
            return FinishSteps(plan, map.Height, map.Water, (x, z) => !layout.WalksFreely(x, z), HeightsOf(map), null);
        }

        private static IEnumerator FinishSteps(WorldPlan plan, Func<float, float, float> heightAt, WaterMap water, Func<float, float, bool> closedAt,
            Func<Vector2[], float[]> heightsAt, Action<float> done)
        {
            // The clock runs only while this works, not over the frames between its steps.
            var watch = System.Diagnostics.Stopwatch.StartNew();
            // GL753: the roads down to the docks end on the real shore, so they are refined and graded to it.
            DockLandings.Land(plan, heightAt, water.IsCreated ? WaterOf(water) : null, closedAt);
            var refiner = new RoadRefiner(plan.Area, heightAt, water.IsCreated ? WaterOf(water) : null, closedAt) { HeightsAt = heightsAt };
            for (int i = 0; i < plan.Roads.Count; i++)
            {
                IEnumerator legs = refiner.RefineSteps(plan, plan.Roads[i], null);
                while (legs.MoveNext())
                {
                    watch.Stop();
                    yield return RefinedShare * i / plan.Roads.Count;
                    watch.Start();
                }
            }
            Func<float, float, RoadWater> waterAt = water.IsCreated ? WaterOf(water) : null;
            // GL737: graded into the ground where it is over its limit, as the search priced it.
            IEnumerator grading = RoadGrading.GradeSteps(plan, heightAt, heightsAt, waterAt);
            while (grading.MoveNext())
            {
                watch.Stop();
                yield return null;
                watch.Start();
            }
            // GL642: where a cart path is still over its limit for a long stretch, a tunnel under it, where that costs less.
            int tunnels = 0;
            IEnumerator boring = RoadTunnels.BoreSteps(plan, heightAt, heightsAt, waterAt, null, bored => tunnels += bored);
            while (boring.MoveNext())
            {
                watch.Stop();
                yield return null;
                watch.Start();
            }
            if (tunnels > 0)
            {
                // The tunnelled roads graded again on their new line (a bore's floor is its own grade).
                IEnumerator again = RoadGrading.GradeSteps(plan, heightAt, heightsAt, waterAt);
                while (again.MoveNext())
                {
                    watch.Stop();
                    yield return null;
                    watch.Start();
                }
            }
            if (water.IsCreated)
            {
                DockLandings.Prune(plan, heightAt, water.SeaLevel);
            }
            plan.Signposts = SignpostPlanner.Plan(plan);
            float seconds = (float)watch.Elapsed.TotalSeconds;
            plan.Timings += $", roads on the ground {seconds * 1000f:0} ms ({refiner.Refined} refined, {refiner.Kept} kept, {tunnels} tunnels, {plan.Signposts.Count} signposts)";
            done?.Invoke(seconds);
        }

        // The share of the finishing that refining the roads is, for the load screen's progress.
        private const float RefinedShare = 0.8f;

        /// <summary>
        /// A finished road's line on the map's ground, metre by metre, as the game lays it: in a tunnel (GL642) it runs on
        /// the tunnel's floor, and on a graded stretch (GL737) on the planned cut-and-fill bed.
        /// </summary>
        public static RoadLine LineOf(PlannedRoad road, BoundedMapGenerator map)
        {
            return new RoadLine(road.Points, RoadGrading.HeightAt(road, map.Height), 1f, RoadGrading.HeightsAt(road, HeightsOf(map)));
        }

        /// <summary>A map's heights at many points in one job: asked one by one they are most of the time its roads take.</summary>
        public static Func<Vector2[], float[]> HeightsOf(HeightfieldGenerator map)
        {
            return points => Heights(map, points);
        }

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
