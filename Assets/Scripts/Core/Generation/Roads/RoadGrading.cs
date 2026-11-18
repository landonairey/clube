using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>A stretch of road graded into the ground (GL737): points along the road and its bed's height at each.</summary>
    public sealed class RoadGrade
    {
        public List<Vector2> Points = new List<Vector2>();
        public List<float> Heights = new List<float>();
        private Rect bounds;
        private bool measured;

        /// <summary>The bed's height at (x, z) when that lies on this stretch (within half a metre of its line).</summary>
        public bool TryHeight(Vector2 point, out float height)
        {
            height = 0f;
            if (Points.Count < 2)
            {
                return false;
            }
            if (!measured)
            {
                Vector2 min = Points[0], max = Points[0];
                foreach (Vector2 p in Points)
                {
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }
                bounds = Rect.MinMaxRect(min.x - 1f, min.y - 1f, max.x + 1f, max.y + 1f);
                measured = true;
            }
            if (!bounds.Contains(point))
            {
                return false;
            }
            float best = 0.25f;
            bool found = false;
            for (int i = 0; i < Points.Count - 1; i++)
            {
                Vector2 a = Points[i], ab = Points[i + 1] - a;
                float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
                float distance = (a + ab * t - point).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    height = Mathf.Lerp(Heights[i], Heights[i + 1], t);
                    found = true;
                }
            }
            return found;
        }
    }

    /// <summary>
    /// Grades the roads of a plan into the ground where it is over their slope limit (GL737), and measures what each road is
    /// made of and what it costs (<see cref="RoadBuildCosts"/>). A graded class's bed is the ground with every rise steeper
    /// than the class allows cut down from above and built up from below by halves (the mean of the highest bed under the
    /// ground and the lowest over it that keep within the limit), never more than <see cref="RoadBuildCosts.GradeDepth"/>
    /// from the ground: rough ground and ledge edges become an even climb; a wall too tall for that stays steep, and is
    /// counted so. Slopes are then measured on what is built: the bed, a tunnel's floor; water is bridged.
    /// </summary>
    public static class RoadGrading
    {
        // Metres between the points a road is read at.
        private const float Step = 2f;

        // The bed keeps this share of the class's limit, so a slope measured between any two of its points stays under it.
        private const float Margin = 0.97f;

        // Metres a bed may differ from the ground by before it counts as built on its own (a graded stretch to build).
        private const float Departs = 0.02f;

        // Rise over run is measured over at least this many metres of a finished road: a single step of ground is not a slope.
        private const float SlopeRun = 12f;

        /// <summary>Grades and measures every road of a plan that is on the real ground.</summary>
        /// <param name="heightsAt">The ground's heights at many points at once, where that is faster; may be null.</param>
        /// <param name="waterAt">What water lies at (x, z); null on a dry map.</param>
        public static void Grade(WorldPlan plan, Func<float, float, float> heightAt, Func<Vector2[], float[]> heightsAt = null,
            Func<float, float, RoadWater> waterAt = null)
        {
            foreach (PlannedRoad road in plan.Roads)
            {
                if (road.Refined && road.Points.Count >= 2)
                {
                    Grade(road, plan.Rules.Road(road.Class), plan.Rules.BuildCosts, heightAt, heightsAt, waterAt);
                }
            }
        }

        /// <summary>Grades and measures one road: its <see cref="PlannedRoad.Grades"/>, slopes, works and cost.</summary>
        public static void Grade(PlannedRoad road, RoadClassDefinition definition, RoadBuildCosts costs, Func<float, float, float> heightAt,
            Func<Vector2[], float[]> heightsAt = null, Func<float, float, RoadWater> waterAt = null)
        {
            // The road read every few metres; in a tunnel the ground is its floor.
            var points = new List<Vector2> { road.Points[0] };
            var along = new List<float> { 0f };
            for (int i = 1; i < road.Points.Count; i++)
            {
                Vector2 a = road.Points[i - 1], b = road.Points[i];
                float length = Vector2.Distance(a, b);
                int steps = Mathf.Max(1, Mathf.CeilToInt(length / Step));
                for (int s = 1; s <= steps; s++)
                {
                    points.Add(Vector2.Lerp(a, b, s / (float)steps));
                    along.Add(along[along.Count - 1] + length / steps);
                }
            }
            int count = points.Count;
            float[] ground;
            Func<Vector2[], float[]> floors = RoadTunnels.HeightsAt(road, heightsAt);
            if (floors != null)
            {
                ground = floors(points.ToArray());
            }
            else
            {
                Func<float, float, float> floorAt = RoadTunnels.HeightAt(road, heightAt);
                ground = new float[count];
                for (int i = 0; i < count; i++)
                {
                    ground[i] = floorAt(points[i].x, points[i].y);
                }
            }
            var wet = new bool[count];
            var bored = new bool[count];
            for (int i = 0; i < count; i++)
            {
                wet[i] = waterAt != null && waterAt(points[i].x, points[i].y) != RoadWater.Dry;
                bored[i] = RoadTunnels.TryFloor(road, points[i], out float _);
            }

            bool graded = definition.GradeLength > 0f && definition.MaxCut > 0f;
            float[] bed = graded ? Bed(ground, along, wet, definition.MaxSlope * Margin, costs.GradeDepth) : (float[])ground.Clone();

            // What it is made of, piece by piece.
            road.Works = default;
            for (int i = 0; i < count - 1; i++)
            {
                RoadWork work = wet[i] || wet[i + 1] ? RoadWork.Bridge
                    : bored[i] && bored[i + 1] ? RoadWork.Tunnel
                    : graded && Mathf.Max(Mathf.Abs(bed[i] - ground[i]), Mathf.Abs(bed[i + 1] - ground[i + 1])) > definition.MaxCut ? RoadWork.Grade
                    : RoadWork.Path;
                road.Works[work] += along[i + 1] - along[i];
            }
            road.BuildCost = road.Works.CostAt(costs);

            // The graded stretches, a point beyond each end so the bed meets the ground smoothly.
            road.Grades.Clear();
            RoadGrade grade = null;
            for (int i = 0; i < count; i++)
            {
                bool departs = Mathf.Abs(bed[i] - ground[i]) > Departs
                    || (i > 0 && Mathf.Abs(bed[i - 1] - ground[i - 1]) > Departs)
                    || (i < count - 1 && Mathf.Abs(bed[i + 1] - ground[i + 1]) > Departs);
                if (departs)
                {
                    if (grade == null)
                    {
                        grade = new RoadGrade();
                        road.Grades.Add(grade);
                    }
                    grade.Points.Add(points[i]);
                    grade.Heights.Add(bed[i]);
                }
                else
                {
                    grade = null;
                }
            }
            road.Grades.RemoveAll(g => g.Points.Count < 2);

            // Its slopes on what is built, over the run a cart path's bed is evened out over; water is bridged.
            float run = Mathf.Max(SlopeRun, definition.GradeLength);
            road.SteepestSlope = 0f;
            road.SteepLength = 0f;
            int ahead = 0;
            for (int i = 0; i < count - 1; i++)
            {
                ahead = Mathf.Max(ahead, i);
                while (ahead < count - 1 && along[ahead] - along[i] < run)
                {
                    ahead++;
                }
                if (ahead == i || along[ahead] - along[i] < run * 0.5f)
                {
                    continue;
                }
                bool overWater = false;
                for (int k = i; k <= ahead && !overWater; k++)
                {
                    overWater = wet[k];
                }
                if (overWater)
                {
                    continue;
                }
                float slope = Mathf.Abs(bed[ahead] - bed[i]) / (along[ahead] - along[i]);
                road.SteepestSlope = Mathf.Max(road.SteepestSlope, slope);
                if (slope > definition.MaxSlope)
                {
                    road.SteepLength += along[i + 1] - along[i];
                }
            }
        }

        /// <summary>
        /// The graded bed over ground read at points <paramref name="along"/> a line: on each dry run, the mean of the highest
        /// line under the ground and the lowest over it that rise no steeper than <paramref name="limit"/>, kept within
        /// <paramref name="depth"/> of the ground. Over water (a bridge) it is the ground.
        /// </summary>
        public static float[] Bed(float[] ground, IReadOnlyList<float> along, bool[] wet, float limit, float depth)
        {
            int count = ground.Length;
            var bed = (float[])ground.Clone();
            var under = new float[count];
            var over = new float[count];
            int from = 0;
            while (from < count)
            {
                if (wet != null && wet[from])
                {
                    from++;
                    continue;
                }
                int to = from;
                while (to + 1 < count && (wet == null || !wet[to + 1]))
                {
                    to++;
                }
                // Each a forward and a backward sweep: the lowest of the ground and a neighbour's line plus the rise allowed between them.
                for (int i = from; i <= to; i++)
                {
                    under[i] = ground[i];
                    over[i] = ground[i];
                }
                for (int i = from + 1; i <= to; i++)
                {
                    float rise = limit * (along[i] - along[i - 1]);
                    under[i] = Mathf.Min(under[i], under[i - 1] + rise);
                    over[i] = Mathf.Max(over[i], over[i - 1] - rise);
                }
                for (int i = to - 1; i >= from; i--)
                {
                    float rise = limit * (along[i + 1] - along[i]);
                    under[i] = Mathf.Min(under[i], under[i + 1] + rise);
                    over[i] = Mathf.Max(over[i], over[i + 1] - rise);
                }
                for (int i = from; i <= to; i++)
                {
                    bed[i] = Mathf.Clamp((under[i] + over[i]) * 0.5f, ground[i] - depth, ground[i] + depth);
                }
                from = to + 1;
            }
            return bed;
        }

        /// <summary>The height a road is built at, at (x, z): a tunnel's floor, a graded stretch's bed, or the ground.</summary>
        public static Func<float, float, float> HeightAt(PlannedRoad road, Func<float, float, float> heightAt)
        {
            Func<float, float, float> floorAt = RoadTunnels.HeightAt(road, heightAt);
            if (road.Grades.Count == 0)
            {
                return floorAt;
            }
            return (x, z) => TryBed(road, new Vector2(x, z), out float bed) ? bed : floorAt(x, z);
        }

        /// <summary>As <see cref="HeightAt"/>, for many points at once.</summary>
        public static Func<Vector2[], float[]> HeightsAt(PlannedRoad road, Func<Vector2[], float[]> heightsAt)
        {
            Func<Vector2[], float[]> floorsAt = RoadTunnels.HeightsAt(road, heightsAt);
            if (road.Grades.Count == 0 || floorsAt == null)
            {
                return floorsAt;
            }
            return points =>
            {
                float[] result = floorsAt(points);
                for (int i = 0; i < points.Length; i++)
                {
                    if (TryBed(road, points[i], out float bed))
                    {
                        result[i] = bed;
                    }
                }
                return result;
            };
        }

        private static bool TryBed(PlannedRoad road, Vector2 point, out float bed)
        {
            if (!RoadTunnels.TryFloor(road, point, out float _))
            {
                foreach (RoadGrade grade in road.Grades)
                {
                    if (grade.TryHeight(point, out bed))
                    {
                        return true;
                    }
                }
            }
            bed = 0f;
            return false;
        }
    }
}
