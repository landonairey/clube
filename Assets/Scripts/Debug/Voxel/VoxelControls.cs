using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// The voxel sections of the lab panel (K31, M20), for the <see cref="VoxelCornerEditor"/>'s
    /// voxel in any lab: its corner sliders (V2, K7), its case and what it means (V7, V9, V20),
    /// and one preset per base configuration (V10). In ChunkLab and WorldLab they say how to
    /// pick a voxel until one is selected.
    /// </summary>
    public static class VoxelControls
    {
        private const int PresetColumns = 2;
        private const float CornerLabelWidth = 64f;

        private static readonly Color CurrentPresetTint = new Color(1f, 0.8f, 0.4f);

        /// <summary>One slider per corner, 0 = empty to 1 = solid. False (with a hint) when there's no voxel.</summary>
        public static bool DrawCorners(LabPanelFrame frame, VoxelCornerEditor corners)
        {
            if (!DrawVoxelHeader(frame, corners))
            {
                return false;
            }

            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                float value = corners.GetCorner(corner);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"c{corner} {value:0.00}", frame.Label, GUILayout.Width(CornerLabelWidth));
                float chosen = frame.SliderBar(value, 0f, 1f);
                GUILayout.EndHorizontal();
                if (!Mathf.Approximately(chosen, value))
                {
                    corners.SetCorner(corner, chosen);
                }
            }

            string sharing = corners.UsesStoredCorners
                ? ""
                : " Corners are samples shared with neighbouring voxels, which reshape too.";
            GUILayout.Label("0 = empty, 1 = solid. Solid corner c sets bit c of the case index." + sharing, frame.Hint);
            return true;
        }

        /// <summary>The case index, stepping and jumping (V20), and what it means (V7, V9).</summary>
        public static void DrawCase(LabPanelFrame frame, VoxelCornerEditor corners)
        {
            if (!corners.HasVoxel)
            {
                DrawPickHint(frame);
                return;
            }

            int caseIndex = corners.CaseIndex;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Case {caseIndex}", frame.Header, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("◄", frame.Button))
            {
                corners.StepCase(-1);
            }
            if (GUILayout.Button("►", frame.Button))
            {
                corners.StepCase(+1);
            }
            GUILayout.EndHorizontal();

            int chosen = Mathf.RoundToInt(frame.SliderBar(caseIndex, 0f, MarchingCubes.CaseCount - 1));
            if (chosen != caseIndex)
            {
                corners.ApplyCase(chosen);
                caseIndex = corners.CaseIndex;
            }

            BaseConfiguration configuration = MarchingCubesCases.GetBaseConfiguration(caseIndex);
            GUILayout.Label(
                $"Binary (c7 → c0)  {VoxelCaseText.Binary(caseIndex)}\n" +
                $"Solid corners  {VoxelCaseText.List(VoxelCaseText.SolidCorners(caseIndex))}\n" +
                $"Base configuration  {(int)configuration} · {BaseConfigurationText.Name(configuration)}\n" +
                $"Ambiguous face  {(MarchingCubesCases.HasAmbiguousFace(caseIndex) ? "yes" : "no")}\n" +
                $"Crossed edges  {VoxelCaseText.List(VoxelCaseText.CrossedEdges(caseIndex))}\n" +
                $"Triangles  {VoxelCaseText.TriangleCount(caseIndex)}",
                frame.Label);
            GUILayout.Label(BaseConfigurationText.Description(configuration), frame.Hint);
        }

        /// <summary>One button per base configuration (V10); the current one is highlighted.</summary>
        public static void DrawPresets(LabPanelFrame frame, VoxelCornerEditor corners)
        {
            if (!corners.HasVoxel)
            {
                DrawPickHint(frame);
                return;
            }

            BaseConfiguration current = MarchingCubesCases.GetBaseConfiguration(corners.CaseIndex);
            // Fixed widths, so the longer names wrap instead of widening the panel.
            float buttonWidth = (frame.InnerWidth - 4f * (PresetColumns - 1)) / PresetColumns;
            for (int row = 0; row * PresetColumns < MarchingCubesCases.BaseConfigurationCount; row++)
            {
                GUILayout.BeginHorizontal(GUILayout.Width(frame.InnerWidth));
                for (int column = 0; column < PresetColumns; column++)
                {
                    int index = row * PresetColumns + column;
                    if (index >= MarchingCubesCases.BaseConfigurationCount)
                    {
                        break;
                    }

                    var configuration = (BaseConfiguration)index;
                    int representative = MarchingCubesCases.GetRepresentativeCase(configuration);
                    bool ambiguous = MarchingCubesCases.HasAmbiguousFace(representative);
                    Color previous = GUI.backgroundColor;
                    if (configuration == current)
                    {
                        GUI.backgroundColor = CurrentPresetTint;
                    }
                    if (GUILayout.Button($"{index} {BaseConfigurationText.Name(configuration)}{(ambiguous ? " (!)" : "")}", frame.Button, GUILayout.Width(buttonWidth)))
                    {
                        corners.ApplyCase(representative);
                    }
                    GUI.backgroundColor = previous;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("One case per base configuration; (!) = ambiguous face.", frame.Hint);
        }

        // Names the selected voxel; VoxelLab's one voxel needs no name.
        private static bool DrawVoxelHeader(LabPanelFrame frame, VoxelCornerEditor corners)
        {
            if (!corners.HasVoxel)
            {
                DrawPickHint(frame);
                return false;
            }
            if (!corners.UsesStoredCorners)
            {
                Vector3Int voxel = corners.Voxel.Value;
                frame.Line($"Voxel ({voxel.x}, {voxel.y}, {voxel.z})");
            }
            return true;
        }

        private static void DrawPickHint(LabPanelFrame frame)
        {
            GUILayout.Label("Click a voxel on the surface (Select tool) to edit it.", frame.Hint);
        }
    }
}
