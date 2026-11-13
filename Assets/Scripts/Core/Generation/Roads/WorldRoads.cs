using System;
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
        /// <param name="heightAt">The ground's height at (x, z).</param>
        /// <param name="water">The map's water, or an uncreated one for a dry map.</param>
        /// <returns>Seconds it took.</returns>
        public static float Finish(WorldPlan plan, Func<float, float, float> heightAt, WaterMap water)
        {
            float started = Time.realtimeSinceStartup;
            var refiner = new RoadRefiner(plan.Area, heightAt, WaterOf(water));
            refiner.Refine(plan);
            plan.Signposts = SignpostPlanner.Plan(plan);
            float seconds = Time.realtimeSinceStartup - started;
            plan.Timings += $", roads on the ground {seconds * 1000f:0} ms ({refiner.Refined} refined, {refiner.Kept} kept, {plan.Signposts.Count} signposts)";
            return seconds;
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
