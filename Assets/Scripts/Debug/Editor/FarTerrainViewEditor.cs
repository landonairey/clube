using Clube.Core;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// The far terrain's Inspector (M22): its fields, then the lab panel's Far terrain section,
    /// which adds the live level count and rebuild cost.
    /// </summary>
    [CustomEditor(typeof(FarTerrainView))]
    public class FarTerrainViewEditor : UnityEditor.Editor
    {
        private readonly InspectorLabControls controls = new InspectorLabControls();

        public override bool RequiresConstantRepaint()
        {
            return Application.isPlaying;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var far = (FarTerrainView)target;
            EditorGUILayout.Space();
            controls.Draw(new Object[] { far }, frame => FarTerrainControls.Draw(frame, far));
        }
    }
}
