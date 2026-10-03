using Clube.Core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// The per-voxel case panel shared by the voxel lab and the chunk lab's
    /// selected voxel: the case index the corners produce and what it means
    /// (V7, V9), a slider to jump to any case (V20), and one preset button per
    /// base configuration (V10).
    /// </summary>
    internal static class VoxelCaseGui
    {
        private const int PresetColumns = 3;

        private static readonly Color CurrentPresetTint = new Color(1f, 0.8f, 0.4f);

        /// <param name="undoTarget">Object to record for Undo when the case changes, or null if the edit isn't undoable.</param>
        public static void DrawCaseSection(IVoxelCaseTarget voxel, Object undoTarget)
        {
            int caseIndex = voxel.CaseIndex;
            EditorGUILayout.LabelField("Case", EditorStyles.boldLabel);

            // Not a delayed field: dragging the slider (or the label) applies every
            // step, so the mesh updates live instead of only on release.
            int chosen;
            int step = 0;
            using (new EditorGUILayout.HorizontalScope())
            {
                chosen = EditorGUILayout.IntSlider(
                    new GUIContent("Case index", "Type or drag to any case 0-255 to jump to it (V20)."),
                    caseIndex, 0, MarchingCubes.CaseCount - 1);
                if (GUILayout.Button(new GUIContent("◀", "Previous case (Left arrow in Play mode, VoxelLab)"), GUILayout.Width(24f)))
                {
                    step = -1;
                }
                if (GUILayout.Button(new GUIContent("▶", "Next case (Right arrow in Play mode, VoxelLab)"), GUILayout.Width(24f)))
                {
                    step = +1;
                }
            }

            if (step != 0)
            {
                Record(undoTarget, step > 0 ? "Next voxel case" : "Previous voxel case");
                voxel.StepCase(step);
                MarkChanged(undoTarget);
                caseIndex = voxel.CaseIndex;
            }
            else if (chosen != caseIndex)
            {
                ApplyCase(voxel, undoTarget, chosen);
                caseIndex = voxel.CaseIndex;
            }

            BaseConfiguration configuration = MarchingCubesCases.GetBaseConfiguration(caseIndex);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.LabelField("Binary (corner 7 → 0)", VoxelCaseText.Binary(caseIndex));
                EditorGUILayout.LabelField("Solid corners", VoxelCaseText.List(VoxelCaseText.SolidCorners(caseIndex)));
                EditorGUILayout.LabelField(
                    "Base configuration",
                    $"{(int)configuration} · {BaseConfigurationText.Name(configuration)}");
                EditorGUILayout.LabelField(" ", BaseConfigurationText.Description(configuration), EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField("Ambiguous face", MarchingCubesCases.HasAmbiguousFace(caseIndex) ? "Yes" : "No");
                EditorGUILayout.LabelField("Crossed edges", VoxelCaseText.List(VoxelCaseText.CrossedEdges(caseIndex)));
                EditorGUILayout.LabelField("Triangles", VoxelCaseText.TriangleCount(caseIndex).ToString());
            }
        }

        public static void DrawPresets(IVoxelCaseTarget voxel, Object undoTarget)
        {
            EditorGUILayout.LabelField("Presets (one per base configuration, ⚠ = ambiguous face)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("The highlighted preset is the current configuration.", EditorStyles.miniLabel);

            BaseConfiguration current = MarchingCubesCases.GetBaseConfiguration(voxel.CaseIndex);
            for (int row = 0; row * PresetColumns < MarchingCubesCases.BaseConfigurationCount; row++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int column = 0; column < PresetColumns; column++)
                    {
                        int index = row * PresetColumns + column;
                        if (index >= MarchingCubesCases.BaseConfigurationCount)
                        {
                            break;
                        }

                        var configuration = (BaseConfiguration)index;
                        DrawPresetButton(voxel, undoTarget, configuration, configuration == current);
                    }
                }
            }
        }

        private static void DrawPresetButton(IVoxelCaseTarget voxel, Object undoTarget, BaseConfiguration configuration, bool isCurrent)
        {
            int representative = MarchingCubesCases.GetRepresentativeCase(configuration);
            bool ambiguous = MarchingCubesCases.HasAmbiguousFace(representative);

            string label = $"{(int)configuration} {BaseConfigurationText.Name(configuration)}{(ambiguous ? " ⚠" : "")}";
            string tooltip = $"Case {representative}. {BaseConfigurationText.Description(configuration)}";

            Color previous = GUI.backgroundColor;
            if (isCurrent)
            {
                GUI.backgroundColor = CurrentPresetTint;
            }

            if (GUILayout.Button(new GUIContent(label, tooltip)))
            {
                ApplyCase(voxel, undoTarget, representative);
            }

            GUI.backgroundColor = previous;
        }

        private static void ApplyCase(IVoxelCaseTarget voxel, Object undoTarget, int caseIndex)
        {
            Record(undoTarget, $"Set voxel case {caseIndex}");
            voxel.ApplyCase(caseIndex);
            MarkChanged(undoTarget);
        }

        private static void Record(Object undoTarget, string name)
        {
            if (undoTarget != null)
            {
                Undo.RecordObject(undoTarget, name);
            }
        }

        private static void MarkChanged(Object undoTarget)
        {
            if (undoTarget != null)
            {
                EditorUtility.SetDirty(undoTarget);
            }
            SceneView.RepaintAll();
        }
    }
}
