using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
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
        /// whenever the ground's shape does. Areas in <paramref name="keepDry"/> get no lake or river, and hollows
        /// covering less than <paramref name="minLakeArea"/> square metres hold none (the eroded land's small pits).
        /// </summary>
        public static WaterMap For(
            string key, HeightfieldGenerator ground, WaterGeneration settings, WaterBanks banks, Rect keepDry, float minLakeArea = 0f,
            WaterExtent extent = null)
        {
            key = FullKey(key, settings, keepDry, minLakeArea, extent);
            if (Cache.TryGetValue(key, out WaterMap map))
            {
                map.Banks = banks;
                return map;
            }
            map = Build(ground, settings, keepDry, minLakeArea, extent);
            Keep(key, map, banks);
            return Cache[key];
        }

        /// <summary>
        /// Works out what <see cref="For"/> would, a step at a time (GL762, <see cref="SlicedWork"/>), and keeps it for
        /// <see cref="For"/> to find: the load screen does this before the scene that needs the water starts.
        /// </summary>
        public static IEnumerator PrepareSteps(
            string key, HeightfieldGenerator ground, WaterGeneration settings, WaterBanks banks, Rect keepDry, float minLakeArea, WaterExtent extent)
        {
            key = FullKey(key, settings, keepDry, minLakeArea, extent);
            if (Cache.ContainsKey(key))
            {
                yield break;
            }
            WaterMap map = default;
            yield return BuildSteps(ground, settings, keepDry, minLakeArea, extent, built => map = built);
            Keep(key, map, banks);
        }

        private static string FullKey(string key, WaterGeneration settings, Rect keepDry, float minLakeArea, WaterExtent extent)
        {
            return key + "|" + settings.Key + "|" + keepDry + "|" + minLakeArea + "|" + extent?.Key;
        }

        // Keeps a finished map, the oldest going when there are too many.
        private static void Keep(string key, WaterMap map, WaterBanks banks)
        {
            if (!hooked)
            {
                hooked = true;
                AppDomain.CurrentDomain.DomainUnload += (_, __) => DisposeAll();
                Application.quitting += DisposeAll;
            }
            map.Banks = banks;
            Cache[key] = map;
            CacheOrder.Add(key);
            if (CacheOrder.Count > CacheSize)
            {
                Cache[CacheOrder[0]].Dispose();
                Cache.Remove(CacheOrder[0]);
                CacheOrder.RemoveAt(0);
            }
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
        /// <param name="extent">A bounded map's area, grid and sea (GL591); null for the settings' own area, 1 m cells and no sea.</param>
        public static WaterMap Build(
            HeightfieldGenerator ground, WaterGeneration settings, Rect keepDry, float minLakeArea = 0f, WaterExtent extent = null)
        {
            WaterMap built = default;
            SlicedWork.RunToEnd(BuildSteps(ground, settings, keepDry, minLakeArea, extent, map => built = map));
            return built;
        }

        // The water a step at a time (GL762, SlicedWork); done is given the finished map, which the caller then owns.
        private static IEnumerator BuildSteps(
            HeightfieldGenerator ground, WaterGeneration settings, Rect keepDry, float minLakeArea, WaterExtent extent, Action<WaterMap> done)
        {
            float area = extent != null ? extent.AreaSize : settings.AreaSize;
            float flowCell = extent != null ? extent.FlowCellSize : settings.FlowCellSize;
            float fineCell = extent != null ? Mathf.Max(1f, extent.CellSize) : 1f;
            int n = Mathf.CeilToInt(area / flowCell);
            var origin = new float2(-n * flowCell * 0.5f);

            // 1. The ground on the coarse grid.
            float[] h = null;
            yield return new SlicedWork.Part(HeightsSteps(ground, origin, flowCell, n, heights => h = heights), 0f, 0.05f);

            // 2. Priority flood from the edges: filled heights, where each cell drains, the order reached.
            var filled = new float[n * n];
            var drainsTo = new int[n * n];
            var order = new List<int>(n * n);
            var closed = new bool[n * n];
            var queue = new MinHeap(n * 4);
            bool[] sea = extent != null && extent.IsSea != null ? SeaCells(h, n, origin, flowCell, extent) : null;
            yield return 0.08f;
            SeedFlood(filled, drainsTo, closed, queue, h, sea, n, extent);
            while (queue.Count > 0)
            {
                Flood(queue, order, closed, filled, drainsTo, h, n, CellsPerStep);
                yield return null;
            }
            yield return 0.15f;

            // Lakes: connected filled hollows deep enough, away from the dry area.
            var lakeLevel = new float[n * n];
            for (int i = 0; i < lakeLevel.Length; i++)
            {
                lakeLevel[i] = float.NegativeInfinity;
            }
            var seen = new bool[n * n];
            if (sea != null)
            {
                // The sea always stays, and is no part of the hollows judged below: a lagoon or river mouth that joins
                // it and reaches the dry area would otherwise take the whole sea away with it.
                for (int i = 0; i < n * n; i++)
                {
                    if (sea[i])
                    {
                        seen[i] = true;
                        lakeLevel[i] = filled[i];
                    }
                }
            }
            Rect dryCoarse = Expand(keepDry, 6f);
            for (int start = 0; start < n * n; start += CellsPerStep)
            {
                FindLakes(start, Mathf.Min(n * n, start + CellsPerStep), seen, filled, h, lakeLevel, n, origin, flowCell, dryCoarse, settings, minLakeArea, extent);
                yield return null;
            }

            // Eroded land (P12): water off a slope takes the steepest way down, so it follows the gullies the erosion
            // cut; the flood's own links (whichever neighbour reached a cell first) wander across them.
            bool valleys = settings.ValleyRivers && ground.HasValleys;
            if (valleys)
            {
                SteepestDescent(filled, drainsTo, n, flowCell);
                yield return null;
            }

            // 3. Land draining through each cell, highest cells first.
            float[] drained = Drained(order, drainsTo, n, flowCell * flowCell);
            yield return 0.2f;

            // Which cells carry a river: enough land drains through them, or (eroded land) a river started upstream in
            // a gully's valley and runs down from there, into lakes and other rivers.
            float[] valley = null;
            if (valleys)
            {
                yield return new SlicedWork.Part(ValleysSteps(ground, origin, flowCell, n, found => valley = found), 0.2f, 0.25f);
            }
            bool[] river = Rivers(order, drainsTo, drained, valley, settings);

            // 4. The fine grid: the ground, then each river reach cut into it.
            int size = Mathf.CeilToInt(n * flowCell / fineCell);
            var map = new WaterMap
            {
                Origin = origin,
                CellSize = fineCell,
                HasSea = sea != null,
                SeaLevel = sea != null ? extent.SeaLevel : 0f,
                Size = new int2(size, size),
                Carve = new NativeArray<float>(size * size, Allocator.Persistent),
                Surface = new NativeArray<float>(size * size, Allocator.Persistent),
                Depth = new NativeArray<float>(size * size, Allocator.Persistent),
                Kind = new NativeArray<byte>(size * size, Allocator.Persistent),
                Flow = new NativeArray<float2>(size * size, Allocator.Persistent),
                LakeDistance = new NativeArray<byte>(size * size, Allocator.Persistent),
                RiverDistance = new NativeArray<byte>(size * size, Allocator.Persistent),
            };
            bool finished = false;
            try
            {
                float[] fine = null;
                yield return new SlicedWork.Part(HeightsSteps(ground, origin, fineCell, size, heights => fine = heights), 0.25f, 0.4f);
                var grid = new FineGrid
                {
                    Settings = settings, Origin = origin, FlowCell = flowCell, FineCell = fineCell, N = n, Size = size, DrainsTo = drainsTo, River = river,
                    LakeLevel = lakeLevel, Drained = drained, Filled = filled, Fine = fine, Valleys = valleys, DryFine = Expand(keepDry, 4f),
                };
                grid.Begin();
                yield return 0.42f;
                for (int c = 0; c < n * n; c += CellsPerStep)
                {
                    grid.CutReaches(c, Mathf.Min(n * n, c + CellsPerStep));
                    yield return 0.42f + 0.13f * c / (n * n);
                }

                // Ground, river water, then lake water over the fine grid.
                for (int z = 0; z < size; z += FineRowsPerStep)
                {
                    grid.FillRows(z, Mathf.Min(size, z + FineRowsPerStep), map);
                    yield return 0.55f + 0.3f * z / size;
                }

                // The lakes were found on the coarse grid, which misses narrow dips in their rims: held to the fine ground, a
                // lake drains to its lowest rim and a river to its lowest bank, so neither stands as a wall over lower ground (GL320).
                yield return new SlicedWork.Part(ContainLakes(map, size), 0.85f, 0.9f);
                yield return new SlicedWork.Part(ContainRivers(map, size), 0.9f, 0.93f);

                // 5. Distances to water, for the shores and banks.
                yield return new SlicedWork.Part(Distances(map.Kind, WaterKind.Lake, size, map.LakeDistance), 0.93f, 0.96f);
                yield return new SlicedWork.Part(Distances(map.Kind, WaterKind.River, size, map.RiverDistance), 0.96f, 0.99f);
                if (fineCell > 1f)
                {
                    // Everything reading the distances takes them as metres.
                    for (int z = 0; z < size; z += DistanceRowsPerStep)
                    {
                        ScaleDistances(map, size, fineCell, z, Mathf.Min(size, z + DistanceRowsPerStep));
                        yield return null;
                    }
                }
                finished = true;
            }
            finally
            {
                if (!finished)
                {
                    map.Dispose();
                }
            }
            done(map);
        }

        // Coarse cells flooded, fine rows filled and fine rows measured in one step.
        private const int CellsPerStep = 8192, FineRowsPerStep = 8, DistanceRowsPerStep = 128, ContainCellsPerStep = 65536;

        // The flood's outlets: the sea, and the area's edges.
        private static void SeedFlood(float[] filled, int[] drainsTo, bool[] closed, MinHeap queue, float[] h, bool[] sea, int n, WaterExtent extent)
        {
            for (int i = 0; i < n * n; i++)
            {
                int x = i % n, z = i / n;
                drainsTo[i] = -1;
                if (sea != null && sea[i])
                {
                    // The sea is an outlet at its own level everywhere (GL591): it reads as one big lake from here on.
                    filled[i] = extent.SeaLevel;
                    closed[i] = true;
                    queue.Push(filled[i], i);
                }
                else if (x == 0 || z == 0 || x == n - 1 || z == n - 1)
                {
                    filled[i] = h[i];
                    closed[i] = true;
                    queue.Push(h[i], i);
                }
            }
        }

        // Takes up to so many cells off the flood's queue.
        private static void Flood(MinHeap queue, List<int> order, bool[] closed, float[] filled, int[] drainsTo, float[] h, int n, int most)
        {
            for (int taken = 0; taken < most && queue.Count > 0; taken++)
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
        }

        // The hollows starting in a range of coarse cells: those deep and wide enough, away from the dry area, are lakes.
        private static void FindLakes(int from, int to, bool[] seen, float[] filled, float[] h, float[] lakeLevel, int n, float2 origin, float flowCell,
            Rect dryCoarse, WaterGeneration settings, float minLakeArea, WaterExtent extent)
        {
            var basin = new List<int>();
            var stack = new Stack<int>();
            for (int i = from; i < to; i++)
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
                int soaked = 0;
                while (stack.Count > 0)
                {
                    int c = stack.Pop();
                    basin.Add(c);
                    deepest = Mathf.Max(deepest, filled[c] - h[c]);
                    touchesDry |= dryCoarse.Contains(CellCentre(origin, flowCell, n, c));
                    soaked += extent?.IsDry != null && extent.IsDry(CellCentre(origin, flowCell, n, c)) ? 1 : 0;
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
                // A hollow that lies mostly on ground that soaks water up (a desert's sand) holds no lake either.
                if (deepest >= settings.LakeMinDepth && basin.Count >= 2 && basin.Count * flowCell * flowCell >= minLakeArea && !touchesDry
                    && soaked * 2 <= basin.Count)
                {
                    foreach (int c in basin)
                    {
                        lakeLevel[c] = filled[c];
                    }
                }
            }
        }

        private static float[] Drained(List<int> order, int[] drainsTo, int n, float cellArea)
        {
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
            return drained;
        }

        private static bool[] Rivers(List<int> order, int[] drainsTo, float[] drained, float[] valley, WaterGeneration settings)
        {
            var river = new bool[drained.Length];
            for (int k = order.Count - 1; k >= 0; k--)
            {
                int c = order[k];
                river[c] |= drained[c] >= settings.RiverArea
                    || (valley != null && valley[c] < -settings.ValleyDepth && drained[c] >= settings.ValleyHeadArea);
                if (river[c] && drainsTo[c] >= 0)
                {
                    river[drainsTo[c]] = true;
                }
            }
            return river;
        }

        private static void ScaleDistances(WaterMap map, int size, float fineCell, int fromRow, int toRow)
        {
            for (int i = fromRow * size; i < toRow * size; i++)
            {
                map.LakeDistance[i] = (byte)Mathf.Min(255, Mathf.RoundToInt(map.LakeDistance[i] * fineCell));
                map.RiverDistance[i] = (byte)Mathf.Min(255, Mathf.RoundToInt(map.RiverDistance[i] * fineCell));
            }
        }

        // The fine grid being worked out: the river reaches cut into the ground, then what stands on each cell.
        private sealed class FineGrid
        {
            public WaterGeneration Settings;
            public float2 Origin;
            public float FlowCell, FineCell;
            public int N, Size;
            public int[] DrainsTo;
            public bool[] River;
            public float[] LakeLevel, Drained, Filled, Fine;
            public bool Valleys;
            public Rect DryFine;

            private float[] target, surface;
            private float2[] flow;

            public void Begin()
            {
                target = new float[Size * Size];
                surface = new float[Size * Size];
                flow = new float2[Size * Size];
                for (int i = 0; i < target.Length; i++)
                {
                    target[i] = float.PositiveInfinity;
                    surface[i] = float.NegativeInfinity;
                }
            }

            // Cuts the reaches leaving a range of coarse cells.
            public void CutReaches(int from, int to)
            {
                WaterGeneration settings = Settings;
                float2 origin = Origin;
                float flowCell = FlowCell, fineCell = FineCell;
                int n = N, size = Size;
                Vector2 widths = settings.RiverWidth;
                Vector2 depths = settings.RiverDepth;
                float growth = Mathf.Log(30f);
                for (int c = from; c < to; c++)
                {
                    int p = DrainsTo[c];
                    if (p < 0 || !River[c] || (IsLake(LakeLevel, c) && IsLake(LakeLevel, p)))
                    {
                        continue;
                    }
                    float2 a = Node(origin, flowCell, n, c);
                    float2 b = Node(origin, flowCell, n, p);
                    if (DryFine.Contains(new Vector2(a.x, a.y)) || DryFine.Contains(new Vector2(b.x, b.y)))
                    {
                        continue;
                    }
                    float sizeA = Mathf.Clamp01(Mathf.Log(Drained[c] / settings.RiverArea) / growth);
                    float sizeB = Mathf.Clamp01(Mathf.Log(Mathf.Max(Drained[p], Drained[c]) / settings.RiverArea) / growth);
                    // On cells wider than 1 m a stream is at least a cell and a half across, or it would come out dashed.
                    float narrowest = fineCell > 1f ? fineCell * 0.75f : 0f;
                    float halfA = Mathf.Max(narrowest, Mathf.Lerp(widths.x, widths.y, sizeA) * 0.5f);
                    float halfB = Mathf.Max(narrowest, Mathf.Lerp(widths.x, widths.y, sizeB) * 0.5f);
                    float deepA = Mathf.Lerp(depths.x, depths.y, sizeA), deepB = Mathf.Lerp(depths.x, depths.y, sizeB);
                    // The bed line: from the water's own level at each end, so it only ever runs downhill.
                    float topA = Filled[c], topB = Mathf.Min(Filled[p], Filled[c]);
                    float2 along = b - a;
                    float length = math.length(along);
                    float2 direction = along / math.max(length, 1e-4f);
                    float speed = 0.4f + Mathf.Clamp01((topA - topB) / math.max(length, 1e-4f) * 4f);
                    float reach = math.max(halfA, halfB) + math.max(deepA, deepB) + 3f * fineCell;
                    int x0 = Mathf.Max(0, Mathf.FloorToInt((math.min(a.x, b.x) - reach - origin.x) / fineCell));
                    int x1 = Mathf.Min(size - 1, Mathf.CeilToInt((math.max(a.x, b.x) + reach - origin.x) / fineCell));
                    int z0 = Mathf.Max(0, Mathf.FloorToInt((math.min(a.y, b.y) - reach - origin.y) / fineCell));
                    int z1 = Mathf.Min(size - 1, Mathf.CeilToInt((math.max(a.y, b.y) + reach - origin.y) / fineCell));
                    for (int z = z0; z <= z1; z++)
                    {
                        for (int x = x0; x <= x1; x++)
                        {
                            var q = new float2(origin.x + (x + 0.5f) * fineCell, origin.y + (z + 0.5f) * fineCell);
                            float t = math.saturate(math.dot(q - a, along) / math.max(length * length, 1e-6f));
                            float distance = math.distance(q, a + along * t);
                            float half = math.lerp(halfA, halfB, t);
                            float deep = math.lerp(deepA, deepB, t);
                            float top = math.lerp(topA, topB, t);
                            int i = x + z * size;
                            // Eroded land: the straight line between nodes floats over a steep gully's convex floor, where no
                            // water stood and streams came out dashed. The water's level there follows the ground under the
                            // channel's centre, the same across the whole cross-section, so the bed is cut right across and
                            // the whole channel fills (GL503).
                            if (Valleys)
                            {
                                float2 centre = a + along * t;
                                int cx = Mathf.Clamp(Mathf.FloorToInt((centre.x - origin.x) / fineCell), 0, size - 1);
                                int cz = Mathf.Clamp(Mathf.FloorToInt((centre.y - origin.y) / fineCell), 0, size - 1);
                                top = math.min(top, Fine[cx + cz * size]);
                            }
                            float bed = top - deep;
                            float shape = distance < half
                                ? bed + deep * (distance / half) * (distance / half)
                                : top + (distance - half);
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
            }

            // The ground, river water, then lake water of a range of rows.
            public void FillRows(int fromRow, int toRow, WaterMap map)
            {
                int size = Size, n = N;
                float fineCell = FineCell, flowCell = FlowCell;
                for (int z = fromRow; z < toRow; z++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        int i = x + z * size;
                        float carve = Mathf.Max(0f, Fine[i] - target[i]);
                        float groundAfter = Fine[i] - carve;
                        map.Carve[i] = carve;
                        var kind = WaterKind.Dry;
                        float level = 0f;
                        if (surface[i] > groundAfter)
                        {
                            kind = WaterKind.River;
                            level = surface[i];
                            map.Flow[i] = flow[i];
                        }
                        float lake = LakeLevelNear(LakeLevel, n, (x + 0.5f) * fineCell / flowCell, (z + 0.5f) * fineCell / flowCell);
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
            }
        }

        // The coarse cells under the sea: below its level, where the map says sea, and joined to the area's edge by
        // such cells (a hollow inland is a lake, found by the flood).
        private static bool[] SeaCells(float[] h, int n, float2 origin, float cell, WaterExtent extent)
        {
            var sea = new bool[n * n];
            var stack = new Stack<int>();
            bool Wet(int i) => h[i] < extent.SeaLevel && extent.IsSea(CellCentre(origin, cell, n, i));
            for (int i = 0; i < n * n; i++)
            {
                int x = i % n, z = i / n;
                if ((x == 0 || z == 0 || x == n - 1 || z == n - 1) && Wet(i))
                {
                    sea[i] = true;
                    stack.Push(i);
                }
            }
            while (stack.Count > 0)
            {
                int c = stack.Pop();
                int cx = c % n, cz = c / n;
                foreach (int2 d in Neighbours)
                {
                    int x = cx + d.x, z = cz + d.y;
                    if (x < 0 || z < 0 || x >= n || z >= n || sea[x + z * n] || !Wet(x + z * n))
                    {
                        continue;
                    }
                    sea[x + z * n] = true;
                    stack.Push(x + z * n);
                }
            }
            return sea;
        }

        private static readonly int[] Steps = { 1, -1, 0, 0 };
        private static readonly int[] StepsZ = { 0, 0, 1, -1 };

        private static float GroundOf(in WaterMap map, int cell)
        {
            return map.Surface[cell] - map.Depth[cell];
        }

        private static void MakeDry(WaterMap map, int cell)
        {
            float ground = GroundOf(map, cell);
            map.Kind[cell] = (byte)WaterKind.Dry;
            map.Surface[cell] = ground;
            map.Depth[cell] = 0f;
            map.Flow[cell] = float2.zero;
        }

        // Each lake (cells of one level, side by side) is lowered to the lowest dry ground on its rim when that is under
        // its level: the water would spill there. Cells left over the new level dry up. Rivers and other lakes on the rim
        // are its outlets and inlets, not leaks.
        private static IEnumerator ContainLakes(WaterMap map, int size)
        {
            var seen = new bool[size * size];
            var lake = new List<int>();
            var queue = new Queue<int>();
            int looked = 0;
            for (int start = 0; start < seen.Length; start++)
            {
                if (++looked >= ContainCellsPerStep)
                {
                    looked = 0;
                    yield return null;
                }
                if (seen[start] || map.Kind[start] != (byte)WaterKind.Lake)
                {
                    continue;
                }
                float level = map.Surface[start];
                lake.Clear();
                queue.Enqueue(start);
                seen[start] = true;
                while (queue.Count > 0)
                {
                    if (++looked >= ContainCellsPerStep)
                    {
                        looked = 0;
                        yield return null;
                    }
                    int c = queue.Dequeue();
                    lake.Add(c);
                    for (int k = 0; k < 4; k++)
                    {
                        int x = c % size + Steps[k], z = c / size + StepsZ[k];
                        if (x < 0 || z < 0 || x >= size || z >= size)
                        {
                            continue;
                        }
                        int n = x + z * size;
                        if (!seen[n] && map.Kind[n] == (byte)WaterKind.Lake && Mathf.Abs(map.Surface[n] - level) < 0.01f)
                        {
                            seen[n] = true;
                            queue.Enqueue(n);
                        }
                    }
                }
                if (map.HasSea && level <= map.SeaLevel + 0.01f)
                {
                    // The sea has nowhere to drain to: beside a side that isn't an ocean (the void under a cliff) it would
                    // otherwise spill into the low dry ground there and empty, taking the whole coast with it.
                    continue;
                }
                for (int pass = 0; pass < 4 && lake.Count > 0; pass++)
                {
                    float spill = float.PositiveInfinity;
                    foreach (int c in lake)
                    {
                        for (int k = 0; k < 4; k++)
                        {
                            int x = c % size + Steps[k], z = c / size + StepsZ[k];
                            if (x < 0 || z < 0 || x >= size || z >= size)
                            {
                                continue;
                            }
                            int n = x + z * size;
                            if (map.Kind[n] != (byte)WaterKind.Dry)
                            {
                                continue;
                            }
                            float ground = GroundOf(map, n);
                            if (ground < level - 0.02f)
                            {
                                spill = Mathf.Min(spill, ground);
                            }
                        }
                    }
                    if (float.IsPositiveInfinity(spill))
                    {
                        break;
                    }
                    level = spill;
                    for (int i = lake.Count - 1; i >= 0; i--)
                    {
                        int c = lake[i];
                        float ground = GroundOf(map, c);
                        if (ground >= level - 0.05f)
                        {
                            MakeDry(map, c);
                            lake.RemoveAt(i);
                            continue;
                        }
                        map.Surface[c] = level;
                        map.Depth[c] = level - ground;
                    }
                }
            }
        }

        // A river cell beside dry ground lower than its water is lowered to that ground (or dries), a few times over, so
        // the change can run on along the river.
        private static IEnumerator ContainRivers(WaterMap map, int size)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                bool changed = false;
                for (int c = 0; c < size * size; c++)
                {
                    if (c % ContainCellsPerStep == ContainCellsPerStep - 1)
                    {
                        yield return null;
                    }
                    if (map.Kind[c] != (byte)WaterKind.River)
                    {
                        continue;
                    }
                    float level = map.Surface[c];
                    float lowest = level;
                    for (int k = 0; k < 4; k++)
                    {
                        int x = c % size + Steps[k], z = c / size + StepsZ[k];
                        if (x < 0 || z < 0 || x >= size || z >= size)
                        {
                            continue;
                        }
                        int n = x + z * size;
                        if (map.Kind[n] == (byte)WaterKind.Dry)
                        {
                            lowest = Mathf.Min(lowest, GroundOf(map, n));
                        }
                    }
                    if (lowest >= level - 0.05f)
                    {
                        continue;
                    }
                    changed = true;
                    float ground = GroundOf(map, c);
                    if (lowest <= ground + 0.05f)
                    {
                        MakeDry(map, c);
                        continue;
                    }
                    map.Surface[c] = lowest;
                    map.Depth[c] = lowest - ground;
                }
                if (!changed)
                {
                    break;
                }
            }
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

        // The middles of a grid's cells, a range of rows.
        private static void GridPoints(NativeArray<float2> points, float2 origin, float cell, int n, int fromRow, int toRow)
        {
            for (int i = fromRow * n; i < toRow * n; i++)
            {
                points[i] = origin + (new float2(i % n, i / n) + 0.5f) * cell;
            }
        }

        // Persistent buffers: the job runs over several frames.
        private static IEnumerator HeightsSteps(HeightfieldGenerator ground, float2 origin, float cell, int n, Action<float[]> done)
        {
            var points = new NativeArray<float2>(n * n, Allocator.Persistent);
            var heights = new NativeArray<float>(n * n, Allocator.Persistent);
            JobHandle job = default;
            try
            {
                for (int z = 0; z < n; z += DistanceRowsPerStep)
                {
                    GridPoints(points, origin, cell, n, z, Mathf.Min(n, z + DistanceRowsPerStep));
                    yield return null;
                }
                job = ground.SchedulePointHeights(points, heights);
                yield return SlicedWork.WaitFor(job);
                done(heights.ToArray());
            }
            finally
            {
                job.Complete();
                points.Dispose();
                heights.Dispose();
            }
        }

        private static IEnumerator ValleysSteps(HeightfieldGenerator ground, float2 origin, float cell, int n, Action<float[]> done)
        {
            var points = new NativeArray<float2>(n * n, Allocator.Persistent);
            var valleys = new NativeArray<float>(n * n, Allocator.Persistent);
            JobHandle job = default;
            try
            {
                GridPoints(points, origin, cell, n, 0, n);
                job = ground.ScheduleValleys(points, valleys);
                yield return SlicedWork.WaitFor(job);
                done(valleys.ToArray());
            }
            finally
            {
                job.Complete();
                points.Dispose();
                valleys.Dispose();
            }
        }

        // Points each cell on a slope at its steepest lower neighbour (by filled height, per metre). Cells in filled
        // hollows (lakes, flats) keep the flood's link. The receiver is always lower, so it was reached earlier in the
        // flood's order and highest-first accumulation still visits every cell before the one it drains to.
        private static void SteepestDescent(float[] filled, int[] drainsTo, int n, float cell)
        {
            for (int c = 0; c < n * n; c++)
            {
                if (drainsTo[c] < 0)
                {
                    continue;
                }
                int cx = c % n, cz = c / n;
                int best = -1;
                float steepest = 0f;
                foreach (int2 d in Neighbours)
                {
                    int x = cx + d.x, z = cz + d.y;
                    if (x < 0 || z < 0 || x >= n || z >= n)
                    {
                        continue;
                    }
                    int j = x + z * n;
                    float drop = (filled[c] - filled[j]) / (cell * math.length(new float2(d)));
                    if (drop > steepest + 1e-5f)
                    {
                        steepest = drop;
                        best = j;
                    }
                }
                if (best >= 0)
                {
                    drainsTo[c] = best;
                }
            }
        }

        // Chamfer distance (3-4) to the nearest cell of a kind, in metres, capped.
        private static IEnumerator Distances(NativeArray<byte> kinds, WaterKind kind, int size, NativeArray<byte> result)
        {
            const int far = WaterMap.MaxDistance * 3;
            var d = new int[size * size];
            for (int z = 0; z < size; z += DistanceRowsPerStep)
            {
                int end = Mathf.Min(size, z + DistanceRowsPerStep) * size;
                for (int i = z * size; i < end; i++)
                {
                    d[i] = kinds[i] == (byte)kind ? 0 : far;
                }
                yield return null;
            }
            for (int z = 0; z < size; z += DistanceRowsPerStep)
            {
                SweepForward(d, size, z, Mathf.Min(size, z + DistanceRowsPerStep));
                yield return null;
            }
            for (int z = size - 1; z >= 0; z -= DistanceRowsPerStep)
            {
                SweepBack(d, size, z, Mathf.Max(-1, z - DistanceRowsPerStep));
                yield return null;
            }
            for (int z = 0; z < size; z += DistanceRowsPerStep)
            {
                int end = Mathf.Min(size, z + DistanceRowsPerStep) * size;
                for (int i = z * size; i < end; i++)
                {
                    result[i] = (byte)Math.Min(WaterMap.MaxDistance, d[i] / 3);
                }
                yield return null;
            }
        }

        private static void SweepForward(int[] d, int size, int fromRow, int toRow)
        {
            for (int z = fromRow; z < toRow; z++)
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
        }

        private static void SweepBack(int[] d, int size, int fromRow, int toRow)
        {
            for (int z = fromRow; z > toRow; z--)
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
