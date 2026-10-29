using Clube.Core;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// The coarse terrain's Inspector (M22): its fields, then the lab panel's Coarse terrain section,
    /// which adds each level's live chunk counts.
    /// </summary>
    [CustomEditor(typeof(TerrainLodView))]
    public class TerrainLodViewEditor : UnityEditor.Editor
    {
        private readonly InspectorLabControls controls = new InspectorLabControls();

        public override bool RequiresConstantRepaint()
        {
            return Application.isPlaying;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var lod = (TerrainLodView)target;
            EditorGUILayout.Space();
            controls.Draw(new Object[] { lod }, frame => TerrainLodControls.Draw(frame, lod));
        }
    }
}
