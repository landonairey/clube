using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>What a town's site is looked for with (GL399).</summary>
    public sealed class TownSiteRequest
    {
        /// <summary>Where the town's centre may lie (x, z in the world's metres): the fixed area less a margin, or any square.</summary>
        public Rect Area;

        /// <summary>The player's spawn (x, z): the town keeps clear of it and, with <see cref="MaxDistance"/>, near it.</summary>
        public Vector2 Spawn;

        /// <summary>Most metres from the spawn to the town's centre; 0 for no limit.</summary>
        public float MaxDistance;

        /// <summary>Fewest metres from the spawn to the town's centre, so the starting land stays the player's.</summary>
        public float KeepFromSpawn = 60f;

        /// <summary>Width of the town's square footprint, walls included, in metres: it must be dry and fairly flat.</summary>
        public float Footprint = 44f;

        /// <summary>Metres between the sites tried.</summary>
        public float Step = 8f;

        /// <summary>A point on the grid of sites tried: sites lie at it plus whole steps (GL408: the centres of land parcels).</summary>
        public Vector2 GridOrigin;

        /// <summary>
        /// The ores to be near, one group each (tin; copper; silver; gold): a group is near when any of its materials' nodes is.
        /// </summary>
        public List<byte[]> OreGroups = new List<byte[]>();

        /// <summary>A group's nearest node within this many metres counts as at the town's door.</summary>
        public float OreNear = 40f;

        /// <summary>Water within this many metres of the footprint's edge counts as at the town's door.</summary>
        public float WaterNear = 30f;

        /// <summary>Score per metre of height across the footprint: steep sites need more levelling.</summary>
        public float RiseWeight = 12f;

        /// <summary>Most metres of height across the footprint a site may have.</summary>
        public float MaxRise = 10f;

        /// <summary>Lowest and highest ground heights searched, in metres (the world's floor and ceiling).</summary>
        public float Bottom;
        public float Top = 160f;
    }

    /// <summary>The site picked for a town (GL399), and why: how far it is from each ore group and from water.</summary>
    public readonly struct TownPlacement
    {
        public TownPlacement(Vector2 centre, float ground, float rise, float score, float waterDistance, float[] oreDistances, Vector3[] oreNodes = null)
        {
            Centre = centre;
            Ground = ground;
            Rise = rise;
            Score = score;
            WaterDistance = waterDistance;
            OreDistances = oreDistances;
            OreNodes = oreNodes;
        }

        /// <summary>False when no site in the area qualified.</summary>
        public bool Found => OreDistances != null;

        /// <summary>The town's centre (x, z), in the world's metres.</summary>
        public Vector2 Centre { get; }

        /// <summary>The height the town's ground is levelled to: the middle of the ground across its footprint.</summary>
        public float Ground { get; }

        /// <summary>Metres between the lowest and highest ground across the footprint, before levelling.</summary>
        public float Rise { get; }

        /// <summary>Lower is better: metres short of each ore group and of water, plus the levelling.</summary>
        public float Score { get; }

        /// <summary>Metres from the footprint's centre to the nearest water; infinity when there's none.</summary>
        public float WaterDistance { get; }

        /// <summary>Metres to the nearest node of each ore group, in the request's order (infinity for none).</summary>
        public float[] OreDistances { get; }

        /// <summary>The nearest node of each ore group (its centre), in the request's order; null for a group with none (GL414).</summary>
        public Vector3[] OreNodes { get; }
    }

    /// <summary>
    /// Picks where the town stands (GL399, first pass of ST1 and ST5) in one go, by running a <see cref="TownSiteSearch"/> to
    /// the end (tests, tools). The game runs the search a few milliseconds a frame instead.
    /// </summary>
    public static class TownSiteFinder
    {
        /// <summary>Finds the best site; <see cref="TownPlacement.Found"/> is false when none qualified.</summary>
        public static TownPlacement Find(TownSiteRequest request, ITerrainGenerator generator, OreField ores, WaterMap water)
        {
            var search = new TownSiteSearch(request, generator, ores, water);
            while (!search.Step(1000f))
            {
            }
            return search.Result;
        }
    }

    /// <summary>
    /// The search for the town's site (GL399, first pass of ST1 and ST5), from the seed alone, without generating any chunk.
    /// Every site on a grid over the area is scored by how far it is from a node of each ore group (<see cref="OreField"/>)
    /// and from rivers and lakes (the <see cref="WaterMap"/>); the best few are then checked for dry, flat enough ground
    /// (the generator's heights), and the best of those wins. The same world always gives the same site.
    /// </summary>
    /// <remarks>
    /// Placing ore nodes samples the terrain for every try, so it's the costly part: only the ore cells that reach from the
    /// ground down to the deepest ore are looked at, and the work is done in steps (<see cref="Step"/>) so the game can
    /// spread it over frames. The ore field's cache isn't touched, so generation keeps its cells.
    /// </remarks>
    public sealed class TownSiteSearch
    {
        // Sites whose ground is checked after the cheap ore and water scoring.
        private const int Shortlist = 160;

        // Metres a missing ore group or no water counts as: far worse than any real distance.
        private const float Missing = 2000f;

        // Cells of the coarse water distance grid, in metres.
        private const float WaterCell = 4f;

        private readonly TownSiteRequest request;
        private readonly ITerrainGenerator generator;
        private readonly OreField ores;
        private readonly WaterMap water;
        private readonly Rect area;
        private readonly List<Vector3>[] nodes;
        private readonly List<Vector3Int> oreCells = new List<Vector3Int>();
        private readonly List<(float score, Vector2 centre, float water, float[] ore, Vector3[] near)> candidates = new List<(float, Vector2, float, float[], Vector3[])>();
        private readonly System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
        private WaterDistance waterDistance;
        private int phase;
        private int next;
        private TownPlacement best;

        public TownSiteSearch(TownSiteRequest request, ITerrainGenerator generator, OreField ores, WaterMap water)
        {
            this.request = request;
            this.generator = generator;
            this.ores = ores;
            this.water = water;
            Rect a = request.Area;
            if (request.MaxDistance > 0f)
            {
                a = Intersect(a, new Rect(request.Spawn - Vector2.one * request.MaxDistance, Vector2.one * request.MaxDistance * 2f));
            }
            if (water.IsCreated)
            {
                // Only the map's area has water: a town beyond it would have none.
                var mapArea = new Rect(new Vector2(water.Origin.x, water.Origin.y), new Vector2(water.Size.x, water.Size.y) * water.CellSize);
                a = Intersect(a, mapArea);
            }
            area = a;
            nodes = new List<Vector3>[request.OreGroups.Count];
            for (int g = 0; g < nodes.Length; g++)
            {
                nodes[g] = new List<Vector3>();
            }
        }

        /// <summary>True once the search has finished.</summary>
        public bool IsDone { get; private set; }

        /// <summary>The site found, once done; <see cref="TownPlacement.Found"/> is false when none qualified.</summary>
        public TownPlacement Result => best;

        /// <summary>Works for about <paramref name="budgetMilliseconds"/>; returns true once the search is done.</summary>
        public bool Step(float budgetMilliseconds)
        {
            watch.Restart();
            while (!IsDone && watch.Elapsed.TotalMilliseconds < budgetMilliseconds)
            {
                switch (phase)
                {
                    case 0:
                        Prepare();
                        break;
                    case 1:
                        CollectNextCell();
                        break;
                    case 2:
                        ScoreCandidates();
                        break;
                    default:
                        CheckNextCandidate();
                        break;
                }
            }
            return IsDone;
        }

        // The water distances, and the ore cells near the ground over the area and a margin round it.
        private void Prepare()
        {
            phase = 1;
            next = 0;
            if (area.width < 0f || area.height < 0f)
            {
                IsDone = true;
                return;
            }
            waterDistance = water.IsCreated ? new WaterDistance(water) : null;
            if (ores == null || nodes.Length == 0 || generator == null)
            {
                return;
            }
            float size = ores.CellSize;
            float deepest = ores.MaxNodeDepth;
            // Nodes a little past the area count too: a site on its edge may be nearest to one outside.
            float margin = request.OreNear;
            Vector2Int low = Vector2Int.FloorToInt((area.min - Vector2.one * margin) / size);
            Vector2Int high = Vector2Int.FloorToInt((area.max + Vector2.one * margin) / size);
            for (int cz = low.y; cz <= high.y; cz++)
            {
                for (int cx = low.x; cx <= high.x; cx++)
                {
                    // The ground over the cell column, at its corners and middle: nodes lie between it and the deepest ore.
                    float lowest = float.PositiveInfinity, highest = float.NegativeInfinity;
                    for (int i = 0; i < 5; i++)
                    {
                        float x = (cx + (i == 4 ? 0.5f : i % 2)) * size;
                        float z = (cz + (i == 4 ? 0.5f : i / 2)) * size;
                        float h = TerrainSurface.HeightAt(generator, x, z, request.Bottom, request.Top);
                        lowest = Mathf.Min(lowest, h);
                        highest = Mathf.Max(highest, h);
                    }
                    int y0 = Mathf.Max(Mathf.FloorToInt((lowest - deepest - 1f) / size), Mathf.FloorToInt(request.Bottom / size));
                    int y1 = Mathf.FloorToInt((highest + 1f) / size);
                    for (int cy = y0; cy <= y1; cy++)
                    {
                        oreCells.Add(new Vector3Int(cx, cy, cz));
                    }
                }
            }
        }

        private void CollectNextCell()
        {
            if (next >= oreCells.Count)
            {
                phase = 2;
                return;
            }
            foreach (OreNode node in ores.NodesInCellUncached(oreCells[next++]))
            {
                for (int g = 0; g < nodes.Length; g++)
                {
                    if (Array.IndexOf(request.OreGroups[g], node.Material) >= 0)
                    {
                        nodes[g].Add(node.Centre);
                    }
                }
            }
        }

        // Every site on the grid, scored by ore and water alone, best first.
        private void ScoreCandidates()
        {
            float halfFootprint = request.Footprint * 0.5f;
            float step = Mathf.Max(1f, request.Step);
            for (float z = FirstOnGrid(area.yMin, request.GridOrigin.y, step); z <= area.yMax + 1e-3f; z += step)
            {
                for (float x = FirstOnGrid(area.xMin, request.GridOrigin.x, step); x <= area.xMax + 1e-3f; x += step)
                {
                    var centre = new Vector2(x, z);
                    float fromSpawn = Vector2.Distance(centre, request.Spawn);
                    if (fromSpawn < request.KeepFromSpawn || (request.MaxDistance > 0f && fromSpawn > request.MaxDistance))
                    {
                        continue;
                    }
                    float wet = waterDistance?.At(centre) ?? float.PositiveInfinity;
                    if (wet < halfFootprint * 1.42f)
                    {
                        // Water inside the footprint (or at its corners): no room for the town.
                        continue;
                    }
                    var oreDistances = new float[nodes.Length];
                    var near = new Vector3[nodes.Length];
                    float score = 0f;
                    for (int g = 0; g < nodes.Length; g++)
                    {
                        oreDistances[g] = Nearest(nodes[g], centre, out near[g]);
                        score += Mathf.Max(0f, Mathf.Min(oreDistances[g], Missing) - request.OreNear);
                    }
                    if (waterDistance != null)
                    {
                        score += Mathf.Max(0f, Mathf.Min(wet - halfFootprint, Missing) - request.WaterNear);
                    }
                    // A little for the walk from the spawn, so of two equal sites the nearer wins.
                    score += fromSpawn * 0.01f;
                    candidates.Add((score, centre, wet, oreDistances, near));
                }
            }
            candidates.Sort((a, b) => a.score != b.score ? a.score.CompareTo(b.score)
                : a.centre.y != b.centre.y ? a.centre.y.CompareTo(b.centre.y) : a.centre.x.CompareTo(b.centre.x));
            phase = 3;
            next = 0;
        }

        // The ground under the next site on the shortlist; the best score with the levelling counted wins.
        private void CheckNextCandidate()
        {
            if (next >= Mathf.Min(Shortlist, candidates.Count))
            {
                IsDone = true;
                return;
            }
            var candidate = candidates[next++];
            if (!TryGround(candidate.centre, out float ground, out float rise))
            {
                return;
            }
            float score = candidate.score + rise * request.RiseWeight;
            if (!best.Found || score < best.Score)
            {
                best = new TownPlacement(candidate.centre, ground, rise, score, candidate.water, candidate.ore, candidate.near);
            }
        }

        private static Rect Intersect(Rect a, Rect b)
        {
            float xMin = Mathf.Max(a.xMin, b.xMin), yMin = Mathf.Max(a.yMin, b.yMin);
            float xMax = Mathf.Min(a.xMax, b.xMax), yMax = Mathf.Min(a.yMax, b.yMax);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        // The horizontal distance to the nearest of the points, and which it is.
        private static float Nearest(List<Vector3> points, Vector2 at, out Vector3 nearest)
        {
            float bestDistance = float.PositiveInfinity;
            nearest = default;
            foreach (Vector3 point in points)
            {
                float distance = (new Vector2(point.x, point.z) - at).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = point;
                }
            }
            return Mathf.Sqrt(bestDistance);
        }

        // The first grid line at or after a value: the origin plus whole steps.
        private static float FirstOnGrid(float from, float origin, float step)
        {
            return origin + Mathf.Ceil((from - origin) / step - 1e-4f) * step;
        }

        // The ground across the footprint, sampled on a 5 x 5 grid: its middle height and how much it rises; false when
        // it is too steep or any of it is under water.
        private bool TryGround(Vector2 centre, out float ground, out float rise)
        {
            ground = 0f;
            rise = 0f;
            if (generator == null)
            {
                return false;
            }
            const int Across = 5;
            var heights = new float[Across * Across];
            float half = request.Footprint * 0.5f;
            int i = 0;
            for (int b = 0; b < Across; b++)
            {
                for (int a = 0; a < Across; a++)
                {
                    float x = centre.x - half + request.Footprint * a / (Across - 1);
                    float z = centre.y - half + request.Footprint * b / (Across - 1);
                    if (water.IsCreated && water.TrySurfaceAt(new float2(x, z), out _, out _))
                    {
                        return false;
                    }
                    heights[i++] = TerrainSurface.HeightAt(generator, x, z, request.Bottom, request.Top);
                }
            }
            Array.Sort(heights);
            rise = heights[heights.Length - 1] - heights[0];
            ground = heights[heights.Length / 2];
            // Ground at the world floor or ceiling is not ground to build on.
            return rise <= request.MaxRise && ground > request.Bottom + 1f && ground < request.Top - 10f;
        }


        /// <summary>Metres from any point to the nearest water, on a coarse grid over the water map (a chamfer distance).</summary>
        private sealed class WaterDistance
        {
            private readonly float[] distance;
            private readonly int width;
            private readonly int height;
            private readonly Vector2 origin;

            public WaterDistance(WaterMap water)
            {
                origin = new Vector2(water.Origin.x, water.Origin.y);
                int per = Mathf.Max(1, Mathf.RoundToInt(WaterCell / water.CellSize));
                width = (water.Size.x + per - 1) / per;
                height = (water.Size.y + per - 1) / per;
                distance = new float[width * height];
                for (int i = 0; i < distance.Length; i++)
                {
                    distance[i] = float.PositiveInfinity;
                }
                for (int y = 0; y < water.Size.y; y++)
                {
                    for (int x = 0; x < water.Size.x; x++)
                    {
                        int cell = x + y * water.Size.x;
                        if (water.Kind[cell] != (byte)WaterKind.Dry && water.Depth[cell] > 0f)
                        {
                            distance[x / per + y / per * width] = 0f;
                        }
                    }
                }
                float size = per * water.CellSize;
                float diagonal = size * 1.41421356f;
                // Two passes of a 3 x 3 chamfer: down and right, then up and left.
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Relax(x, y, x - 1, y, size);
                        Relax(x, y, x, y - 1, size);
                        Relax(x, y, x - 1, y - 1, diagonal);
                        Relax(x, y, x + 1, y - 1, diagonal);
                    }
                }
                for (int y = height - 1; y >= 0; y--)
                {
                    for (int x = width - 1; x >= 0; x--)
                    {
                        Relax(x, y, x + 1, y, size);
                        Relax(x, y, x, y + 1, size);
                        Relax(x, y, x + 1, y + 1, diagonal);
                        Relax(x, y, x - 1, y + 1, diagonal);
                    }
                }
                CellSize = size;
            }

            private float CellSize { get; }

            private void Relax(int x, int y, int nx, int ny, float cost)
            {
                if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                {
                    return;
                }
                float through = distance[nx + ny * width] + cost;
                if (through < distance[x + y * width])
                {
                    distance[x + y * width] = through;
                }
            }

            public float At(Vector2 point)
            {
                int x = Mathf.FloorToInt((point.x - origin.x) / CellSize);
                int y = Mathf.FloorToInt((point.y - origin.y) / CellSize);
                if (x < 0 || y < 0 || x >= width || y >= height)
                {
                    return float.PositiveInfinity;
                }
                return distance[x + y * width];
            }
        }
    }
}
