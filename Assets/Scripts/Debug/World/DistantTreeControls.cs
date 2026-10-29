using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>The distant trees' part of the lab panel (GL162): on or off, how far, where the plain mesh starts, and counts.</summary>
    public static class DistantTreeControls
    {
        public static void Draw(LabPanelFrame frame, DistantTreeView trees)
        {
            bool show = frame.ToggleField("Distant trees", trees.Show);
            if (show != trees.Show)
            {
                trees.Show = show;
            }
            if (!show)
            {
                return;
            }

            // Whole hundreds of metres.
            float range = Mathf.Round(frame.Slider("Reach (m)", trees.Range, DistantTreeView.MinRange, DistantTreeView.MaxRange, "0") / 100f) * 100f;
            if (!Mathf.Approximately(range, trees.Range))
            {
                trees.Range = range;
            }
            float detail = Mathf.Round(frame.Slider("Detailed within (m)", trees.DetailDistance, 50f, 2000f, "0") / 10f) * 10f;
            if (!Mathf.Approximately(detail, trees.DetailDistance))
            {
                trees.DetailDistance = detail;
            }

            if (trees.Problem != null)
            {
                frame.Line(trees.Problem);
            }
            else
            {
                frame.Line($"{trees.TreeCount:N0} trees, {trees.DrawnCount:N0} in view · {trees.LastUpdateMilliseconds:0.00} ms a frame" + (trees.PendingTiles > 0 ? $" · {trees.PendingTiles} tiles to place" : ""));
            }
        }
    }
}
