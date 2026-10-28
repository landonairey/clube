using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// The far terrain's part of the lab panel (P7): on or off, how far it reaches, how fine its
    /// levels are, a tint per level to see the rings, and what it costs.
    /// </summary>
    public static class FarTerrainControls
    {
        public static void Draw(LabPanelFrame frame, FarTerrainView far)
        {
            bool show = frame.ToggleField("Far terrain", far.Show);
            if (show != far.Show)
            {
                far.Show = show;
            }
            if (!show)
            {
                return;
            }

            // Whole kilometres.
            float range = Mathf.Round(frame.Slider("Reach (km)", far.Range / 1000f, FarTerrainView.MinRange / 1000f, FarTerrainView.MaxRange / 1000f, "0")) * 1000f;
            if (!Mathf.Approximately(range, far.Range))
            {
                far.Range = range;
            }
            float spacing = Mathf.Round(frame.Slider("Finest spacing (m)", far.BaseSpacing, 0.5f, 8f, "0.#") * 2f) / 2f;
            if (!Mathf.Approximately(spacing, far.BaseSpacing))
            {
                far.BaseSpacing = spacing;
            }
            int quads = Mathf.RoundToInt(frame.Slider("Quads per level", far.Quads, 32f, 252f, "0") / 4f) * 4;
            if (quads != far.Quads)
            {
                far.Quads = quads;
            }
            bool tint = frame.ToggleField("Tint each level", far.TintLevels);
            if (tint != far.TintLevels)
            {
                far.TintLevels = tint;
            }

            if (far.Problem != null)
            {
                frame.Line(far.Problem);
            }
            else if (far.LevelCount > 0)
            {
                frame.Line($"{far.LevelCount} levels to {far.Reach / 1000f:0.#} km · {far.VertexCount:N0} vertices");
                frame.Line($"Last rebuild: {far.LastBuildLevels} levels, {far.LastBuildMilliseconds:0.0} ms on the workers");
            }
        }
    }
}
