using UnityEditor;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// <see cref="StepThroughLab"/>'s own Inspector: its settings, then the step-through
    /// controls (mode toggle, transport, scrub, speed), so they are there in any lab
    /// scene. VoxelLab also shows the controls in its corner panel.
    /// </summary>
    // Written as UnityEditor.Editor because inside Clube.Debug.Editor, "Editor" names this namespace.
    [CustomEditor(typeof(StepThroughLab))]
    public class StepThroughLabEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint()
        {
            return StepThroughPanelGui.NeedsConstantRepaint((StepThroughLab)target);
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            StepThroughPanelGui.Draw((StepThroughLab)target);
        }
    }
}
