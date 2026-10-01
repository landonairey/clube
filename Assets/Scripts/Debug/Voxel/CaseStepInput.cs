using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Debug
{
    /// <summary>
    /// In Play mode, the right / left arrow keys step the voxel to the next /
    /// previous case index, one per key press (wrapping 255 ↔ 0). Outside Play
    /// mode, the ◀ ▶ buttons next to the case slider do the same.
    /// </summary>
    /// <remarks>
    /// V17 (step-through animation) also plans to use the arrow keys; when it
    /// lands, these need a mode switch or different keys.
    /// </remarks>
    [RequireComponent(typeof(VoxelCornerEditor))]
    public class CaseStepInput : MonoBehaviour
    {
        private VoxelCornerEditor corners;

        private void Awake()
        {
            corners = GetComponent<VoxelCornerEditor>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.rightArrowKey.wasPressedThisFrame)
            {
                corners.StepCase(+1);
            }
            else if (keyboard.leftArrowKey.wasPressedThisFrame)
            {
                corners.StepCase(-1);
            }
        }
    }
}
