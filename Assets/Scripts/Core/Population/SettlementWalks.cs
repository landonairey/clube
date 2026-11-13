using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The ways through a settlement on foot (GL615): its lanes (<see cref="SettlementSite.Lanes"/>) as a web of points
    /// a couple of metres apart, joined where lanes meet. <see cref="Route"/> walks from one place to another along the
    /// streets and climbs rather than through the buildings between: onto the web at the nearest clear point, along it,
    /// and off it where the way on to the end is shortest and clear.
    /// </summary>
    public sealed class SettlementWalks
    {
        /// <summary>Metres between the web's points along a lane, and within which points of two lanes are joined.</summary>
        public const float Spacing = 2f, Join = 1.8f;

        /// <summary>Metres a walker keeps from a building's walls.</summary>
        public const float Clearance = 0.6f;

        // How much longer a metre through a building counts than one in the open.
        private const float BlockedCost = 12f;

        private readonly SettlementSite site;
        private readonly List<Vector2> nodes = new List<Vector2>();
        private readonly List<List<int>> links = new List<List<int>>();

        public SettlementWalks(SettlementSite site)
        {
            this.site = site;
            var lanes = new List<int>();
            for (int l = 0; l < site.Lanes.Count; l++)
            {
                List<Vector2> points = site.Lanes[l].Points;
                int previous = -1;
                for (int i = 0; i < points.Count; i++)
                {
                    if (i > 0)
                    {
                        float length = Vector2.Distance(points[i - 1], points[i]);
                        int steps = Mathf.Max(1, Mathf.CeilToInt(length / Spacing));
                        for (int s = 1; s < steps; s++)
                        {
                            previous = Add(Vector2.Lerp(points[i - 1], points[i], s / (float)steps), previous, l, lanes);
                        }
                    }
                    previous = Add(points[i], previous, l, lanes);
                }
            }
            for (int a = 0; a < nodes.Count; a++)
            {
                for (int b = a + 1; b < nodes.Count; b++)
                {
                    if (lanes[a] != lanes[b] && (nodes[a] - nodes[b]).sqrMagnitude <= Join * Join)
                    {
                        links[a].Add(b);
                        links[b].Add(a);
                    }
                }
            }
        }

        /// <summary>The web's points.</summary>
        public IReadOnlyList<Vector2> Nodes => nodes;

        private int Add(Vector2 point, int previous, int lane, List<int> lanes)
        {
            nodes.Add(point);
            links.Add(new List<int>());
            lanes.Add(lane);
            int index = nodes.Count - 1;
            if (previous >= 0)
            {
                links[index].Add(previous);
                links[previous].Add(index);
            }
            return index;
        }

        /// <summary>The spot on the apron in front of a building, <paramref name="across"/> metres along its front from the middle.</summary>
        public Vector2 Front(int building, float across = 0f, float forward = 1.4f)
        {
            return site.Buildings[building].ToWorld(new Vector2(across, TownLayout.ShopDepth * 0.5f + forward));
        }

        /// <summary>True when a point is inside a building (or within <see cref="Clearance"/> of its walls).</summary>
        public bool IsBlocked(Vector2 point)
        {
            float halfWidth = TownLayout.ShopWidth * 0.5f + Clearance, halfDepth = TownLayout.ShopDepth * 0.5f + Clearance;
            foreach (SettlementBuilding building in site.Buildings)
            {
                Vector2 local = TownLayout.Turn(point - building.Centre, -building.Yaw);
                if (Mathf.Abs(local.x) < halfWidth && Mathf.Abs(local.y) < halfDepth)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The length of the straight way between two points, a metre inside a building counting many times over.</summary>
        public float StraightCost(Vector2 from, Vector2 to)
        {
            float length = Vector2.Distance(from, to);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / 0.75f));
            float cost = 0f;
            for (int s = 0; s < steps; s++)
            {
                cost += (length / steps) * (IsBlocked(Vector2.Lerp(from, to, (s + 0.5f) / steps)) ? BlockedCost : 1f);
            }
            return cost;
        }

        /// <summary>
        /// The way from one point to another, as the corners to walk through, <paramref name="to"/> last
        /// (<paramref name="from"/> is not in it). Straight when that is clear and no longer than by the lanes.
        /// </summary>
        public void Route(Vector2 from, Vector2 to, List<Vector2> path)
        {
            path.Clear();
            float direct = StraightCost(from, to);
            if (nodes.Count == 0 || direct <= Vector2.Distance(from, to) + 0.01f && direct < 3f * Spacing)
            {
                path.Add(to);
                return;
            }
            // Dijkstra over the web, from every point at the cost of reaching it straight from the start.
            int count = nodes.Count;
            var cost = new float[count];
            var before = new int[count];
            var done = new bool[count];
            for (int i = 0; i < count; i++)
            {
                cost[i] = StraightCost(from, nodes[i]);
                before[i] = -1;
            }
            for (int round = 0; round < count; round++)
            {
                int best = -1;
                for (int i = 0; i < count; i++)
                {
                    if (!done[i] && (best < 0 || cost[i] < cost[best]))
                    {
                        best = i;
                    }
                }
                done[best] = true;
                foreach (int next in links[best])
                {
                    float through = cost[best] + Vector2.Distance(nodes[best], nodes[next]);
                    if (through < cost[next])
                    {
                        cost[next] = through;
                        before[next] = best;
                    }
                }
            }
            int leave = -1;
            float total = direct;
            for (int i = 0; i < count; i++)
            {
                float whole = cost[i] + StraightCost(nodes[i], to);
                if (whole < total)
                {
                    total = whole;
                    leave = i;
                }
            }
            int at = path.Count;
            for (int i = leave; i >= 0; i = before[i])
            {
                path.Insert(at, nodes[i]);
            }
            path.Add(to);
        }
    }
}
