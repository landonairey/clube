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
        private static float previousBottom;

        /// <summary>Reports a box drawn in the top-left corner, down to <paramref name="bottom"/> in GUI pixels.</summary>
        public static void ReportTopLeft(float bottom)
        {
            if (reportedFrame != Time.frameCount)
            {
                // Keep last frame's whole stack: boxes report in any order, and one read mid-frame sees only some (GL461).
                previousBottom = reportedFrame == Time.frameCount - 1 ? reportedBottom : 0f;
                reportedBottom = 0f;
                reportedFrame = Time.frameCount;
            }
            reportedBottom = Mathf.Max(reportedBottom, bottom);
        }

        private static float minimapBottom;
        private static int minimapFrame = -10;

        /// <summary>The minimap, first in the corner (GL461), reports its bottom; the lab HUD box starts under it.</summary>
        public static void ReportMinimap(float bottom)
        {
            minimapBottom = bottom;
            minimapFrame = Time.frameCount;
            ReportTopLeft(bottom);
        }

        /// <summary>Where the lab HUD box starts: under the minimap when it showed this frame or the last.</summary>
        public static float BelowMinimap => Time.frameCount - minimapFrame <= 1 ? minimapBottom + 6f : Margin;

        /// <summary>Where the next thing in the top-left corner starts: under what was reported this frame or the last.</summary>
        public static float TopLeftFree
        {
            get
            {
                float bottom = Time.frameCount - reportedFrame <= 1 ? reportedBottom : 0f;
                if (reportedFrame == Time.frameCount)
                {
                    bottom = Mathf.Max(bottom, previousBottom);
                }
                return bottom > 0f ? bottom + 6f : Margin;
            }
        }
    }
}
