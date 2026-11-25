using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Plans the water transit (GL596): sea routes for larger vessels between the hub's harbour (the capital's) and every
    /// other settlement by the sea, and river routes for small boats from each river settlement to the nearest other one
    /// along the water. Then gives the named vessels their routes (GL597).
    /// </summary>
    internal sealed class WaterRoutePlanner
    {
        private readonly PlanContext context;

        public WaterRoutePlanner(PlanContext context)
        {
            this.context = context;
        }

        public void Plan(WorldPlan plan)
        {
            // Thieves keep no harbours; boats of theirs are GL617's.
            var ports = plan.Settlements.FindAll(settlement => !settlement.Type.Hostile);
            SeaRoutes(plan, ports.FindAll(settlement => settlement.IsCoastal));
            RiverRoutes(plan, ports.FindAll(settlement => settlement.IsOnRiver));
            NameVessels(plan);
        }

        private void SeaRoutes(WorldPlan plan, List<PlannedSettlement> coastal)
        {
            PlannedSettlement hub = plan.Capital != null && plan.Capital.IsCoastal ? plan.Capital : null;
            if (hub == null)
            {
                // No capital by the sea: the largest coastal settlement is the home port.
                foreach (PlannedSettlement settlement in coastal)
                {
                    hub = hub == null || settlement.Type.Parcels > hub.Type.Parcels ? settlement : hub;
                }
            }
            if (hub == null)
            {
                plan.Notes.Add("No settlement stands by the sea: no sea routes.");
                return;
            }
            int home = Cell(hub.Harbour.Value);
            context.Search(home, (from, to, metres) => context.IsSea(to) ? metres : -1f, null);
            foreach (PlannedSettlement settlement in coastal)
            {
                int harbour = Cell(settlement.Harbour.Value);
                // Camps have no jetty of their own, but for one on another land than the home port (GL643: the sea is its
                // only way); a neighbour's harbour serves a route only once.
                if (settlement == hub || (settlement.Type.MinimumRoad == RoadClass.None && settlement.Land == hub.Land) || !context.Reached(harbour)
                    || harbour == home)
                {
                    continue;
                }
                plan.WaterRoutes.Add(Route(WaterRouteKind.Sea, hub, settlement, context.PathTo(harbour)));
            }
            bool none = !plan.WaterRoutes.Exists(route => route.Kind == WaterRouteKind.Sea);
            if (none || context.Rules.OpenSeaRoutes)
            {
                // The hub's route out to the open sea, for ports beyond the map (GL725: its vessels sail on past the edge).
                // With nowhere on the map to sail to it runs to the farthest edge; beside routes on the map, to the nearest.
                // 2026-11-24b item 4: an edge on a sea side when one is reached, so every map with a sea side has one.
                List<MapSide> seaSides = SeaSides();
                int edge = OpenSeaEdge(home, none, seaSides, null);
                if (edge < 0)
                {
                    edge = OpenSeaEdge(home, none, null, null);
                }
                if (edge >= 0 && edge != home)
                {
                    plan.WaterRoutes.Add(Route(WaterRouteKind.Sea, hub, null, context.PathTo(edge)));
                    MapSide first = context.SideOf(context.Centre(edge));
                    ExtraSeaRoutes(plan, coastal, hub, seaSides, first);
                }
            }
        }

        // 2026-11-24b item 4: "likelihood for more depends on how many edges are ocean". Each other sea side gets a route out
        // by chance, from a port picked leaning to the larger and the nearer that side, to the nearest edge there it reaches.
        private void ExtraSeaRoutes(WorldPlan plan, List<PlannedSettlement> coastal, PlannedSettlement hub, List<MapSide> seaSides, MapSide first)
        {
            float chance = Mathf.Clamp01(context.Rules.ExtraSeaRouteChance * seaSides.Count);
            Rect area = context.Survey.Area;
            foreach (MapSide side in seaSides)
            {
                if (side == first || context.Hash01((int)side, 0, 7501) >= chance)
                {
                    continue;
                }
                var ports = coastal.FindAll(settlement => settlement == hub || settlement.Type.MinimumRoad != RoadClass.None);
                var weights = new List<float>();
                float total = 0f;
                foreach (PlannedSettlement port in ports)
                {
                    float toSide = side switch
                    {
                        MapSide.West => port.Position.x - area.xMin,
                        MapSide.East => area.xMax - port.Position.x,
                        MapSide.South => port.Position.y - area.yMin,
                        _ => area.yMax - port.Position.y,
                    };
                    float near = 1f / (1f + toSide / 1500f);
                    weights.Add(Mathf.Max(0.5f, port.Type.Parcels) * near * near);
                    total += weights[weights.Count - 1];
                }
                float roll = context.Hash01((int)side, 1, 7502) * total;
                PlannedSettlement from = null;
                for (int i = 0; i < ports.Count && from == null; i++)
                {
                    roll -= weights[i];
                    from = roll < 0f || i == ports.Count - 1 ? ports[i] : null;
                }
                if (from == null)
                {
                    continue;
                }
                int harbour = Cell(from.Harbour.Value);
                context.Search(harbour, (a, to, metres) => context.IsSea(to) ? metres : -1f, null);
                int edge = OpenSeaEdge(harbour, false, null, side);
                if (edge >= 0 && edge != harbour)
                {
                    plan.WaterRoutes.Add(Route(WaterRouteKind.Sea, from, null, context.PathTo(edge)));
                    plan.Notes.Add($"A sea route also leaves the map to the {side.ToString().ToLowerInvariant()}, from {from.Name}.");
                }
            }
        }

        // The sides of the map with the sea past them; empty when the map has no layout (the labs).
        private List<MapSide> SeaSides()
        {
            var sides = new List<MapSide>();
            MapLayout layout = context.Survey.Layout;
            for (var side = MapSide.West; layout != null && side <= MapSide.North; side++)
            {
                if (layout.Boundary(side) != null && layout.Boundary(side).IsOcean)
                {
                    sides.Add(side);
                }
            }
            return sides;
        }

        // The edge cell of the last search reached nearest (or, with farthest, farthest from) a cell: on one of the sides
        // given (null for any), or on the one side given; -1 when none is reached.
        private int OpenSeaEdge(int home, bool farthest, List<MapSide> sides, MapSide? only)
        {
            int edge = -1;
            float best = farthest ? -1f : float.MaxValue;
            for (int i = 0; i < context.Cells.Length; i++)
            {
                int x = i % context.Size, z = i / context.Size;
                bool onEdge = x == 0 || z == 0 || x == context.Size - 1 || z == context.Size - 1;
                if (!onEdge || !context.Reached(i))
                {
                    continue;
                }
                MapSide side = context.SideOf(context.Centre(i));
                if ((sides != null && !sides.Contains(side)) || (only.HasValue && side != only.Value))
                {
                    continue;
                }
                float distance = (context.Centre(i) - context.Centre(home)).sqrMagnitude;
                if (farthest ? distance > best : distance < best)
                {
                    best = distance;
                    edge = i;
                }
            }
            return edge;
        }

        private void RiverRoutes(WorldPlan plan, List<PlannedSettlement> onRivers)
        {
            var landings = new Dictionary<int, PlannedSettlement>();
            foreach (PlannedSettlement settlement in onRivers)
            {
                int landing = Cell(settlement.Landing.Value);
                if (!landings.ContainsKey(landing))
                {
                    landings[landing] = settlement;
                }
            }
            var linked = new HashSet<long>();
            foreach (PlannedSettlement settlement in onRivers)
            {
                int landing = Cell(settlement.Landing.Value);
                if (landings[landing] != settlement)
                {
                    continue;
                }
                int goal = context.Search(landing, (from, to, metres) => IsRiver(to) ? metres : -1f, cell => landings.ContainsKey(cell));
                if (goal < 0)
                {
                    continue;
                }
                PlannedSettlement other = landings[goal];
                long pair = ((long)Mathf.Min(settlement.Index, other.Index) << 32) | (uint)Mathf.Max(settlement.Index, other.Index);
                if (linked.Add(pair))
                {
                    plan.WaterRoutes.Add(Route(WaterRouteKind.River, settlement, other, context.PathTo(goal)));
                }
            }
        }

        // A named vessel sails the sea route to the settlement with the most of its ore nearby, which mines it.
        private void NameVessels(WorldPlan plan)
        {
            foreach (NamedVessel vessel in context.Rules.Vessels)
            {
                var ores = new List<int>();
                for (int r = 0; r < context.Resources.Count; r++)
                {
                    string id = context.Resources[r].Id;
                    if (id.StartsWith("ore:") && SurveyResource.Matches(id.Substring(4), vessel.OreWords) && context.Reach.Totals[r] > 0f)
                    {
                        ores.Add(r);
                    }
                }
                if (ores.Count == 0)
                {
                    Reserve(plan, vessel, $"No {vessel.Cargo} on this map yet: the name is kept for the first route that serves it.");
                    continue;
                }
                WaterRoute best = null;
                PlannedSettlement bestPort = null;
                float bestOre = 0f;
                foreach (WaterRoute route in plan.WaterRoutes)
                {
                    if (route.Kind != vessel.Kind || route.Vessel != null)
                    {
                        continue;
                    }
                    foreach (int end in new[] { route.To, route.From })
                    {
                        if (end < 0)
                        {
                            continue;
                        }
                        PlannedSettlement port = plan.Settlements[end];
                        float ore = 0f;
                        foreach (int r in ores)
                        {
                            ore += context.Reach.Richness(r, context.ReachOf(port.Type))[port.Cell.x + port.Cell.y * context.Size];
                        }
                        if (ore > bestOre)
                        {
                            bestOre = ore;
                            best = route;
                            bestPort = port;
                        }
                    }
                }
                if (best == null)
                {
                    Reserve(plan, vessel, $"No settlement on a {vessel.Kind.ToString().ToLowerInvariant()} route has {vessel.Cargo} nearby on this map.");
                    continue;
                }
                best.Vessel = vessel.Name;
                best.Cargo = vessel.Cargo;
                SpecialtyAssigner.AddExtra(context, bestPort, "mining", $"Mining Post (ore): the {vessel.Name} loads its {vessel.Cargo} here.");
                bestPort.Reasons.Add($"Home of the {vessel.Cargo} the {vessel.Name} carries by sea.");
            }
        }

        private static void Reserve(WorldPlan plan, NamedVessel vessel, string note)
        {
            plan.ReservedVessels.Add(new NamedVessel
            {
                Name = vessel.Name, Kind = vessel.Kind, Cargo = vessel.Cargo, OreWords = vessel.OreWords, Class = vessel.Class, Note = note,
            });
        }

        private WaterRoute Route(WaterRouteKind kind, PlannedSettlement from, PlannedSettlement to, List<int> cells)
        {
            var route = new WaterRoute { Kind = kind, From = from.Index, To = to != null ? to.Index : -1 };
            foreach (int cell in cells)
            {
                Vector2 point = context.Centre(cell);
                if (route.Points.Count > 0)
                {
                    route.Length += Vector2.Distance(point, route.Points[route.Points.Count - 1]);
                }
                route.Points.Add(point);
            }
            return route;
        }

        private bool IsRiver(int cell)
        {
            return !context.IsSea(cell) && (context.Cells[cell].River || context.Cells[cell].FreshWater > 0.25f);
        }

        private int Cell(Vector2Int cell)
        {
            return cell.x + cell.y * context.Size;
        }
    }
}
