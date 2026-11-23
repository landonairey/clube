using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// GL805: the stretches of the roads laid so far that go over water (where their bridges will stand), so a road laid
    /// later never crosses one with a bridge of its own: two decks crossing at slightly different heights make a wall
    /// nobody can walk through. Kept in 20 m buckets for a quick look along a step.
    /// </summary>
    public sealed class BridgeDecks
    {
        private const float Bucket = 20f;

        private readonly List<(Vector2 A, Vector2 B)> decks = new List<(Vector2 A, Vector2 B)>();
        private readonly Dictionary<long, List<int>> buckets = new Dictionary<long, List<int>>();

        /// <summary>The stretches noted so far.</summary>
        public int Count => decks.Count;

        /// <summary>
        /// Notes the wet pieces of a road's line, and the piece on each side of a wet run (the deck reaches onto the banks).
        /// </summary>
        public void Note(IReadOnlyList<Vector2> points, Func<float, float, RoadWater> waterAt)
        {
            if (points == null || points.Count < 2 || waterAt == null)
            {
                return;
            }
            int pieces = points.Count - 1;
            var wet = new bool[pieces];
            for (int i = 0; i < pieces; i++)
            {
                Vector2 a = points[i], b = points[i + 1], middle = (a + b) * 0.5f;
                wet[i] = waterAt(a.x, a.y) != RoadWater.Dry || waterAt(middle.x, middle.y) != RoadWater.Dry || waterAt(b.x, b.y) != RoadWater.Dry;
            }
            for (int i = 0; i < pieces; i++)
            {
                if (wet[i] || (i > 0 && wet[i - 1]) || (i + 1 < pieces && wet[i + 1]))
                {
                    Add(points[i], points[i + 1]);
                }
            }
        }

        /// <summary>Notes one stretch of deck.</summary>
        public void Add(Vector2 a, Vector2 b)
        {
            int index = decks.Count;
            decks.Add((a, b));
            Bounds(a, b, out int x0, out int x1, out int z0, out int z1);
            for (int x = x0; x <= x1; x++)
            {
                for (int z = z0; z <= z1; z++)
                {
                    long key = Key(x, z);
                    if (!buckets.TryGetValue(key, out List<int> list))
                    {
                        buckets[key] = list = new List<int>();
                    }
                    list.Add(index);
                }
            }
        }

        /// <summary>True when the stretch from <paramref name="a"/> to <paramref name="b"/> crosses a noted deck.</summary>
        public bool Crosses(Vector2 a, Vector2 b)
        {
            if (decks.Count == 0)
            {
                return false;
            }
            Bounds(a, b, out int x0, out int x1, out int z0, out int z1);
            for (int x = x0; x <= x1; x++)
            {
                for (int z = z0; z <= z1; z++)
                {
                    if (!buckets.TryGetValue(Key(x, z), out List<int> list))
                    {
                        continue;
                    }
                    foreach (int i in list)
                    {
                        if (SegmentsCross(a, b, decks[i].A, decks[i].B))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>True when the two stretches cross (touching ends count).</summary>
        public static bool SegmentsCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            Vector2 r = b - a, s = d - c;
            float den = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(den) < 1e-6f)
            {
                return false;
            }
            float t = ((c.x - a.x) * s.y - (c.y - a.y) * s.x) / den;
            float u = ((c.x - a.x) * r.y - (c.y - a.y) * r.x) / den;
            return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
        }

        private static void Bounds(Vector2 a, Vector2 b, out int x0, out int x1, out int z0, out int z1)
        {
            x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x) / Bucket);
            x1 = Mathf.FloorToInt(Mathf.Max(a.x, b.x) / Bucket);
            z0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y) / Bucket);
            z1 = Mathf.FloorToInt(Mathf.Max(a.y, b.y) / Bucket);
        }

        private static long Key(int x, int z)
        {
            return ((long)x << 32) ^ (uint)z;
        }
    }
}
