using System.Diagnostics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The share of a frame all the scripts may use between them (GL770). Each system that works through a backlog (grass
    /// and bushes growing, water meshing, distant trees, writing roads) has its own budget, and with the world always
    /// streaming they all spent it every frame: the frame was their sum. A system asks <see cref="Limit"/> before it takes
    /// on more, so what a slow frame can't afford waits for the next one. Each still does at least one piece a frame.
    /// </summary>
    public static class FrameBudget
    {
        /// <summary>Milliseconds for the scripts, leaving the rest of 16.7 ms for drawing the frame.</summary>
        public const float ScriptMilliseconds = 12.5f;

        private static int frame = -1;
        private static long started;

        /// <summary>Starts the frame's clock; the first caller of a frame counts. Call it early in the frame.</summary>
        public static void StartFrame(int frameCount)
        {
            if (frameCount != frame)
            {
                frame = frameCount;
                started = Stopwatch.GetTimestamp();
            }
        }

        /// <summary>Milliseconds the scripts have used so far this frame; none where nothing started the clock (the labs).</summary>
        public static float SpentMilliseconds =>
            Time.frameCount == frame ? (float)((Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency) : 0f;

        /// <summary>True while the scripts are within their share of this frame.</summary>
        public static bool HasRoom => SpentMilliseconds < ScriptMilliseconds;

        /// <summary>A system's own budget, cut to what is left of the frame.</summary>
        public static float Limit(float budgetMilliseconds)
        {
            return Limited(budgetMilliseconds, SpentMilliseconds);
        }

        /// <summary>A budget cut to what is left once <paramref name="spentMilliseconds"/> have gone; never below nothing.</summary>
        public static float Limited(float budgetMilliseconds, float spentMilliseconds)
        {
            return Mathf.Min(budgetMilliseconds, Mathf.Max(0f, ScriptMilliseconds - spentMilliseconds));
        }
    }
}
