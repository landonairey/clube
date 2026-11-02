using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Works out a world's rivers and lakes (GL307) from its heightfield, into a <see cref="WaterMap"/>:
    /// <list type="number">
    /// <item>samples the ground on a coarse grid (<see cref="WaterGeneration.FlowCellSize"/>) over the area;</item>
    /// <item>floods it from the area's edges, lowest first (priority flood): every cell learns the
    /// neighbour its water drains to, and hollows fill up to the height where they spill over, which is
    /// where lakes stand (hollows shallower than <see cref="WaterGeneration.LakeMinDepth"/> stay dry);</item>
    /// <item>adds up the land draining through each cell; where it passes <see cref="WaterGeneration.RiverArea"/>
    /// the cell is on a river, wider and deeper the more drains through it;</item>
    /// <item>on a 1 m grid, cuts each river's bed along its course, always downhill, with banks at 45°,
    /// and sets where water stands: in the cut beds, and in the lakes up to their level;</item>
    /// <item>measures every cell's distance to river and lake water, for the sand and clay beside them.</item>
    /// </list>
    /// The result depends only on the generator, the settings and the seed, and is cached per world shape,
    /// so every chunk, level of detail and water mesh of a world reads the same map.
    /// </summary>
    public static class Hydrology
    {
        private static readonly Dictionary<string, WaterMap> Cache = new Dictionary<string, WaterMap>();
        private static readonly List<string> CacheOrder = new List<string>();
        private const int CacheSize = 8;
        private static bool hooked;

        /// <summary>
        /// The water map for a world shape, worked out on first use. <paramref name="key"/> must change
        /// whenever the ground's shape does. Areas in <paramref name="keepDry"/> get no lake or river.
        /// </summary>
        public static WaterMap For(string key, HeightfieldGenerator ground, WaterGeneration settings, WaterBanks banks, Rect keepDry)
        {
            if (!hooked)
            {
                hooked = true;
                AppDomain.CurrentDomain.DomainUnload += (_, __) => DisposeAll();
                Application.quitting += DisposeAll;
            }
            key = key + "|" + settings.Key + "|" + keepDry;
            if (Cache.TryGetValue(key, out WaterMap map))
            {
                map.Banks = banks;
                return map;
            }
            map = Build(ground, settings, keepDry);
            map.Banks = banks;
            Cache[key] = map;
            CacheOrder.Add(key);
            if (CacheOrder.Count > CacheSize)
            {
                Cache[CacheOrder[0]].Dispose();
                Cache.Remove(CacheOrder[0]);
                CacheOrder.RemoveAt(0);
            }
            return map;
        }

        /// <summary>Frees every cached map (tests, domain unload).</summary>
        public static void DisposeAll()
        {
            foreach (WaterMap map in Cache.Values)
            {
                map.Dispose();
            }
            Cache.Clear();
            CacheOrder.Clear();
        }

        private static readonly int2[] Neighbours =
        {
            new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1),
            new int2(1, 1), new int2(-1, 1), new int2(1, -1), new int2(-1, -1),
        };

        /// <summary>Works out the water over the area (uncached; the caller owns the result).</summary>
        public static WaterMap Build(HeightfieldGenerator ground, WaterGeneration settings, Rect keepDry)
        {
            float area = settings.AreaSize;
            float flowCell = settings.FlowCellSize;
            int n = Mathf.CeilToInt(area / flowCell);
            var origin = new float2(-n * flowCell * 0.5f);

            // 1. The ground on the coarse grid.
            float[] h = Heights(ground, origin, flowCell, n);

            // 2. Priority flood from the edges: filled heights, where each cell drains, the order reached.
            var filled = new float[n * n];
            var drainsTo = new int[n * n];
            var order = new List<int>(n * n);
            var closed = new bool[n * n];
            var queue = new MinHeap(n * 4);
            for (int i = 0; i < n * n; i++)
            {
                int x = i % n, z = i / n;
                drainsTo[i] = -1;
                if (x == 0 || z == 0 || x == n - 1 || z == n - 1)
                {
                    filled[i] = h[i];
                    closed[i] = true;
                    queue.Push(h[i], i);
                }
            }
            while (queue.Count > 0)
            {
                int c = queue.Pop();
                order.Add(c);
                int cx = c % n, cz = c / n;
                foreach (int2 d in Neighbours)
                {
                    int x = cx + d.x, z = cz + d.y;
                    if (x < 0 || z < 0 || x >= n || z >= n)
                    {
                        continue;
                    }
                    int j = x + z * n;
                    if (closed[j])
                    {
                        continue;
                    }
                    closed[j] = true;
                    filled[j] = Mathf.Max(h[j], filled[c]);
                    drainsTo[j] = c;
                    queue.Push(filled[j], j);
                }
            }

            // Lakes: connected filled hollows deep enough, away from the dry area.
            var lakeLevel = new float[n * n];
            for (int i = 0; i < lakeLevel.Length; i++)
            {
                lakeLevel[i] = float.NegativeInfinity;
            }
            var seen = new bool[n * n];
            var basin = new List<int>();
            var stack = new Stack<int>();
            Rect dryCoarse = Expand(keepDry, 6f);
            for (int i = 0; i < n * n; i++)
            {
                if (seen[i] || filled[i] - h[i] <= 0.01f)
                {
                    continue;
                }
                basin.Clear();
                stack.Push(i);
                seen[i] = true;
                float deepest = 0f;
                bool touchesDry = false;
                while (stack.Count > 0)
                {
                    int c = stack.Pop();
                    basin.Add(c);
                    deepest = Mathf.Max(deepest, filled[c] - h[c]);
                    touchesDry |= dryCoarse.Contains(CellCentre(origin, flowCell, n, c));
                    int cx = c % n, cz = c / n;
                    foreach (int2 d in Neighbours)
                    {
                        int x = cx + d.x, z = cz + d.y;
                        if (x < 0 || z < 0 || x >= n || z >= n)
                        {
                            continue;
                        }
                        int j = x + z * n;
                        if (!seen[j] && filled[j] - h[j] > 0.01f)
                        {
                            seen[j] = true;
                            stack.Push(j);
                        }
                    }
                }
                if (deepest >= settings.LakeMinDepth && basin.Count >= 2 && !touchesDry)
                {
                    foreach (int c in basin)
                    {
                        lakeLevel[c] = filled[c];
                    }
                }
            }

            // 3. Land draining through each cell, highest cells first.
            float cellArea = flowCell * flowCell;
            var drained = new float[n * n];
            for (int k = order.Count - 1; k >= 0; k--)
            {
                int c = order[k];
                drained[c] += cellArea;
                if (drainsTo[c] >= 0)
                {
                    drained[drainsTo[c]] += drained[c];
                }
            }

            // 4. The fine grid: the ground, then each river reach cut into it.
            int size = Mathf.CeilToInt(n * flowCell);
            var map = new WaterMap
            {
                Origin = origin,
                CellSize = 1f,
                Size = new int2(size, size),
                Carve = new NativeArray<float>(size * size, Allocator.Persistent),
                Surface = new NativeArray<float>(size * size, Allocator.Persistent),
                Depth = new NativeArray<float>(size * size, Allocator.Persistent),
                Kind = new NativeArray<byte>(size * size, Allocator.Persistent),
                Flow = new NativeArray<float2>(size * size, Allocator.Persistent),
                LakeDistance = new NativeArray<byte>(size * size, Allocator.Persistent),
                RiverDistance = new NativeArray<byte>(size * size, Allocator.Persistent),
            };
            float[] fine = Heights(ground, origin, 1f, size);
            var target = new float[size * size];
            var surface = new float[size * size];
            var flow = new float2[size * size];
            for (int i = 0; i < target.Length; i++)
            {
                target[i] = float.PositiveInfinity;
                surface[i] = float.NegativeInfinity;
            }

            Vector2 widths = settings.RiverWidth;
            Vector2 depths = settings.RiverDepth;
            float growth = Mathf.Log(30f);
            Rect dryFine = Expand(keepDry, 4f);
            for (int c = 0; c < n * n; c++)
            {
                int p = drainsTo[c];
                if (p < 0 || drained[c] < settings.RiverArea || (IsLake(lakeLevel, c) && IsLake(lakeLevel, p)))
                {
                    continue;
                }
                float2 a = Node(origin, flowCell, n, c);
                float2 b = Node(origin, flowCell, n, p);
                if (dryFine.Contains(new Vector2(a.x, a.y)) || dryFine.Contains(new Vector2(b.x, b.y)))
                {
                    continue;
                }
                float sizeA = Mathf.Clamp01(Mathf.Log(drained[c] / settings.RiverArea) / growth);
                float sizeB = Mathf.Clamp01(Mathf.Log(Mathf.Max(drained[p], drained[c]) / settings.RiverArea) / growth);
                float halfA = Mathf.Lerp(widths.x, widths.y, sizeA) * 0.5f, halfB = Mathf.Lerp(widths.x, widths.y, sizeB) * 0.5f;
                float deepA = Mathf.Lerp(depths.x, depths.y, sizeA), deepB = Mathf.Lerp(depths.x, depths.y, sizeB);
                // The bed line: from the water's own level at each end, so it only ever runs downhill.
                float topA = filled[c], topB = Mathf.Min(filled[p], filled[c]);
                float2 along = b - a;
                float length = math.length(along);
                float2 direction = along / math.max(length, 1e-4f);
                float speed = 0.4f + Mathf.Clamp01((topA - topB) / math.max(length, 1e-4f) * 4f);
                float reach = math.max(halfA, halfB) + math.max(deepA, deepB) + 3f;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(math.min(a.x, b.x) - reach - origin.x));
                int x1 = Mathf.Min(size - 1, Mathf.CeilToInt(math.max(a.x, b.x) + reach - origin.x));
                int z0 = Mathf.Max(0, Mathf.FloorToInt(math.min(a.y, b.y) - reach - origin.y));
                int z1 = Mathf.Min(size - 1, Mathf.CeilToInt(math.max(a.y, b.y) + reach - origin.y));
                for (int z = z0; z <= z1; z++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        var q = new float2(origin.x + x + 0.5f, origin.y + z + 0.5f);
                        float t = math.saturate(math.dot(q - a, along) / math.max(length * length, 1e-6f));
                        float distance = math.distance(q, a + along * t);
                        float half = math.lerp(halfA, halfB, t);
                        float deep = math.lerp(deepA, deepB, t);
                        float top = math.lerp(topA, topB, t);
                        float bed = top - deep;
                        float shape = distance < half
                            ? bed + deep * (distance / half) * (distance / half)
                            : top + (distance - half);
                        int i = x + z * size;
                        if (shape < target[i])
                        {
                            target[i] = shape;
                            if (distance < half)
                            {
                                surface[i] = bed + deep * settings.RiverFill;
                                flow[i] = direction * speed;
                            }
                            else
                            {
                                surface[i] = float.NegativeInfinity;
                            }
                        }
                    }
                }
            }

            // Ground, river water, then lake water over the fine grid.
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = x + z * size;
                    float carve = Mathf.Max(0f, fine[i] - target[i]);
                    float groundAfter = fine[i] - carve;
                    map.Carve[i] = carve;
                    var kind = WaterKind.Dry;
                    float level = 0f;
                    if (surface[i] > groundAfter)
                    {
                        kind = WaterKind.River;
                        level = surface[i];
                        map.Flow[i] = flow[i];
                    }
                    float lake = LakeLevelNear(lakeLevel, n, (x + 0.5f) / flowCell, (z + 0.5f) / flowCell);
                    if (lake > groundAfter && lake >= level)
                    {
                        kind = WaterKind.Lake;
                        level = lake;
                        map.Flow[i] = float2.zero;
                    }
                    map.Kind[i] = (byte)kind;
                    map.Surface[i] = kind == WaterKind.Dry ? groundAfter : level;
                    map.Depth[i] = kind == WaterKind.Dry ? 0f : level - groundAfter;
                }
            }

            // 5. Distances to water, for the shores and banks.
            Distances(map.Kind, WaterKind.Lake, size, map.LakeDistance);
            Distances(map.Kind, WaterKind.River, size, map.RiverDistance);
            return map;
        }

        private static bool IsLake(float[] lakeLevel, int cell)
        {
            return !float.IsNegativeInfinity(lakeLevel[cell]);
        }

        // The highest lake level among the coarse cells round a fine one, so a lake's water reaches
        // up its shores to the true contour between coarse samples.
        private static float LakeLevelNear(float[] lakeLevel, int n, float cx, float cz)
        {
            int x = Mathf.FloorToInt(cx), z = Mathf.FloorToInt(cz);
            float best = float.NegativeInfinity;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, nz = z + dz;
                    if (nx >= 0 && nz >= 0 && nx < n && nz < n)
                    {
                        best = Mathf.Max(best, lakeLevel[nx + nz * n]);
                    }
                }
            }
            return best;
        }

        private static Vector2 CellCentre(float2 origin, float cell, int n, int i)
        {
            return new Vector2(origin.x + (i % n + 0.5f) * cell, origin.y + (i / n + 0.5f) * cell);
        }

        // A river's course point in a coarse cell: its centre, nudged by a hash so rivers don't run on a grid.
        private static float2 Node(float2 origin, float cell, int n, int i)
        {
            int x = i % n, z = i / n;
            var jitter = new float2(VoxelHash.Uniform(0, x, 7, z, 0x52495645), VoxelHash.Uniform(0, x, 7, z, 0x52495646)) - 0.5f;
            return origin + (new float2(x, z) + 0.5f + jitter * 0.6f) * cell;
        }

        private static Rect Expand(Rect rect, float by)
        {
            return rect.width <= 0f ? rect : new Rect(rect.xMin - by, rect.yMin - by, rect.width + by * 2f, rect.height + by * 2f);
        }

        private static float[] Heights(HeightfieldGenerator ground, float2 origin, float cell, int n)
        {
            var points = new NativeArray<float2>(n * n, Allocator.TempJob);
            var heights = new NativeArray<float>(n * n, Allocator.TempJob);
            try
            {
                for (int i = 0; i < points.Length; i++)
                {
                    points[i] = origin + (new float2(i % n, i / n) + 0.5f) * cell;
                }
                ground.SchedulePointHeights(points, heights).Complete();
                return heights.ToArray();
            }
            finally
            {
                points.Dispose();
                heights.Dispose();
            }
        }

        // Chamfer distance (3-4) to the nearest cell of a kind, in metres, capped.
        private static void Distances(NativeArray<byte> kinds, WaterKind kind, int size, NativeArray<byte> result)
        {
            const int far = WaterMap.MaxDistance * 3;
            var d = new int[size * size];
            for (int i = 0; i < d.Length; i++)
            {
                d[i] = kinds[i] == (byte)kind ? 0 : far;
            }
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = x + z * size;
                    if (x > 0) d[i] = Math.Min(d[i], d[i - 1] + 3);
                    if (z > 0) d[i] = Math.Min(d[i], d[i - size] + 3);
                    if (x > 0 && z > 0) d[i] = Math.Min(d[i], d[i - size - 1] + 4);
                    if (x < size - 1 && z > 0) d[i] = Math.Min(d[i], d[i - size + 1] + 4);
                }
            }
            for (int z = size - 1; z >= 0; z--)
            {
                for (int x = size - 1; x >= 0; x--)
                {
                    int i = x + z * size;
                    if (x < size - 1) d[i] = Math.Min(d[i], d[i + 1] + 3);
                    if (z < size - 1) d[i] = Math.Min(d[i], d[i + size] + 3);
                    if (x < size - 1 && z < size - 1) d[i] = Math.Min(d[i], d[i + size + 1] + 4);
                    if (x > 0 && z < size - 1) d[i] = Math.Min(d[i], d[i + size - 1] + 4);
                }
            }
            for (int i = 0; i < d.Length; i++)
            {
                result[i] = (byte)Math.Min(WaterMap.MaxDistance, d[i] / 3);
            }
        }

        // A binary min-heap of cells by height.
        private sealed class MinHeap
        {
            private float[] keys;
            private int[] values;

            public MinHeap(int capacity)
            {
                keys = new float[capacity];
                values = new int[capacity];
            }

            public int Count { get; private set; }

            public void Push(float key, int value)
            {
                if (Count == keys.Length)
                {
                    Array.Resize(ref keys, Count * 2);
                    Array.Resize(ref values, Count * 2);
                }
                int i = Count++;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (keys[parent] <= key)
                    {
                        break;
                    }
                    keys[i] = keys[parent];
                    values[i] = values[parent];
                    i = parent;
                }
                keys[i] = key;
                values[i] = value;
            }

            public int Pop()
            {
                int top = values[0];
                float key = keys[--Count];
                int value = values[Count];
                int i = 0;
                while (true)
                {
                    int child = i * 2 + 1;
                    if (child >= Count)
                    {
                        break;
                    }
                    if (child + 1 < Count && keys[child + 1] < keys[child])
                    {
                        child++;
                    }
                    if (keys[child] >= key)
                    {
                        break;
                    }
                    keys[i] = keys[child];
                    values[i] = values[child];
                    i = child;
                }
                keys[i] = key;
                values[i] = value;
                return top;
            }
        }
    }
}
