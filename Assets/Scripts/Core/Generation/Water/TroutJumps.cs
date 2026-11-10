using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// When river trout jump (GL466): plain math from the hour of the day and the daylight. Often in the morning, the
    /// rest of the day less, least in the afternoon, and never at night.
    /// </summary>
    public static class TroutJumps
    {
        /// <summary>Jumps a minute, in the stretch of river in view, at the morning peak.</summary>
        public const float PeakPerMinute = 6f;

        /// <summary>
        /// How active the trout are, 0-1: 1 through the early morning to 09:00, easing to 0.45 by noon and 0.15 by
        /// 15:00, staying there until dusk; 0 when there is little daylight.
        /// </summary>
        public static float Activity(float hour, float daylight)
        {
            if (daylight < 0.35f)
            {
                return 0f;
            }
            float byHour = hour < 9f
                ? 1f
                : hour < 12f
                    ? Mathf.Lerp(1f, 0.45f, (hour - 9f) / 3f)
                    : hour < 15f ? Mathf.Lerp(0.45f, 0.15f, (hour - 12f) / 3f) : 0.15f;
            return byHour * Mathf.InverseLerp(0.35f, 0.7f, daylight);
        }

        /// <summary>Rivers shallower than this (metres) are too shallow for a trout to leap from.</summary>
        public const float MinDepth = 0.4f;

        /// <summary>
        /// Finds the water surface at (x, z) when it's a river cell deep enough for a trout, with the current's
        /// direction there on the ground plane (zero when still).
        /// </summary>
        public static bool TryRiverSpot(WaterMap map, float x, float z, out Vector3 surfacePoint, out Vector2 flow)
        {
            surfacePoint = default;
            flow = default;
            if (!map.IsCreated || !map.TrySurfaceAt(new Unity.Mathematics.float2(x, z), out float surface, out int cell)
                || map.Kind[cell] != (byte)WaterKind.River || map.Depth[cell] < MinDepth)
            {
                return false;
            }
            surfacePoint = new Vector3(x, surface, z);
            Unity.Mathematics.float2 f = map.Flow[cell];
            flow = new Vector2(f.x, f.y);
            return true;
        }

        /// <summary>The chance of a jump in a frame of <paramref name="deltaTime"/> seconds.</summary>
        public static float ChanceIn(float deltaTime, float hour, float daylight)
        {
            return Activity(hour, daylight) * PeakPerMinute / 60f * deltaTime;
        }
    }
}
