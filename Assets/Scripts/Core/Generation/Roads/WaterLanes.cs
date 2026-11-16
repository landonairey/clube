using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Puts a planned water route on the real water (GL618). The plan's routes run through the middles of the survey's
    /// 40 m cells, which is near the water but not on it: a river winds between them and a coast cuts across them. This
    /// finds the way a vessel can float, from one end to the other, staying within a corridor of the planned line and
    /// keeping off the banks where it can, and smooths it. What "floats" means (how wide and deep the water must be) is
    /// the caller's to say, so one finder serves river boats and sea vessels.
    /// </summary>
    public static class WaterLanes
    {
        private const int MostCells = 3_000_000;
        private const float ShoreCost = 2.5f;

        /// <summary>
        /// The way along <paramref name="planned"/> on water that <paramref name="floats"/>, as points
        /// <paramref name="step"/> metres apart or more, smoothed; null when no water lies near the route's start.
        /// <paramref name="whole"/> is false when the far end could not be reached by water inside the corridor: the lane
        /// then ends at the water nearest to it.
        /// </summary>
        public static List<Vector2> Find(IReadOnlyList<Vector2> planned, Func<float, float, bool> floats, float step, float corridor, out bool whole)
        {
            whole = false;
            if (planned == null || planned.Count < 2)
            {
                return null;
            }
            Vector2 low = planned[0], high = planned[0];
            foreach (Vector2 point in planned)
            {
                low = Vector2.Min(low, point);
                high = Vector2.Max(high, point);
            }
            low -= Vector2.one * corridor;
            high += Vector2.one * corridor;
            // A long route on a fine grid is searched on a coarser one rather than on millions of cells.
            while ((high.x - low.x) / step * ((high.y - low.y) / step) > MostCells)
            {
                step *= 1.5f;
            }
            var grid = new Grid(low, step, Mathf.CeilToInt((high.x - low.x) / step) + 1, Mathf.CeilToInt((high.y - low.y) / step) + 1, floats);
            grid.Open(planned, corridor);
            int start = grid.NearestWet(planned[0], corridor);
            int goal = grid.NearestWet(planned[planned.Count - 1], corridor);
            if (start < 0)
            {
                return null;
            }
            List<int> cells = grid.Search(start, goal, goal >= 0 ? grid.Centre(goal) : planned[planned.Count - 1], out whole);
            var lane = new List<Vector2>(cells.Count);
            foreach (int cell in cells)
            {
                lane.Add(grid.Centre(cell));
            }
            return Smooth(Thin(lane), floats);
        }

        /// <summary>
        /// Where a vessel of a class floats on a water map. A sea vessel: on the sea, deep enough for its keel and with its
        /// own width of water either side (<paramref name="strict"/>), or on any sea a hull can scrape over (not strict).
        /// A river boat: on any water or within a cell of it, since a stream one cell wide that runs on the diagonal is no
        /// unbroken line of cells (<see cref="For"/> then sets the lane's points back on the water).
        /// </summary>
        public static Func<float, float, bool> Floats(WaterMap water, VesselClass vessel, bool strict = true)
        {
            if (!water.IsCreated)
            {
                return (x, z) => false;
            }
            if (vessel.Kind == WaterRouteKind.River)
            {
                float c = water.CellSize;
                bool Wet(float x, float z)
                {
                    return water.TryCell(new float2(x, z), out int cell) && water.Kind[cell] != 0;
                }
                return (x, z) => Wet(x, z) || Wet(x - c, z) || Wet(x + c, z) || Wet(x, z - c) || Wet(x, z + c)
                    || Wet(x - c, z - c) || Wet(x + c, z - c) || Wet(x - c, z + c) || Wet(x + c, z + c);
            }
            float keel = strict ? vessel.Draught + 0.2f : 0.3f, clear = vessel.Beam * 0.6f;
            bool Deep(float x, float z)
            {
                return water.TryCell(new float2(x, z), out int cell) && water.IsSea(cell) && water.Depth[cell] >= keel;
            }
            if (!strict)
            {
                return Deep;
            }
            return (x, z) => Deep(x, z) && Deep(x - clear, z) && Deep(x + clear, z) && Deep(x, z - clear) && Deep(x, z + clear);
        }

        /// <summary>
        /// A planned water route as a lane a vessel of a class can sail on a water map: <see cref="Find"/> with the grid
        /// and corridor its kind of water wants, then with a wider corridor and less water under the keel when that does
        /// not reach the far end, and from the far end back when nothing does (the planned route may hop between streams
        /// that never join: the vessel then plies the longest stretch that is one water). Null when no water lies by
        /// either end.
        /// </summary>
        public static List<Vector2> For(WaterRoute route, VesselClass vessel, WaterMap water, out bool whole)
        {
            bool river = route.Kind == WaterRouteKind.River;
            float step = river ? Mathf.Max(2f, water.CellSize) : 8f;
            var back = new List<Vector2>(route.Points);
            back.Reverse();
            List<Vector2> best = null;
            float bestLength = -1f;
            whole = false;
            for (int attempt = 0; attempt < 4 && !whole; attempt++)
            {
                bool strict = attempt < 2, wide = attempt % 2 == 1;
                if (river && !strict)
                {
                    break;
                }
                Func<float, float, bool> floats = Floats(water, vessel, strict);
                float corridor = (river ? 70f : 100f) * (wide ? 2.2f : 1f);
                for (int way = 0; way < 2 && !whole; way++)
                {
                    List<Vector2> lane = Find(way == 0 ? route.Points : back, floats, step, corridor, out whole);
                    if (lane == null)
                    {
                        continue;
                    }
                    if (way == 1)
                    {
                        lane.Reverse();
                    }
                    float length = Length(lane);
                    if (whole || length > bestLength)
                    {
                        best = lane;
                        bestLength = length;
                    }
                }
            }
            if (river && best != null)
            {
                OntoWater(best, water);
            }
            return best;
        }

        // Moves each point of a river lane that lies a cell off the water onto the wet cell beside it.
        private static void OntoWater(List<Vector2> lane, WaterMap water)
        {
            float c = water.CellSize;
            for (int i = 0; i < lane.Count; i++)
            {
                Vector2 point = lane[i];
                float nearest = float.MaxValue;
                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        var at = new Vector2(point.x + dx * c, point.y + dz * c);
                        float distance = dx * dx + dz * dz;
                        if (distance < nearest && water.TryCell(new float2(at.x, at.y), out int cell) && water.Kind[cell] != 0)
                        {
                            nearest = distance;
                            lane[i] = distance == 0f ? point : (Vector2)(water.Origin + (math.floor((new float2(at.x, at.y) - water.Origin) / c) + 0.5f) * c);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// The water's surface at a place (x, z): the river's or the sea's there or a cell away; false on dry land.
        /// </summary>
        public static bool TrySurfaceAt(WaterMap water, Vector2 at, out float surface)
        {
            surface = 0f;
            if (!water.IsCreated)
            {
                return false;
            }
            if (water.TrySurfaceAt(new float2(at.x, at.y), out surface, out _))
            {
                return true;
            }
            float c = water.CellSize;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (water.TrySurfaceAt(new float2(at.x + dx * c, at.y + dz * c), out surface, out _))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>The point a distance along a line of points (clamped to its ends).</summary>
        public static Vector2 Along(IReadOnlyList<Vector2> points, float distance)
        {
            for (int i = 1; i < points.Count; i++)
            {
                float length = Vector2.Distance(points[i - 1], points[i]);
                if (distance <= length || i == points.Count - 1)
                {
                    return Vector2.Lerp(points[i - 1], points[i], length > 1e-4f ? Mathf.Clamp01(distance / length) : 0f);
                }
                distance -= length;
            }
            return points[0];
        }

        /// <summary>The length of a line of points, in metres.</summary>
        public static float Length(IReadOnlyList<Vector2> points)
        {
            float length = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                length += Vector2.Distance(points[i - 1], points[i]);
            }
            return length;
        }

        // Drops the points in the middle of straight runs.
        private static List<Vector2> Thin(List<Vector2> points)
        {
            if (points.Count < 3)
            {
                return points;
            }
            var thinned = new List<Vector2> { points[0] };
            for (int i = 1; i < points.Count - 1; i++)
            {
                Vector2 before = (points[i] - thinned[thinned.Count - 1]).normalized, after = (points[i + 1] - points[i]).normalized;
                if (Vector2.Dot(before, after) < 0.999f)
                {
                    thinned.Add(points[i]);
                }
            }
            thinned.Add(points[points.Count - 1]);
            return thinned;
        }

        // Rounds the corners (two passes of corner cutting), wherever the cut corner still floats.
        private static List<Vector2> Smooth(List<Vector2> points, Func<float, float, bool> floats)
        {
            for (int pass = 0; pass < 2 && points.Count > 2; pass++)
            {
                var rounded = new List<Vector2> { points[0] };
                for (int i = 1; i < points.Count - 1; i++)
                {
                    Vector2 a = Vector2.Lerp(points[i], points[i - 1], 0.25f), b = Vector2.Lerp(points[i], points[i + 1], 0.25f);
                    Vector2 middle = (a + b) * 0.5f;
                    if (floats(a.x, a.y) && floats(b.x, b.y) && floats(middle.x, middle.y))
                    {
                        rounded.Add(a);
                        rounded.Add(b);
                    }
                    else
                    {
                        rounded.Add(points[i]);
                    }
                }
                rounded.Add(points[points.Count - 1]);
                points = rounded;
            }
            return points;
        }

        // The corridor as a grid of cells: which are open to the search, which are wet (asked once each), and the search.
        private sealed class Grid
        {
            private readonly Vector2 origin;
            private readonly float step;
            private readonly int width, depth;
            private readonly Func<float, float, bool> floats;
            private readonly bool[] open;
            private readonly byte[] wet;

            public Grid(Vector2 origin, float step, int width, int depth, Func<float, float, bool> floats)
            {
                this.origin = origin;
                this.step = step;
                this.width = width;
                this.depth = depth;
                this.floats = floats;
                open = new bool[width * depth];
                wet = new byte[width * depth];
            }

            public Vector2 Centre(int cell)
            {
                return origin + new Vector2(cell % width + 0.5f, cell / width + 0.5f) * step;
            }

            // Opens every cell within the corridor of the planned line.
            public void Open(IReadOnlyList<Vector2> planned, float corridor)
            {
                int reach = Mathf.CeilToInt(corridor / step);
                for (int i = 1; i < planned.Count; i++)
                {
                    float length = Vector2.Distance(planned[i - 1], planned[i]);
                    int stamps = Mathf.Max(1, Mathf.CeilToInt(length / (corridor * 0.5f)));
                    for (int s = 0; s <= stamps; s++)
                    {
                        Vector2 at = (Vector2.Lerp(planned[i - 1], planned[i], s / (float)stamps) - origin) / step;
                        int cx = Mathf.FloorToInt(at.x), cz = Mathf.FloorToInt(at.y);
                        for (int z = Mathf.Max(0, cz - reach); z <= Mathf.Min(depth - 1, cz + reach); z++)
                        {
                            for (int x = Mathf.Max(0, cx - reach); x <= Mathf.Min(width - 1, cx + reach); x++)
                            {
                                if ((x - cx) * (x - cx) + (z - cz) * (z - cz) <= reach * reach)
                                {
                                    open[x + z * width] = true;
                                }
                            }
                        }
                    }
                }
            }

            public bool Wet(int cell)
            {
                if (wet[cell] == 0)
                {
                    Vector2 centre = Centre(cell);
                    wet[cell] = open[cell] && floats(centre.x, centre.y) ? (byte)1 : (byte)2;
                }
                return wet[cell] == 1;
            }

            // The wet cell nearest a point, within a distance; -1 for none.
            public int NearestWet(Vector2 point, float within)
            {
                Vector2 at = (point - origin) / step;
                int cx = Mathf.Clamp(Mathf.FloorToInt(at.x), 0, width - 1), cz = Mathf.Clamp(Mathf.FloorToInt(at.y), 0, depth - 1);
                int reach = Mathf.CeilToInt(within / step);
                int best = -1;
                int bestDistance = int.MaxValue;
                for (int z = Mathf.Max(0, cz - reach); z <= Mathf.Min(depth - 1, cz + reach); z++)
                {
                    for (int x = Mathf.Max(0, cx - reach); x <= Mathf.Min(width - 1, cx + reach); x++)
                    {
                        int distance = (x - cx) * (x - cx) + (z - cz) * (z - cz);
                        if (distance < bestDistance && Wet(x + z * width))
                        {
                            bestDistance = distance;
                            best = x + z * width;
                        }
                    }
                }
                return best;
            }

            // The cheapest way over wet cells from start to goal, a cell by the bank costing more than one in mid-water;
            // when the goal is not reached, the way to the reached cell nearest it.
            public List<int> Search(int start, int goal, Vector2 towards, out bool reached)
            {
                var cost = new Dictionary<int, float> { { start, 0f } };
                var from = new Dictionary<int, int>();
                var queue = new Heap();
                queue.Push(start, Vector2.Distance(Centre(start), towards));
                int nearest = start;
                float nearestDistance = Vector2.Distance(Centre(start), towards);
                reached = false;
                while (queue.Count > 0)
                {
                    int cell = queue.Pop(out float key);
                    float sofar = cost[cell];
                    float left = Vector2.Distance(Centre(cell), towards);
                    if (key > sofar + left + 1e-3f)
                    {
                        continue;
                    }
                    if (left < nearestDistance)
                    {
                        nearestDistance = left;
                        nearest = cell;
                    }
                    if (cell == goal)
                    {
                        reached = true;
                        nearest = cell;
                        break;
                    }
                    int x = cell % width, z = cell / width;
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, nz = z + dz;
                            if ((dx == 0 && dz == 0) || nx < 0 || nz < 0 || nx >= width || nz >= depth)
                            {
                                continue;
                            }
                            int next = nx + nz * width;
                            // No cutting a corner between two dry cells.
                            if (!Wet(next) || (dx != 0 && dz != 0 && (!Wet(x + dx + z * width) || !Wet(x + nz * width))))
                            {
                                continue;
                            }
                            float stepCost = step * (dx != 0 && dz != 0 ? 1.4142f : 1f) * (1f + ShoreCost * Dryness(nx, nz));
                            float total = sofar + stepCost;
                            if (!cost.TryGetValue(next, out float known) || total < known - 1e-4f)
                            {
                                cost[next] = total;
                                from[next] = cell;
                                queue.Push(next, total + Vector2.Distance(Centre(next), towards));
                            }
                        }
                    }
                }
                var path = new List<int>();
                for (int cell = nearest; ; cell = from[cell])
                {
                    path.Add(cell);
                    if (!from.ContainsKey(cell))
                    {
                        break;
                    }
                }
                path.Reverse();
                return path;
            }

            // The share of a cell's eight neighbours that are not water.
            private float Dryness(int x, int z)
            {
                int dry = 0;
                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, nz = z + dz;
                        if ((dx != 0 || dz != 0) && (nx < 0 || nz < 0 || nx >= width || nz >= depth || !Wet(nx + nz * width)))
                        {
                            dry++;
                        }
                    }
                }
                return dry / 8f;
            }
        }

        // A binary heap of cells by key, smallest first.
        private sealed class Heap
        {
            private readonly List<(float Key, int Cell)> items = new List<(float, int)>();

            public int Count => items.Count;

            public void Push(int cell, float key)
            {
                items.Add((key, cell));
                int i = items.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (items[parent].Key <= items[i].Key)
                    {
                        break;
                    }
                    (items[parent], items[i]) = (items[i], items[parent]);
                    i = parent;
                }
            }

            public int Pop(out float key)
            {
                (float Key, int Cell) top = items[0];
                int last = items.Count - 1;
                items[0] = items[last];
                items.RemoveAt(last);
                int i = 0;
                while (true)
                {
                    int left = i * 2 + 1, right = left + 1, least = i;
                    if (left < items.Count && items[left].Key < items[least].Key)
                    {
                        least = left;
                    }
                    if (right < items.Count && items[right].Key < items[least].Key)
                    {
                        least = right;
                    }
                    if (least == i)
                    {
                        break;
                    }
                    (items[least], items[i]) = (items[i], items[least]);
                    i = least;
                }
                key = top.Key;
                return top.Cell;
            }
        }
    }
}
