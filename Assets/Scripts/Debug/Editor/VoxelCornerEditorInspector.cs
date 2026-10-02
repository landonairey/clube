using Clube.Core;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Lab panel under the corner sliders: the shared case and preset panel
    /// (<see cref="VoxelCaseGui"/>: V7, V9, V10, V20), the step-through controls (1D) when the
    /// voxel has a Step Through child, and the volume readout (V11, V12, V14) when
    /// it has a Volume Lab child. Label toggles live on VoxelLabels.
    /// </summary>
    [CustomEditor(typeof(VoxelCornerEditor))]
    public class VoxelCornerEditorInspector : UnityEditor.Editor
    {
        // Must match the serialized field name in VoxelCornerEditor.
        private const string CornerValuesField = "cornerValues";

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

            EditorGUILayout.Space();
            VoxelCaseGui.DrawCaseSection(corners, corners);

            EditorGUILayout.Space();
            VoxelCaseGui.DrawPresets(corners, corners);

            StepThroughLab stepThrough = StepThroughOf(corners);
            if (stepThrough != null)
            {
                EditorGUILayout.Space();
                StepThroughPanelGui.Draw(stepThrough);
            }

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

        public override bool RequiresConstantRepaint()
        {
            return StepThroughPanelGui.NeedsConstantRepaint(StepThroughOf((VoxelCornerEditor)target));
        }

        // Includes a disabled lab: disabled is just step-through mode being off.
        private static StepThroughLab StepThroughOf(VoxelCornerEditor corners)
        {
            return corners.GetComponentInChildren<StepThroughLab>(true);
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
    }
}
