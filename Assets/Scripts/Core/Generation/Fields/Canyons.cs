using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Canyons (GL737) as plain values: in the dry lands the ground stands as a tableland over its water, so every river,
    /// lake and stretch of sea there lies in a canyon. The tableland's height is <see cref="Depth"/> (a little more or less
    /// by a noise) times the land's dryness; from the water out it stays at the water's own ground for a floor, then climbs
    /// in <see cref="Ledges"/> steps to the rim, each a steep face and a level ledge, so the walls show the strata. A rim
    /// round the dry land's edge climbs the same way, so the tableland ends in cliffs too.
    /// </summary>
    /// <remarks>
    /// Read from a grid of each cell's distance to the nearest water (<see cref="Build"/>, on the water map's cells), which
    /// needs the water first: the canyons are added after the water is worked out, and only ever raise dry ground away
    /// from it, so no water is left standing over ground or ground under water. Burst-compatible like the water map.
    /// </remarks>
    public struct Canyons
    {
        public const int Salt = 0x43414E59;

        public float2 Origin;
        public float CellSize;
        public int2 Size;

        /// <summary>Metres from each cell to the nearest water, capped at <see cref="MaxReach"/>.</summary>
        [ReadOnly] public NativeArray<byte> Reach;

        public uint Seed;
        public float Depth;
        public float Floor;
        public float Wall;
        public int Ledges;
        public float DryFrom;
        public float DryTo;

        /// <summary>The climate's region size, which the moisture is read at (<see cref="BiomeField.MoistureAt"/>).</summary>
        public float RegionSize;

        public const int MaxReach = 255;

        // Share of each step that is face; the rest is ledge.
        private const float FaceShare = 0.4f;

        // Metres inside the grid's edge over which the tableland comes down to the land, so the grid's end makes no cliff.
        private const float EdgeFade = 96f;

        private static Canyons none;

        /// <summary>No canyons: empty arrays rather than unassigned ones, since a job refuses unassigned containers even unread.</summary>
        public static Canyons None
        {
            get
            {
                if (!none.Reach.IsCreated)
                {
                    none = new Canyons { Reach = new NativeArray<byte>(0, Allocator.Persistent) };
                    AppDomain.CurrentDomain.DomainUnload += (_, __) =>
                    {
                        if (none.Reach.IsCreated)
                        {
                            none.Reach.Dispose();
                        }
                    };
                }
                return none;
            }
        }

        public bool IsCreated => Reach.IsCreated && Reach.Length > 0 && Depth > 0f;

        /// <summary>Metres the ground at (x, z) is raised, where the moisture is <paramref name="moisture"/>.</summary>
        public float Lift(float x, float z, float moisture)
        {
            if (!IsCreated)
            {
                return 0f;
            }
            float dry = 1f - math.smoothstep(DryFrom, DryTo, moisture);
            if (dry <= 0f)
            {
                return 0f;
            }
            float2 p = (new float2(x, z) - Origin) / CellSize;
            float inside = math.cmin(math.min(p, (float2)Size - p)) * CellSize;
            if (inside <= 0f)
            {
                return 0f;
            }
            // The floor and the walls wander in width, so a canyon narrows and widens along its run.
            float wander = 0.5f + 0.5f * math.clamp(GradientNoise.Fractal(Seed, x / 180f, z / 180f, 2) * 1.6f, -1f, 1f);
            float floor = Floor * (0.5f + wander);
            float wall = Wall * (0.7f + 0.6f * wander);
            float climb = math.saturate((ReachAt(x, z) - floor) / wall);
            float t = math.min(climb, dry) * math.smoothstep(0f, EdgeFade, inside);
            if (t <= 0f)
            {
                return 0f;
            }
            float depth = Depth * (0.8f + 0.4f * (0.5f + 0.5f * GradientNoise.At(Seed + 1u, x / 900f, z / 900f)));
            return depth * Steps(t, Ledges);
        }

        /// <summary>0 to 1 in <paramref name="ledges"/> steps: each a steep face, then a level ledge.</summary>
        public static float Steps(float t, int ledges)
        {
            float u = math.saturate(t) * ledges;
            float step = math.floor(u);
            float within = u - step;
            return (step + math.smoothstep(1f - FaceShare, 1f, within)) / ledges;
        }

        /// <summary>Metres to the nearest water at (x, z), smoothly between cells; <see cref="MaxReach"/> past the grid.</summary>
        public float ReachAt(float x, float z)
        {
            float2 p = (new float2(x, z) - Origin) / CellSize - 0.5f;
            int2 c = (int2)math.floor(p);
            float2 t = p - c;
            float a = At(c.x, c.y), b = At(c.x + 1, c.y), d = At(c.x, c.y + 1), e = At(c.x + 1, c.y + 1);
            return math.lerp(math.lerp(a, b, t.x), math.lerp(d, e, t.x), t.y);
        }

        private float At(int x, int z)
        {
            if (x < 0 || z < 0 || x >= Size.x || z >= Size.y)
            {
                return MaxReach;
            }
            return Reach[x + z * Size.x];
        }

        public void Dispose()
        {
            if (Reach.IsCreated && Reach.Length > 0)
            {
                Reach.Dispose();
            }
        }
    }

    /// <summary>
    /// Works out and keeps <see cref="Canyons"/>' reach grids (GL737): kept apart from the struct, which jobs copy, so no
    /// managed static is anywhere near Burst.
    /// </summary>
    public static class CanyonReach
    {
        private static readonly Dictionary<string, Canyons> Cache = new Dictionary<string, Canyons>();
        private static readonly List<string> CacheOrder = new List<string>();
        private const int CacheSize = 8;
        private static bool hooked;

        /// <summary>
        /// The canyons over a water map, its reach grid worked out once per key and kept (the last few, like
        /// <see cref="Hydrology.For"/>'s maps); <see cref="Canyons.None"/> when the shape asks for none or there is no water.
        /// </summary>
        public static Canyons For(string key, Canyons shape, WaterMap water)
        {
            if (shape.Depth <= 0f || !water.IsCreated)
            {
                return Canyons.None;
            }
            if (!hooked)
            {
                hooked = true;
                AppDomain.CurrentDomain.DomainUnload += (_, __) => DisposeAll();
                Application.quitting += DisposeAll;
            }
            if (!Cache.TryGetValue(key, out Canyons reach))
            {
                reach = Build(water);
                Cache[key] = reach;
                CacheOrder.Add(key);
                if (CacheOrder.Count > CacheSize)
                {
                    Cache[CacheOrder[0]].Dispose();
                    Cache.Remove(CacheOrder[0]);
                    CacheOrder.RemoveAt(0);
                }
            }
            shape.Origin = reach.Origin;
            shape.CellSize = reach.CellSize;
            shape.Size = reach.Size;
            shape.Reach = reach.Reach;
            return shape;
        }

        /// <summary>Frees every cached grid (tests, domain unload).</summary>
        public static void DisposeAll()
        {
            foreach (Canyons canyons in Cache.Values)
            {
                canyons.Dispose();
            }
            Cache.Clear();
            CacheOrder.Clear();
        }

        /// <summary>The reach grid over a water map (uncached; the caller owns the result): every cell's metres to the nearest wet cell.</summary>
        public static Canyons Build(WaterMap water)
        {
            int count = water.Size.x * water.Size.y;
            var reach = new NativeArray<byte>(count, Allocator.Persistent);
            var scratch = new NativeArray<int>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            try
            {
                new ReachJob { Kind = water.Kind, Size = water.Size, CellSize = water.CellSize, Distances = scratch, Reach = reach }.Run();
            }
            finally
            {
                scratch.Dispose();
            }
            return new Canyons { Origin = water.Origin, CellSize = water.CellSize, Size = water.Size, Reach = reach };
        }

        // Chamfer distance (3-4) to the nearest wet cell, in metres, capped: two sweeps over the grid.
        [BurstCompile]
        private struct ReachJob : IJob
        {
            [ReadOnly] public NativeArray<byte> Kind;
            public int2 Size;
            public float CellSize;
            public NativeArray<int> Distances;
            public NativeArray<byte> Reach;

            public void Execute()
            {
                int w = Size.x, h = Size.y;
                int far = (int)math.ceil(Canyons.MaxReach / CellSize) * 3 + 4;
                NativeArray<int> d = Distances;
                for (int i = 0; i < d.Length; i++)
                {
                    d[i] = Kind[i] != 0 ? 0 : far;
                }
                for (int z = 0; z < h; z++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int i = x + z * w;
                        int v = d[i];
                        if (x > 0) v = math.min(v, d[i - 1] + 3);
                        if (z > 0) v = math.min(v, d[i - w] + 3);
                        if (x > 0 && z > 0) v = math.min(v, d[i - w - 1] + 4);
                        if (x < w - 1 && z > 0) v = math.min(v, d[i - w + 1] + 4);
                        d[i] = v;
                    }
                }
                for (int z = h - 1; z >= 0; z--)
                {
                    for (int x = w - 1; x >= 0; x--)
                    {
                        int i = x + z * w;
                        int v = d[i];
                        if (x < w - 1) v = math.min(v, d[i + 1] + 3);
                        if (z < h - 1) v = math.min(v, d[i + w] + 3);
                        if (x < w - 1 && z < h - 1) v = math.min(v, d[i + w + 1] + 4);
                        if (x > 0 && z < h - 1) v = math.min(v, d[i + w - 1] + 4);
                        d[i] = v;
                        Reach[i] = (byte)math.min(Canyons.MaxReach, (int)math.round(v / 3f * CellSize));
                    }
                }
            }
        }
    }
}
