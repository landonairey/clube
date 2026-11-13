using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Lays the road network over the survey's cells (GL596): the cheapest way by slope and water, class by class. Paved
    /// roads first, from the capital out to the cities and its nearest towns; then dirt cart paths from the towns and
    /// villages; then footpaths from the outposts. Each new road runs from its settlement to the nearest point of the
    /// network laid so far, so the whole is one connected network. Camps get no paths.
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

        public RoadPlanner(PlanContext context)
        {
            this.context = context;
            int count = context.Size * context.Size;
            network = new RoadClass[count];
            roadAt = new int[count];
            settlementAt = new int[count];
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
            foreach (PlannedSettlement settlement in onRoads)
            {
                settlement.BestRoad = network[CellOf(settlement)];
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
            if (goal < 0)
            {
                plan.Notes.Add($"{settlement.Name}: no way by land to the road network.");
                return;
            }
            List<int> cells = context.PathTo(goal);
            var road = new PlannedRoad
            {
                Index = plan.Roads.Count, Class = roadClass, From = settlement.Index, To = settlementAt[goal], Joins = roadAt[goal],
            };
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
                if (network[cell] < roadClass)
                {
                    network[cell] = roadClass;
                    roadAt[cell] = road.Index;
                }
            }
            plan.Roads.Add(road);
        }

        private float StepCost(RoadClassDefinition definition, int from, int to, float metres)
        {
            if (context.IsSea(to) || context.Cells[to].Outside)
            {
                return -1f;
            }
            if (IsWater(to))
            {
                return metres + definition.BridgeCost;
            }
            return definition.StretchCost(metres, Mathf.Abs(context.Cells[to].Height - context.Cells[from].Height) / metres);
        }

        private bool IsWater(int cell)
        {
            return context.Cells[cell].River || context.Cells[cell].FreshWater > 0.25f;
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
