using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Picks the site of every settlement from the resource survey (GL595): each type's count, on dry ground of any
    /// land that is level or can be terraced (GL642), kept apart, scored by what lies nearby as its type's <see cref="SiteRule"/> asks. The largest go first, so
    /// the capital and the cities get the best ground. The capital stands on the largest land; every other piece of land with
    /// resources gets a settlement of its own first (GL643), then the rest go wherever the resources are richest. Each site keeps its reasons, and gets its name (GL597).
    /// </summary>
    internal sealed class SettlementPlacer
    {
        private readonly PlanContext context;
        private readonly WorldPlanRules rules;
        private readonly int size;
        private readonly bool[] dry;
        private readonly double[] dryTable;
        private readonly double[] flatTable;
        private readonly int[] served;
        private readonly int freshWater;
        private readonly int wood;
        private readonly HashSet<string> names = new HashSet<string>();
        private int turn;

        public SettlementPlacer(PlanContext context)
        {
            this.context = context;
            rules = context.Rules;
            size = context.Size;
            served = new int[context.Resources.Count];
            freshWater = context.Reach.IndexOf("fresh-water");
            wood = context.Reach.IndexOf("wood");

            int margin = Mathf.Max(1, Mathf.RoundToInt(rules.EdgeMargin * context.Scale / context.CellSize));
            dry = new bool[size * size];
            for (int i = 0; i < dry.Length; i++)
            {
                SurveyCell cell = context.Cells[i];
                int x = i % size, z = i / size;
                dry[i] = context.Land[i] != 0 && cell.Land >= 0.9f && cell.Sea <= 0.01f && cell.FreshWater <= 0.1f
                    && cell.Flatness >= rules.TerraceFlatness && x >= margin && z >= margin && x < size - margin && z < size - margin;
            }
            dryTable = Table(i => dry[i] ? 1f : 0f);
            flatTable = Table(i => context.Cells[i].Flatness);
        }

        public void Place(WorldPlan plan)
        {
            var order = new List<SettlementType>(rules.Types);
            // Stable: thieves last (they hide from the others), an ocean type first, then by size.
            order.Sort((a, b) =>
            {
                int byHostile = a.Hostile.CompareTo(b.Hostile);
                int byOcean = b.NeedsOcean.CompareTo(a.NeedsOcean);
                int bySize = b.Parcels.CompareTo(a.Parcels);
                return byHostile != 0 ? byHostile : byOcean != 0 ? byOcean : bySize != 0 ? bySize
                    : rules.Types.IndexOf(a).CompareTo(rules.Types.IndexOf(b));
            });
            var left = new Dictionary<SettlementType, int>();
            foreach (SettlementType type in order)
            {
                left[type] = type.Count;
            }
            bool islandsServed = false;
            foreach (SettlementType type in order)
            {
                if (!type.NeedsOcean && !islandsServed)
                {
                    // After the capital, before the rest: each other land with resources gets its own settlement.
                    islandsServed = true;
                    ServeIslands(plan, order, left);
                }
                for (int n = type.Count - left[type]; n < type.Count; n++)
                {
                    if (!PlaceOne(plan, type, type.NeedsOcean ? context.MainLandId : 0))
                    {
                        plan.Notes.Add($"No site left for {type.Name} {n + 1} of {type.Count}.");
                        break;
                    }
                }
            }
        }

        // GL643: a settlement for every piece of land, other than the largest, that has resources within reach: the largest
        // type its ground has room for (never one that must have a paved road), by the sea when it can be, so vessels reach it.
        private void ServeIslands(WorldPlan plan, List<SettlementType> order, Dictionary<SettlementType, int> left)
        {
            var lands = new List<int>();
            for (int land = 1; land < context.LandCells.Length; land++)
            {
                if (land != context.MainLandId && context.LandCells[land] >= rules.IslandCells && HasResources(land))
                {
                    lands.Add(land);
                }
            }
            lands.Sort((a, b) => context.LandCells[b] != context.LandCells[a] ? context.LandCells[b].CompareTo(context.LandCells[a]) : a.CompareTo(b));
            foreach (int land in lands)
            {
                float parcels = context.LandCells[land] * context.CellSize * context.CellSize / (rules.ParcelSide * rules.ParcelSide);
                foreach (SettlementType type in order)
                {
                    if (type.Hostile || type.NeedsOcean || type.MinimumRoad == RoadClass.PavedCartPath || left[type] <= 0
                        || type.Parcels * rules.IslandLandPerParcel > parcels)
                    {
                        continue;
                    }
                    if (PlaceOne(plan, type, land, true) || PlaceOne(plan, type, land))
                    {
                        left[type]--;
                        PlannedSettlement placed = plan.Settlements[plan.Settlements.Count - 1];
                        placed.Reasons.Add("The settlement of its island: every land with resources has one.");
                        break;
                    }
                }
            }
        }

        private bool HasResources(int land)
        {
            foreach (int r in context.TypeResources)
            {
                if (r == freshWater)
                {
                    continue;
                }
                float[] richness = context.Reach.Richness(r, context.NearCells);
                for (int i = 0; i < richness.Length; i++)
                {
                    if (context.Land[i] == land && richness[i] > 0f)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <param name="land">The piece of land it must stand on, or 0 for any.</param>
        /// <param name="byTheSea">True to take only sites with the sea within reach, whatever the type.</param>
        private bool PlaceOne(WorldPlan plan, SettlementType type, int land = 0, bool byTheSea = false)
        {
            int footprint = Mathf.Max(0, Mathf.CeilToInt(
                (Mathf.Sqrt(type.Parcels) * rules.ParcelSide * 0.5f - context.CellSize * 0.5f) / context.CellSize));
            int coastReach = Mathf.Max(1, Mathf.RoundToInt(rules.CoastReach / context.CellSize));
            int reach = context.ReachOf(type);
            float flatness = rules.FlatnessFor(type);
            int target = -1;
            if (type.Site == SiteRule.OneResource && context.TypeResources.Count > 0)
            {
                // Each new one goes for the next resource type in turn (fresh water is no trade), so every one gets its sites.
                do
                {
                    target = context.TypeResources[turn++ % context.TypeResources.Count];
                }
                while (target == freshWater && context.TypeResources.Count > 1);
            }

            // Keep the spacing if the map allows; give way step by step on a crowded one, then on level ground too.
            for (int attempt = 0; attempt < 14; attempt++)
            {
                float relax = Mathf.Pow(0.8f, Mathf.Min(attempt, 11));
                bool anyGround = attempt >= 12;
                int best = -1;
                float bestScore = float.NegativeInfinity;
                for (int i = 0; i < dry.Length; i++)
                {
                    if (land != 0 && context.Land[i] != land)
                    {
                        continue;
                    }
                    if (!(anyGround ? context.Land[i] != 0 && context.Cells[i].Land >= 0.6f && context.Cells[i].FreshWater <= 0.3f : dry[i]))
                    {
                        continue;
                    }
                    int x = i % size, z = i / size;
                    // How steep its ground may be goes by the road it must be reached by (GL642): a cart can't climb to a terrace a walker can.
                    if (!anyGround && context.Cells[i].Flatness < flatness)
                    {
                        continue;
                    }
                    if (!anyGround && footprint > 0
                        && (Mean(dryTable, x, z, footprint) < 0.8f || Mean(flatTable, x, z, footprint) < flatness * 0.9f))
                    {
                        continue;
                    }
                    if (!FarEnough(plan, type, context.Centre(i), relax))
                    {
                        continue;
                    }
                    if ((byTheSea || (type.NeedsOcean && attempt < 13)) && context.NearestCell(new Vector2Int(x, z), coastReach, context.IsSea) < 0)
                    {
                        continue;
                    }
                    float score = Score(plan, type, i, reach, target) + 0.05f * context.Hash01(i, plan.Settlements.Count, 17);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = i;
                    }
                }
                if (best >= 0)
                {
                    Add(plan, type, best, reach, target, coastReach);
                    if (attempt > 0)
                    {
                        plan.Notes.Add(anyGround
                            ? $"{plan.Settlements[plan.Settlements.Count - 1].Name}: no level dry site was left, so it stands on the best ground there was."
                            : $"{plan.Settlements[plan.Settlements.Count - 1].Name}: spacing eased to {relax * 100f:F0}% to fit the map.");
                    }
                    return true;
                }
            }
            return false;
        }

        private bool FarEnough(WorldPlan plan, SettlementType type, Vector2 position, float relax)
        {
            foreach (PlannedSettlement other in plan.Settlements)
            {
                float apart = (type.Spacing + other.Type.Spacing) * 0.5f * context.Scale * relax;
                if ((other.Position - position).sqrMagnitude < apart * apart)
                {
                    return false;
                }
            }
            return true;
        }

        private float Score(WorldPlan plan, SettlementType type, int cell, int reach, int target)
        {
            int x = cell % size, z = cell / size;
            float flat = Mean(flatTable, x, z, 1);
            float water = freshWater >= 0 ? context.Reach.Richness(freshWater, context.NearCells)[cell] : 0f;
            switch (type.Site)
            {
                case SiteRule.OneResource:
                    return (target >= 0 ? context.Reach.Richness(target, reach)[cell] : 0f) + 0.2f * flat + 0.15f * water;
                case SiteRule.SeveralResources:
                {
                    float first = 0f, second = 0f, third = 0f;
                    foreach (int r in context.TypeResources)
                    {
                        if (r == freshWater)
                        {
                            continue;
                        }
                        // A resource many settlements already live off counts for less: the next one looks elsewhere.
                        float value = context.Reach.Richness(r, reach)[cell] / (1f + 0.5f * served[r]);
                        if (value > first)
                        {
                            third = second;
                            second = first;
                            first = value;
                        }
                        else if (value > second)
                        {
                            third = second;
                            second = value;
                        }
                        else if (value > third)
                        {
                            third = value;
                        }
                    }
                    return first + second + third + 0.4f * water + 0.2f * flat;
                }
                case SiteRule.AllResources:
                {
                    float sum = 0f;
                    foreach (int r in context.TypeResources)
                    {
                        float value = context.Reach.Richness(r, reach)[cell];
                        sum += value + (value > 0f ? 0.5f : 0f);
                    }
                    return sum + 0.3f * flat;
                }
                default:
                {
                    // Hidden: rough or wooded ground, near enough to settlements to rob and far enough not to be seen.
                    float nearest = float.MaxValue;
                    foreach (PlannedSettlement other in plan.Settlements)
                    {
                        if (!other.Type.Hostile)
                        {
                            nearest = Mathf.Min(nearest, Vector2.Distance(other.Position, context.Centre(cell)));
                        }
                    }
                    float band = nearest < rules.ThievesNear * context.Scale ? -1f : nearest > rules.ThievesFar * context.Scale ? -0.5f : 0.5f;
                    float cover = wood >= 0 ? context.Reach.Richness(wood, context.NearCells)[cell] : 0f;
                    return 0.5f * cover + 0.5f * (1f - Mean(flatTable, x, z, 2)) + band;
                }
            }
        }

        private void Add(WorldPlan plan, SettlementType type, int cell, int reach, int target, int coastReach)
        {
            var at = new Vector2Int(cell % size, cell / size);
            var settlement = new PlannedSettlement
            {
                Index = plan.Settlements.Count, Type = type, Cell = at, Land = context.Land[cell], Position = context.Centre(cell), Ground = context.Cells[cell].Height,
                Name = SettlementNames.Generate(context.Seed, at, type.Name, names),
                Population = type.PopulationMin
                    + Mathf.FloorToInt(context.Hash01(cell, 3, 5) * (type.PopulationMax - type.PopulationMin + 1)),
            };
            int harbour = context.NearestCell(at, coastReach, context.IsSea);
            if (harbour >= 0)
            {
                settlement.Harbour = new Vector2Int(harbour % size, harbour / size);
            }
            int riverReach = Mathf.Max(1, Mathf.RoundToInt(rules.RiverReach / context.CellSize));
            int landing = context.NearestCell(at, riverReach, i => context.Cells[i].River);
            if (landing >= 0)
            {
                settlement.Landing = new Vector2Int(landing % size, landing / size);
            }

            float metres = reach * context.CellSize;
            var has = new List<string>();
            var lacks = new List<string>();
            foreach (int r in context.TypeResources)
            {
                float value = context.Reach.Richness(r, reach)[cell];
                if (value > 0.4f)
                {
                    served[r]++;
                }
                (value > 0f ? has : lacks).Add(value > 0f ? $"{context.Resources[r].Name} {value * 100f:F0}%" : context.Resources[r].Name);
            }
            switch (type.Site)
            {
                case SiteRule.OneResource:
                    if (target >= 0)
                    {
                        settlement.Reasons.Add(
                            $"Sited for {context.Resources[target].Name}: {context.Reach.Richness(target, reach)[cell] * 100f:F0}% of the map's richest spot, within {metres:F0} m.");
                    }
                    break;
                case SiteRule.Hidden:
                {
                    PlannedSettlement prey = null;
                    foreach (PlannedSettlement other in plan.Settlements)
                    {
                        if (!other.Type.Hostile
                            && (prey == null || (other.Position - settlement.Position).sqrMagnitude < (prey.Position - settlement.Position).sqrMagnitude))
                        {
                            prey = other;
                        }
                    }
                    settlement.Reasons.Add(prey == null
                        ? "Hidden in rough or wooded ground."
                        : $"Hidden in rough or wooded ground, {Vector2.Distance(prey.Position, settlement.Position):F0} m from {prey.Name}.");
                    break;
                }
                default:
                    if (type.NeedsOcean)
                    {
                        settlement.Reasons.Add(settlement.IsCoastal ? "On the ocean's edge." : "No level site by the ocean was found.");
                    }
                    break;
            }
            if (type.Site != SiteRule.Hidden)
            {
                var line = new StringBuilder($"Within {metres:F0} m (share of the map's richest spot): {string.Join(", ", has)}.");
                if (lacks.Count > 0 && type.Site == SiteRule.AllResources)
                {
                    line.Append($" None of: {string.Join(", ", lacks)}.");
                }
                settlement.Reasons.Add(line.ToString());
            }
            if (settlement.IsOnRiver)
            {
                settlement.Reasons.Add("On a river.");
            }
            plan.Settlements.Add(settlement);
        }

        private double[] Table(System.Func<int, float> value)
        {
            var table = new double[(size + 1) * (size + 1)];
            for (int z = 0; z < size; z++)
            {
                double row = 0.0;
                for (int x = 0; x < size; x++)
                {
                    row += value(x + z * size);
                    table[(x + 1) + (z + 1) * (size + 1)] = table[(x + 1) + z * (size + 1)] + row;
                }
            }
            return table;
        }

        // The mean of a table's values in the cells within a radius of (x, z).
        private float Mean(double[] table, int x, int z, int radius)
        {
            int x0 = Mathf.Max(0, x - radius), z0 = Mathf.Max(0, z - radius);
            int x1 = Mathf.Min(size, x + radius + 1), z1 = Mathf.Min(size, z + radius + 1);
            int w = size + 1;
            double sum = table[x1 + z1 * w] - table[x0 + z1 * w] - table[x1 + z0 * w] + table[x0 + z0 * w];
            return (float)(sum / ((x1 - x0) * (z1 - z0)));
        }
    }
}
