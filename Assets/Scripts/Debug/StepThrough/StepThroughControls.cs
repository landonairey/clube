using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// The step-through section of the in-game lab panels (K31): the mode toggle, then
    /// play / pause, stepping, a position slider, what one step covers (K19), skipping
    /// empty voxels (K20) and the speed. The in-game twin of the Inspector's step-through
    /// panel, so the controls are there in the demo exe too.
    /// </summary>
    public static class StepThroughControls
    {
        private static readonly string[] GranularityNames = { "Sub-step", "Voxel", "Z slice" };

        public static void Draw(LabPanelFrame frame, StepThroughLab lab)
        {
            bool on = frame.ToggleField("Step-through mode (replays the mesh build)", lab.enabled);
            if (on != lab.enabled)
            {
                lab.enabled = on;
            }
            if (!on)
            {
                return;
            }

            if (!MeshingRecorder.IsAvailable)
            {
                GUILayout.Label("Recording isn't in this build. It needs the Editor, a development build or the lab demo build.", frame.Hint);
                return;
            }
            if (!lab.HasRecording)
            {
                GUILayout.Label("Waiting for the first mesh build to record.", frame.Hint);
                return;
            }

            // Everything below keeps a fixed size while playback runs: equal-width buttons
            // and single clipped lines, so the panel never resizes from frame to frame.
            StepPlayback playback = lab.Playback;
            switch (frame.ButtonRow("Restart", "◄", playback.IsPlaying ? "Pause" : "Play", "►"))
            {
                case 0:
                    playback.Restart();
                    break;
                case 1:
                    playback.Step(-1);
                    break;
                case 2:
                    playback.TogglePlay();
                    break;
                case 3:
                    playback.Step(+1);
                    break;
            }

            int step = lab.Current.Step;
            frame.Line($"Position {playback.StepIndex + 1} / {playback.StepCount}");
            frame.Line($"Step {step + 1} / {lab.Recording.Steps.Count} · {lab.Recording.Steps[step].Type}");
            float position = GUILayout.HorizontalSlider(playback.StepIndex, 0f, Mathf.Max(0, playback.StepCount - 1));
            int target = Mathf.RoundToInt(position);
            if (target != playback.StepIndex)
            {
                playback.Seek(target);
            }

            var granularity = (StepGranularity)frame.Toolbar("Step by", (int)lab.Granularity, GranularityNames);
            if (granularity != lab.Granularity)
            {
                lab.Granularity = granularity;
            }

            bool skip = frame.ToggleField($"Skip empty voxels ({lab.Units.EmptyVoxelCount} here)", lab.SkipEmptyVoxels);
            if (skip != lab.SkipEmptyVoxels)
            {
                lab.SkipEmptyVoxels = skip;
            }

            lab.Speed = frame.Slider("Speed", lab.Speed, 0.1f, 5f, "0.0×");
            GUILayout.Label("Space play/pause · ← → step · G step by · H skip empty · R restart", frame.Hint);
        }
    }
}
