using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The plan's road segments by map cell, for asking whether a point is on a road (GL771): looking through every point of
    /// every road took a tenth of a millisecond a question, and laying out a settlement's digs asks hundreds of them.
    /// </summary>
    internal sealed class RoadSegmentIndex
    {
        private const float CellSize = 32f;

        private readonly Dictionary<Vector2Int, List<(Vector2 A, Vector2 B, float HalfWidth)>> cells = new Dictionary<Vector2Int, List<(Vector2, Vector2, float)>>();
        private readonly List<(List<Vector2> Points, int Count, Vector2 Last)> builtFrom = new List<(List<Vector2>, int, Vector2)>();
        private readonly float widest;

        public RoadSegmentIndex(IReadOnlyList<PlannedRoad> roads, WorldPlanRules rules)
        {
            foreach (PlannedRoad road in roads)
            {
                List<Vector2> points = road.Points;
                builtFrom.Add((points, points.Count, points.Count > 0 ? points[points.Count - 1] : Vector2.zero));
                float halfWidth = rules.Road(road.Class).Width * 0.5f;
                widest = Mathf.Max(widest, halfWidth);
                for (int i = 1; i < points.Count; i++)
                {
                    Add(points[i - 1], points[i], halfWidth);
                }
            }
        }

        /// <summary>True while the roads are still the ones this was made from.</summary>
        public bool IsFor(IReadOnlyList<PlannedRoad> roads)
        {
            if (roads.Count != builtFrom.Count)
            {
                return false;
            }
            for (int i = 0; i < roads.Count; i++)
            {
                List<Vector2> points = roads[i].Points;
                (List<Vector2> known, int count, Vector2 last) = builtFrom[i];
                if (!ReferenceEquals(points, known) || points.Count != count || (count > 0 && points[count - 1] != last))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>True when the point lies on a road, or within a margin of its edge.</summary>
        public bool IsOn(Vector2 point, float margin)
        {
            // A segment is listed in the cells it passes through, sampled every half a cell.
            float reach = widest + margin + CellSize * 0.5f;
            Vector2Int low = CellOf(point - Vector2.one * reach), high = CellOf(point + Vector2.one * reach);
            for (int z = low.y; z <= high.y; z++)
            {
                for (int x = low.x; x <= high.x; x++)
                {
                    if (!cells.TryGetValue(new Vector2Int(x, z), out List<(Vector2 A, Vector2 B, float HalfWidth)> segments))
                    {
                        continue;
                    }
                    foreach ((Vector2 a, Vector2 b, float halfWidth) in segments)
                    {
                        if (SettlementSite.Distance(point, a, b) <= halfWidth + margin)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private void Add(Vector2 a, Vector2 b, float halfWidth)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / (CellSize * 0.5f)));
            var last = new Vector2Int(int.MinValue, int.MinValue);
            for (int s = 0; s <= steps; s++)
            {
                Vector2Int cell = CellOf(Vector2.Lerp(a, b, s / (float)steps));
                if (cell == last)
                {
                    continue;
                }
                last = cell;
                if (!cells.TryGetValue(cell, out List<(Vector2 A, Vector2 B, float HalfWidth)> segments))
                {
                    segments = new List<(Vector2, Vector2, float)>();
                    cells.Add(cell, segments);
                }
                segments.Add((a, b, halfWidth));
            }
        }

        private static Vector2Int CellOf(Vector2 point)
        {
            return new Vector2Int(Mathf.FloorToInt(point.x / CellSize), Mathf.FloorToInt(point.y / CellSize));
        }
    }
}
