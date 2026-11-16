using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A road as a line over the ground: its corners (x, z), how far along each is, and the ground's height kept every
    /// so many metres, so a point on it, its height and the distance from it are cheap to ask for. The trade cart's
    /// <see cref="CartRoute"/> and the world plan's roads (<see cref="PlannedRoad"/>, GL613) are both one of these.
    /// </summary>
    public sealed class RoadLine
    {
        private readonly List<Vector2> points;
        private readonly float[] distances;
        private readonly float[] heights;

        /// <param name="corners">The road's corners in order; at least two.</param>
        /// <param name="heightAt">The ground's height at (x, z).</param>
        /// <param name="heightSpacing">Metres between the heights kept along the road.</param>
        /// <param name="heightsAt">The ground's heights at many points at once, where the map has that (a job): a long road asks for thousands.</param>
        public RoadLine(IReadOnlyList<Vector2> corners, Func<float, float, float> heightAt, float heightSpacing = 1f, Func<Vector2[], float[]> heightsAt = null)
        {
            if (corners == null || corners.Count < 2)
            {
                throw new ArgumentException("A road needs two corners.", nameof(corners));
            }
            if (heightAt == null)
            {
                throw new ArgumentNullException(nameof(heightAt));
            }
            points = new List<Vector2>(corners);
            distances = new float[points.Count];
            Vector2 min = points[0], max = points[0];
            for (int i = 1; i < points.Count; i++)
            {
                distances[i] = distances[i - 1] + (points[i] - points[i - 1]).magnitude;
                min = Vector2.Min(min, points[i]);
                max = Vector2.Max(max, points[i]);
            }
            Length = distances[points.Count - 1];
            Bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            HeightSpacing = Mathf.Max(0.25f, heightSpacing);
            heights = new float[Mathf.CeilToInt(Length / HeightSpacing) + 2];
            if (heightsAt != null)
            {
                var along = new Vector2[heights.Length];
                for (int i = 0; i < along.Length; i++)
                {
                    along[i] = Flat(i * HeightSpacing);
                }
                heights = heightsAt(along);
                return;
            }
            for (int i = 0; i < heights.Length; i++)
            {
                Vector2 p = Flat(i * HeightSpacing);
                heights[i] = heightAt(p.x, p.y);
            }
        }

        /// <summary>The road's corners, from its start.</summary>
        public IReadOnlyList<Vector2> Points => points;

        /// <summary>The road's length in metres.</summary>
        public float Length { get; }

        /// <summary>The box round its corners (x, z).</summary>
        public Rect Bounds { get; }

        /// <summary>Metres between the heights kept along it.</summary>
        public float HeightSpacing { get; }

        /// <summary>Metres along the road at a corner.</summary>
        public float DistanceAt(int corner)
        {
            return distances[Mathf.Clamp(corner, 0, distances.Length - 1)];
        }

        /// <summary>The corner at or before a distance along the road.</summary>
        public int CornerBefore(float distance)
        {
            int index = Array.BinarySearch(distances, distance);
            return index >= 0 ? index : Mathf.Max(0, ~index - 1);
        }

        /// <summary>The point (x, z) <paramref name="distance"/> metres along the road.</summary>
        public Vector2 Flat(float distance)
        {
            distance = Mathf.Clamp(distance, 0f, Length);
            int low = Mathf.Min(CornerBefore(distance), points.Count - 2);
            float span = distances[low + 1] - distances[low];
            float t = span > 1e-5f ? (distance - distances[low]) / span : 0f;
            return Vector2.Lerp(points[low], points[low + 1], t);
        }

        /// <summary>The way the road runs (x, z, a unit vector) <paramref name="distance"/> metres along it.</summary>
        public Vector2 Direction(float distance)
        {
            Vector2 d = Flat(distance + 1f) - Flat(distance - 1f);
            return d.sqrMagnitude > 1e-8f ? d.normalized : Vector2.right;
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
            return DistanceTo(point, first, last, out _);
        }

        /// <summary>As <see cref="DistanceTo(Vector2, int, int)"/>, and how far along the road that nearest part is.</summary>
        public float DistanceTo(Vector2 point, int first, int last, out float along)
        {
            float best = float.MaxValue;
            along = 0f;
            for (int i = Mathf.Max(0, first); i < Mathf.Min(last, points.Count - 1); i++)
            {
                Vector2 a = points[i], b = points[i + 1];
                Vector2 ab = b - a;
                float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
                float distance = (a + ab * t - point).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    along = Mathf.Lerp(distances[i], distances[i + 1], t);
                }
            }
            return best == float.MaxValue ? best : Mathf.Sqrt(best);
        }

        /// <summary>Metres from (x, z) to the nearest part of the whole road, and how far along it that is.</summary>
        public float DistanceTo(Vector2 point, out float along)
        {
            return DistanceTo(point, 0, points.Count - 1, out along);
        }
    }
}
