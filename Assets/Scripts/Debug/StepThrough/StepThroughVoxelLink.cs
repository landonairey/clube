using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Ties step-through to voxel selection (K21, K5): while step-through mode is on,
    /// selecting a voxel jumps playback to the start of that voxel's build, paused, so
    /// Play shows it being built. <see cref="JumpToSelection"/> does the same for a
    /// voxel that was selected earlier (the panel button, or J).
    /// </summary>
    /// <remarks>
    /// Sits next to the <see cref="StepThroughLab"/>; the selector is the chunk's
    /// <see cref="VoxelSelector"/>. Without one (VoxelLab) it does nothing.
    /// </remarks>
    [RequireComponent(typeof(StepThroughLab))]
    public class StepThroughVoxelLink : MonoBehaviour
    {
        private StepThroughLab lab;
        private VoxelSelector selector;

        /// <summary>The selected voxel, or null when there is no selector or nothing is selected.</summary>
        public Vector3Int? SelectedVoxel => selector != null ? selector.SelectedVoxel : null;

        /// <summary>Jumps to the selected voxel; returns false if there is none to jump to.</summary>
        public bool JumpToSelection()
        {
            Vector3Int? voxel = SelectedVoxel;
            return voxel.HasValue && lab.enabled && lab.JumpToVoxel(voxel.Value);
        }

        private void Awake()
        {
            lab = GetComponent<StepThroughLab>();
            var target = GetComponentInParent<LabChunkTarget>();
            selector = target != null ? target.GetComponent<VoxelSelector>() : null;
        }

        private void OnEnable()
        {
            if (selector != null)
            {
                selector.SelectionChanged += OnSelectionChanged;
            }
        }

        private void OnDisable()
        {
            if (selector != null)
            {
                selector.SelectionChanged -= OnSelectionChanged;
            }
        }

        private void OnSelectionChanged(Vector3Int? voxel)
        {
            if (voxel.HasValue && lab.enabled)
            {
                lab.JumpToVoxel(voxel.Value);
            }
        }
    }
}
