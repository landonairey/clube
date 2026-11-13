using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The shape a road is cut into the ground with (GL613), from its class's numbers: how wide it is at each point (a
    /// trodden path's edges wander), and for the cart paths a bed evened out along the road ("wider and flatter") that
    /// meets the ground over a shoulder at each side. A footpath has no bed: it follows the ground and only changes what
    /// grows on it. Bridges are left alone: the bed is the ground's own height at their ends.
    /// </summary>
    public sealed class RoadBed
    {
        private readonly float[] bed;
        private readonly IReadOnlyList<RouteBridges.Span> bridges;
        private readonly float wanderOffset;

        /// <param name="endHeight">
        /// The height its far end must meet (the bed of the road it joins there), or NaN for the ground's own.
        /// </param>
        public RoadBed(RoadLine line, RoadClassDefinition definition, IReadOnlyList<RouteBridges.Span> bridges, int seed, float endHeight = float.NaN)
        {
            Line = line;
            Definition = definition;
            this.bridges = bridges ?? new List<RouteBridges.Span>();
            wanderOffset = (seed & 1023) * 0.37f;
            FollowsGround = definition.GradeLength <= 0f || definition.MaxCut <= 0f;
            if (!FollowsGround)
            {
                bed = Grade(line, definition, this.bridges);
                if (!float.IsNaN(endHeight))
                {
                    // Eased onto the joined road's bed over the last grade length, so the junction has no step.
                    float lift = endHeight - bed[bed.Length - 1];
                    for (int i = 0; i < bed.Length; i++)
                    {
                        float fromEnd = line.Length - i * line.HeightSpacing;
                        bed[i] += lift * (1f - Mathf.Clamp01(fromEnd / definition.GradeLength));
                    }
                }
            }
        }

        public RoadLine Line { get; }

        public RoadClassDefinition Definition { get; }

        /// <summary>True when the road keeps the ground's shape and only its surface changes.</summary>
        public bool FollowsGround { get; }

        /// <summary>Metres from the centre line past which the road changes nothing.</summary>
        public float Reach => Definition.Width * 0.5f * (1f + Definition.EdgeWander) + (FollowsGround ? 0f : Definition.Shoulder);

        /// <summary>Half the road's width <paramref name="distance"/> metres along it.</summary>
        public float HalfWidthAt(float distance)
        {
            float wander = Definition.EdgeWander > 0f ? (Mathf.PerlinNoise(distance / 9f + wanderOffset, wanderOffset) * 2f - 1f) * Definition.EdgeWander : 0f;
            return Definition.Width * 0.5f * (1f + wander);
        }

        /// <summary>The height of the road's surface <paramref name="distance"/> metres along it.</summary>
        public float HeightAlong(float distance)
        {
            if (bed == null)
            {
                return Line.HeightAlong(distance);
            }
            float f = Mathf.Clamp(distance, 0f, Line.Length) / Line.HeightSpacing;
            int i = Mathf.Min((int)f, bed.Length - 2);
            return Mathf.Lerp(bed[i], bed[i + 1], f - i);
        }

        /// <summary>True when a stretch of the road lies wholly on a bridge's deck: nothing is written under it.</summary>
        public bool OnBridge(float from, float to)
        {
            foreach (RouteBridges.Span span in bridges)
            {
                if (from >= span.Start && to <= span.End)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The stretches of the road (first corner, last corner) that come within <paramref name="reach"/> metres of a
        /// box: usually one, two where the road doubles back past itself (a switchback). The questions below are asked
        /// against these, so one leg of a hairpin knows the other is there.
        /// </summary>
        public List<Vector2Int> StretchesNear(Rect box, float reach)
        {
            var stretches = new List<Vector2Int>();
            IReadOnlyList<Vector2> points = Line.Points;
            var wide = Rect.MinMaxRect(box.xMin - reach, box.yMin - reach, box.xMax + reach, box.yMax + reach);
            int from = -1;
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 a = points[i], b = points[i + 1];
                bool near = Mathf.Max(a.x, b.x) >= wide.xMin && Mathf.Min(a.x, b.x) <= wide.xMax
                    && Mathf.Max(a.y, b.y) >= wide.yMin && Mathf.Min(a.y, b.y) <= wide.yMax;
                if (near && from < 0)
                {
                    from = i;
                }
                if (from >= 0 && (!near || i == points.Count - 2))
                {
                    stretches.Add(new Vector2Int(from, near ? i + 1 : i));
                    from = -1;
                }
            }
            return stretches;
        }

        // Metres from a point to the nearest of some stretches of the road, and how far along the road that is.
        private float Nearest(Vector2 point, List<Vector2Int> stretches, out float along)
        {
            float best = float.MaxValue;
            along = 0f;
            foreach (Vector2Int stretch in stretches)
            {
                float distance = Line.DistanceTo(point, stretch.x, stretch.y, out float at);
                if (distance < best)
                {
                    best = distance;
                    along = at;
                }
            }
            return best;
        }

        /// <summary>True where (x, z) is on the road itself.</summary>
        public bool IsRoad(Vector2 point, List<Vector2Int> stretches)
        {
            float distance = Nearest(point, stretches, out float along);
            return distance <= HalfWidthAt(along);
        }

        /// <summary>True where (x, z) is on the road or its shoulder: outside it the road changes nothing.</summary>
        public bool IsBed(Vector2 point, List<Vector2Int> stretches)
        {
            float distance = Nearest(point, stretches, out float along);
            return distance <= HalfWidthAt(along) + (FollowsGround ? 0f : Definition.Shoulder);
        }

        /// <summary>
        /// The height the ground at (x, z) is set to, given the ground as it was: the bed on the road, the ground past
        /// its shoulder, and between them a smooth step from one to the other.
        /// </summary>
        public float Surface(Vector2 point, float ground, List<Vector2Int> stretches)
        {
            if (FollowsGround)
            {
                return ground;
            }
            float distance = Nearest(point, stretches, out float along);
            float beyond = distance - HalfWidthAt(along);
            if (beyond >= Definition.Shoulder)
            {
                return ground;
            }
            float height = HeightAlong(along);
            if (beyond <= 0f)
            {
                return height;
            }
            float t = beyond / Definition.Shoulder;
            return Mathf.Lerp(height, ground, t * t * (3f - 2f * t));
        }

        // The ground's heights along the road, evened out over the class's grade length and kept within its cut of the ground.
        private static float[] Grade(RoadLine line, RoadClassDefinition definition, IReadOnlyList<RouteBridges.Span> bridges)
        {
            float spacing = line.HeightSpacing;
            int count = Mathf.CeilToInt(line.Length / spacing) + 2;
            var ground = new float[count];
            for (int i = 0; i < count; i++)
            {
                ground[i] = line.HeightAlong(i * spacing);
            }
            int half = Mathf.Max(1, Mathf.RoundToInt(definition.GradeLength * 0.5f / spacing));
            float[] even = Average(Average(ground, half), half);
            for (int i = 0; i < count; i++)
            {
                float distance = i * spacing;
                // How much of the evening-out holds here: none at the road's ends (where it meets another road or a
                // settlement's ground) and at a bridge's ends (where it meets the deck).
                float hold = Mathf.Clamp01(Mathf.Min(distance, line.Length - distance) / definition.GradeLength);
                foreach (RouteBridges.Span span in bridges)
                {
                    float outside = distance < span.Start ? span.Start - distance : distance > span.End ? distance - span.End : 0f;
                    hold = Mathf.Min(hold, Mathf.Clamp01(outside / definition.GradeLength));
                }
                float height = Mathf.Lerp(ground[i], even[i], hold);
                even[i] = Mathf.Clamp(height, ground[i] - definition.MaxCut, ground[i] + definition.MaxCut);
            }
            return even;
        }

        private static float[] Average(float[] values, int half)
        {
            var sums = new float[values.Length + 1];
            for (int i = 0; i < values.Length; i++)
            {
                sums[i + 1] = sums[i] + values[i];
            }
            var result = new float[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                int from = Mathf.Max(0, i - half), to = Mathf.Min(values.Length - 1, i + half);
                result[i] = (sums[to + 1] - sums[from]) / (to - from + 1);
            }
            return result;
        }
    }
}
