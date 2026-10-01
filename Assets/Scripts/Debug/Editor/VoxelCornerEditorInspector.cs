using System;
using System.Collections.Generic;
using Clube.Core;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Lab panel under the corner sliders: the case index the corners produce and
    /// what it means (V7, V9), a slider to jump to any case (V20), one preset
    /// button per base configuration (V10), and the volume readout (V11, V12, V14)
    /// when the voxel has a Volume Lab child. Label toggles live on VoxelLabels.
    /// </summary>
    [CustomEditor(typeof(VoxelCornerEditor))]
    public class VoxelCornerEditorInspector : UnityEditor.Editor
    {
        private const int PresetColumns = 3;

        // Must match the serialized field name in VoxelCornerEditor.
        private const string CornerValuesField = "cornerValues";

        private static readonly Color CurrentPresetTint = new Color(1f, 0.8f, 0.4f);

        private SerializedProperty cornerValues;

        private void OnEnable()
        {
            cornerValues = serializedObject.FindProperty(CornerValuesField);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawCornerSliders();
            serializedObject.ApplyModifiedProperties();

            var corners = (VoxelCornerEditor)target;
            int caseIndex = corners.CaseIndex;

            EditorGUILayout.Space();
            DrawCaseSection(corners, caseIndex);

            EditorGUILayout.Space();
            DrawPresets(corners, caseIndex);

            // Volume readout here too, so it stays in view while dragging corners.
            VoxelVolumeLab volumeLab = corners.GetComponentInChildren<VoxelVolumeLab>();
            if (volumeLab != null)
            {
                EditorGUILayout.Space();
                VolumeReadoutGui.Draw(volumeLab);
                VolumeReadoutGui.DrawViewControls(volumeLab);
            }

            AxesHud axesHud = FindFirstObjectByType<AxesHud>();
            if (axesHud != null)
            {
                EditorGUILayout.Space();
                DrawAxesToggle(axesHud);
            }
        }

        private static void DrawAxesToggle(AxesHud axesHud)
        {
            EditorGUILayout.LabelField("View", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            bool show = EditorGUILayout.Toggle(
                new GUIContent("Show axes", "X / Y / Z triad in the Game view's corner (AxesHud on the camera)."),
                axesHud.ShowAxes);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(axesHud, "Toggle axes HUD");
                axesHud.ShowAxes = show;
                EditorUtility.SetDirty(axesHud);

                // The Game view only repaints on its own when something in it changes.
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
        }

        // Labelled c0-c7 to match the corner labels in the views, instead of Element 0-7.
        private void DrawCornerSliders()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            }

            EditorGUILayout.LabelField("Corner values (0 = empty, 1 = solid)", EditorStyles.boldLabel);
            for (int corner = 0; corner < cornerValues.arraySize; corner++)
            {
                Vector3Int offset = MarchingCubes.CornerOffset(corner);
                var label = new GUIContent(
                    $"c{corner}",
                    $"Corner {corner} at ({offset.x}, {offset.y}, {offset.z}). When solid it sets bit {corner} (= {1 << corner}) of the case index.");
                EditorGUILayout.Slider(cornerValues.GetArrayElementAtIndex(corner), 0f, 1f, label);
            }
        }

        private static void DrawCaseSection(VoxelCornerEditor corners, int caseIndex)
        {
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
                if (GUILayout.Button(new GUIContent("◀", "Previous case (Left arrow in Play mode)"), GUILayout.Width(24f)))
                {
                    step = -1;
                }
                if (GUILayout.Button(new GUIContent("▶", "Next case (Right arrow in Play mode)"), GUILayout.Width(24f)))
                {
                    step = +1;
                }
            }

            if (step != 0)
            {
                Undo.RecordObject(corners, step > 0 ? "Next voxel case" : "Previous voxel case");
                corners.StepCase(step);
                EditorUtility.SetDirty(corners);
                SceneView.RepaintAll();
                caseIndex = corners.CaseIndex;
            }
            else if (chosen != caseIndex)
            {
                ApplyCase(corners, chosen);
                caseIndex = corners.CaseIndex;
            }

            BaseConfiguration configuration = MarchingCubesCases.GetBaseConfiguration(caseIndex);
            int triangleCount = CountTriangles(caseIndex);

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.LabelField("Binary (corner 7 → 0)", FormatBinary(caseIndex));
                EditorGUILayout.LabelField("Solid corners", FormatList(SolidCorners(caseIndex)));
                EditorGUILayout.LabelField(
                    "Base configuration",
                    $"{(int)configuration} · {BaseConfigurationText.Name(configuration)}");
                EditorGUILayout.LabelField(" ", BaseConfigurationText.Description(configuration), EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField("Ambiguous face", MarchingCubesCases.HasAmbiguousFace(caseIndex) ? "Yes" : "No");
                EditorGUILayout.LabelField("Crossed edges", FormatList(CrossedEdges(caseIndex)));
                EditorGUILayout.LabelField("Triangles", triangleCount.ToString());
            }
        }

        private static void DrawPresets(VoxelCornerEditor corners, int currentCase)
        {
            EditorGUILayout.LabelField("Presets (one per base configuration, ⚠ = ambiguous face)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("The highlighted preset is the current configuration.", EditorStyles.miniLabel);

            BaseConfiguration current = MarchingCubesCases.GetBaseConfiguration(currentCase);
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

                        DrawPresetButton(corners, (BaseConfiguration)index, isCurrent: (BaseConfiguration)index == current);
                    }
                }
            }
        }

        private static void DrawPresetButton(VoxelCornerEditor corners, BaseConfiguration configuration, bool isCurrent)
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
                ApplyCase(corners, representative);
            }

            GUI.backgroundColor = previous;
        }

        private static void ApplyCase(VoxelCornerEditor corners, int caseIndex)
        {
            Undo.RecordObject(corners, $"Set voxel case {caseIndex}");
            corners.ApplyCase(caseIndex);
            EditorUtility.SetDirty(corners);
            SceneView.RepaintAll();
        }

        private static int CountTriangles(int caseIndex)
        {
            int indices = 0;
            while (MarchingCubesTables.Triangles[caseIndex, indices] != -1)
            {
                indices++;
            }
            return indices / 3;
        }

        private static IEnumerable<int> SolidCorners(int caseIndex)
        {
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                if (MarchingCubes.IsCornerSolid(caseIndex, corner))
                {
                    yield return corner;
                }
            }
        }

        private static IEnumerable<int> CrossedEdges(int caseIndex)
        {
            int mask = MarchingCubes.GetCrossedEdgeMask(caseIndex);
            for (int edge = 0; edge < MarchingCubes.EdgeCount; edge++)
            {
                if ((mask & (1 << edge)) != 0)
                {
                    yield return edge;
                }
            }
        }

        /// <summary>e.g. 41 → "0010 1001", most significant bit (corner 7) first.</summary>
        private static string FormatBinary(int caseIndex)
        {
            string bits = Convert.ToString(caseIndex, 2).PadLeft(8, '0');
            return $"{bits.Substring(0, 4)} {bits.Substring(4)}";
        }

        private static string FormatList(IEnumerable<int> values)
        {
            string text = string.Join(", ", values);
            return text.Length > 0 ? text : "none";
        }
    }
}
