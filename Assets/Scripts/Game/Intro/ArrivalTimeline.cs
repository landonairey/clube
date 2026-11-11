using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The timings and easing of the arrival cutscene (GL583), apart from the scene objects: how long each part lasts, and the
    /// black the screen starts under while the character's eyes open. About 10 s in all.
    /// </summary>
    public static class ArrivalTimeline
    {
        /// <summary>Seconds in the cart, driving up under the sky.</summary>
        public const float RideSeconds = 5f;

        /// <summary>Seconds the cart stands still and tips.</summary>
        public const float TipSeconds = 1.5f;

        /// <summary>Seconds lying where the player landed.</summary>
        public const float LieSeconds = 1.7f;

        /// <summary>Seconds getting up.</summary>
        public const float RiseSeconds = 1.8f;

        /// <summary>Metres the cart drives in from.</summary>
        public const float ApproachMetres = 22f;

        /// <summary>Degrees the bed tips back.</summary>
        public const float TipDegrees = 38f;

        /// <summary>
        /// How black the screen is <paramref name="seconds"/> after the cutscene starts, 1 to 0: the eyes half open, shut once more,
        /// then open for good.
        /// </summary>
        public static float Black(float seconds)
        {
            if (seconds < 0.5f)
            {
                return Mathf.Lerp(1f, 0.55f, Smooth(seconds / 0.5f));
            }
            if (seconds < 0.85f)
            {
                return Mathf.Lerp(0.55f, 0.85f, Smooth((seconds - 0.5f) / 0.35f));
            }
            return Mathf.Lerp(0.85f, 0f, Smooth((seconds - 0.85f) / 1.4f));
        }

        /// <summary>Ease in and out over 0 to 1 (clamped).</summary>
        public static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// The metres past the stop the cart is <paramref name="seconds"/> into the ride: <see cref="ApproachMetres"/> before it,
        /// slowing to a halt at it.
        /// </summary>
        public static float RideAlong(float seconds)
        {
            return -ApproachMetres * (1f - Mathf.Sin(Mathf.Clamp01(seconds / RideSeconds) * Mathf.PI * 0.5f));
        }

        /// <summary>The camera's jolt on landing, <paramref name="seconds"/> after it: a quick wobble that dies away (-1 to 1).</summary>
        public static float Jolt(float seconds)
        {
            return Mathf.Exp(-7f * seconds) * Mathf.Sin(seconds * 38f);
        }
    }
}
