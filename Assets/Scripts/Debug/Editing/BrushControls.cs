using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// The brush section of the in-game lab panels (K33, K31): the click tool (select,
    /// dig, add), radius, strength and falloff of a <see cref="TerrainBrushTool"/>.
    /// </summary>
    public static class BrushControls
    {
        private static readonly string[] ToolNames = { "Select", "Dig", "Add" };
        private static readonly string[] FalloffNames = { "Hard", "Smooth" };

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
        }
    }
}
