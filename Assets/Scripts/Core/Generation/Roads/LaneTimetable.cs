using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// GL847: where along a vessel's lane it is when its timetable has it some distance along its planned route. The lane
    /// is found on the real water near the route (<see cref="WaterLanes"/>): it wanders, it is longer or shorter, and it
    /// may cover only a stretch of the route, so the same fraction of each is not the same place. Each lane point is
    /// matched to the route distance of the route point nearest it (never going back), and a route distance is looked up
    /// between them, so the vessel in the world stays beside its place on the map.
    /// </summary>
    public sealed class LaneTimetable
    {
        private readonly float[] routeAt;
        private readonly float[] laneAt;

        /// <summary>Matches each point of <paramref name="lane"/> to the distance along <paramref name="route"/> nearest it.</summary>
        public LaneTimetable(IReadOnlyList<Vector2> lane, IReadOnlyList<Vector2> route)
        {
            int count = lane.Count;
            routeAt = new float[count];
            laneAt = new float[count];
            var routeStarts = new float[route.Count];
            for (int i = 1; i < route.Count; i++)
            {
                routeStarts[i] = routeStarts[i - 1] + (route[i] - route[i - 1]).magnitude;
            }
            float most = 0f;
            for (int i = 0; i < count; i++)
            {
                laneAt[i] = i == 0 ? 0f : laneAt[i - 1] + (lane[i] - lane[i - 1]).magnitude;
                most = Mathf.Max(most, Nearest(lane[i], route, routeStarts));
                routeAt[i] = most;
            }
        }

        /// <summary>Distance along the lane for a distance along the route; the lane's ends where the route runs past them.</summary>
        public float LaneDistance(float routeDistance)
        {
            int count = routeAt.Length;
            if (count == 0)
            {
                return 0f;
            }
            if (routeDistance <= routeAt[0])
            {
                return 0f;
            }
            if (routeDistance >= routeAt[count - 1])
            {
                return laneAt[count - 1];
            }
            int low = 0, high = count - 1;
            while (high - low > 1)
            {
                int middle = (low + high) / 2;
                if (routeAt[middle] <= routeDistance)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }
            float span = routeAt[high] - routeAt[low];
            float t = span > 1e-4f ? (routeDistance - routeAt[low]) / span : 0f;
            return Mathf.Lerp(laneAt[low], laneAt[high], t);
        }

        // The distance along the route of the route point nearest a place.
        private static float Nearest(Vector2 at, IReadOnlyList<Vector2> route, float[] starts)
        {
            float best = float.MaxValue, distance = 0f;
            for (int i = 0; i + 1 < route.Count; i++)
            {
                Vector2 a = route[i], ab = route[i + 1] - a;
                float lengthSquared = ab.sqrMagnitude;
                float t = lengthSquared > 1e-8f ? Mathf.Clamp01(Vector2.Dot(at - a, ab) / lengthSquared) : 0f;
                float off = (a + ab * t - at).sqrMagnitude;
                if (off < best)
                {
                    best = off;
                    distance = starts[i] + Mathf.Sqrt(lengthSquared) * t;
                }
            }
            return distance;
        }
    }
}
