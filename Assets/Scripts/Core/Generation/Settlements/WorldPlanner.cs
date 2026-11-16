using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Makes the <see cref="WorldPlan"/> of a bounded map from its resource survey (GL593–GL597), step by step: sites
    /// (<see cref="SettlementPlacer"/>), shops (<see cref="SpecialtyAssigner"/>), roads (<see cref="RoadPlanner"/>), water
    /// routes and named vessels (<see cref="WaterRoutePlanner"/>). The same survey, seed and rules give the same plan.
    /// </summary>
    public static class WorldPlanner
    {
        /// <summary>Surveys a terrain's bounded map and plans its world with the terrain's seed.</summary>
        public static WorldPlan Build(TerrainSettings terrain, WorldPlanRules rules = null)
        {
            return Build(ResourceSurvey.Build(terrain), terrain.Seed, rules);
        }

        /// <summary>Plans the world of a surveyed map.</summary>
        /// <param name="rules">The tables to plan from; null for the defaults.</param>
        public static WorldPlan Build(ResourceSurvey survey, int seed, WorldPlanRules rules = null)
        {
            if (survey == null)
            {
                throw new ArgumentNullException(nameof(survey));
            }
            rules ??= new WorldPlanRules();
            var plan = new WorldPlan { Seed = seed, Area = survey.Area, Rules = rules };
            var watch = Stopwatch.StartNew();
            var context = new PlanContext(survey, seed, rules);
            long prepared = watch.ElapsedMilliseconds;
            new SettlementPlacer(context).Place(plan);
            long placed = watch.ElapsedMilliseconds;
            new SpecialtyAssigner(context).Assign(plan);
            long assigned = watch.ElapsedMilliseconds;
            new RoadPlanner(context).Plan(plan);
            long roads = watch.ElapsedMilliseconds;
            new WaterRoutePlanner(context).Plan(plan);
            ConnectCapital(plan, context);
            plan.Timings = $"reach {prepared} ms, sites {placed - prepared} ms, shops {assigned - placed} ms, roads {roads - assigned} ms, "
                + $"water {watch.ElapsedMilliseconds - roads} ms";
            return plan;
        }

        // The capital reaches every resource type: what its own area lacks, the nearest settlement on its roads has.
        private static void ConnectCapital(WorldPlan plan, PlanContext context)
        {
            PlannedSettlement capital = plan.Capital;
            if (capital == null)
            {
                return;
            }
            int cell = capital.Cell.x + capital.Cell.y * context.Size;
            foreach (int r in context.TypeResources)
            {
                if (context.Reach.Richness(r, context.AreaCells)[cell] > 0f)
                {
                    continue;
                }
                PlannedSettlement source = null;
                foreach (PlannedSettlement other in plan.Settlements)
                {
                    bool has = context.Reach.Richness(r, context.ReachOf(other.Type))[other.Cell.x + other.Cell.y * context.Size] > 0f;
                    if (has && other.BestRoad != RoadClass.None
                        && (source == null || (other.Position - capital.Position).sqrMagnitude < (source.Position - capital.Position).sqrMagnitude))
                    {
                        source = other;
                    }
                }
                string name = context.Resources[r].Name;
                if (source != null)
                {
                    capital.Reasons.Add($"Its {name} comes by road from {source.Name}.");
                }
                else
                {
                    plan.Notes.Add($"The capital has no {name} in its area, and no settlement on the roads has any.");
                }
            }
            var chains = new List<string>();
            foreach (PlannedShop shop in capital.Shops)
            {
                if (!chains.Contains(shop.Specialty.Chain))
                {
                    chains.Add(shop.Specialty.Chain);
                }
            }
            capital.Reasons.Add($"Holds every economy type: {string.Join(", ", chains)}.");
        }
    }
}
