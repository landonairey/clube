using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// The brush section of the in-game lab panels (K33, K31): the click tool (select,
    /// dig, add), radius, strength and falloff of a <see cref="TerrainBrushTool"/>, and
    /// running totals of the volume it has placed and dug out.
    /// </summary>
    public static class BrushControls
    {
        private static readonly string[] ToolNames = { "Select", "Dig", "Add" };
        private static readonly string[] FalloffNames = { "Hard", "Soft" };

        public static void Draw(LabPanelFrame frame, TerrainBrushTool brush)
        {
            var tool = (ChunkClickTool)GUILayout.Toolbar((int)brush.Tool, ToolNames, frame.Button);
            if (tool != brush.Tool)
            {
                brush.Tool = tool;
            }

            brush.Radius = frame.Slider("Radius", brush.Radius, TerrainBrushTool.MinRadius, TerrainBrushTool.MaxRadius, "0.##");
            brush.Strength = frame.Slider("Strength", brush.Strength, 0.01f, 1f, "0.##");
            brush.Falloff = (BrushFalloff)GUILayout.Toolbar((int)brush.Falloff, FalloffNames, frame.Button);

            // Running totals of what the brush has moved (approximate: density × voxel volume).
            float net = brush.TotalVolumeAdded - brush.TotalVolumeRemoved;
            frame.Line($"Placed {brush.TotalVolumeAdded:0.0} u³ · dug {brush.TotalVolumeRemoved:0.0} u³");
            frame.Line($"Net change {net:+0.0;-0.0;0.0} u³");
            if (GUILayout.Button("Reset volume totals", frame.Button))
            {
                brush.ResetVolumeTotals();
            }
        }
    }
}
