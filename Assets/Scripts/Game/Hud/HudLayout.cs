using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Where the top-left corner of the screen is taken (GL216): a box drawn there (the lab HUD) reports its bottom
    /// each frame, and what goes under it (<see cref="QuestHud"/>) starts below. Without a report in the last frame,
    /// the corner is free.
    /// </summary>
    public static class HudLayout
    {
        private const float Margin = 10f;

        private static float reportedBottom;
        private static int reportedFrame = -10;

        /// <summary>Reports a box drawn in the top-left corner, down to <paramref name="bottom"/> in GUI pixels.</summary>
        public static void ReportTopLeft(float bottom)
        {
            if (reportedFrame != Time.frameCount)
            {
                reportedBottom = 0f;
                reportedFrame = Time.frameCount;
            }
            reportedBottom = Mathf.Max(reportedBottom, bottom);
        }

        /// <summary>Where the next thing in the top-left corner starts: under what was reported this frame or the last.</summary>
        public static float TopLeftFree => Time.frameCount - reportedFrame <= 1 ? reportedBottom + 6f : Margin;
    }
}
