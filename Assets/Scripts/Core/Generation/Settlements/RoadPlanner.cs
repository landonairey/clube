using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Lays the road network over the survey's cells (GL596): the cheapest way by slope and water, class by class. Paved
    /// roads first, from the capital out to the cities and its nearest towns; then dirt cart paths from the towns and
    /// villages; then footpaths from the outposts. Each new road runs from its settlement to the nearest point of the
    /// network laid so far, so the whole is one connected network. Camps get no paths. A settlement with no way by land to
    /// the network (on an island, GL643) needs no road: it starts a network of its own land, which later settlements of
    /// that land join, and is reached by water.
    /// </summary>
    /// <remarks>
    /// A step steeper than the class allows is not forbidden but costs so much that a road only takes it where there is
    /// no gentler way; such stretches are noted (<see cref="PlannedRoad.SteepLength"/>). The sea is never crossed; a river
    /// or lake cell is, at the class's bridge cost, and each crossing is noted as a bridge (the spans themselves are
    /// <see cref="RouteBridges"/>' work when the road is built, GL613).
    /// </remarks>
    internal sealed class RoadPlanner
    {
        private readonly PlanContext context;
        // GL753: metres over the sea a survey cell's ground may stand for a dock's road to come down to it.
        private const float LowShore = 6f;

        private readonly RoadClass[] network;
        private readonly int[] roadAt;
        private readonly int[] settlementAt;
        private readonly int[] networkAt;
        private int networks = 1;

        public RoadPlanner(PlanContext context)
        {
            this.context = context;
            int count = context.Size * context.Size;
            network = new RoadClass[count];
            roadAt = new int[count];
            settlementAt = new int[count];
            networkAt = new int[count];
            for (int i = 0; i < count; i++)
            {
                roadAt[i] = -1;
                settlementAt[i] = -1;
            }
        }

        public void Plan(WorldPlan plan)
        {
            var onRoads = plan.Settlements.FindAll(settlement => settlement.Type.MinimumRoad != RoadClass.None);
            if (onRoads.Count == 0)
            {
                return;
            }
            foreach (PlannedSettlement settlement in onRoads)
            {
                settlementAt[CellOf(settlement)] = settlement.Index;
            }
            // The hub: the capital, or without one the largest settlement.
            PlannedSettlement hub = plan.Capital ?? onRoads[0];
            foreach (PlannedSettlement settlement in onRoads)
            {
                if (plan.Capital == null && settlement.Type.Parcels > hub.Type.Parcels)
                {
                    hub = settlement;
                }
            }
            network[CellOf(hub)] = hub.Type.MinimumRoad;

            // The towns nearest the hub get a paved road too, so it is well connected.
            var promoted = onRoads.FindAll(s => s != hub && s.Type.MinimumRoad == RoadClass.DirtCartPath && s.Type.Shops >= 3);
            promoted.Sort((a, b) => Distance(a, hub).CompareTo(Distance(b, hub)));
            if (promoted.Count > context.Rules.PavedTownsFromCapital)
            {
                promoted.RemoveRange(context.Rules.PavedTownsFromCapital, promoted.Count - context.Rules.PavedTownsFromCapital);
            }

            for (RoadClass roadClass = RoadClass.PavedCartPath; roadClass >= RoadClass.Footpath; roadClass--)
            {
                var turn = onRoads.FindAll(s => s != hub
                    && (s.Type.MinimumRoad == roadClass || (roadClass == RoadClass.PavedCartPath && promoted.Contains(s))));
                turn.RemoveAll(s => roadClass != RoadClass.PavedCartPath && promoted.Contains(s));
                // Nearest the hub first, so the network grows outwards and far places join the roads of nearer ones.
                turn.Sort((a, b) =>
                {
                    int byDistance = Distance(a, hub).CompareTo(Distance(b, hub));
                    return byDistance != 0 ? byDistance : a.Index.CompareTo(b.Index);
                });
                foreach (PlannedSettlement settlement in turn)
                {
                    Connect(plan, settlement, roadClass);
                }
            }
            ExitRoads(plan, onRoads);
            foreach (PlannedSettlement settlement in onRoads)
            {
                settlement.BestRoad = network[CellOf(settlement)];
                settlement.RoadNetwork = networkAt[CellOf(settlement)];
            }
        }

        private void Connect(WorldPlan plan, PlannedSettlement settlement, RoadClass roadClass)
        {
            int start = CellOf(settlement);
            if (network[start] >= roadClass)
            {
                // An earlier road already runs through it.
                return;
            }
            RoadClassDefinition definition = context.Rules.Road(roadClass);
            int goal = context.Search(start, (from, to, metres) => StepCost(definition, from, to, metres), cell => network[cell] >= roadClass);
            bool overSea = false;
            if (goal < 0 && context.Rules.SeaBridgeMaxSpan > 0f && settlement.Type.MinimumRoad >= context.Rules.SeaBridgeMinimumRoad)
            {
                // GL651: no way by land. A settlement large enough is bridged to the roads where the sea between is narrow enough.
                goal = context.Search(start, (from, to, metres) => SeaStepCost(definition, from, to, metres), cell => network[cell] >= roadClass);
                if (goal >= 0)
                {
                    float span = LongestSeaRun(context.PathTo(goal));
                    if (span > context.Rules.SeaBridgeMaxSpan)
                    {
                        plan.Notes.Add($"{settlement.Name}: the narrowest sea to the road network is about {span:0} m, too wide to bridge.");
                        goal = -1;
                    }
                    else
                    {
                        overSea = true;
                        plan.Notes.Add($"{settlement.Name}: joined to the road network by a bridge over about {span:0} m of sea.");
                    }
                }
            }
            if (goal < 0)
            {
                // GL643: it needs no road. Its land's later settlements join it, as a network of their own.
                network[start] = roadClass;
                networkAt[start] = networks++;
                plan.Notes.Add($"{settlement.Name}: no way by land to the road network; it is reached by water.");
                return;
            }
            var road = new PlannedRoad
            {
                Index = plan.Roads.Count, Class = roadClass, From = settlement.Index, To = settlementAt[goal], Joins = roadAt[goal], CrossesSea = overSea,
            };
            Lay(plan, road, context.PathTo(goal), definition, networkAt[goal]);
        }

        /// <summary>
        /// GL753: the road down to the dock of each port its vessels call at: the class its <see cref="DockDesign"/> row names,
        /// from the settlement to the low shore by its harbour (any shore there when none is low), at the price of any road.
        /// <see cref="DockLandings"/> brings its end to the real water.
        /// </summary>
        public void DockRoads(WorldPlan plan)
        {
            foreach (PlannedSettlement port in plan.Settlements)
            {
                DockDesign design = DockDesign.For(plan.Rules.Docks, port.Type);
                Vector2? harbour = plan.HarbourOf(port.Index);
                if (design == null || harbour == null)
                {
                    continue;
                }
                RoadClassDefinition definition = context.Rules.Road(design.Road);
                float reach = context.Rules.CoastReach + context.CellSize;
                float sea = context.Survey.Layout != null ? context.Survey.Layout.SeaLevel : float.MinValue;
                bool Shore(int cell) => context.Cells[cell].Coast && !context.IsSea(cell) && Vector2.Distance(context.Centre(cell), harbour.Value) <= reach;
                float Step(int from, int to, float metres) => StepCost(definition, from, to, metres);
                int goal = context.Search(CellOf(port), Step, cell => Shore(cell) && context.Cells[cell].Height - sea <= LowShore);
                goal = goal >= 0 ? goal : context.Search(CellOf(port), Step, Shore);
                if (goal < 0)
                {
                    plan.Notes.Add($"{port.Name}: no way by land to its harbour, so no dock.");
                    continue;
                }
                var road = new PlannedRoad { Index = plan.Roads.Count, Class = design.Road, From = port.Index, ToDock = true, DockHarbour = harbour.Value };
                Lay(plan, road, context.PathTo(goal), definition, networkAt[CellOf(port)]);
                if (road.Points.Count == 1)
                {
                    // The settlement stands on the shore: a first step towards the harbour, for the landing to finish.
                    road.Points.Add(road.Points[0] + (harbour.Value - road.Points[0]).normalized * context.CellSize * 0.25f);
                    road.Length = context.CellSize * 0.25f;
                }
            }
        }

        // GL725, 2026-11-24b item 4: trade roads off the map over land. Each side a road can leave by (land, neither sea, the
        // void nor a mountain ridge) gets one by chance, likelier the more such sides the map has, and a further one at half
        // the chance of the one before. Each starts at a settlement on the cart roads picked at random, leaning to the larger
        // and the nearer that side, and runs to the nearest edge of the free ground there, never over mountain ground.
        private void ExitRoads(WorldPlan plan, List<PlannedSettlement> onRoads)
        {
            MapLayout layout = context.Survey.Layout;
            WorldPlanRules rules = context.Rules;
            if (!rules.ExitRoads || layout == null)
            {
                return;
            }
            var open = new List<MapSide>();
            for (var side = MapSide.West; side <= MapSide.North; side++)
            {
                MapBoundaryType boundary = layout.Boundary(side);
                if (boundary == null || boundary.IsOcean || boundary.ToProfile().Empty)
                {
                    continue;
                }
                if (boundary.HasRange)
                {
                    plan.Notes.Add($"No trade road leaves the map to the {side.ToString().ToLowerInvariant()}: a mountain ridge lies past that edge.");
                    continue;
                }
                open.Add(side);
            }
            float first = Mathf.Clamp01(rules.LandRouteChance + rules.LandRouteChancePerSide * open.Count);
            foreach (MapSide side in open)
            {
                var used = new HashSet<PlannedSettlement>();
                int laid = 0;
                float chance = first;
                for (int n = 0; n < rules.LandRoutesPerSide; n++, chance *= 0.5f)
                {
                    if (context.Hash01((int)side, n, 7301) >= chance || !ExitRoad(plan, onRoads, side, n, used))
                    {
                        break;
                    }
                    laid++;
                }
                if (laid == 0)
                {
                    plan.Notes.Add($"No trade road leaves the map to the {side.ToString().ToLowerInvariant()} on this map.");
                }
            }
        }

        // One trade road off the map by a side, from a settlement picked by size and nearness (tried in turn until one has a
        // way there that a cart can take and that crosses no mountain). False when none has.
        private bool ExitRoad(WorldPlan plan, List<PlannedSettlement> onRoads, MapSide side, int n, HashSet<PlannedSettlement> used)
        {
            WorldPlanRules rules = context.Rules;
            var candidates = new List<PlannedSettlement>();
            var weights = new List<float>();
            foreach (PlannedSettlement settlement in onRoads)
            {
                if (used.Contains(settlement) || settlement.Type.Hostile || network[CellOf(settlement)] < RoadClass.DirtCartPath
                    || context.IsMountain(CellOf(settlement)))
                {
                    continue;
                }
                float near = 1f / (1f + DistanceToSide(settlement.Position, side) / Mathf.Max(1f, rules.LandRouteReach * context.Scale));
                candidates.Add(settlement);
                weights.Add(Mathf.Max(0.5f, settlement.Type.Parcels) * near * near);
            }
            string sideName = side.ToString().ToLowerInvariant();
            for (int attempt = 0; attempt < 3 && candidates.Count > 0; attempt++)
            {
                float total = 0f;
                foreach (float weight in weights)
                {
                    total += weight;
                }
                float roll = context.Hash01((int)side, n, 7400 + attempt) * total;
                int pick = candidates.Count - 1;
                for (int i = 0; i < candidates.Count; i++)
                {
                    roll -= weights[i];
                    if (roll < 0f)
                    {
                        pick = i;
                        break;
                    }
                }
                PlannedSettlement start = candidates[pick];
                candidates.RemoveAt(pick);
                weights.RemoveAt(pick);
                used.Add(start);
                RoadClass roadClass = network[CellOf(start)] >= RoadClass.PavedCartPath ? RoadClass.PavedCartPath : RoadClass.DirtCartPath;
                RoadClassDefinition definition = rules.Road(roadClass);
                int goal = context.Search(CellOf(start),
                    (from, to, metres) => context.IsMountain(to) ? -1f : StepCost(definition, from, to, metres), cell => IsEdge(cell, side));
                if (goal < 0 || goal == CellOf(start))
                {
                    plan.Notes.Add($"No trade road leaves the map to the {sideName} from {start.Name}: no way by land round the mountains to that edge.");
                    continue;
                }
                var road = new PlannedRoad { Index = plan.Roads.Count, Class = roadClass, From = start.Index, LeavesMap = true, ExitSide = side };
                // Only where a cart can go: a way out with long steep stretches gets no trade road.
                bool laid = Lay(plan, road, context.PathTo(goal), definition, networkAt[CellOf(start)],
                    made => made.SteepLength <= made.Length * rules.ExitRoadSteepShare && made.SteepestSlope < definition.MaxSlope * 4f);
                plan.Notes.Add(laid
                    ? $"A trade road leaves the map to the {sideName} from {start.Name} ({road.Length:0} m to the edge)."
                    : $"No trade road leaves the map to the {sideName} from {start.Name}: the way is too steep for carts.");
                if (laid)
                {
                    return true;
                }
            }
            return false;
        }

        // Metres from a point to a side's edge of the map.
        private float DistanceToSide(Vector2 point, MapSide side)
        {
            Rect area = context.Survey.Area;
            return side switch
            {
                MapSide.West => point.x - area.xMin,
                MapSide.East => area.xMax - point.x,
                MapSide.South => point.y - area.yMin,
                _ => area.yMax - point.y,
            };
        }

        // The side a point of the map lies towards, as the sides share the land.
        private MapSide SideOf(Vector2 point)
        {
            return context.SideOf(point);
        }

        // A dry cell of the free ground on the side wanted, beside a cell past the edge that is land too.
        private bool IsEdge(int cell, MapSide side)
        {
            if (context.Cells[cell].Outside || IsWater(cell) || SideOf(context.Centre(cell)) != side)
            {
                return false;
            }
            int x = cell % context.Size, z = cell / context.Size;
            for (int k = 0; k < 4; k++)
            {
                int nx = x + (k == 0 ? -1 : k == 1 ? 1 : 0), nz = z + (k == 2 ? -1 : k == 3 ? 1 : 0);
                if (nx < 0 || nz < 0 || nx >= context.Size || nz >= context.Size)
                {
                    return true;
                }
                int next = nx + nz * context.Size;
                if (context.Cells[next].Outside && !context.IsSea(next) && SideOf(context.Centre(next)) == side)
                {
                    return true;
                }
            }
            return false;
        }

        // Lays a planned road along a path of cells: its points, length, steep stretches and bridges, and the cells it takes onto the network.
        // False, and nothing laid, when <paramref name="accept"/> turns the finished road down.
        private bool Lay(WorldPlan plan, PlannedRoad road, List<int> cells, RoadClassDefinition definition, int networkId, Func<PlannedRoad, bool> accept = null)
        {
            RoadClass roadClass = road.Class;
            float waterStart = -1f;
            Vector2 waterFrom = default;
            for (int i = 0; i < cells.Count; i++)
            {
                int cell = cells[i];
                Vector2 point = context.Centre(cell);
                road.Points.Add(point);
                if (i > 0)
                {
                    float metres = Vector2.Distance(point, road.Points[i - 1]);
                    float slope = Mathf.Abs(context.Cells[cell].Height - context.Cells[cells[i - 1]].Height) / metres;
                    road.SteepestSlope = Mathf.Max(road.SteepestSlope, IsWater(cell) || IsWater(cells[i - 1]) ? 0f : slope);
                    if (slope > definition.MaxSlope && !IsWater(cell) && !IsWater(cells[i - 1]))
                    {
                        road.SteepLength += metres;
                    }
                    road.Length += metres;
                }
                // A run of water cells is one bridge.
                if (IsWater(cell) && waterStart < 0f)
                {
                    waterStart = road.Length;
                    waterFrom = point;
                }
                else if (!IsWater(cell) && waterStart >= 0f)
                {
                    road.Bridges.Add(new RoadBridge((waterFrom + road.Points[i - 1]) * 0.5f, road.Length - waterStart));
                    waterStart = -1f;
                }
            }
            if (accept != null && !accept(road))
            {
                return false;
            }
            foreach (int cell in cells)
            {
                if (network[cell] < roadClass)
                {
                    network[cell] = roadClass;
                    roadAt[cell] = road.Index;
                    networkAt[cell] = networkId;
                }
            }
            plan.Roads.Add(road);
            return true;
        }

        private float StepCost(RoadClassDefinition definition, int from, int to, float metres)
        {
            if (context.IsSea(to) || context.Cells[to].Outside)
            {
                return -1f;
            }
            // A water cell is bridged at a price over what its slope costs: a river coming down a mountain is no way up it.
            float stretch = definition.StretchCost(metres, Mathf.Abs(context.Cells[to].Height - context.Cells[from].Height) / metres);
            if (!IsWater(to))
            {
                return stretch;
            }
            // GL737: its deck at the bridge price by the metre (dearer over still water), and its ends at the class's own
            // price where the bridge starts.
            RoadBuildCosts costs = context.Rules.BuildCosts;
            float deck = metres * costs.Relative(RoadWork.Bridge) * (context.Cells[to].River ? 1f : costs.LakePrice);
            return stretch + deck + (IsWater(from) ? 0f : definition.BridgeCost);
        }

        // GL651: as StepCost, but the sea may be crossed, dearly by the metre.
        private float SeaStepCost(RoadClassDefinition definition, int from, int to, float metres)
        {
            if (context.Cells[to].Outside)
            {
                return -1f;
            }
            if (context.IsSea(to) || context.IsSea(from))
            {
                return metres * (context.IsSea(to) ? context.Rules.SeaBridgeCost : 1f);
            }
            return StepCost(definition, from, to, metres);
        }

        // The longest unbroken stretch of sea on a path of cells, in metres: from the last land cell's middle to the next one's, less a cell (the shores lie between the middles).
        private float LongestSeaRun(List<int> cells)
        {
            float longest = 0f, run = 0f;
            bool inSea = false;
            for (int i = 1; i < cells.Count; i++)
            {
                bool sea = context.IsSea(cells[i]);
                if (sea || inSea)
                {
                    run += Vector2.Distance(context.Centre(cells[i]), context.Centre(cells[i - 1]));
                }
                if (!sea && inSea)
                {
                    longest = Mathf.Max(longest, run - context.CellSize);
                    run = 0f;
                }
                inSea = sea;
            }
            return longest;
        }

        private bool IsWater(int cell)
        {
            return context.IsSea(cell) || context.Cells[cell].River || context.Cells[cell].FreshWater > 0.25f;
        }

        private int CellOf(PlannedSettlement settlement)
        {
            return settlement.Cell.x + settlement.Cell.y * context.Size;
        }

        private static float Distance(PlannedSettlement a, PlannedSettlement b)
        {
            return (a.Position - b.Position).sqrMagnitude;
        }
    }
}
