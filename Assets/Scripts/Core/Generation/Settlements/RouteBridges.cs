using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Where a road (<see cref="CartRoute"/>) crosses a river (GL570), found from the generator's <see cref="WaterMap"/>: each
    /// stretch of road over river water, widened to the banks, is a <see cref="Span"/> a bridge goes across.
    /// </summary>
    public static class RouteBridges
    {
        /// <summary>Metres between the samples taken along the road.</summary>
        private const float Step = 0.5f;

        /// <summary>Metres of bank the deck reaches past the water at each end.</summary>
        public const float BankReach = 1.2f;

        /// <summary>Metres the deck stands over the water's surface at least.</summary>
        public const float Clearance = 0.45f;

        /// <summary>Stretches of water closer than this (metres) are one bridge.</summary>
        private const float MergeGap = 4f;

        /// <summary>One bridge: its ends along the road, in the world, and the deck's height at each end.</summary>
        public readonly struct Span
        {
            public Span(float start, float end, Vector2 from, Vector2 to, float fromHeight, float toHeight, float water)
            {
                Start = start;
                End = end;
                From = from;
                To = to;
                FromHeight = fromHeight;
                ToHeight = toHeight;
                WaterSurface = water;
            }

            /// <summary>Metres along the road at each end of the deck.</summary>
            public readonly float Start, End;

            /// <summary>The deck's two ends (x, z).</summary>
            public readonly Vector2 From, To;

            /// <summary>The height of the top of the deck at each end.</summary>
            public readonly float FromHeight, ToHeight;

            /// <summary>The highest water surface under the deck.</summary>
            public readonly float WaterSurface;

            public float Length => Vector2.Distance(From, To);
        }

        /// <summary>The bridges a road needs over this water map (none when it crosses no river).</summary>
        public static List<Span> Find(CartRoute route, in WaterMap water)
        {
            return route == null ? new List<Span>() : Find(route.Line, water, false);
        }

        /// <summary>
        /// The bridges a road needs over this water map. <paramref name="lakesToo"/>: still water under the road is
        /// bridged as well (a world road may cross a pond or a river's pool; the trade road only knows rivers).
        /// </summary>
        public static List<Span> Find(RoadLine route, in WaterMap water, bool lakesToo)
        {
            var spans = new List<Span>();
            if (route == null || !water.IsCreated)
            {
                return spans;
            }
            bool inWater = false;
            float wetStart = 0f, wetEnd = 0f, surface = float.MinValue;
            var wet = new List<Vector3>(); // start, end, surface
            for (float d = 0f; d <= route.Length + 1e-3f; d += Step)
            {
                Vector2 p = route.Flat(d);
                bool river = water.TrySurfaceAt(new float2(p.x, p.y), out float s, out int cell) && (lakesToo || water.Kind[cell] == (byte)WaterKind.River);
                if (river)
                {
                    if (!inWater)
                    {
                        inWater = true;
                        wetStart = d;
                    }
                    wetEnd = d;
                    surface = Mathf.Max(surface, s);
                }
                else if (inWater)
                {
                    wet.Add(new Vector3(wetStart, wetEnd, surface));
                    inWater = false;
                    surface = float.MinValue;
                }
            }
            if (inWater)
            {
                wet.Add(new Vector3(wetStart, wetEnd, surface));
            }
            for (int i = 0; i < wet.Count; i++)
            {
                Vector3 w = wet[i];
                while (i + 1 < wet.Count && wet[i + 1].x - w.y < MergeGap)
                {
                    w = new Vector3(w.x, wet[i + 1].y, Mathf.Max(w.z, wet[i + 1].z));
                    i++;
                }
                float start = Mathf.Max(0f, w.x - BankReach);
                float end = Mathf.Min(route.Length, w.y + BankReach);
                if (end - start < 1f)
                {
                    continue;
                }
                float level = w.z + Clearance;
                spans.Add(new Span(start, end, route.Flat(start), route.Flat(end),
                    Mathf.Max(level, BankHeight(route, start, -1)), Mathf.Max(level, BankHeight(route, end, 1)), w.z));
            }
            return spans;
        }

        // The ground at a deck's end: the highest the road's height reaches in the three metres beyond it (the bank).
        private static float BankHeight(RoadLine route, float at, int direction)
        {
            float best = route.HeightAlong(at);
            for (float k = 0.5f; k <= 3f; k += 0.5f)
            {
                best = Mathf.Max(best, route.HeightAlong(Mathf.Clamp(at + direction * k, 0f, route.Length)));
            }
            return best + 0.1f;
        }
    }
}
