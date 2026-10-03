using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Debug
{
    /// <summary>
    /// Keyboard controls for step-through playback (V17): Space plays or pauses,
    /// right / left arrow step one step, R restarts, G cycles what one step covers
    /// (sub-step, voxel, Z slice; K19), H skips or shows empty voxels (K20). Only listens while the
    /// <see cref="StepThroughLab"/> is on; otherwise the arrows go back to case
    /// stepping (<see cref="CaseStepInput"/>).
    /// </summary>
    [RequireComponent(typeof(StepThroughLab))]
    public class StepThroughInput : MonoBehaviour
    {
        private StepThroughLab lab;

        private void Awake()
        {
            lab = GetComponent<StepThroughLab>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !lab.enabled)
            {
                return;
            }

            StepPlayback playback = lab.Playback;
            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                playback.TogglePlay();
            }
            else if (keyboard.rightArrowKey.wasPressedThisFrame)
            {
                playback.Step(+1);
            }
            else if (keyboard.leftArrowKey.wasPressedThisFrame)
            {
                playback.Step(-1);
            }
            else if (keyboard.rKey.wasPressedThisFrame)
            {
                playback.Restart();
            }
            else if (keyboard.gKey.wasPressedThisFrame)
            {
                int next = ((int)lab.Granularity + 1) % System.Enum.GetValues(typeof(StepGranularity)).Length;
                lab.Granularity = (StepGranularity)next;
            }
            else if (keyboard.hKey.wasPressedThisFrame)
            {
                lab.SkipEmptyVoxels = !lab.SkipEmptyVoxels;
            }
        }
    }
}
