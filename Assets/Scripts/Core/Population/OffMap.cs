using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// GL725: the stretch of a trade route past the map's edge, "to simulate trading with a larger world". A road that
    /// leaves the map, or a sea route out to the open sea, goes on from its end straight out of the map until past the
    /// line the player cannot pass, and a fade's length beyond. Along it, a cart or vessel can be dealt with only while
    /// it is on free ground (<see cref="Interactable"/>), and it fades out past the border (<see cref="Fade"/>).
    /// </summary>
    public sealed class OffMapPath
    {
        /// <summary>The way on from the route's end (x, z), its first point that end.</summary>
        public List<Vector2> Points = new List<Vector2>();

        /// <summary>Metres along <see cref="Points"/> where it leaves the free ground (the buildable area).</summary>
        public float Leaves;

        /// <summary>Metres along <see cref="Points"/> where it passes the map's border (the line the player cannot pass).</summary>
        public float Border;

        /// <summary>Its whole length, in metres: past the border by <see cref="OffMap.FadeLength"/>.</summary>
        public float Length;
    }

    /// <summary>Lays out <see cref="OffMapPath"/>s and says how present a traveller on one is.</summary>
    public static class OffMap
    {
        /// <summary>Metres past the border over which a traveller fades out.</summary>
        public const float FadeLength = 60f;

        /// <summary>Metres between the points of a path off the map.</summary>
        public const float Step = 10f;

        /// <summary>The longest a path off the map runs before the border, in metres, whatever the edge.</summary>
        public const float Longest = 2000f;

        /// <summary>
        /// The way on from <paramref name="end"/> (where a route stops on the map), heading out along
        /// <paramref name="along"/> at first and turning out of the map (<paramref name="outwardAt"/>) within a few steps.
        /// <paramref name="walksFreely"/> is true on the free ground; <paramref name="limitAt"/> is 0 there and 1 at the border.
        /// </summary>
        public static OffMapPath Lay(Vector2 end, Vector2 along, Func<Vector2, bool> walksFreely, Func<Vector2, float> limitAt, Func<Vector2, Vector2> outwardAt)
        {
            var path = new OffMapPath();
            path.Points.Add(end);
            Vector2 heading = along.sqrMagnitude > 1e-6f ? along.normalized : outwardAt(end);
            Vector2 at = end;
            float travelled = 0f;
            bool left = !walksFreely(end), passed = limitAt(end) >= 1f;
            path.Leaves = left ? 0f : float.NaN;
            path.Border = passed ? 0f : float.NaN;
            float beyond = 0f;
            while (beyond < FadeLength && travelled < Longest + FadeLength)
            {
                Vector2 outward = outwardAt(at);
                if (outward.sqrMagnitude > 1e-6f)
                {
                    // Turns out of the map by a third each step, so a road heading along the edge bends off it smoothly.
                    heading = Vector2.Lerp(heading, outward.normalized, 0.34f).normalized;
                }
                at += heading * Step;
                travelled += Step;
                path.Points.Add(at);
                if (!left && !walksFreely(at))
                {
                    left = true;
                    path.Leaves = travelled;
                }
                if (!passed && limitAt(at) >= 1f)
                {
                    passed = true;
                    path.Border = travelled;
                }
                if (passed)
                {
                    beyond = travelled - path.Border;
                }
                else if (travelled >= Longest)
                {
                    // An edge whose limit never closes (no stop line within reach): the border is taken here.
                    passed = true;
                    path.Border = travelled;
                    path.Leaves = left ? path.Leaves : travelled;
                    left = true;
                }
            }
            if (!left)
            {
                path.Leaves = path.Border;
            }
            path.Leaves = Mathf.Min(path.Leaves, path.Border);
            path.Length = travelled;
            return path;
        }

        /// <summary>True while a traveller <paramref name="along"/> metres down a path off the map is still on the free ground.</summary>
        public static bool Interactable(OffMapPath path, float along)
        {
            return path == null || along < path.Leaves;
        }

        /// <summary>How much of a traveller <paramref name="along"/> metres down a path off the map is seen: 1 up to the border, 0 a fade past it.</summary>
        public static float Fade(OffMapPath path, float along)
        {
            return path == null ? 1f : 1f - Mathf.Clamp01((along - path.Border) / FadeLength);
        }
    }
}
