using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// <see cref="ChunkGizmos"/> settings, plus a warning while its sample spheres
    /// are suppressed because the chunk is too big for Unity's gizmo limit.
    /// </summary>
    [CustomEditor(typeof(ChunkGizmos))]
    public class ChunkGizmosEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint()
        {
            return Application.isPlaying;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (((ChunkGizmos)target).AreSamplesSuppressed)
            {
                EditorGUILayout.HelpBox(ChunkStatsHud.SamplesSuppressedWarning, MessageType.Warning);
            }
        }
    }
}
