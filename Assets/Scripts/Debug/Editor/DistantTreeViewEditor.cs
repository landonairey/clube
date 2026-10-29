using Clube.Core;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// The distant trees' Inspector (M22): its fields, then the lab panel's Distant trees section,
    /// which adds the live tree count.
    /// </summary>
    [CustomEditor(typeof(DistantTreeView))]
    public class DistantTreeViewEditor : UnityEditor.Editor
    {
        private readonly InspectorLabControls controls = new InspectorLabControls();

        public override bool RequiresConstantRepaint()
        {
            return Application.isPlaying;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var trees = (DistantTreeView)target;
            EditorGUILayout.Space();
            controls.Draw(new Object[] { trees }, frame => DistantTreeControls.Draw(frame, trees));
        }
    }
}
