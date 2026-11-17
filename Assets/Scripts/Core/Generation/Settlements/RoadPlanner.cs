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

        // GL725: a trade road off the map on each land side that is neither sea nor the void, from the settlement on the
        // roads farthest towards that side to the nearest edge of the free ground there. Its carts go on past the edge.
        private void ExitRoads(WorldPlan plan, List<PlannedSettlement> onRoads)
        {
            MapLayout layout = context.Survey.Layout;
            if (!context.Rules.ExitRoads || layout == null)
            {
                return;
            }
            for (var side = MapSide.West; side <= MapSide.North; side++)
            {
                MapBoundaryType boundary = layout.Boundary(side);
                if (boundary == null || boundary.IsOcean || boundary.ToProfile().Empty)
                {
                    continue;
                }
                Vector2 outward = Outward(side);
                PlannedSettlement start = null;
                foreach (PlannedSettlement settlement in onRoads)
                {
                    if (network[CellOf(settlement)] >= RoadClass.DirtCartPath
                        && (start == null || Vector2.Dot(settlement.Position, outward) > Vector2.Dot(start.Position, outward)))
                    {
                        start = settlement;
                    }
                }
                if (start == null)
                {
                    continue;
                }
                RoadClass roadClass = network[CellOf(start)] >= RoadClass.PavedCartPath ? RoadClass.PavedCartPath : RoadClass.DirtCartPath;
                RoadClassDefinition definition = context.Rules.Road(roadClass);
                MapSide wanted = side;
                int goal = context.Search(CellOf(start), (from, to, metres) => StepCost(definition, from, to, metres), cell => IsEdge(cell, wanted));
                if (goal < 0 || goal == CellOf(start))
                {
                    plan.Notes.Add($"No trade road leaves the map to the {side.ToString().ToLowerInvariant()}: no way by land from {start.Name} to that edge.");
                    continue;
                }
                var road = new PlannedRoad { Index = plan.Roads.Count, Class = roadClass, From = start.Index, LeavesMap = true, ExitSide = side };
                // Only where a cart can go: an edge up a mountain range has no gentle way out, and gets no trade road.
                bool laid = Lay(plan, road, context.PathTo(goal), definition, networkAt[CellOf(start)],
                    made => made.SteepLength <= made.Length * context.Rules.ExitRoadSteepShare && made.SteepestSlope < definition.MaxSlope * 4f);
                plan.Notes.Add(laid
                    ? $"A trade road leaves the map to the {side.ToString().ToLowerInvariant()} from {start.Name} ({road.Length:0} m to the edge)."
                    : $"No trade road leaves the map to the {side.ToString().ToLowerInvariant()}: the way from {start.Name} is too steep for carts.");
            }
        }

        // The way out of the map from a side's middle.
        private static Vector2 Outward(MapSide side)
        {
            return side switch
            {
                MapSide.West => Vector2.left,
                MapSide.East => Vector2.right,
                MapSide.South => Vector2.down,
                _ => Vector2.up,
            };
        }

        // The side a point of the map lies towards, as the sides share the land.
        private MapSide SideOf(Vector2 point)
        {
            Vector2 local = point - context.Survey.Area.center;
            if (Mathf.Abs(local.x) >= Mathf.Abs(local.y))
            {
                return local.x < 0f ? MapSide.West : MapSide.East;
            }
            return local.y < 0f ? MapSide.South : MapSide.North;
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
            return IsWater(to) ? stretch + definition.BridgeCost : stretch;
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
