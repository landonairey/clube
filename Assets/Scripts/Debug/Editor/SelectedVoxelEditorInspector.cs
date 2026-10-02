using Clube.Core;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// The selected voxel's panel in the chunk lab (K7): its 8 corner sliders, each
    /// noting how many voxels share that corner, and the shared case and preset
    /// panel (<see cref="VoxelCaseGui"/>). Edits are chunk data, not scene data, so
    /// they aren't undoable and reset when Play mode ends.
    /// </summary>
    [CustomEditor(typeof(SelectedVoxelEditor))]
    public class SelectedVoxelEditorInspector : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint()
        {
            return Application.isPlaying;
        }

        public override void OnInspectorGUI()
        {
            var editor = (SelectedVoxelEditor)target;
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode and click a voxel in the Game view to edit its corners.", MessageType.Info);
                return;
            }

            if (!editor.HasSelection)
            {
                EditorGUILayout.HelpBox("Click a voxel on the surface in the Game view to select it.", MessageType.Info);
                return;
            }

            Vector3Int voxel = editor.Voxel.Value;
            EditorGUILayout.LabelField($"Voxel ({voxel.x}, {voxel.y}, {voxel.z})", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Corners are shared samples: editing one reshapes every voxel listed beside it.",
                EditorStyles.wordWrappedMiniLabel);

            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                int sharing = editor.VoxelsSharing(corner);
                var label = new GUIContent(
                    $"c{corner}  ({sharing} voxel{(sharing == 1 ? "" : "s")})",
                    $"Corner {corner}: sample {voxel + MarchingCubes.CornerOffset(corner)}, shared by {sharing} voxel(s). " +
                    $"When solid it sets bit {corner} (= {1 << corner}) of the case index.");

                float current = editor.GetCorner(corner);
                float chosen = EditorGUILayout.Slider(label, current, 0f, 1f);
                if (!Mathf.Approximately(chosen, current))
                {
                    editor.SetCorner(corner, chosen);
                }
            }

            EditorGUILayout.Space();
            VoxelCaseGui.DrawCaseSection(editor, null);

            EditorGUILayout.Space();
            VoxelCaseGui.DrawPresets(editor, null);
        }
    }
}
