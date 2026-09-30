using Clube.Core;
using UnityEditor;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Draws the assigned <see cref="WorldConfig"/> inline under <see cref="ChunkView"/>,
    /// so config values such as the iso level (V1) can be tuned next to the
    /// per-voxel lab controls. Edits still go to the shared config asset.
    /// </summary>
    // Written as UnityEditor.Editor because inside Clube.Debug.Editor, "Editor" names this namespace.
    [CustomEditor(typeof(ChunkView))]
    public class ChunkViewEditor : UnityEditor.Editor
    {
        private const string FoldoutStateKey = "Clube.ChunkViewEditor.ShowConfig";

        private UnityEditor.Editor configEditor;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            WorldConfig config = ((ChunkView)target).Config;
            if (config == null)
            {
                return;
            }

            bool showConfig = EditorGUILayout.Foldout(
                SessionState.GetBool(FoldoutStateKey, true),
                $"{config.name} (shared asset, changes persist after Play mode)",
                toggleOnLabelClick: true);
            SessionState.SetBool(FoldoutStateKey, showConfig);
            if (!showConfig)
            {
                return;
            }

            // Applying edits through the config's own editor runs its OnValidate,
            // which raises WorldConfig.Changed and triggers the rebuild.
            CreateCachedEditor(config, null, ref configEditor);
            using (new EditorGUI.IndentLevelScope())
            {
                configEditor.OnInspectorGUI();
            }
        }

        private void OnDisable()
        {
            if (configEditor != null)
            {
                DestroyImmediate(configEditor);
            }
        }
    }
}
