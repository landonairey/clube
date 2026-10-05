using Clube.Core;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Draws a <see cref="WorldView"/>'s live counts in Play mode and its <see cref="WorldConfig"/>
    /// inline: chunk size, voxel size, world height, meshing, and the terrain settings
    /// (generator, surface level, amplitude, frequency, octaves, the height curve, seed…),
    /// so the world can be shaped from the Inspector. Outside Play mode that is the shared
    /// asset; in Play mode it is the view's private copy, which the world actually uses, so
    /// tweaks apply at once and are discarded on exit.
    /// </summary>
    // Written as UnityEditor.Editor because inside Clube.Debug.Editor, "Editor" names this namespace.
    [CustomEditor(typeof(WorldView))]
    public class WorldViewEditor : UnityEditor.Editor
    {
        private const string FoldoutStateKey = "Clube.WorldViewEditor.ShowConfig";
        private const string ControlsStateKey = "Clube.WorldViewEditor.ShowControls";

        private readonly InspectorLabControls controls = new InspectorLabControls();
        private UnityEditor.Editor configEditor;

        // Keeps the counts current in Play mode, where chunks load and unload every frame.
        public override bool RequiresConstantRepaint()
        {
            return Application.isPlaying;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var view = (WorldView)target;
            if (view.World != null)
            {
                EditorGUILayout.HelpBox(
                    $"Loaded {view.World.LoadedCount} chunks · waiting {view.PendingCount} · edited {view.World.EditedCount}" +
                    (view.Problem != null ? $"\nGenerate skipped: {view.Problem}" : ""),
                    view.Problem != null ? MessageType.Warning : MessageType.None);
            }

            WorldConfig config = view.Config;
            if (config == null)
            {
                return;
            }

            DrawLabControls(config);

            // In Play mode this is the view's private copy, so edits here reset on exit.
            string title = Application.isPlaying
                ? $"{config.name}: edits apply live, reset when Play mode ends"
                : $"{config.name} (shared asset, edits are saved)";
            bool showConfig = EditorGUILayout.Foldout(
                SessionState.GetBool(FoldoutStateKey, true), title, toggleOnLabelClick: true);
            SessionState.SetBool(FoldoutStateKey, showConfig);
            if (!showConfig)
            {
                return;
            }

            // Applying edits through the config's own editor runs its OnValidate, which raises
            // WorldConfig.Changed: terrain and size changes regenerate, the rest remesh.
            CreateCachedEditor(config, null, ref configEditor);
            using (new EditorGUI.IndentLevelScope())
            {
                configEditor.OnInspectorGUI();
            }
        }

        // The lab panel's chunk shape, terrain and meshing sections, drawn by the same code (M22).
        private void DrawLabControls(WorldConfig config)
        {
            bool show = EditorGUILayout.Foldout(
                SessionState.GetBool(ControlsStateKey, true), "World, terrain and meshing (same as the lab panel)", toggleOnLabelClick: true);
            SessionState.SetBool(ControlsStateKey, show);
            if (!show)
            {
                return;
            }

            controls.Draw(new Object[] { config }, frame =>
            {
                GUILayout.Label("Chunks", frame.Header);
                ChunkShapeControls.Draw(frame, config, withLayers: true);
                GUILayout.Label("Terrain", frame.Header);
                TerrainControls.Draw(frame, config);
                GUILayout.Label("Meshing", frame.Header);
                MeshingControls.Draw(frame, config);
                GUILayout.Label("Storage", frame.Header);
                StorageControls.Draw(frame, config, null, null);
            });
            EditorGUILayout.Space();
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
