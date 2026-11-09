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
        private readonly List<float> distances = new List<float>();
        private readonly float[] heights;

        /// <summary>The road's corners, from the trading post's end to the distant town's.</summary>
        public IReadOnlyList<Vector2> Points => points;

        /// <summary>The road's length in metres.</summary>
        public float Length { get; }

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

        /// <summary>Plans the road from <paramref name="from"/> to <paramref name="to"/> over the ground <paramref name="heightAt"/>.</summary>
        /// <param name="spacing">Metres between bends.</param>
        /// <param name="sway">The most metres a bend may move off the straight line.</param>
        public CartRoute(Vector2 from, Vector2 to, Func<float, float, float> heightAt, int seed, float spacing = 16f, float sway = 10f)
        {
            if (heightAt == null)
            {
                throw new ArgumentNullException(nameof(heightAt));
            }
            var random = new System.Random(seed * 17 + 4770);
            Vector2 along = to - from;
            float straight = along.magnitude;
            Vector2 forward = straight > 1e-4f ? along / straight : Vector2.right;
            var side = new Vector2(-forward.y, forward.x);
            int bends = Mathf.Max(1, Mathf.CeilToInt(straight / Mathf.Max(1f, spacing)));
            var rough = new List<Vector2> { from };
            float lastHeight = heightAt(from.x, from.y);
            float lastOffset = 0f;
            for (int i = 1; i < bends; i++)
            {
                Vector2 onLine = from + along * (i / (float)bends);
                float bestScore = float.MaxValue;
                float bestOffset = 0f;
                float bestHeight = lastHeight;
                // The ends are fixed, so the bends near them can't swing as far.
                float reach = sway * Mathf.Min(1f, Mathf.Min(i, bends - i) / 2f);
                for (int k = -2; k <= 2; k++)
                {
                    float offset = Mathf.Clamp(lastOffset + k * reach * 0.5f + ((float)random.NextDouble() - 0.5f) * reach * 0.2f, -reach, reach);
                    Vector2 point = onLine + side * offset;
                    float h = heightAt(point.x, point.y);
                    // Least climb, then least turning.
                    float score = Mathf.Abs(h - lastHeight) + 0.05f * Mathf.Abs(offset - lastOffset);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestOffset = offset;
                        bestHeight = h;
                    }
                }
                rough.Add(onLine + side * bestOffset);
                lastOffset = bestOffset;
                lastHeight = bestHeight;
            }
            rough.Add(to);

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

            float length = 0f;
            distances.Add(0f);
            for (int i = 1; i < points.Count; i++)
            {
                length += (points[i] - points[i - 1]).magnitude;
                distances.Add(length);
            }
            Length = length;
            heights = new float[Mathf.CeilToInt(length / HeightSpacing) + 2];
            for (int i = 0; i < heights.Length; i++)
            {
                Vector2 p = Flat(i * HeightSpacing);
                heights[i] = heightAt(p.x, p.y);
            }
        }

        /// <summary>The point (x, z) <paramref name="distance"/> metres along the road from the trading post's end.</summary>
        public Vector2 Flat(float distance)
        {
            distance = Mathf.Clamp(distance, 0f, Length);
            int low = 0, high = distances.Count - 1;
            while (high - low > 1)
            {
                int middle = (low + high) / 2;
                if (distances[middle] <= distance)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }
            float span = distances[high] - distances[low];
            float t = span > 1e-5f ? (distance - distances[low]) / span : 0f;
            return Vector2.Lerp(points[low], points[high], t);
        }

        /// <summary>The ground height <paramref name="distance"/> metres along the road, from the heights kept.</summary>
        public float HeightAlong(float distance)
        {
            float f = Mathf.Clamp(distance, 0f, Length) / HeightSpacing;
            int i = Mathf.Min((int)f, heights.Length - 2);
            return Mathf.Lerp(heights[i], heights[i + 1], f - i);
        }

        /// <summary>The point on the ground <paramref name="distance"/> metres along the road (x, height, z).</summary>
        public Vector3 At(float distance)
        {
            Vector2 p = Flat(distance);
            return new Vector3(p.x, HeightAlong(distance), p.y);
        }

        /// <summary>Metres from (x, z) to the nearest part of the road between corners <paramref name="first"/> and <paramref name="last"/>.</summary>
        public float DistanceTo(Vector2 point, int first, int last)
        {
            float best = float.MaxValue;
            for (int i = Mathf.Max(0, first); i < Mathf.Min(last, points.Count - 1); i++)
            {
                Vector2 a = points[i], b = points[i + 1];
                Vector2 ab = b - a;
                float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
                best = Mathf.Min(best, (a + ab * t - point).magnitude);
            }
            return best;
        }
    }
}
