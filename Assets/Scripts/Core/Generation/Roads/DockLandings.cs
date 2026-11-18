using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// GL753: brings the roads down to the docks to the real water. The plan routes them over 40 m survey cells, so each
    /// ends somewhere near the shore; before the roads are refined, its end moves to a few metres short of the sea, at the
    /// nearest place the land comes down low to it (a sea cliff is taken only where there is nothing else). A road that
    /// finds no shore is taken off the plan, and its settlement has no dock.
    /// </summary>
    public static class DockLandings
    {
        /// <summary>Metres short of the water a dock's road ends: the pier's root stands in them.</summary>
        public const float Inland = 4f;

        /// <summary>The farthest, in metres, a road's planned end looks for the shore.</summary>
        public const float Reach = 120f;

        /// <summary>Metres the land at a landing may stand over the shore before the place counts as a cliff.</summary>
        public const float LowRise = 2.5f;

        /// <summary>The share of a dock's road, and the metres beyond it, that may be steeper than its class.</summary>
        public const float SteepShare = 0.05f, SteepAllowance = 5f;

        // What a metre of cliff over that costs a landing, in metres farther along the shore.
        private const float CliffPrice = 12f;

        private const int Rays = 32;
        private const float Step = 1f;

        // Metres of sea past the shore that show it is the sea and not a wet patch.
        private const float SeaBeyond = 4f;

        /// <param name="waterAt">What water a point is in; null on a dry map, where the roads stay as planned.</param>
        /// <param name="closedAt">True where no road may run; null for nowhere.</param>
        public static void Land(WorldPlan plan, Func<float, float, float> heightAt, Func<float, float, RoadWater> waterAt, Func<float, float, bool> closedAt = null)
        {
            if (waterAt == null)
            {
                return;
            }
            for (int i = plan.Roads.Count - 1; i >= 0; i--)
            {
                PlannedRoad road = plan.Roads[i];
                if (!road.ToDock || road.Refined || road.Points.Count < 2)
                {
                    continue;
                }
                if (TryLanding(road.Points[road.Points.Count - 1], heightAt, waterAt, closedAt, out Vector2 landing))
                {
                    Finish(road, landing, waterAt);
                    continue;
                }
                plan.Notes.Add($"{plan.Settlements[road.From].Name}: its road finds no shore near its harbour, so no dock.");
                Remove(plan, i);
            }
        }

        /// <summary>
        /// Takes off the plan, once the roads are graded, each road down to a dock that breaks the road rules: one that ends
        /// too high over the sea for the pier's ramp to reach (a sea cliff), or runs steeper than its class for more than
        /// <see cref="SteepShare"/> of its length and <see cref="SteepAllowance"/> metres. Its settlement then has no dock,
        /// and its vessels lie off the shore: better than a pier nobody can walk to.
        /// </summary>
        public static void Prune(WorldPlan plan, Func<float, float, float> heightAt, float seaLevel)
        {
            for (int i = plan.Roads.Count - 1; i >= 0; i--)
            {
                PlannedRoad road = plan.Roads[i];
                if (!road.ToDock || road.Points.Count < 2)
                {
                    continue;
                }
                DockDesign design = DockDesign.For(plan.Rules.Docks, plan.Settlements[road.From].Type);
                Vector2 end = road.Points[road.Points.Count - 1];
                float reachable = (design != null ? design.DeckRise : 0f) + Docks.MostRampRise;
                bool tooHigh = heightAt(end.x, end.y) - seaLevel > reachable;
                bool tooSteep = road.SteepLength > road.Length * SteepShare + SteepAllowance;
                if (!tooHigh && !tooSteep)
                {
                    continue;
                }
                plan.Notes.Add($"{plan.Settlements[road.From].Name}: {(tooHigh ? "its shore is a cliff" : "the way down to its shore is too steep")}, so no dock.");
                Remove(plan, i);
            }
        }

        private static void Remove(WorldPlan plan, int index)
        {
            plan.Roads.RemoveAt(index);
            for (int k = index; k < plan.Roads.Count; k++)
            {
                plan.Roads[k].Index = k;
            }
        }

        /// <summary>The best place near <paramref name="from"/> for a dock's road to end; false when no sea shore is within reach.</summary>
        public static bool TryLanding(Vector2 from, Func<float, float, float> heightAt, Func<float, float, RoadWater> waterAt, Func<float, float, bool> closedAt,
            out Vector2 landing)
        {
            landing = from;
            float best = float.MaxValue;
            bool startsWet = waterAt(from.x, from.y) == RoadWater.Sea;
            for (int r = 0; r < Rays; r++)
            {
                float angle = r * Mathf.PI * 2f / Rays;
                var way = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                if (!TryShore(from, way, startsWet, waterAt, out float shore))
                {
                    continue;
                }
                // Land lies behind the shore on a ray from dry ground, and ahead of it on one from the shallows.
                Vector2 shorePoint = from + way * shore;
                Vector2 spot = shorePoint + way * (startsWet ? Inland : -Inland);
                if (waterAt(spot.x, spot.y) != RoadWater.Dry || (closedAt != null && closedAt(spot.x, spot.y)))
                {
                    continue;
                }
                float rise = heightAt(spot.x, spot.y) - heightAt(shorePoint.x, shorePoint.y);
                float cost = shore + Mathf.Max(0f, rise - LowRise) * CliffPrice;
                if (cost < best)
                {
                    best = cost;
                    landing = spot;
                }
            }
            return best < float.MaxValue;
        }

        // Metres along a ray to where dry land meets the sea. A river or lake in the way ends the ray: a road does not wade.
        private static bool TryShore(Vector2 from, Vector2 way, bool startsWet, Func<float, float, RoadWater> waterAt, out float shore)
        {
            for (shore = Step; shore <= Reach; shore += Step)
            {
                Vector2 at = from + way * shore;
                RoadWater water = waterAt(at.x, at.y);
                if (water == RoadWater.River || water == RoadWater.Lake)
                {
                    return false;
                }
                if (startsWet)
                {
                    if (water == RoadWater.Dry)
                    {
                        return true;
                    }
                }
                else if (water == RoadWater.Sea)
                {
                    Vector2 past = at + way * SeaBeyond;
                    return waterAt(past.x, past.y) == RoadWater.Sea;
                }
            }
            return false;
        }

        // The road's last planned point gives way to the landing when it stands in the sea, else the landing follows it.
        private static void Finish(PlannedRoad road, Vector2 landing, Func<float, float, RoadWater> waterAt)
        {
            int last = road.Points.Count - 1;
            Vector2 end = road.Points[last];
            if (waterAt(end.x, end.y) == RoadWater.Sea || Vector2.Distance(end, landing) < Step)
            {
                road.Length += Vector2.Distance(road.Points[last - 1], landing) - Vector2.Distance(road.Points[last - 1], end);
                road.Points[last] = landing;
                return;
            }
            road.Length += Vector2.Distance(end, landing);
            road.Points.Add(landing);
        }
    }
}
