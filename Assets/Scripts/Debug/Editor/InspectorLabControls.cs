using System;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Draws lab panel controls (the shared <c>*Controls</c> classes) inside a custom inspector,
    /// so every in-game panel knob is also an Inspector knob (M22). Outside Play mode the edits
    /// are recorded for Undo and the edited objects marked dirty, so asset and scene changes
    /// are saved; in Play mode they apply to the live (runtime) objects, as in the game panel.
    /// </summary>
    internal sealed class InspectorLabControls
    {
        // Leaves room for the Inspector's margins and scrollbar.
        private const float WidthAllowance = 40f;

        private LabPanelFrame frame;

        /// <param name="edited">Everything the controls may change (configs, lab components).</param>
        /// <param name="draw">Draws the controls into the frame.</param>
        public void Draw(Object[] edited, Action<LabPanelFrame> draw)
        {
            frame ??= LabPanelFrame.ForInspector(0f);
            frame.Width = EditorGUIUtility.currentViewWidth - WidthAllowance;

            if (edited.Length > 0)
            {
                Undo.RecordObjects(edited, "Lab control");
            }

            EditorGUI.BeginChangeCheck();
            frame.Begin();
            draw(frame);
            frame.End();
            if (EditorGUI.EndChangeCheck() && !UnityEngine.Application.isPlaying)
            {
                foreach (Object target in edited)
                {
                    EditorUtility.SetDirty(target);
                }
            }
        }
    }
}
