using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// How long the player waits between uses of a tool (GL270): a second, a quarter less with bare hands, and a quarter
    /// less again at dawn (the early bird's bonus, <see cref="DawnFrom"/> to <see cref="DawnTo"/>, round the 06:00 sunrise).
    /// Every use waits it out, a click as much as a held button.
    /// </summary>
    /// <remarks>Plain math.</remarks>
    public static class ToolCooldown
    {
        /// <summary>Seconds between uses of a tool.</summary>
        public const float Seconds = 1f;

        /// <summary>Bare hands are this share of a tool's wait: a quarter quicker.</summary>
        public const float HandShare = 0.75f;

        /// <summary>At dawn every wait is this share: a quarter quicker.</summary>
        public const float DawnShare = 0.75f;

        /// <summary>When dawn starts and ends, hours of the day.</summary>
        public const float DawnFrom = 4.5f;
        public const float DawnTo = 9.5f;

        /// <summary>True at dawn, when the early bird's bonus holds.</summary>
        public static bool IsDawn(float hour)
        {
            float h = Mathf.Repeat(hour, 24f);
            return h >= DawnFrom && h < DawnTo;
        }

        /// <summary>
        /// Seconds to wait after a use, with bare hands or a tool, at an hour of the day; <paramref name="boost"/> is a share
        /// from a drink on top (GL456: coffee, 0.75).
        /// </summary>
        public static float SecondsFor(bool hands, float hour, float boost = 1f)
        {
            return Seconds * (hands ? HandShare : 1f) * (IsDawn(hour) ? DawnShare : 1f) * Mathf.Clamp(boost, 0.1f, 1f);
        }
    }
}
