using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// An open pit dug down to a node near the surface (GL474: the town's tin mine), by hand, a few samples at a time. Its
    /// final shape is a cone opening upward: a floor of <see cref="FloorRadius"/> at the floor's height, walls at 45°, so it
    /// can be walked into. The digging goes in shells parallel to those walls, centre and top first, so it's a small pit that
    /// widens and deepens together. Samples are emptied through the world's edit path (A7).
    /// </summary>
    public static class OpenPit
    {
        /// <summary>The radius of the pit's floor, in metres.</summary>
        public const float FloorRadius = 1.5f;

        /// <summary>The deepest a pit is dug (from the ground over the node to its floor); deeper nodes get a stair shaft.</summary>
        public const float MaxDepth = 6f;

        /// <summary>The pit's radius at the ground, for a floor that far under it.</summary>
        public static float RimRadius(float depth)
        {
            return FloorRadius + Mathf.Max(0f, depth);
        }

        /// <summary>True for a point (the world's local metres) inside the finished pit over a floor at <paramref name="floor"/>.</summary>
        public static bool Contains(Vector3 point, Vector2 centre, float floor)
        {
            float distance = (new Vector2(point.x, point.z) - centre).magnitude;
            return point.y >= floor && point.y - floor >= distance - FloorRadius;
        }

        // The order samples are dug in: highest over the pit's final wall first (y - distance), nearer the middle on a tie.
        private static float Order(Vector3 point, Vector2 centre)
        {
            float distance = (new Vector2(point.x, point.z) - centre).magnitude;
            return point.y - distance - distance * 1e-3f;
        }

        /// <summary>
        /// Every solid sample inside the finished pit, up to <paramref name="top"/> metres, in the order they're dug.
        /// Worked out once; the digging then takes them off the front.
        /// </summary>
        public static Queue<Vector3Int> Plan(World world, Vector2 centre, float floor, float top, float iso)
        {
            WorldGrid grid = world.Grid;
            float rim = RimRadius(top - floor);
            Vector3Int low = grid.WorldToNearestSample(new Vector3(centre.x - rim, floor, centre.y - rim));
            Vector3Int high = grid.WorldToNearestSample(new Vector3(centre.x + rim, top, centre.y + rim));
            var samples = new List<(Vector3Int Sample, float Order)>();
            for (int z = low.z; z <= high.z; z++)
            {
                for (int x = low.x; x <= high.x; x++)
                {
                    for (int y = low.y; y <= high.y; y++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        Vector3 at = grid.SampleToWorld(sample);
                        if (!Contains(at, centre, floor) || !world.TryGetOwner(sample, out Chunk chunk, out Vector3Int local) || chunk.GetDensity(local) < iso)
                        {
                            continue;
                        }
                        samples.Add((sample, Order(at, centre)));
                    }
                }
            }
            samples.Sort((a, b) => b.Order.CompareTo(a.Order));
            var queue = new Queue<Vector3Int>(samples.Count);
            foreach ((Vector3Int sample, float _) in samples)
            {
                queue.Enqueue(sample);
            }
            return queue;
        }

        /// <summary>
        /// Empties up to <paramref name="count"/> more samples of the pit (skipping any already gone); returns how many of them
        /// were one of <paramref name="ores"/>.
        /// </summary>
        public static int Dig(World world, Queue<Vector3Int> order, int count, float iso, ICollection<byte> ores)
        {
            int ore = 0;
            while (count > 0 && order.Count > 0)
            {
                Vector3Int sample = order.Dequeue();
                if (!world.TryGetOwner(sample, out Chunk chunk, out Vector3Int local) || chunk.GetDensity(local) < iso)
                {
                    continue;
                }
                if (ores != null && ores.Contains(chunk.GetMaterial(local)))
                {
                    ore++;
                }
                world.SetDensity(sample, 0f);
                count--;
            }
            return ore;
        }
    }
}
