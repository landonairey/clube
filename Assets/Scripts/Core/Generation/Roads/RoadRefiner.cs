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
        /// <summary>Crossed only where a road has no dry way at all: a strait too narrow for the survey to have seen.</summary>
        Sea,
    }

    /// <summary>
    /// Turns the plan's roads from lines through 40 m survey cells into travelled routes on the real ground (GL631):
    /// each is searched again on an 8 m grid inside a corridor round its planned line, forty-eight ways from every point,
    /// where a stretch costs what its class says for its slope (<see cref="RoadClassDefinition.StretchCost"/>, GL634)
    /// and a turn costs what its class says for turning (<see cref="RoadClassDefinition.TurnCost"/>). So a road runs
    /// along a hillside and doubles back up it in long legs (switchbacks) rather than straight up, crosses water at a
    /// narrow place, and is then rounded into curves. It still passes through every settlement it passed through, and a
    /// road that joined another ends on that road's new line, so the network stays one network.
    /// </summary>
    /// <remarks>
    /// The ways on from a point include long shallow ones (up to eight grid steps along for one across): on a steep hillside
    /// only a line a few degrees off the contour is gentle enough for a cart, and a search that can only step straight
    /// or at 27 degrees never finds it. Where the planned line is steep, or crosses a lake, the corridor is
    /// <see cref="HardReach"/> times as wide, which is the room switchbacks and a way round the water need. Ground
    /// the map closes (past the playable edge, or inside an edge's limit) is never entered.
    /// </remarks>
    public sealed class RoadRefiner
    {
        /// <summary>Metres between the points a route is searched over.</summary>
        public const float CellSize = 8f;

        /// <summary>Metres a route may leave its planned line by.</summary>
        public const float Corridor = 64f;

        /// <summary>How many times wider the corridor is round a hard stretch of the planned line (steep, or over a lake).</summary>
        public const float HardReach = 3f;

        // A planned stretch is hard for this many stretches before and after a steep or wet one: a climb needs its run-up.
        private const int HardSpread = 3;

        // Metres between the looks for water along a step: rivers are narrower than the grid.
        private const float WaterStep = 2f;
        private const int WaterPerCell = 4;

        // The planner's bridge cost is for one survey cell of river.
        private const float PlannedCellSize = 40f;

        // Rise over run is measured over at least this many metres of a finished road: a single step of ground is not a slope.
        private const float SlopeRun = 12f;

        // The longest piece of a searched route, in metres, before it is rounded: long steps are cut up so the curve is even.
        private const float LongestPiece = 9f;

        // The ways on from a grid point in one eighth of the compass (grid steps along, across); the rest mirror these.
        private static readonly int[] OctantAlong = { 1, 8, 6, 4, 3, 2, 1 };
        private static readonly int[] OctantAcross = { 0, 1, 1, 1, 1, 1, 1 };

        // The ways on from a grid point, and "none yet" for a route's first step.
        private const int Ways = 48;
        private const int NoWay = Ways;
        private static readonly int[] StepX = new int[Ways];
        private static readonly int[] StepZ = new int[Ways];
        private static readonly float[] StepMetres = new float[Ways];

        // What turning from one way to another costs, as a share of the class's cost for a right angle (the square of
        // the angle over 90 degrees; nothing for the slightest bend). A road may double back on the spot, at four times
        // that: a hairpin that takes its turn in a few metres climbs less in it than one that swings wide up the slope.
        private static readonly float[] Turn = new float[Ways * Ways];

        static RoadRefiner()
        {
            int count = 0;
            void Add(int x, int z)
            {
                for (int i = 0; i < count; i++)
                {
                    if (StepX[i] == x && StepZ[i] == z)
                    {
                        return;
                    }
                }
                StepX[count] = x;
                StepZ[count] = z;
                count++;
            }
            for (int i = 0; i < OctantAlong.Length; i++)
            {
                int a = OctantAlong[i], b = OctantAcross[i];
                for (int sign = 0; sign < 4; sign++)
                {
                    int sa = (sign & 1) == 0 ? a : -a, sb = (sign & 2) == 0 ? b : -b;
                    Add(sa, sb);
                    Add(sb, sa);
                }
            }
            for (int a = 0; a < Ways; a++)
            {
                var from = new Vector2(StepX[a], StepZ[a]);
                StepMetres[a] = from.magnitude * CellSize;
                for (int b = 0; b < Ways; b++)
                {
                    float angle = Vector2.Angle(from, new Vector2(StepX[b], StepZ[b]));
                    Turn[a * Ways + b] = angle < 28f ? 0f : angle / 90f * (angle / 90f);
                }
            }
        }

        private const byte Unknown = byte.MaxValue;

        // The shares a grid point's neighbours have in its evened-out height, by how many points away they are.
        private static readonly float[][] EvenWeight = { new[] { 1f }, new[] { 2f, 1f }, new[] { 6f, 4f, 1f } };

        private readonly Func<float, float, float> heightAt;
        private readonly Func<float, float, RoadWater> waterAt;
        private readonly Func<float, float, bool> closedAt;
        private readonly Vector2 origin;
        private readonly int width, depth, waterWidth, waterDepth;
        private readonly float[] heights;
        // The ground as a cart path's bed evens it out: each grid point with its neighbours.
        private readonly float[][] evenedBy = new float[3][];
        private readonly int[] asked;
        private int evenReach;
        private bool bridgesSea;
        // Water every 2 m and closed ground every 8 m, each looked up once.
        private readonly byte[] water;
        private readonly byte[] closed;
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
        /// <param name="closedAt">True where no road may run (past the map's playable edge); null for nowhere.</param>
        public RoadRefiner(Rect area, Func<float, float, float> heightAt, Func<float, float, RoadWater> waterAt, Func<float, float, bool> closedAt = null)
        {
            this.heightAt = heightAt ?? throw new ArgumentNullException(nameof(heightAt));
            this.waterAt = waterAt;
            this.closedAt = closedAt;
            origin = new Vector2(Mathf.Floor(area.xMin / CellSize) * CellSize, Mathf.Floor(area.yMin / CellSize) * CellSize);
            width = Mathf.CeilToInt((area.xMax - origin.x) / CellSize) + 1;
            depth = Mathf.CeilToInt((area.yMax - origin.y) / CellSize) + 1;
            int count = width * depth;
            heights = new float[count];
            evenedBy[1] = new float[count];
            evenedBy[2] = new float[count];
            asked = new int[count];
            closed = new byte[count];
            allowed = new int[count];
            slotOf = new int[count];
            for (int i = 0; i < count; i++)
            {
                heights[i] = float.NaN;
                evenedBy[1][i] = float.NaN;
                evenedBy[2][i] = float.NaN;
                closed[i] = Unknown;
            }
            waterWidth = (width - 1) * WaterPerCell + 1;
            waterDepth = (depth - 1) * WaterPerCell + 1;
            if (waterAt != null)
            {
                water = new byte[waterWidth * waterDepth];
                for (int i = 0; i < water.Length; i++)
                {
                    water[i] = Unknown;
                }
            }
        }

        /// <summary>
        /// The ground's heights at many points at once, when the map can give them faster that way (a job); the
        /// search asks for a whole corridor's before it starts.
        /// </summary>
        public Func<Vector2[], float[]> HeightsAt { get; set; }

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
                if (route == null)
                {
                    // No dry way: the plan's 40 m cells took a narrow strait of the sea for land. It is bridged, dearly.
                    route = Route(leg, definition, 1f, true);
                    if (route != null)
                    {
                        plan.Notes.Add($"Road {road.Index}: bridges a strait of the sea, having no way round it.");
                    }
                }
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

        // The height a step's slope is taken from: for a cart path the ground evened out over its neighbours, as its
        // bed will be, so a gentle line across a rough hillside is seen for what it is; for a footpath the ground itself.
        private float GroundOf(int node)
        {
            if (evenReach == 0)
            {
                return HeightOf(node);
            }
            float[] evened = evenedBy[evenReach];
            if (float.IsNaN(evened[node]))
            {
                int x = node % width, z = node / width;
                float sum = 0f, weight = 0f;
                for (int dz = -evenReach; dz <= evenReach; dz++)
                {
                    for (int dx = -evenReach; dx <= evenReach; dx++)
                    {
                        int nx = x + dx, nz = z + dz;
                        if (nx < 0 || nz < 0 || nx >= width || nz >= depth)
                        {
                            continue;
                        }
                        float w = EvenWeight[evenReach][Mathf.Abs(dx)] * EvenWeight[evenReach][Mathf.Abs(dz)];
                        sum += HeightOf(nx + nz * width) * w;
                        weight += w;
                    }
                }
                evened[node] = sum / weight;
            }
            return evened[node];
        }

        // Asks for the heights of the search's grid points (and their neighbours) all at once, where the map can.
        private void FetchHeights()
        {
            if (HeightsAt == null)
            {
                return;
            }
            var wanted = new List<int>();
            foreach (int node in slotNode)
            {
                int x = node % width, z = node / width;
                for (int dz = -evenReach; dz <= evenReach; dz++)
                {
                    for (int dx = -evenReach; dx <= evenReach; dx++)
                    {
                        int nx = x + dx, nz = z + dz;
                        if (nx < 0 || nz < 0 || nx >= width || nz >= depth)
                        {
                            continue;
                        }
                        int near = nx + nz * width;
                        if (asked[near] != stamp && float.IsNaN(heights[near]))
                        {
                            asked[near] = stamp;
                            wanted.Add(near);
                        }
                    }
                }
            }
            if (wanted.Count == 0)
            {
                return;
            }
            var points = new Vector2[wanted.Count];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = PointOf(wanted[i]);
            }
            float[] found = HeightsAt(points);
            for (int i = 0; i < points.Length; i++)
            {
                heights[wanted[i]] = found[i];
            }
        }

        // What water lies at a point, from the 2 m grid of looks.
        private RoadWater WaterAt(Vector2 point)
        {
            if (water == null)
            {
                return RoadWater.Dry;
            }
            int x = Mathf.Clamp(Mathf.RoundToInt((point.x - origin.x) / WaterStep), 0, waterWidth - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt((point.y - origin.y) / WaterStep), 0, waterDepth - 1);
            int i = x + z * waterWidth;
            if (water[i] == Unknown)
            {
                water[i] = (byte)waterAt(origin.x + x * WaterStep, origin.y + z * WaterStep);
            }
            return (RoadWater)water[i];
        }

        private bool IsClosed(int node)
        {
            if (closedAt == null)
            {
                return false;
            }
            if (closed[node] == Unknown)
            {
                Vector2 p = PointOf(node);
                closed[node] = (byte)(closedAt(p.x, p.y) ? 1 : 0);
            }
            return closed[node] == 1;
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

        // The stretches of a planned leg that need room: steeper than the class is laid out for, or over a lake,
        // and those just before and after them.
        private bool[] HardStretches(List<Vector2> leg, RoadClassDefinition definition)
        {
            int count = leg.Count - 1;
            var steep = new bool[count];
            float before = heightAt(leg[0].x, leg[0].y);
            for (int i = 0; i < count; i++)
            {
                float after = heightAt(leg[i + 1].x, leg[i + 1].y);
                float metres = Mathf.Max(1f, Vector2.Distance(leg[i], leg[i + 1]));
                steep[i] = Mathf.Abs(after - before) / metres > definition.EasySlope;
                int looks = Mathf.CeilToInt(metres / CellSize);
                for (int k = 0; k <= looks && !steep[i]; k++)
                {
                    steep[i] = WaterAt(Vector2.Lerp(leg[i], leg[i + 1], k / (float)looks)) == RoadWater.Lake;
                }
                before = after;
            }
            var hard = new bool[count];
            for (int i = 0; i < count; i++)
            {
                for (int k = Mathf.Max(0, i - HardSpread); k <= Mathf.Min(count - 1, i + HardSpread) && !hard[i]; k++)
                {
                    hard[i] = steep[k];
                }
            }
            return hard;
        }

        // The cheapest way along a leg of the planned line, as points from its first to its last; null when there is none.
        private List<Vector2> Route(List<Vector2> leg, RoadClassDefinition definition, float reachScale, bool overSea = false)
        {
            stamp++;
            bridgesSea = overSea;
            slotNode.Clear();
            bool[] hard = HardStretches(leg, definition);
            for (int i = 0; i < leg.Count - 1; i++)
            {
                Open(leg[i], leg[i + 1], Corridor * reachScale * (hard[i] ? HardReach : 1f));
            }
            int start = NodeAt(leg[0]), goal = NodeAt(leg[leg.Count - 1]);
            if (start == goal)
            {
                return new List<Vector2> { leg[0], leg[leg.Count - 1] };
            }
            OpenNode(start);
            OpenNode(goal);
            // A bed evened over 16 m or more is taken from two grid points each way, a shorter one from one.
            evenReach = definition.GradeLength <= 0f || definition.MaxCut <= 0f ? 0 : definition.GradeLength >= 2f * CellSize ? 2 : 1;
            FetchHeights();
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
            int goalX = goal % width, goalZ = goal / width;
            // The heap is ordered by the cost so far plus the straight line still to go: no stretch costs less than its length.
            Push(0f, first);
            float perWetMetre = definition.BridgeCost / PlannedCellSize;
            int found = -1;
            while (heapCount > 0)
            {
                Pop(out float reached, out int state);
                int slot = state / (Ways + 1), came = state % (Ways + 1);
                int node = slotNode[slot];
                int x = node % width, z = node / width;
                float soFar = stateCost[state];
                if (reached > (soFar + ToGo(x - goalX, z - goalZ)) * 1.0001f + 0.01f)
                {
                    continue;
                }
                if (node == goal)
                {
                    found = state;
                    break;
                }
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
                        step = StepCost(definition, node, way, perWetMetre, next == goal);
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
                    Push(total + ToGo(nx - goalX, nz - goalZ), nextState);
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

        private static float ToGo(int dx, int dz)
        {
            return Mathf.Sqrt(dx * dx + dz * dz) * CellSize;
        }

        // How many times a river's price a metre of bridge over the sea costs: only the narrowest strait is worth it.
        private const float SeaBridgePrice = 8f;

        // What a step from a grid point costs; negative when it can't be taken (through the sea, or over closed ground).
        private float StepCost(RoadClassDefinition definition, int from, int way, float perWetMetre, bool toGoal)
        {
            int fx = from % width, fz = from / width;
            int dx = StepX[way], dz = StepZ[way];
            Vector2 a = PointOf(from), b = a + new Vector2(dx, dz) * CellSize;
            float metres = StepMetres[way];
            int looks = Mathf.CeilToInt(metres / WaterStep);
            int wet = 0;
            bool lake = false;
            int sea = 0;
            for (int i = 1; i <= looks; i++)
            {
                RoadWater found = WaterAt(Vector2.Lerp(a, b, i / (float)looks));
                if (found == RoadWater.Sea)
                {
                    if (bridgesSea)
                    {
                        sea++;
                        wet++;
                    }
                    else if (!toGoal)
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
            // The step is walked a grid line at a time: a long one must not leap a ravine or cut across closed ground.
            int parts = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz));
            float partMetres = metres / parts;
            float dry = 0f;
            float before = GroundOf(from);
            for (int k = 1; k <= parts; k++)
            {
                float px = fx + dx * k / (float)parts, pz = fz + dz * k / (float)parts;
                int x0 = Mathf.FloorToInt(px), z0 = Mathf.FloorToInt(pz);
                float tx = px - x0, tz = pz - z0;
                // One of the two is a whole grid line, so the height lies between two grid points.
                int low = x0 + z0 * width;
                int high = Mathf.Min(width - 1, x0 + (tx > 1e-4f ? 1 : 0)) + Mathf.Min(depth - 1, z0 + (tz > 1e-4f ? 1 : 0)) * width;
                float t = Mathf.Max(tx, tz);
                if (!toGoal && IsClosed(t < 0.5f ? low : high))
                {
                    return -1f;
                }
                float after = low == high ? GroundOf(low) : Mathf.Lerp(GroundOf(low), GroundOf(high), t);
                dry += definition.StretchCost(partMetres, Mathf.Abs(after - before) / partMetres);
                before = after;
            }
            if (wet > 0)
            {
                // A bridge: dearer by the metre of water, and by half a cell's worth for starting one at all. Its deck
                // climbs from bank to bank like any stretch, so a stream running down a hillside is no cheap way up it.
                float wetMetres = metres * wet / looks;
                float starting = WaterAt(a) == RoadWater.Dry ? definition.BridgeCost * 0.5f : 0f;
                float deck = definition.StretchCost(metres, Mathf.Abs(before - GroundOf(from)) / metres);
                return deck + wetMetres * perWetMetre * (lake ? 3f : 1f) + metres * sea / looks * perWetMetre * SeaBridgePrice + starting;
            }
            return dry;
        }

        // Rounds a searched route into curves: cut into even pieces, its points eased towards their neighbours, then
        // its corners cut once.
        private static List<Vector2> Round(List<Vector2> route)
        {
            var points = new List<Vector2>(route.Count * 2) { route[0] };
            for (int i = 1; i < route.Count; i++)
            {
                int pieces = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(route[i - 1], route[i]) / LongestPiece));
                for (int k = 1; k <= pieces; k++)
                {
                    points.Add(Vector2.Lerp(route[i - 1], route[i], k / (float)pieces));
                }
            }
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
                wet[i] = waterAt != null && waterAt(points[i].x, points[i].y) != RoadWater.Dry;
                along[i] = i == 0 ? 0f : along[i - 1] + Vector2.Distance(points[i], points[i - 1]);
            }
            road.Length = along[count - 1];
            road.SteepestSlope = 0f;
            road.SteepLength = 0f;
            road.Bridges.Clear();
            // A cart path's bed is evened out over its grade length, so that is the run its slope is taken over.
            float run = Mathf.Max(SlopeRun, definition.GradeLength);
            int wetFrom = -1, ahead = 0;
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
                ahead = Mathf.Max(ahead, i);
                while (ahead < count - 1 && along[ahead] - along[i] < run)
                {
                    ahead++;
                }
                if (ahead == i || along[ahead] - along[i] < run * 0.5f)
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
                float slope = Mathf.Abs(ground[ahead] - ground[i]) / (along[ahead] - along[i]);
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
