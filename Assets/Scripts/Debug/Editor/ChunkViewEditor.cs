using Clube.Core;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Draws the view's last build stats in Play mode (K4) and its <see cref="WorldConfig"/>
    /// inline under <see cref="ChunkView"/>, so config values such as the iso level (V1)
    /// can be tuned next to the per-voxel lab controls. Outside Play mode that is the shared asset; in
    /// Play mode it is the view's private copy, so tweaks are discarded on exit.
    /// </summary>
    // Written as UnityEditor.Editor because inside Clube.Debug.Editor, "Editor" names this namespace.
    [CustomEditor(typeof(ChunkView))]
    public class ChunkViewEditor : UnityEditor.Editor
    {
        private const string FoldoutStateKey = "Clube.ChunkViewEditor.ShowConfig";

        private UnityEditor.Editor configEditor;

        private GUIStyle statsStyle;

        // Keeps the build stats current in Play mode, where the chunk can rebuild every frame.
        public override bool RequiresConstantRepaint()
        {
            return Application.isPlaying;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var view = (ChunkView)target;
            if (view.Chunk != null)
            {
                statsStyle ??= new GUIStyle(EditorStyles.helpBox) { richText = true, fontSize = 11 };
                EditorGUILayout.LabelField(ChunkStatsHud.Describe(view.Chunk.VoxelCount, view.LastBuildStats), statsStyle);

                if (GUILayout.Button(new GUIContent(
                        "Export heightmap PNG",
                        "Save the surface height of each column as a grayscale PNG in Assets/Heightmaps (K29). " +
                        "Import it with surface level = amplitude = half the chunk height to get the same surface back.")))
                {
                    string path = HeightmapPngExporter.Export(view);
                    EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(path));
                    UnityEngine.Debug.Log($"Heightmap exported to {path}", AssetDatabase.LoadMainAssetAtPath(path));
                }
            }

            WorldConfig config = view.Config;
            if (config == null)
            {
                return;
            }

            // In Play mode this is the view's private copy, so edits here reset on exit.
            string title = view.IsUsingRuntimeConfig
                ? $"{config.name}: edits reset when Play mode ends"
                : $"{config.name} (shared asset, edits are saved)";
            bool showConfig = EditorGUILayout.Foldout(
                SessionState.GetBool(FoldoutStateKey, true), title, toggleOnLabelClick: true);
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
