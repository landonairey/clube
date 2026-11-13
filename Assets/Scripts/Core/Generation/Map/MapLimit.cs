using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The rule that ends the playable map (GL640): inside an edge's limit (<see cref="MapLayout.LimitAt"/>) each step
    /// outwards carries less far, down to nothing at the stop line; steps back in, and along the line, are never
    /// held. The player's own limit (<c>MapBoundaryLimit</c>) applies it frame by frame.
    /// </summary>
    public static class MapLimit
    {
        /// <summary>
        /// The share of an outward step that is taken back at a limit: none of it well short of the stop line, all
        /// of it at the line, easing between so the going gets harder before it stops.
        /// </summary>
        public static float Resistance(float limit)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 1f, limit));
        }

        /// <summary>A step on the ground plane (x, z) with its outward part held back by a limit.</summary>
        /// <param name="outward">The direction out of the map (length 1).</param>
        public static Vector2 Held(Vector2 step, Vector2 outward, float limit)
        {
            float outwards = Vector2.Dot(step, outward);
            return outwards <= 0f ? step : step - outward * (outwards * Resistance(limit));
        }

        /// <summary>A step that ends at <paramref name="end"/>, held by the map's limit there.</summary>
        public static Vector2 Held(MapLayout layout, Vector2 end, Vector2 step)
        {
            float limit = layout.LimitAt(end.x, end.y);
            return limit <= 0f ? step : Held(step, layout.OutwardAt(end.x, end.y), limit);
        }
    }
}
