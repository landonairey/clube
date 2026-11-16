using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The trade cart's road (GL477, first pass of ST8's roads): from the trading post to a distant town at least
    /// <see cref="MinParcels"/> parcels away. Roughed in: a line broken every <c>spacing</c> metres, each bend moved
    /// sideways to whichever of a few spots is nearest the height of the last, so it skirts hills rather than climbing
    /// them, then rounded once. Heights along it are kept every metre, so the cart can be placed cheaply each frame.
    /// </summary>
    public sealed class CartRoute
    {
        /// <summary>The fewest parcels between the trading post and the distant town.</summary>
        public const int MinParcels = 10;

        // Extra parcels the distant town may lie past the fewest, and how many directions are tried in a fixed area.
        private const int ExtraParcels = 3;
        private const int Directions = 16;

        // Metres between the heights kept along the road.
        private const float HeightSpacing = 1f;

        private readonly List<Vector2> points = new List<Vector2>();

        /// <summary>The road's corners, from the trading post's end to the distant town's.</summary>
        public IReadOnlyList<Vector2> Points => points;

        /// <summary>The road's length in metres.</summary>
        public float Length => Line.Length;

        /// <summary>
        /// Where the distant town is (a parcel's centre): <see cref="MinParcels"/> to <see cref="MinParcels"/> +
        /// <see cref="ExtraParcels"/> parcels from <paramref name="post"/>, in a direction from the seed within
        /// <paramref name="cone"/> degrees of <paramref name="facing"/> (out of the trading post's gate, so the road doesn't
        /// cross the town). In a fixed area (<paramref name="area"/>), the direction in the cone that keeps the road longest
        /// inside it, so most of the road can be seen.
        /// </summary>
        public static Vector2 DistantTown(Vector2 post, float parcelSize, int seed, Rect? area, Vector2 facing, float cone)
        {
            var random = new System.Random(seed * 31 + 477);
            float facingAngle = Mathf.Atan2(facing.y, facing.x);
            float coneRadians = cone * Mathf.Deg2Rad;
            float angle = facingAngle + ((float)random.NextDouble() * 2f - 1f) * coneRadians;
            float parcels = MinParcels + 0.5f + (float)random.NextDouble() * ExtraParcels;
            if (area.HasValue)
            {
                float best = -1f;
                float start = angle;
                for (int i = 0; i < Directions; i++)
                {
                    // Across the cone, starting from the seed's direction.
                    float a = facingAngle + Mathf.Repeat(start - facingAngle + coneRadians + i * 2f * coneRadians / Directions, 2f * coneRadians) - coneRadians;
                    float inside = InsideLength(post, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), area.Value);
                    if (inside > best + 0.5f)
                    {
                        best = inside;
                        angle = a;
                    }
                }
            }
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 parcel = Snap(post + direction * (parcels * parcelSize), parcelSize);
            // Snapping to a parcel's centre can bring it a little closer; push it out until it's far enough.
            while ((parcel - Snap(post, parcelSize)).magnitude < MinParcels * parcelSize - 0.01f)
            {
                parcel = Snap(parcel + direction * parcelSize, parcelSize);
            }
            return parcel;
        }

        // The centre of the parcel a point is on.
        private static Vector2 Snap(Vector2 point, float parcelSize)
        {
            return new Vector2((Mathf.Floor(point.x / parcelSize) + 0.5f) * parcelSize, (Mathf.Floor(point.y / parcelSize) + 0.5f) * parcelSize);
        }

        // Metres from a point inside the area to its edge along a direction.
        private static float InsideLength(Vector2 from, Vector2 direction, Rect area)
        {
            const float Step = 4f;
            float length = 0f;
            while (length < 10000f && area.Contains(from + direction * (length + Step)))
            {
                length += Step;
            }
            return length;
        }

        // How much a metre of road costs per unit of steepness squared (GL510): a 45-degree face costs 1 + this per metre.
        private const float SlopeCost = 40f;

        // The cost of a stretch from a to b (ground heights ha, hb): its length, dearer where it climbs.
        private static float StretchCost(Vector2 a, Vector2 b, float ha, float hb, Func<float, float, float> heightAt)
        {
            float length = Mathf.Max((b - a).magnitude, 1e-3f);
            // The middle too, so a face between two bends isn't stepped over.
            Vector2 m = (a + b) * 0.5f;
            float hm = heightAt(m.x, m.y);
            float half = length * 0.5f;
            float s1 = Mathf.Abs(hm - ha) / half, s2 = Mathf.Abs(hb - hm) / half;
            return half * (2f + SlopeCost * (s1 * s1 + s2 * s2));
        }

        /// <summary>Plans the road from <paramref name="from"/> to <paramref name="to"/> over the ground <paramref name="heightAt"/>.</summary>
        /// <param name="spacing">Metres between bends.</param>
        /// <param name="sway">The most metres a bend may move off the straight line.</param>
        public CartRoute(Vector2 from, Vector2 to, Func<float, float, float> heightAt, int seed, float spacing = 16f, float sway = 30f)
        {
            if (heightAt == null)
            {
                throw new ArgumentNullException(nameof(heightAt));
            }
            Vector2 along = to - from;
            float straight = along.magnitude;
            Vector2 forward = straight > 1e-4f ? along / straight : Vector2.right;
            var side = new Vector2(-forward.y, forward.x);
            int bends = Mathf.Max(1, Mathf.CeilToInt(straight / Mathf.Max(1f, spacing)));

            // GL510: the cheapest way through a bend at each station, where a stretch costs its length times
            // 1 + SlopeCost * (steepness squared), so the road goes round a mountain's face rather than up it.
            const int Levels = 7;
            var offsets = new float[bends, Levels];
            for (int i = 1; i < bends; i++)
            {
                float reach = sway * Mathf.Min(1f, Mathf.Min(i, bends - i) / 2f);
                for (int j = 0; j < Levels; j++)
                {
                    offsets[i, j] = reach * (j / (Levels - 1f) * 2f - 1f);
                }
            }
            var cost = new float[bends + 1, Levels];
            var from_ = new int[bends + 1, Levels];
            Vector2 PointAt(int station, int level)
            {
                return station >= bends ? to : from + along * (station / (float)bends) + side * (station == 0 ? 0f : offsets[station, level]);
            }
            // Ground heights at each candidate (the ends have one).
            var ground = new float[bends + 1, Levels];
            for (int i = 0; i <= bends; i++)
            {
                for (int j = 0; j < (i == 0 || i == bends ? 1 : Levels); j++)
                {
                    Vector2 p = PointAt(i, j);
                    ground[i, j] = heightAt(p.x, p.y);
                }
            }
            for (int i = 1; i <= bends; i++)
            {
                int levels = i == bends ? 1 : Levels;
                int previous = i == 1 ? 1 : Levels;
                for (int j = 0; j < levels; j++)
                {
                    float best = float.MaxValue;
                    int bestFrom = 0;
                    for (int k = 0; k < previous; k++)
                    {
                        float c = (i == 1 ? 0f : cost[i - 1, k]) + StretchCost(PointAt(i - 1, k), PointAt(i, j), ground[i - 1, k], ground[i, j], heightAt);
                        // A little against turning, so the road doesn't zigzag on level ground.
                        if (i > 1 && i < bends)
                        {
                            c += 0.02f * Mathf.Abs(offsets[i, j] - offsets[i - 1, k]);
                        }
                        if (c < best)
                        {
                            best = c;
                            bestFrom = k;
                        }
                    }
                    cost[i, j] = best;
                    from_[i, j] = bestFrom;
                }
            }
            var rough = new List<Vector2>(bends + 1) { to };
            int at = 0;
            for (int i = bends; i >= 2; i--)
            {
                at = from_[i, at];
                rough.Add(PointAt(i - 1, at));
            }
            rough.Add(from);
            rough.Reverse();

            // Rounded once (Chaikin), keeping the ends.
            points.Add(rough[0]);
            for (int i = 0; i < rough.Count - 1; i++)
            {
                Vector2 a = rough[i], b = rough[i + 1];
                if (i > 0)
                {
                    points.Add(Vector2.Lerp(a, b, 0.25f));
                }
                if (i < rough.Count - 2)
                {
                    points.Add(Vector2.Lerp(a, b, 0.75f));
                }
            }
            points.Add(rough[rough.Count - 1]);

            Line = new RoadLine(points, heightAt, HeightSpacing);
        }

        /// <summary>The road as a line over the ground: what the track writer and the bridges work from.</summary>
        public RoadLine Line { get; }

        /// <summary>The point (x, z) <paramref name="distance"/> metres along the road from the trading post's end.</summary>
        public Vector2 Flat(float distance)
        {
            return Line.Flat(distance);
        }

        /// <summary>The ground height <paramref name="distance"/> metres along the road, from the heights kept.</summary>
        public float HeightAlong(float distance)
        {
            return Line.HeightAlong(distance);
        }

        /// <summary>The point on the ground <paramref name="distance"/> metres along the road (x, height, z).</summary>
        public Vector3 At(float distance)
        {
            return Line.At(distance);
        }

        /// <summary>Metres from (x, z) to the nearest part of the road between corners <paramref name="first"/> and <paramref name="last"/>.</summary>
        public float DistanceTo(Vector2 point, int first, int last)
        {
            return Line.DistanceTo(point, first, last);
        }
    }
}
