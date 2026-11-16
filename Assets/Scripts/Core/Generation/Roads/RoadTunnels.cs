using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A tunnel on a road (GL642): a straight bore under a ridge, from one point of the road to another, its floor an
    /// even grade from the ground's height at one end to the ground's at the other.
    /// </summary>
    public sealed class RoadTunnel
    {
        /// <summary>Its two ends on the road (x, z), in the road's own direction.</summary>
        public Vector2 From, To;
        /// <summary>The floor's height at each end: the ground's there.</summary>
        public float FromHeight, ToHeight;

        public float Length => Vector2.Distance(From, To);

        /// <summary>The floor's height <paramref name="along"/> metres from <see cref="From"/>.</summary>
        public float FloorAt(float along)
        {
            return Mathf.Lerp(FromHeight, ToHeight, Mathf.Clamp01(along / Mathf.Max(0.01f, Length)));
        }

        /// <summary>Metres from (x, z) to its centre line, and how far along it the nearest point is.</summary>
        public float DistanceTo(Vector2 point, out float along)
        {
            Vector2 way = (To - From) / Mathf.Max(0.01f, Length);
            along = Mathf.Clamp(Vector2.Dot(point - From, way), 0f, Length);
            return Vector2.Distance(point, From + way * along);
        }
    }

    /// <summary>When a road is given a tunnel, and what one may be like (GL642).</summary>
    [Serializable]
    public sealed class RoadTunnelRules
    {
        /// <summary>Metres of a cart path over its slope limit that a tunnel must take out to be bored: what switchbacks could not ease.</summary>
        public float MinSteepLength = 60f;
        /// <summary>The most road one tunnel may stand in for, in metres along the road.</summary>
        public float Reach = 1500f;
        /// <summary>What a metre of tunnel costs, in metres of over-limit road it must take out: a tunnel is bored only where it is worth its length.</summary>
        public float LengthPrice = 0.15f;
        /// <summary>The most tunnels on one road.</summary>
        public int MostPerRoad = 3;
        public float MinLength = 40f;
        public float MaxLength = 600f;
        /// <summary>Metres of ground over the floor that make a stretch a bore; with less it is an open cutting leading to a portal.</summary>
        public float MinCover = 5f;
        /// <summary>The least share of its length that must be bore: less and it is a long ditch, not a tunnel.</summary>
        public float BoredShare = 0.35f;
        /// <summary>The most the ground may lie under the floor anywhere along it, in metres (the road fills that much).</summary>
        public float MaxFill = 1.5f;
        /// <summary>The steepest its floor may run, as a share of the road class's own limit.</summary>
        public float SlopeShare = 0.8f;
        /// <summary>Metres a tunnel's end keeps from the road's own ends (a settlement, a junction).</summary>
        public float EndClearance = 40f;
    }

    /// <summary>
    /// Bores tunnels on the cart paths of a plan (GL642), after its roads are on the real ground: where a road is still
    /// over its class's slope limit (a range or a spur between two settlements, which switchbacks could not ease), a
    /// straight bore from one point of the road to another, at a gentle grade and with ground enough over it, stands in
    /// for the road between them. Of all such bores the one that takes out the most over-limit road for its length is
    /// taken, then the next, a few to a road. Footpaths climb as they are. A tunnel never swallows a junction or a settlement.
    /// </summary>
    public static class RoadTunnels
    {
        // Metres between the points a road is read at.
        private const float Step = 4f;

        // Metres between the heights read along a bore to judge the ground over it.
        private const float CoverStep = 6f;

        // The shortest candidates whose cover is read, per steep stretch.
        private const int MostTried = 80;

        /// <summary>Bores what tunnels the plan's cart paths need; returns how many.</summary>
        /// <param name="heightsAt">The ground's heights at many points at once, where that is faster; may be null.</param>
        /// <param name="waterAt">What water lies at (x, z); null on a dry map.</param>
        public static int Bore(WorldPlan plan, Func<float, float, float> heightAt, Func<Vector2[], float[]> heightsAt = null,
            Func<float, float, RoadWater> waterAt = null, RoadTunnelRules rules = null)
        {
            rules ??= new RoadTunnelRules();
            Func<Vector2[], float[]> heights = heightsAt ?? (points =>
            {
                var result = new float[points.Length];
                for (int i = 0; i < points.Length; i++)
                {
                    result[i] = heightAt(points[i].x, points[i].y);
                }
                return result;
            });
            int bored = 0;
            foreach (PlannedRoad road in plan.Roads)
            {
                RoadClassDefinition definition = plan.Rules.Road(road.Class);
                bool graded = definition.GradeLength > 0f && definition.MaxCut > 0f;
                if (!graded || road.Points.Count < 2 || road.SteepLength < rules.MinSteepLength)
                {
                    continue;
                }
                bored += Bore(plan, road, definition, heights, waterAt, rules);
            }
            return bored;
        }

        /// <summary>
        /// The ground's height for a road's line: the tunnel's floor inside a tunnel, the ground elsewhere. A road's bed
        /// is worked out from this, so it runs into a tunnel on its floor.
        /// </summary>
        public static Func<float, float, float> HeightAt(PlannedRoad road, Func<float, float, float> heightAt)
        {
            if (road.Tunnels.Count == 0)
            {
                return heightAt;
            }
            return (x, z) => TryFloor(road, new Vector2(x, z), out float floor) ? floor : heightAt(x, z);
        }

        /// <summary>As <see cref="HeightAt"/>, for many points at once.</summary>
        public static Func<Vector2[], float[]> HeightsAt(PlannedRoad road, Func<Vector2[], float[]> heightsAt)
        {
            if (road.Tunnels.Count == 0 || heightsAt == null)
            {
                return heightsAt;
            }
            return points =>
            {
                float[] result = heightsAt(points);
                for (int i = 0; i < points.Length; i++)
                {
                    if (TryFloor(road, points[i], out float floor))
                    {
                        result[i] = floor;
                    }
                }
                return result;
            };
        }

        private static bool TryFloor(PlannedRoad road, Vector2 point, out float floor)
        {
            foreach (RoadTunnel tunnel in road.Tunnels)
            {
                if (tunnel.DistanceTo(point, out float along) < 0.5f && along > 0f && along < tunnel.Length)
                {
                    floor = tunnel.FloorAt(along);
                    return true;
                }
            }
            floor = 0f;
            return false;
        }

        private static int Bore(WorldPlan plan, PlannedRoad road, RoadClassDefinition definition, Func<Vector2[], float[]> heights,
            Func<float, float, RoadWater> waterAt, RoadTunnelRules rules)
        {
            // The road read every few metres: its points, how far along each is, the ground there, and whether it is over water.
            var points = new List<Vector2>();
            var along = new List<float>();
            float travelled = 0f;
            points.Add(road.Points[0]);
            along.Add(0f);
            for (int i = 1; i < road.Points.Count; i++)
            {
                Vector2 a = road.Points[i - 1], b = road.Points[i];
                float length = Vector2.Distance(a, b);
                int steps = Mathf.Max(1, Mathf.CeilToInt(length / Step));
                for (int s = 1; s <= steps; s++)
                {
                    points.Add(Vector2.Lerp(a, b, s / (float)steps));
                    along.Add(travelled + length * s / steps);
                }
                travelled += length;
            }
            var ground = new List<float>(heights(points.ToArray()));
            var wet = new List<bool>(points.Count);
            foreach (Vector2 point in points)
            {
                wet.Add(waterAt != null && waterAt(point.x, point.y) != RoadWater.Dry);
            }

            // Where a tunnel may not pass: the settlements on the road and the junctions other roads make with it.
            var keep = new List<float>();
            foreach (PlannedSettlement settlement in plan.Settlements)
            {
                AddKept(keep, points, along, settlement.Position, 2f);
            }
            foreach (PlannedRoad other in plan.Roads)
            {
                if (other.Joins == road.Index && other.Points.Count > 0)
                {
                    AddKept(keep, points, along, other.Points[other.Points.Count - 1], 12f);
                }
            }

            int bored = 0;
            float run = Mathf.Max(16f, definition.GradeLength);
            float limit = definition.MaxSlope * rules.SlopeShare;
            float end = along[along.Count - 1];
            // One tunnel at a time: of every pair of points of the road a bore could join, the one that takes the most
            // over-limit road out for its length. (Distances along stay the road's first ones throughout: a tunnel
            // already bored then reads as a long level stretch, and nothing has to be measured again in between.)
            for (int round = 0; round < rules.MostPerRoad; round++)
            {
                // Metres of road over its limit before each point.
                var steepBefore = new float[points.Count];
                for (int i = 0; i < points.Count - 1; i++)
                {
                    bool over = Slope(along, ground, wet, i, run, out float slope) && slope > definition.MaxSlope;
                    steepBefore[i + 1] = steepBefore[i] + (over ? along[i + 1] - along[i] : 0f);
                }
                if (steepBefore[points.Count - 1] < rules.MinSteepLength)
                {
                    break;
                }
                var candidates = new List<(float Worth, int A, int B)>();
                for (int a = 0; a < points.Count; a += 2)
                {
                    if (wet[a] || along[a] < rules.EndClearance)
                    {
                        continue;
                    }
                    for (int b = a + 2; b < points.Count && along[b] - along[a] <= rules.Reach; b += 2)
                    {
                        float removed = steepBefore[b] - steepBefore[a];
                        if (removed < rules.MinSteepLength || wet[b] || end - along[b] < rules.EndClearance)
                        {
                            continue;
                        }
                        float chord = Vector2.Distance(points[a], points[b]);
                        if (chord < rules.MinLength || chord > rules.MaxLength || Mathf.Abs(ground[b] - ground[a]) / chord > limit
                            || keep.Exists(k => k > along[a] && k < along[b]))
                        {
                            continue;
                        }
                        float worth = removed - rules.LengthPrice * chord;
                        if (worth > 0f)
                        {
                            candidates.Add((worth, a, b));
                        }
                    }
                }
                candidates.Sort((x, y) => x.Worth != y.Worth ? y.Worth.CompareTo(x.Worth) : x.A != y.A ? x.A.CompareTo(y.A) : x.B.CompareTo(y.B));
                bool found = false;
                for (int c = 0; c < candidates.Count && c < MostTried && !found; c++)
                {
                    (float _, int a, int b) = candidates[c];
                    if (!Covered(points[a], points[b], ground[a], ground[b], heights, waterAt, rules))
                    {
                        continue;
                    }
                    found = true;
                    float length = Vector2.Distance(points[a], points[b]);
                    road.Tunnels.Add(new RoadTunnel { From = points[a], To = points[b], FromHeight = ground[a], ToHeight = ground[b] });
                    plan.Notes.Add($"Road {road.Index}: a tunnel of {length:0} m in place of {along[b] - along[a]:0} m of road, "
                        + $"{steepBefore[b] - steepBefore[a]:0} m of it over its slope limit.");
                    // A tunnel is never swallowed by another.
                    keep.Add(along[a] + 0.01f);
                    points.RemoveRange(a + 1, b - a - 1);
                    ground.RemoveRange(a + 1, b - a - 1);
                    wet.RemoveRange(a + 1, b - a - 1);
                    along.RemoveRange(a + 1, b - a - 1);
                    bored++;
                }
                if (!found)
                {
                    break;
                }
            }
            if (bored == 0)
            {
                return 0;
            }
            // The road as it now runs, measured again; its tunnels in its own direction.
            road.Tunnels.Sort((x, y) => Nearest(points, x.From).CompareTo(Nearest(points, y.From)));
            road.Points = points;
            for (int i = 1; i < points.Count; i++)
            {
                along[i] = along[i - 1] + Vector2.Distance(points[i], points[i - 1]);
            }
            road.Length = along[along.Count - 1];
            road.SteepestSlope = 0f;
            road.SteepLength = 0f;
            for (int i = 0; i < points.Count - 1; i++)
            {
                if (Slope(along, ground, wet, i, run, out float slope))
                {
                    road.SteepestSlope = Mathf.Max(road.SteepestSlope, slope);
                    road.SteepLength += slope > definition.MaxSlope ? along[i + 1] - along[i] : 0f;
                }
            }
            road.Bridges.RemoveAll(bridge =>
            {
                float nearest = float.MaxValue;
                for (int i = 1; i < points.Count; i++)
                {
                    nearest = Mathf.Min(nearest, SettlementSite.Distance(bridge.Position, points[i - 1], points[i]));
                }
                return nearest > 20f;
            });
            return bored;
        }

        private static void AddKept(List<float> keep, List<Vector2> points, List<float> along, Vector2 at, float within)
        {
            float nearest = float.MaxValue;
            int best = -1;
            for (int i = 0; i < points.Count; i++)
            {
                float distance = (points[i] - at).sqrMagnitude;
                if (distance < nearest)
                {
                    nearest = distance;
                    best = i;
                }
            }
            if (best >= 0 && nearest <= Mathf.Max(within, Step) * Mathf.Max(within, Step))
            {
                keep.Add(along[best]);
            }
        }

        // The slope of the stretch that starts at a point, over the run a cart path's bed is evened out over; false over water.
        private static bool Slope(List<float> along, List<float> ground, List<bool> wet, int from, float run, out float slope)
        {
            slope = 0f;
            int to = from;
            while (to < along.Count - 1 && along[to] - along[from] < run)
            {
                to++;
            }
            if (to == from || along[to] - along[from] < run * 0.5f)
            {
                return false;
            }
            for (int k = from; k <= to; k++)
            {
                if (wet[k])
                {
                    return false;
                }
            }
            slope = Mathf.Abs(ground[to] - ground[from]) / (along[to] - along[from]);
            return true;
        }

        // The point of a road nearest a place, by its place in the list.
        private static int Nearest(List<Vector2> points, Vector2 at)
        {
            int best = 0;
            for (int i = 1; i < points.Count; i++)
            {
                best = (points[i] - at).sqrMagnitude < (points[best] - at).sqrMagnitude ? i : best;
            }
            return best;
        }

        // True when the ground never lies far under the floor along it, lies well over it for a fair share of it (the bore; the
        // rest is the cuttings that lead to its portals), and no water lies over it.
        private static bool Covered(Vector2 from, Vector2 to, float fromHeight, float toHeight, Func<Vector2[], float[]> heights,
            Func<float, float, RoadWater> waterAt, RoadTunnelRules rules)
        {
            float length = Vector2.Distance(from, to);
            int count = Mathf.Max(2, Mathf.CeilToInt(length / CoverStep));
            var samples = new Vector2[count - 1];
            for (int i = 1; i < count; i++)
            {
                samples[i - 1] = Vector2.Lerp(from, to, i / (float)count);
            }
            float[] ground = heights(samples);
            int bored = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float cover = ground[i] - Mathf.Lerp(fromHeight, toHeight, (i + 1) / (float)count);
                if (cover < -rules.MaxFill || (waterAt != null && waterAt(samples[i].x, samples[i].y) != RoadWater.Dry))
                {
                    return false;
                }
                bored += cover >= rules.MinCover ? 1 : 0;
            }
            return bored >= Mathf.Max(1f, samples.Length * rules.BoredShare);
        }
    }
}
