using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// The brush section of the in-game lab panels (K33, K31): the click tool (select,
    /// dig, add), radius, strength and falloff of a <see cref="TerrainBrushTool"/>, the
    /// material Add places (M10), and running totals of the volume it has placed and dug out.
    /// </summary>
    public static class BrushControls
    {
        private static readonly string[] ToolNames = { "Select", "Dig", "Add" };
        private static readonly string[] FalloffNames = { "Hard", "Soft" };
        private static readonly List<VoxelMaterial> Choices = new List<VoxelMaterial>();
        private static readonly List<string> Names = new List<string>();

        /// <param name="materials">The world's materials, to pick what Add places (M10); null hides the picker.</param>
        public static void Draw(LabPanelFrame frame, TerrainBrushTool brush, MaterialRegistry materials = null)
        {
            var tool = (ChunkClickTool)GUILayout.Toolbar((int)brush.Tool, ToolNames, frame.Button);
            if (tool != brush.Tool)
            {
                brush.Tool = tool;
            }

            brush.Radius = frame.Slider("Radius", brush.Radius, TerrainBrushTool.MinRadius, TerrainBrushTool.MaxRadius, "0.##");
            brush.Strength = frame.Slider("Strength", brush.Strength, 0.01f, 1f, "0.##");
            brush.Falloff = (BrushFalloff)GUILayout.Toolbar((int)brush.Falloff, FalloffNames, frame.Button);
            if (materials != null)
            {
                DrawAddMaterial(frame, brush, materials);
            }

            // Running totals of what the brush has moved (approximate: density × voxel volume).
            float net = brush.TotalVolumeAdded - brush.TotalVolumeRemoved;
            frame.Line($"Placed {brush.TotalVolumeAdded:0.0} u³ · dug {brush.TotalVolumeRemoved:0.0} u³");
            frame.Line($"Net change {net:+0.0;-0.0;0.0} u³");
            if (GUILayout.Button("Reset volume totals", frame.Button))
            {
                brush.ResetVolumeTotals();
            }
        }

        // What Add places, as a grid of the registry's materials.
        private static void DrawAddMaterial(LabPanelFrame frame, TerrainBrushTool brush, MaterialRegistry materials)
        {
            Choices.Clear();
            Names.Clear();
            foreach (VoxelMaterial material in materials.Materials)
            {
                if (material != null)
                {
                    Choices.Add(material);
                    Names.Add(material.DisplayName);
                }
            }
            if (Choices.Count == 0)
            {
                return;
            }

            GUILayout.Label("Add places", frame.Label);
            int current = Choices.IndexOf(brush.AddMaterial);
            int picked = GUILayout.SelectionGrid(current, Names.ToArray(), 3, frame.Button, GUILayout.Width(frame.InnerWidth));
            if (picked != current && picked >= 0)
            {
                brush.AddMaterial = Choices[picked];
            }
        }
    }
}
