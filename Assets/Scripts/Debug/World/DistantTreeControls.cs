using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>The distant trees' part of the lab panel (GL162): on or off, how far, where each level of detail ends and how wide they cross-fade (GL258), and counts.</summary>
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
            // Where each level of detail ends (GL258), in whole tens of metres.
            Vector3 ends = trees.LevelEnds;
            float full = Mathf.Round(frame.Slider("Full trees within (m)", ends.x, 50f, 2000f, "0") / 10f) * 10f;
            float clusters = Mathf.Round(frame.Slider("Leaf clusters within (m)", ends.y, 70f, 4000f, "0") / 10f) * 10f;
            float crowns = Mathf.Round(frame.Slider("Crowns within (m)", ends.z, 90f, DistantTreeView.MaxRange, "0") / 10f) * 10f;
            var picked = new Vector3(full, clusters, crowns);
            if (picked != ends)
            {
                trees.LevelEnds = picked;
            }
            float fade = Mathf.Round(frame.Slider("Cross-fade (m)", trees.FadeWidth, 0f, 200f, "0"));
            if (!Mathf.Approximately(fade, trees.FadeWidth))
            {
                trees.FadeWidth = fade;
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
