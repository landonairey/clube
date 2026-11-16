using System;
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
            public Span(float start, float end, Vector2 from, Vector2 to, float fromHeight, float toHeight, float water, float rise = 0f, float ramp = 0.5f)
            {
                Rise = rise;
                Ramp = Mathf.Clamp(ramp, 0.05f, 0.5f);
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

            /// <summary>
            /// GL651: metres the deck's middle stands over the straight line between its ends (a humped deck, for vessels
            /// to pass under, or 0), and the share of its length each end takes to climb there.
            /// </summary>
            public readonly float Rise, Ramp;

            /// <summary>The height of the top of the deck a share <paramref name="t"/> (0 to 1) of the way along it.</summary>
            public float HeightAt(float t)
            {
                t = Mathf.Clamp01(t);
                float height = Mathf.Lerp(FromHeight, ToHeight, t);
                if (Rise <= 0f)
                {
                    return height;
                }
                float climb = Mathf.Clamp01(Mathf.Min(t, 1f - t) / Ramp);
                return height + Rise * climb * climb * (3f - 2f * climb);
            }

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
        /// <param name="seaClearance">
        /// GL651: metres a deck over the sea must stand over it for what sails under, given the deck's middle (x, z);
        /// null for a low deck there too. Such a deck is humped, and reaches back over the land at each end as far as
        /// its climb needs (<see cref="RampSlope"/>).
        /// </param>
        public static List<Span> Find(RoadLine route, in WaterMap water, bool lakesToo, Func<Vector2, float> seaClearance = null)
        {
            var spans = new List<Span>();
            if (route == null || !water.IsCreated)
            {
                return spans;
            }
            bool inWater = false;
            float wetStart = 0f, wetEnd = 0f, surface = float.MinValue;
            var wet = new List<Vector3>(); // start, end, surface
            var overSea = new List<bool>();
            bool sea = false;
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
                    sea = sea || water.IsSea(cell);
                }
                else if (inWater)
                {
                    wet.Add(new Vector3(wetStart, wetEnd, surface));
                    overSea.Add(sea);
                    inWater = sea = false;
                    surface = float.MinValue;
                }
            }
            if (inWater)
            {
                wet.Add(new Vector3(wetStart, wetEnd, surface));
                overSea.Add(sea);
            }
            for (int i = 0; i < wet.Count; i++)
            {
                Vector3 w = wet[i];
                bool salt = overSea[i];
                while (i + 1 < wet.Count && wet[i + 1].x - w.y < MergeGap)
                {
                    w = new Vector3(w.x, wet[i + 1].y, Mathf.Max(w.z, wet[i + 1].z));
                    salt = salt || overSea[i + 1];
                    i++;
                }
                float start = Mathf.Max(0f, w.x - BankReach);
                float end = Mathf.Min(route.Length, w.y + BankReach);
                if (end - start < 1f)
                {
                    continue;
                }
                float level = w.z + Clearance;
                float clear = salt && seaClearance != null ? seaClearance(route.Flat((w.x + w.y) * 0.5f)) : 0f;
                if (clear > Clearance)
                {
                    spans.Add(Humped(route, start, end, w, clear));
                    continue;
                }
                spans.Add(new Span(start, end, route.Flat(start), route.Flat(end),
                    Mathf.Max(level, BankHeight(route, start, -1)), Mathf.Max(level, BankHeight(route, end, 1)), w.z));
            }
            return spans;
        }

        /// <summary>GL651: the longest dry span over a ravine, in metres.</summary>
        public const float RavineMaxSpan = 36f;

        /// <summary>GL651: the least depth of ground under a road's line, in metres, that is bridged rather than gone down into.</summary>
        public const float RavineMinDepth = 4f;

        /// <summary>
        /// GL651: the dry bridges a road wants ("where appropriate, bridges can be used to span distances across ravines,
        /// canyons"): where the ground under its line drops <see cref="RavineMinDepth"/> or more below a straight deck
        /// between two points at most <see cref="RavineMaxSpan"/> apart and nearly level with each other, and the drop is
        /// steep for its width (a gap, not a valley). The deepest first; none over a bridge already <paramref name="taken"/>.
        /// </summary>
        public static List<Span> Ravines(RoadLine route, IReadOnlyList<Span> taken)
        {
            var spans = new List<Span>();
            if (route == null || route.Length < 8f)
            {
                return spans;
            }
            int count = Mathf.FloorToInt(route.Length) + 1;
            var ground = new float[count];
            for (int i = 0; i < count; i++)
            {
                ground[i] = route.HeightAlong(i);
            }
            var found = new List<(float Score, int From, int To, float Low)>();
            for (int from = 0; from < count - 6; from++)
            {
                for (int length = 6; length <= RavineMaxSpan && from + length < count; length += 2)
                {
                    int to = from + length;
                    if (Mathf.Abs(ground[to] - ground[from]) > 0.12f * length)
                    {
                        continue;
                    }
                    float depth = 0f, low = float.MaxValue;
                    bool clear = true;
                    for (int k = from + 1; k < to && clear; k++)
                    {
                        float deck = Mathf.Lerp(ground[from], ground[to], (k - from) / (float)length);
                        clear = ground[k] <= deck + 0.3f;
                        depth = Mathf.Max(depth, deck - ground[k]);
                        low = Mathf.Min(low, ground[k]);
                    }
                    if (clear && depth >= RavineMinDepth && depth >= 0.2f * length)
                    {
                        found.Add((depth - 0.05f * length, from, to, low));
                    }
                }
            }
            found.Sort((a, b) => b.Score.CompareTo(a.Score));
            foreach ((float _, int from, int to, float low) in found)
            {
                bool free = true;
                foreach (Span other in taken)
                {
                    free &= to < other.Start - 3f || from > other.End + 3f;
                }
                foreach (Span other in spans)
                {
                    free &= to < other.Start - 3f || from > other.End + 3f;
                }
                if (free)
                {
                    spans.Add(new Span(from, to, route.Flat(from), route.Flat(to), ground[from] + 0.1f, ground[to] + 0.1f, low));
                }
            }
            return spans;
        }

        /// <summary>GL651: the mean slope of a humped deck's climb (its steepest part is half as steep again).</summary>
        public const float RampSlope = 0.15f;

        // Metres of level deck at the top of a hump: the channel what sails under keeps to.
        private const float Channel = 14f;

        // The most a humped deck reaches back over the land at each end for its climb.
        private const float MostApproach = 80f;

        // GL651: a deck over the sea that vessels pass under. Its middle stands the clearance over the water; each end
        // climbs there at the ramp's slope, from as far back over the land as that takes.
        private static Span Humped(RoadLine route, float start, float end, Vector3 wet, float clear)
        {
            float top = wet.z + clear;
            for (int pass = 0; pass < 3; pass++)
            {
                float fromHeight = Mathf.Max(wet.z + Clearance, BankHeight(route, start, -1)), toHeight = Mathf.Max(wet.z + Clearance, BankHeight(route, end, 1));
                float climb = Mathf.Max(0f, top - Mathf.Min(fromHeight, toHeight)) / RampSlope;
                float lacking = climb * 2f + Channel - (end - start);
                if (lacking <= 0.5f)
                {
                    break;
                }
                start = Mathf.Max(0f, Mathf.Max(wet.x - BankReach - MostApproach, start - lacking * 0.5f));
                end = Mathf.Min(route.Length, Mathf.Min(wet.y + BankReach + MostApproach, end + lacking * 0.5f));
            }
            float a = Mathf.Max(wet.z + Clearance, BankHeight(route, start, -1)), b = Mathf.Max(wet.z + Clearance, BankHeight(route, end, 1));
            float length = Mathf.Max(1f, end - start);
            float ramp = Mathf.Clamp((length - Channel) * 0.5f / length, 0.1f, 0.5f);
            // No higher than its length lets it climb: a short deck between high banks is as high as it can be.
            float rise = Mathf.Clamp(top - (a + b) * 0.5f, 0f, ramp * length * RampSlope * 1.2f);
            return new Span(start, end, route.Flat(start), route.Flat(end), a, b, wet.z, rise, ramp);
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
