using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>What a road finds at a point of the map.</summary>
    public enum RoadWater : byte
    {
        Dry,
        /// <summary>Running water: bridged.</summary>
        River,
        /// <summary>Still water inland: bridged at a price, so a road goes round all but a narrow neck.</summary>
        Lake,
        /// <summary>Never crossed.</summary>
        Sea,
    }

    /// <summary>
    /// Turns the plan's roads from lines through 40 m survey cells into travelled routes on the real ground (GL631):
    /// each is searched again on an 8 m grid inside a corridor round its planned line, sixteen ways from every point,
    /// where a stretch costs what its class says for its slope (<see cref="RoadClassDefinition.StretchCost"/>, GL634)
    /// and a turn costs what its class says for turning (<see cref="RoadClassDefinition.TurnCost"/>). So a road runs
    /// along a hillside and doubles back up it in long legs (switchbacks) rather than straight up, crosses water at a
    /// narrow place, and is then rounded into curves. It still passes through every settlement it passed through, and a
    /// road that joined another ends on that road's new line, so the network stays one network.
    /// </summary>
    public sealed class RoadRefiner
    {
        /// <summary>Metres between the points a route is searched over.</summary>
        public const float CellSize = 8f;

        /// <summary>Metres a route may leave its planned line by.</summary>
        public const float Corridor = 64f;

        // Metres between the looks for water along a step: rivers are narrower than the grid.
        private const float WaterStep = 2f;

        // The planner's bridge cost is for one survey cell of river.
        private const float PlannedCellSize = 40f;

        // Rise over run is measured over this many of a finished road's points (about 12 m): a single step of ground is not a slope.
        private const int SlopeStride = 3;

        // The sixteen ways on from a grid point, in order round the compass, and "none yet" for a route's first step.
        private const int Ways = 16;
        private const int NoWay = Ways;
        private static readonly int[] StepX = { 1, 2, 1, 1, 0, -1, -1, -2, -1, -2, -1, -1, 0, 1, 1, 2 };
        private static readonly int[] StepZ = { 0, 1, 1, 2, 1, 2, 1, 1, 0, -1, -1, -2, -1, -2, -1, -1 };
        private static readonly float[] StepMetres = new float[Ways];

        // What turning from one way to another costs, as a share of the class's cost for a right angle (the square of
        // the angle over 90 degrees; nothing for the slightest bend); negative for a turn too sharp to make in one step.
        private static readonly float[] Turn = new float[Ways * Ways];

        static RoadRefiner()
        {
            for (int a = 0; a < Ways; a++)
            {
                var from = new Vector2(StepX[a], StepZ[a]);
                StepMetres[a] = from.magnitude * CellSize;
                for (int b = 0; b < Ways; b++)
                {
                    float angle = Vector2.Angle(from, new Vector2(StepX[b], StepZ[b]));
                    Turn[a * Ways + b] = angle < 28f ? 0f : angle > 95f ? -1f : angle / 90f * (angle / 90f);
                }
            }
        }

        private readonly Func<float, float, float> heightAt;
        private readonly Func<float, float, RoadWater> waterAt;
        private readonly Vector2 origin;
        private readonly int width, depth;
        private readonly float[] heights;
        private readonly byte[] water;
        // Per search: the grid points open to it get a slot each (allowed holds the search's stamp), and a state is a
        // slot and the way it was come to by.
        private readonly int[] allowed;
        private readonly int[] slotOf;
        private readonly List<int> slotNode = new List<int>();
        private float[] stateCost = new float[0];
        private int[] stateParent = new int[0];
        private float[] stepCache = new float[0];
        private int stamp;
        private float[] heapCost = new float[1024];
        private int[] heapState = new int[1024];
        private int heapCount;

        /// <param name="area">The map's bounds (x, z).</param>
        /// <param name="heightAt">The ground's height at (x, z).</param>
        /// <param name="waterAt">What water lies at (x, z); null for a dry map.</param>
        public RoadRefiner(Rect area, Func<float, float, float> heightAt, Func<float, float, RoadWater> waterAt)
        {
            this.heightAt = heightAt ?? throw new ArgumentNullException(nameof(heightAt));
            this.waterAt = waterAt ?? ((x, z) => RoadWater.Dry);
            origin = new Vector2(Mathf.Floor(area.xMin / CellSize) * CellSize, Mathf.Floor(area.yMin / CellSize) * CellSize);
            width = Mathf.CeilToInt((area.xMax - origin.x) / CellSize) + 1;
            depth = Mathf.CeilToInt((area.yMax - origin.y) / CellSize) + 1;
            int count = width * depth;
            heights = new float[count];
            water = new byte[count];
            allowed = new int[count];
            slotOf = new int[count];
            for (int i = 0; i < count; i++)
            {
                heights[i] = float.NaN;
                water[i] = byte.MaxValue;
            }
        }

        /// <summary>Roads refined, and those left on their planned line because no way was found.</summary>
        public int Refined { get; private set; }

        public int Kept { get; private set; }

        /// <summary>Refines every road of a plan, in the order they were laid (a road joins only earlier ones).</summary>
        public void Refine(WorldPlan plan)
        {
            foreach (PlannedRoad road in plan.Roads)
            {
                Refine(plan, road);
            }
        }

        /// <summary>Refines one road; false (and a note on the plan) when it had to be left on its planned line.</summary>
        public bool Refine(WorldPlan plan, PlannedRoad road)
        {
            if (road.Refined || road.Points.Count < 2)
            {
                return road.Refined;
            }
            RoadClassDefinition definition = plan.Rules.Road(road.Class);
            var planned = new List<Vector2>(road.Points);
            Vector2 end = EndOf(plan, road, planned[planned.Count - 1]);

            // The road is routed leg by leg between the settlements it passes through, so it still passes through them.
            var stops = new List<int> { 0 };
            for (int i = 1; i < planned.Count - 1; i++)
            {
                if (plan.Nearest(planned[i], 1f) != null)
                {
                    stops.Add(i);
                }
            }
            stops.Add(planned.Count - 1);

            var result = new List<Vector2>();
            bool whole = true;
            for (int s = 0; s < stops.Count - 1; s++)
            {
                var leg = planned.GetRange(stops[s], stops[s + 1] - stops[s] + 1);
                if (s == stops.Count - 2)
                {
                    leg[leg.Count - 1] = end;
                }
                List<Vector2> route = Route(leg, definition, 1f) ?? Route(leg, definition, 2.5f);
                whole &= route != null;
                List<Vector2> rounded = route != null ? Round(route) : leg;
                result.AddRange(s == 0 ? rounded : rounded.GetRange(1, rounded.Count - 1));
            }
            if (!whole)
            {
                Kept++;
                plan.Notes.Add($"Road {road.Index}: part of it found no way over the real ground and keeps its planned line.");
            }
            else
            {
                Refined++;
            }
            road.Points = result;
            road.Refined = true;
            Measure(road, definition);
            return whole;
        }

        // Where a road ends: at its settlement, or on the (new) line of the road it joined.
        private static Vector2 EndOf(WorldPlan plan, PlannedRoad road, Vector2 planned)
        {
            if (road.To >= 0 || road.Joins < 0 || road.Joins >= plan.Roads.Count)
            {
                return planned;
            }
            List<Vector2> joined = plan.Roads[road.Joins].Points;
            Vector2 best = planned;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < joined.Count - 1; i++)
            {
                Vector2 a = joined[i], ab = joined[i + 1] - joined[i];
                float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(planned - a, ab) / ab.sqrMagnitude) : 0f;
                Vector2 p = a + ab * t;
                float distance = (p - planned).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = p;
                }
            }
            return best;
        }

        private int NodeAt(Vector2 point)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt((point.x - origin.x) / CellSize), 0, width - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt((point.y - origin.y) / CellSize), 0, depth - 1);
            return x + z * width;
        }

        private Vector2 PointOf(int node)
        {
            return origin + new Vector2(node % width, node / width) * CellSize;
        }

        private float HeightOf(int node)
        {
            if (float.IsNaN(heights[node]))
            {
                Vector2 p = PointOf(node);
                heights[node] = heightAt(p.x, p.y);
            }
            return heights[node];
        }

        private RoadWater WaterOf(int node)
        {
            if (water[node] == byte.MaxValue)
            {
                Vector2 p = PointOf(node);
                water[node] = (byte)waterAt(p.x, p.y);
            }
            return (RoadWater)water[node];
        }

        private void OpenNode(int node)
        {
            if (allowed[node] != stamp)
            {
                allowed[node] = stamp;
                slotOf[node] = slotNode.Count;
                slotNode.Add(node);
            }
        }

        // Opens the grid points within reach of a stretch of the planned line to this search.
        private void Open(Vector2 a, Vector2 b, float reach)
        {
            Vector2 min = Vector2.Min(a, b) - Vector2.one * reach, max = Vector2.Max(a, b) + Vector2.one * reach;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((min.x - origin.x) / CellSize)), x1 = Mathf.Min(width - 1, Mathf.CeilToInt((max.x - origin.x) / CellSize));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((min.y - origin.y) / CellSize)), z1 = Mathf.Min(depth - 1, Mathf.CeilToInt((max.y - origin.y) / CellSize));
            Vector2 ab = b - a;
            float lengthSquared = Mathf.Max(1e-6f, ab.sqrMagnitude);
            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = origin + new Vector2(x, z) * CellSize;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSquared);
                    if ((a + ab * t - p).sqrMagnitude <= reach * reach)
                    {
                        OpenNode(x + z * width);
                    }
                }
            }
        }

        // The cheapest way along a leg of the planned line, as points from its first to its last; null when there is none.
        private List<Vector2> Route(List<Vector2> leg, RoadClassDefinition definition, float reachScale)
        {
            stamp++;
            slotNode.Clear();
            float reach = Corridor * reachScale;
            for (int i = 0; i < leg.Count - 1; i++)
            {
                Open(leg[i], leg[i + 1], reach);
            }
            int start = NodeAt(leg[0]), goal = NodeAt(leg[leg.Count - 1]);
            if (start == goal)
            {
                return new List<Vector2> { leg[0], leg[leg.Count - 1] };
            }
            OpenNode(start);
            OpenNode(goal);
            int states = slotNode.Count * (Ways + 1), steps = slotNode.Count * Ways;
            if (stateCost.Length < states)
            {
                stateCost = new float[states];
                stateParent = new int[states];
                stepCache = new float[steps];
            }
            for (int i = 0; i < states; i++)
            {
                stateCost[i] = float.MaxValue;
            }
            for (int i = 0; i < steps; i++)
            {
                stepCache[i] = float.NaN;
            }
            heapCount = 0;
            int first = slotOf[start] * (Ways + 1) + NoWay;
            stateCost[first] = 0f;
            stateParent[first] = -1;
            Push(0f, first);
            float perWetMetre = definition.BridgeCost / PlannedCellSize;
            int found = -1;
            while (heapCount > 0)
            {
                Pop(out float soFar, out int state);
                if (soFar > stateCost[state])
                {
                    continue;
                }
                int slot = state / (Ways + 1), came = state % (Ways + 1);
                int node = slotNode[slot];
                if (node == goal)
                {
                    found = state;
                    break;
                }
                int x = node % width, z = node / width;
                for (int way = 0; way < Ways; way++)
                {
                    float turn = came == NoWay ? 0f : Turn[came * Ways + way];
                    if (turn < 0f)
                    {
                        continue;
                    }
                    int nx = x + StepX[way], nz = z + StepZ[way];
                    if (nx < 0 || nz < 0 || nx >= width || nz >= depth)
                    {
                        continue;
                    }
                    int next = nx + nz * width;
                    if (allowed[next] != stamp)
                    {
                        continue;
                    }
                    float step = stepCache[slot * Ways + way];
                    if (float.IsNaN(step))
                    {
                        step = StepCost(definition, node, next, way, perWetMetre, next == goal);
                        stepCache[slot * Ways + way] = step;
                    }
                    if (step < 0f)
                    {
                        continue;
                    }
                    float total = soFar + step + turn * definition.TurnCost;
                    int nextState = slotOf[next] * (Ways + 1) + way;
                    if (total >= stateCost[nextState])
                    {
                        continue;
                    }
                    stateCost[nextState] = total;
                    stateParent[nextState] = state;
                    Push(total, nextState);
                }
            }
            if (found < 0)
            {
                return null;
            }
            var route = new List<Vector2>();
            for (int state = found; state >= 0; state = stateParent[state])
            {
                route.Add(PointOf(slotNode[state / (Ways + 1)]));
            }
            route.Reverse();
            // The ends are the leg's own, not the grid's: grid points too close to them would leave a kink there.
            route[0] = leg[0];
            route[route.Count - 1] = leg[leg.Count - 1];
            while (route.Count > 2 && (route[1] - route[0]).sqrMagnitude < CellSize * CellSize)
            {
                route.RemoveAt(1);
            }
            while (route.Count > 2 && (route[route.Count - 2] - route[route.Count - 1]).sqrMagnitude < CellSize * CellSize)
            {
                route.RemoveAt(route.Count - 2);
            }
            return route;
        }

        // What a step between two grid points costs; negative when it can't be taken (through the sea).
        private float StepCost(RoadClassDefinition definition, int from, int to, int way, float perWetMetre, bool toGoal)
        {
            Vector2 a = PointOf(from), b = PointOf(to);
            float metres = StepMetres[way];
            int looks = Mathf.CeilToInt(metres / WaterStep);
            int wet = 0;
            bool lake = false;
            for (int i = 1; i <= looks; i++)
            {
                RoadWater found;
                if (i == looks)
                {
                    found = WaterOf(to);
                }
                else
                {
                    Vector2 p = Vector2.Lerp(a, b, i / (float)looks);
                    found = waterAt(p.x, p.y);
                }
                if (found == RoadWater.Sea)
                {
                    if (!toGoal)
                    {
                        return -1f;
                    }
                    continue;
                }
                if (found != RoadWater.Dry)
                {
                    wet++;
                    lake |= found == RoadWater.Lake;
                }
            }
            if (wet > 0)
            {
                // A bridge: dearer by the metre of water, and by half a cell's worth for starting one at all.
                float wetMetres = metres * wet / looks;
                float starting = WaterOf(from) == RoadWater.Dry ? definition.BridgeCost * 0.5f : 0f;
                return metres + wetMetres * perWetMetre * (lake ? 3f : 1f) + starting;
            }
            return definition.StretchCost(metres, Mathf.Abs(HeightOf(to) - HeightOf(from)) / metres);
        }

        // Rounds a searched route into curves: its points eased towards their neighbours, then its corners cut once.
        private static List<Vector2> Round(List<Vector2> route)
        {
            var points = new List<Vector2>(route);
            for (int pass = 0; pass < 3; pass++)
            {
                Vector2 previous = points[0];
                for (int i = 1; i < points.Count - 1; i++)
                {
                    Vector2 here = points[i];
                    points[i] = (previous + here * 2f + points[i + 1]) * 0.25f;
                    previous = here;
                }
            }
            var rounded = new List<Vector2>(points.Count * 2) { points[0] };
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 a = points[i], b = points[i + 1];
                if (i > 0)
                {
                    rounded.Add(Vector2.Lerp(a, b, 0.25f));
                }
                if (i < points.Count - 2)
                {
                    rounded.Add(Vector2.Lerp(a, b, 0.75f));
                }
            }
            rounded.Add(points[points.Count - 1]);
            return rounded;
        }

        // The road's length, its slopes on the real ground and where it needs bridges.
        private void Measure(PlannedRoad road, RoadClassDefinition definition)
        {
            List<Vector2> points = road.Points;
            int count = points.Count;
            var ground = new float[count];
            var wet = new bool[count];
            var along = new float[count];
            for (int i = 0; i < count; i++)
            {
                ground[i] = heightAt(points[i].x, points[i].y);
                wet[i] = waterAt(points[i].x, points[i].y) != RoadWater.Dry;
                along[i] = i == 0 ? 0f : along[i - 1] + Vector2.Distance(points[i], points[i - 1]);
            }
            road.Length = along[count - 1];
            road.SteepestSlope = 0f;
            road.SteepLength = 0f;
            road.Bridges.Clear();
            int wetFrom = -1;
            for (int i = 0; i < count; i++)
            {
                if (wet[i] && wetFrom < 0)
                {
                    wetFrom = i;
                }
                if (wetFrom >= 0 && (!wet[i] || i == count - 1))
                {
                    int last = wet[i] ? i : i - 1;
                    int before = Mathf.Max(0, wetFrom - 1), after = Mathf.Min(count - 1, last + 1);
                    road.Bridges.Add(new RoadBridge((points[before] + points[after]) * 0.5f, along[after] - along[before]));
                    wetFrom = -1;
                }
                int ahead = Mathf.Min(count - 1, i + SlopeStride);
                if (ahead == i)
                {
                    continue;
                }
                bool overWater = false;
                for (int k = i; k <= ahead; k++)
                {
                    overWater |= wet[k];
                }
                if (overWater)
                {
                    continue;
                }
                float slope = Mathf.Abs(ground[ahead] - ground[i]) / Mathf.Max(0.5f, along[ahead] - along[i]);
                road.SteepestSlope = Mathf.Max(road.SteepestSlope, slope);
                if (slope > definition.MaxSlope)
                {
                    road.SteepLength += along[i + 1] - along[i];
                }
            }
        }

        private void Push(float value, int state)
        {
            if (heapCount == heapCost.Length)
            {
                Array.Resize(ref heapCost, heapCount * 2);
                Array.Resize(ref heapState, heapCount * 2);
            }
            int i = heapCount++;
            while (i > 0)
            {
                int up = (i - 1) / 2;
                if (heapCost[up] <= value)
                {
                    break;
                }
                heapCost[i] = heapCost[up];
                heapState[i] = heapState[up];
                i = up;
            }
            heapCost[i] = value;
            heapState[i] = state;
        }

        private void Pop(out float value, out int state)
        {
            value = heapCost[0];
            state = heapState[0];
            heapCount--;
            float lastCost = heapCost[heapCount];
            int lastState = heapState[heapCount];
            int i = 0;
            while (true)
            {
                int child = i * 2 + 1;
                if (child >= heapCount)
                {
                    break;
                }
                if (child + 1 < heapCount && heapCost[child + 1] < heapCost[child])
                {
                    child++;
                }
                if (heapCost[child] >= lastCost)
                {
                    break;
                }
                heapCost[i] = heapCost[child];
                heapState[i] = heapState[child];
                i = child;
            }
            heapCost[i] = lastCost;
            heapState[i] = lastState;
        }
    }
}
