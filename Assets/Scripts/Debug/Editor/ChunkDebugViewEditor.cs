using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// <see cref="ChunkDebugView"/> settings, plus a warning while its sample spheres
    /// are suppressed because the chunk has too many samples.
    /// </summary>
    [CustomEditor(typeof(ChunkDebugView))]
    public class ChunkDebugViewEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint()
        {
            return Application.isPlaying;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (((ChunkDebugView)target).AreSamplesSuppressed)
            {
                EditorGUILayout.HelpBox(ChunkStatsHud.SamplesSuppressedWarning, MessageType.Warning);
            }
        }
    }
}
