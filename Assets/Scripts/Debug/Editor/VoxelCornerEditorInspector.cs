using Clube.Core;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// The voxel's corner sliders: VoxelLab's stored corners (also outside Play mode), or the
    /// selected voxel's live ones in ChunkLab and WorldLab (K7, M19). Under them: the shared case and preset panel
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
            var corners = (VoxelCornerEditor)target;
            if (corners.UsesStoredCorners)
            {
                serializedObject.Update();
                DrawCornerSliders();
                serializedObject.ApplyModifiedProperties();
            }
            else if (!DrawSelectedCorners(corners))
            {
                return;
            }

            // Stored corners are serialized, so they can be undone; a chunk's densities can't.
            Object undoTarget = corners.UsesStoredCorners ? corners : null;

            EditorGUILayout.Space();
            VoxelCaseGui.DrawCaseSection(corners, undoTarget);

            EditorGUILayout.Space();
            VoxelCaseGui.DrawPresets(corners, undoTarget);

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
            var corners = (VoxelCornerEditor)target;
            return (!corners.UsesStoredCorners && Application.isPlaying)
                   || StepThroughPanelGui.NeedsConstantRepaint(StepThroughOf(corners));
        }

        // Includes a disabled lab: disabled is just step-through mode being off.
        private static StepThroughLab StepThroughOf(VoxelCornerEditor corners)
        {
            var chunkTools = corners.GetComponentInParent<LabChunkTarget>(true);
            return (chunkTools != null ? (Component)chunkTools : corners).GetComponentInChildren<StepThroughLab>(true);
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

        // The selected voxel's corners, read from and written to the chunk. False when there's
        // nothing to edit yet, after saying how to pick a voxel.
        private static bool DrawSelectedCorners(VoxelCornerEditor corners)
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode and click a voxel in the Game view to edit its corners.", MessageType.Info);
                return false;
            }

            if (!corners.HasVoxel)
            {
                EditorGUILayout.HelpBox("Click a voxel on the surface in the Game view to select it.", MessageType.Info);
                return false;
            }

            Vector3Int voxel = corners.Voxel.Value;
            EditorGUILayout.LabelField($"Voxel ({voxel.x}, {voxel.y}, {voxel.z})", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Corners are shared samples: editing one reshapes every voxel listed beside it.",
                EditorStyles.wordWrappedMiniLabel);

            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                int sharing = corners.VoxelsSharing(corner);
                var label = new GUIContent(
                    $"c{corner}  ({sharing} voxel{(sharing == 1 ? "" : "s")})",
                    $"Corner {corner}: sample {voxel + MarchingCubes.CornerOffset(corner)}, shared by {sharing} voxel(s) in this chunk. " +
                    $"When solid it sets bit {corner} (= {1 << corner}) of the case index.");

                float current = corners.GetCorner(corner);
                float chosen = EditorGUILayout.Slider(label, current, 0f, 1f);
                if (!Mathf.Approximately(chosen, current))
                {
                    corners.SetCorner(corner, chosen);
                }
            }
            return true;
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
