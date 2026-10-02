using Clube.Core;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Step-through section of the voxel lab panel (1D): the mode toggle, and in
    /// Play mode the transport controls, a scrub slider and the speed (V17).
    /// Durations per step type stay on the <see cref="StepThroughLab"/> itself.
    /// </summary>
    public static class StepThroughPanelGui
    {
        /// <summary>True while the panel should repaint every frame to follow playback.</summary>
        public static bool NeedsConstantRepaint(StepThroughLab lab)
        {
            return lab != null && Application.isPlaying && lab.isActiveAndEnabled;
        }

        public static void Draw(StepThroughLab lab)
        {
            EditorGUILayout.LabelField("Step-through", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            bool on = EditorGUILayout.Toggle(
                new GUIContent(
                    "Step-through mode",
                    "Replay the mesh build step by step. Hides the real mesh, and the arrow keys step the animation instead of the case."),
                lab.enabled);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(lab, on ? "Enable step-through" : "Disable step-through");
                lab.enabled = on;
                EditorUtility.SetDirty(lab);
            }

            if (!on)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode to record the build and play it back.", MessageType.Info);
                return;
            }

            if (!lab.HasRecording)
            {
                EditorGUILayout.HelpBox("Waiting for the first mesh build to record.", MessageType.Info);
                return;
            }

            StepPlayback playback = lab.Playback;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("⏮", "Restart (R)")))
                {
                    playback.Restart();
                }
                if (GUILayout.Button(new GUIContent("◀", "Step back (Left arrow)")))
                {
                    playback.Step(-1);
                }
                if (GUILayout.Button(new GUIContent(playback.IsPlaying ? "Pause" : "Play", "Play / pause (Space)")))
                {
                    playback.TogglePlay();
                }
                if (GUILayout.Button(new GUIContent("▶", "Step forward (Right arrow)")))
                {
                    playback.Step(+1);
                }
            }

            int shown = playback.StepIndex + 1;
            int chosen = EditorGUILayout.IntSlider(
                new GUIContent("Step", "Drag to scrub through the recorded steps."), shown, 1, playback.StepCount);
            if (chosen != shown)
            {
                playback.Seek(chosen - 1);
            }

            MeshingStep step = lab.Recording.Steps[playback.StepIndex];
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.LabelField("Current", $"{step.Type} ({playback.Progress:P0})");
            }

            lab.Speed = EditorGUILayout.Slider(new GUIContent("Speed", "Playback speed multiplier."), lab.Speed, 0.1f, 5f);
        }
    }
}
