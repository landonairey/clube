using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Where a thieves' camp does its thieving (GL617), read off the <see cref="WorldPlan"/>: the stretch of cart path
    /// its band lies in wait by ("hijack cart paths, trading paths"), the settlement it steals from ("they also steal
    /// from towns"), and the place on a water route its boat waits at ("intercept sea vessels and/or river boat
    /// vessels"). The same plan gives the same haunts, so a band is where it should be whenever the player comes by.
    /// </summary>
    public static class ThiefHaunts
    {
        /// <summary>An ambush is laid at least this far from any honest settlement, where the road allows.</summary>
        public const float OutOfSight = 140f;

        /// <summary>
        /// The place on a cart path (dirt or paved: where the carts run) nearest a camp, within <paramref name="range"/>
        /// metres of it and out of sight of the settlements where the road has such a place; false with no cart path in range.
        /// </summary>
        public static bool TryAmbush(WorldPlan plan, PlannedSettlement camp, float range, out Vector2 spot, out int road)
        {
            spot = default;
            road = -1;
            float best = range, bestSeen = range;
            Vector2 seenSpot = default;
            int seenRoad = -1;
            foreach (PlannedRoad each in plan.Roads)
            {
                if (each.Class != RoadClass.DirtCartPath && each.Class != RoadClass.PavedCartPath)
                {
                    continue;
                }
                for (int i = 1; i < each.Points.Count; i++)
                {
                    Vector2 a = each.Points[i - 1], b = each.Points[i];
                    // Long legs of an unrefined road are walked in steps, so the nearest place on them is found too.
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 10f));
                    for (int s = 0; s <= steps; s++)
                    {
                        Vector2 point = Vector2.Lerp(a, b, s / (float)steps);
                        float distance = Vector2.Distance(point, camp.Position);
                        if (distance >= best && distance >= bestSeen)
                        {
                            continue;
                        }
                        if (InSight(plan, point))
                        {
                            if (distance < bestSeen)
                            {
                                bestSeen = distance;
                                seenSpot = point;
                                seenRoad = each.Index;
                            }
                        }
                        else if (distance < best)
                        {
                            best = distance;
                            spot = point;
                            road = each.Index;
                        }
                    }
                }
            }
            if (road < 0 && seenRoad >= 0)
            {
                spot = seenSpot;
                road = seenRoad;
            }
            return road >= 0;
        }

        /// <summary>The honest settlement with shops nearest a camp, within <paramref name="range"/> metres; null with none.</summary>
        public static PlannedSettlement Mark(WorldPlan plan, PlannedSettlement camp, float range)
        {
            PlannedSettlement mark = null;
            float best = range;
            foreach (PlannedSettlement settlement in plan.Settlements)
            {
                float distance = Vector2.Distance(settlement.Position, camp.Position);
                if (!settlement.Type.Hostile && settlement.Shops.Count > 0 && distance < best)
                {
                    best = distance;
                    mark = settlement;
                }
            }
            return mark;
        }

        /// <summary>
        /// The place on a water route nearest a camp that has water of its own (a harbour for the sea, a landing for a
        /// river), within <paramref name="range"/> metres of that water; false for a camp with none, or no route in range.
        /// </summary>
        public static bool TryIntercept(WorldPlan plan, PlannedSettlement camp, float cellSize, float range, out WaterRouteKind kind, out Vector2 cove, out Vector2 spot, out int route)
        {
            kind = WaterRouteKind.Sea;
            cove = spot = default;
            route = -1;
            float best = range;
            for (int pass = 0; pass < 2; pass++)
            {
                WaterRouteKind wanted = pass == 0 ? WaterRouteKind.Sea : WaterRouteKind.River;
                Vector2Int? cell = wanted == WaterRouteKind.Sea ? camp.Harbour : camp.Landing;
                if (!cell.HasValue)
                {
                    continue;
                }
                Vector2 water = plan.Area.min + new Vector2(cell.Value.x + 0.5f, cell.Value.y + 0.5f) * cellSize;
                for (int r = 0; r < plan.WaterRoutes.Count; r++)
                {
                    WaterRoute each = plan.WaterRoutes[r];
                    if (each.Kind != wanted)
                    {
                        continue;
                    }
                    // Not the route's ends: a vessel lying in a harbour is among its own people.
                    for (int i = 1; i < each.Points.Count - 1; i++)
                    {
                        float distance = Vector2.Distance(each.Points[i], water);
                        if (distance < best)
                        {
                            best = distance;
                            kind = wanted;
                            cove = water;
                            spot = each.Points[i];
                            route = r;
                        }
                    }
                }
            }
            return route >= 0;
        }

        private static bool InSight(WorldPlan plan, Vector2 point)
        {
            foreach (PlannedSettlement settlement in plan.Settlements)
            {
                if (!settlement.Type.Hostile && (settlement.Position - point).sqrMagnitude < OutOfSight * OutOfSight)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
