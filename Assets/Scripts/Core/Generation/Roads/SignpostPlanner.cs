using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>One arm of a signpost: the way it points and the settlement it names.</summary>
    public sealed class SignArm
    {
        /// <summary>The way the road leaves the post towards the place (x, z, a unit vector).</summary>
        public Vector2 Direction;
        public int Settlement;
        public string Name;
        /// <summary>Metres to the place by road.</summary>
        public float Metres;
    }

    /// <summary>A signpost of the world plan (GL632): where it stands, beside the road, and its arms.</summary>
    public sealed class PlannedSignpost
    {
        /// <summary>Where the post stands (x, z): off the road, in the widest gap between the ways leaving it.</summary>
        public Vector2 Position;
        /// <summary>True for a post at a settlement's edge, false for one at a junction of roads.</summary>
        public bool AtSettlement;
        public List<SignArm> Arms = new List<SignArm>();
    }

    /// <summary>
    /// Plans the signposts (GL632: "sign posts pointing to directions of nearby towns"): one at every junction of two
    /// roads and one where each road leaves a settlement. Every way out of a post gets an arm naming the nearest
    /// settlement that way by road, and a second naming the capital when the shortest way to it leaves that way too. A
    /// post at a settlement's edge also names the settlement itself, pointing back in.
    /// </summary>
    public static class SignpostPlanner
    {
        // Metres within which a road passing a settlement's centre counts as running through it.
        private const float ThroughReach = 12f;

        // Metres a post stands clear of the edge of the widest road it serves.
        private const float Clear = 1.1f;

        // A junction this close to a settlement's edge post replaces it.
        private const float TooClose = 30f;

        private sealed class Edge
        {
            public int A, B, Road;
            public float AlongA, AlongB;
            public float Length => Mathf.Abs(AlongB - AlongA);
        }

        private sealed class Track
        {
            public PlannedRoad Road;
            public float[] Along;
            public List<(float Along, int Node)> Stops = new List<(float, int)>();
        }

        public static List<PlannedSignpost> Plan(WorldPlan plan)
        {
            var posts = new List<PlannedSignpost>();
            if (plan == null || plan.Roads.Count == 0)
            {
                return posts;
            }
            int settlements = plan.Settlements.Count;
            var nodes = new List<Vector2>(settlements);
            foreach (PlannedSettlement settlement in plan.Settlements)
            {
                nodes.Add(settlement.Position);
            }

            // Each road with the places along it: its ends, the roads that join it, the settlements it runs through.
            var tracks = new List<Track>(plan.Roads.Count);
            foreach (PlannedRoad road in plan.Roads)
            {
                var track = new Track { Road = road, Along = new float[road.Points.Count] };
                for (int i = 1; i < road.Points.Count; i++)
                {
                    track.Along[i] = track.Along[i - 1] + Vector2.Distance(road.Points[i], road.Points[i - 1]);
                }
                tracks.Add(track);
            }
            foreach (Track track in tracks)
            {
                PlannedRoad road = track.Road;
                if (road.Points.Count < 2)
                {
                    continue;
                }
                float length = track.Along[track.Along.Length - 1];
                track.Stops.Add((0f, road.From));
                if (road.To >= 0)
                {
                    track.Stops.Add((length, road.To));
                }
                else if (road.Joins >= 0 && road.Joins < tracks.Count && road.Joins != road.Index)
                {
                    Vector2 end = road.Points[road.Points.Count - 1];
                    nodes.Add(end);
                    track.Stops.Add((length, nodes.Count - 1));
                    Track joined = tracks[road.Joins];
                    Nearest(joined, end, out float along);
                    joined.Stops.Add((along, nodes.Count - 1));
                }
                foreach (PlannedSettlement settlement in plan.Settlements)
                {
                    if (settlement.Index == road.From || settlement.Index == road.To)
                    {
                        continue;
                    }
                    if (Nearest(track, settlement.Position, out float along) <= ThroughReach)
                    {
                        track.Stops.Add((along, settlement.Index));
                    }
                }
            }

            var edges = new List<Edge>();
            var edgesOf = new List<List<int>>();
            for (int i = 0; i < nodes.Count; i++)
            {
                edgesOf.Add(new List<int>());
            }
            foreach (Track track in tracks)
            {
                track.Stops.Sort((a, b) => a.Along.CompareTo(b.Along));
                for (int i = 1; i < track.Stops.Count; i++)
                {
                    (float Along, int Node) a = track.Stops[i - 1], b = track.Stops[i];
                    if (a.Node == b.Node || b.Along - a.Along < 0.5f)
                    {
                        continue;
                    }
                    edgesOf[a.Node].Add(edges.Count);
                    edgesOf[b.Node].Add(edges.Count);
                    edges.Add(new Edge { A = a.Node, B = b.Node, Road = track.Road.Index, AlongA = a.Along, AlongB = b.Along });
                }
            }

            PlannedSettlement capital = plan.Capital;
            var distance = new float[nodes.Count];
            var firstEdge = new int[nodes.Count];
            var junctions = new List<Vector2>();
            // Junctions first, so a settlement's edge post can give way to one beside it.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int node = 0; node < nodes.Count; node++)
                {
                    bool settlement = node < settlements;
                    if (settlement != (pass == 1) || edgesOf[node].Count == 0)
                    {
                        continue;
                    }
                    Search(node, edges, edgesOf, distance, firstEdge);
                    if (!settlement)
                    {
                        var post = new PlannedSignpost();
                        var ways = new List<Vector2>();
                        float widest = 0f;
                        foreach (int e in edgesOf[node])
                        {
                            Vector2 way = WayOut(tracks, edges[e], node, 0f, out _);
                            ways.Add(way);
                            widest = Mathf.Max(widest, plan.Rules.Road(tracks[edges[e].Road].Road.Class).Width);
                            AddArms(post, plan, capital, node, e, way, 0f, settlements, distance, firstEdge);
                        }
                        if (post.Arms.Count > 0)
                        {
                            post.Position = nodes[node] + Stand(ways, widest * 0.5f + Clear);
                            posts.Add(post);
                            junctions.Add(nodes[node]);
                        }
                        continue;
                    }
                    PlannedSettlement here = plan.Settlements[node];
                    float radius = Mathf.Sqrt(Mathf.Max(1, here.Type.Parcels)) * plan.Rules.ParcelSide * 0.5f + 8f;
                    foreach (int e in edgesOf[node])
                    {
                        float out_ = Mathf.Min(radius, edges[e].Length * 0.4f);
                        Vector2 way = WayOut(tracks, edges[e], node, out_, out Vector2 at);
                        if (junctions.Exists(j => (j - at).sqrMagnitude < TooClose * TooClose))
                        {
                            continue;
                        }
                        var post = new PlannedSignpost { AtSettlement = true };
                        AddArms(post, plan, capital, node, e, way, out_, settlements, distance, firstEdge);
                        if (post.Arms.Count == 0)
                        {
                            continue;
                        }
                        post.Arms.Add(new SignArm { Direction = -way, Settlement = node, Name = here.Name, Metres = out_ });
                        float width = plan.Rules.Road(tracks[edges[e].Road].Road.Class).Width;
                        post.Position = at + Stand(new List<Vector2> { way, -way }, width * 0.5f + Clear);
                        posts.Add(post);
                    }
                }
            }
            return posts;
        }

        // The arms for one way out of a node: the nearest settlement that way, and the capital when it lies that way too.
        private static void AddArms(PlannedSignpost post, WorldPlan plan, PlannedSettlement capital, int node, int edge, Vector2 way,
            float walked, int settlements, float[] distance, int[] firstEdge)
        {
            int nearest = -1;
            for (int s = 0; s < settlements; s++)
            {
                if (s != node && firstEdge[s] == edge && (nearest < 0 || distance[s] < distance[nearest]))
                {
                    nearest = s;
                }
            }
            if (nearest < 0)
            {
                return;
            }
            post.Arms.Add(new SignArm { Direction = way, Settlement = nearest, Name = plan.Settlements[nearest].Name, Metres = distance[nearest] - walked });
            if (capital != null && capital.Index != nearest && capital.Index != node && firstEdge[capital.Index] == edge)
            {
                post.Arms.Add(new SignArm { Direction = way, Settlement = capital.Index, Name = capital.Name, Metres = distance[capital.Index] - walked });
            }
        }

        // The way a road leaves a node along an edge, measured a little way out from the node (plus out metres), and that point.
        private static Vector2 WayOut(List<Track> tracks, Edge edge, int node, float out_, out Vector2 at)
        {
            Track track = tracks[edge.Road];
            bool fromA = edge.A == node;
            float here = fromA ? edge.AlongA : edge.AlongB;
            float sign = (fromA ? edge.AlongB : edge.AlongA) > here ? 1f : -1f;
            at = PointAt(track, here + sign * out_);
            Vector2 ahead = PointAt(track, here + sign * (out_ + Mathf.Min(10f, edge.Length))) - at;
            return ahead.sqrMagnitude > 1e-6f ? ahead.normalized : Vector2.right;
        }

        // Where a post stands from the point the ways meet: out along the middle of the widest gap between them.
        private static Vector2 Stand(List<Vector2> ways, float clear)
        {
            var angles = new List<float>();
            foreach (Vector2 way in ways)
            {
                angles.Add(Mathf.Atan2(way.y, way.x));
            }
            angles.Sort();
            float bestGap = -1f, bestAngle = 0f;
            for (int i = 0; i < angles.Count; i++)
            {
                float next = i + 1 < angles.Count ? angles[i + 1] : angles[0] + Mathf.PI * 2f;
                float gap = next - angles[i];
                if (gap > bestGap)
                {
                    bestGap = gap;
                    bestAngle = angles[i] + gap * 0.5f;
                }
            }
            // Far enough out to clear both roads of a narrow gap, but never far from the junction.
            float outwards = Mathf.Min(clear * 2.5f, clear / Mathf.Max(0.3f, Mathf.Sin(Mathf.Min(bestGap, Mathf.PI) * 0.5f)));
            return new Vector2(Mathf.Cos(bestAngle), Mathf.Sin(bestAngle)) * outwards;
        }

        // The shortest way by road from a node to every other, and the edge each way leaves the node by.
        private static void Search(int start, List<Edge> edges, List<List<int>> edgesOf, float[] distance, int[] firstEdge)
        {
            int count = distance.Length;
            var done = new bool[count];
            for (int i = 0; i < count; i++)
            {
                distance[i] = float.MaxValue;
                firstEdge[i] = -1;
            }
            distance[start] = 0f;
            while (true)
            {
                int node = -1;
                for (int i = 0; i < count; i++)
                {
                    if (!done[i] && distance[i] < float.MaxValue && (node < 0 || distance[i] < distance[node]))
                    {
                        node = i;
                    }
                }
                if (node < 0)
                {
                    return;
                }
                done[node] = true;
                foreach (int e in edgesOf[node])
                {
                    Edge edge = edges[e];
                    int other = edge.A == node ? edge.B : edge.A;
                    float total = distance[node] + edge.Length;
                    if (total < distance[other])
                    {
                        distance[other] = total;
                        firstEdge[other] = node == start ? e : firstEdge[node];
                    }
                }
            }
        }

        private static Vector2 PointAt(Track track, float along)
        {
            List<Vector2> points = track.Road.Points;
            along = Mathf.Clamp(along, 0f, track.Along[track.Along.Length - 1]);
            int i = System.Array.BinarySearch(track.Along, along);
            i = Mathf.Clamp(i >= 0 ? i : ~i - 1, 0, points.Count - 2);
            float span = track.Along[i + 1] - track.Along[i];
            return Vector2.Lerp(points[i], points[i + 1], span > 1e-5f ? (along - track.Along[i]) / span : 0f);
        }

        // Metres from a point to a road, and how far along the road its nearest part is.
        private static float Nearest(Track track, Vector2 point, out float along)
        {
            List<Vector2> points = track.Road.Points;
            float best = float.MaxValue;
            along = 0f;
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 a = points[i], ab = points[i + 1] - a;
                float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
                float d = (a + ab * t - point).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    along = Mathf.Lerp(track.Along[i], track.Along[i + 1], t);
                }
            }
            return Mathf.Sqrt(best);
        }
    }
}
