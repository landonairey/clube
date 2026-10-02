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
    /// While step-through mode is on (a <see cref="StepThroughLab"/> child is
    /// enabled), the arrows step the animation instead (V17).
    /// </remarks>
    [RequireComponent(typeof(VoxelCornerEditor))]
    public class CaseStepInput : MonoBehaviour
    {
        private VoxelCornerEditor corners;
        private StepThroughLab stepThrough;

        private void Awake()
        {
            corners = GetComponent<VoxelCornerEditor>();
            stepThrough = GetComponentInChildren<StepThroughLab>(true);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || (stepThrough != null && stepThrough.isActiveAndEnabled))
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
