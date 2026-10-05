using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// The <see cref="LabPanel"/>'s Inspector: its own settings, then every section of the
    /// in-game panel drawn by the same code (<see cref="LabPanel.DrawSections"/>), so the
    /// Inspector always has every panel knob (M22). Sections that need the running lab (the
    /// focused chunk, a selected voxel, step-through playback) say so outside Play mode.
    /// </summary>
    // Written as UnityEditor.Editor because inside Clube.Debug.Editor, "Editor" names this namespace.
    [CustomEditor(typeof(LabPanel))]
    public class LabPanelEditor : UnityEditor.Editor
    {
        private const string FoldoutStateKey = "Clube.LabPanelEditor.ShowControls";

        private readonly InspectorLabControls controls = new InspectorLabControls();

        // Keeps readouts and playback current in Play mode.
        public override bool RequiresConstantRepaint()
        {
            return Application.isPlaying;
        }

        private void OnEnable()
        {
            // The scene may have changed since the panel last looked.
            ((LabPanel)target).FindParts(again: true);
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            bool show = EditorGUILayout.Foldout(
                SessionState.GetBool(FoldoutStateKey, true),
                Application.isPlaying ? "Lab controls (same as the in-game panel)" : "Lab controls (same as the in-game panel; config edits are saved)",
                toggleOnLabelClick: true);
            SessionState.SetBool(FoldoutStateKey, show);
            if (!show)
            {
                return;
            }

            var panel = (LabPanel)target;
            controls.Draw(panel.EditableParts(), panel.DrawSections);
        }
    }
}
