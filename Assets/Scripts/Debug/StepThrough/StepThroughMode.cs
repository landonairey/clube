using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Whether step-through mode is on for a chunk: a <see cref="StepThroughLab"/> under
    /// its chunk tools (the <see cref="LabChunkTarget"/>) is enabled. Lab tools that would
    /// show the finished answer (labels, normals, density spheres) or share its keys check
    /// this and stand aside.
    /// </summary>
    public static class StepThroughMode
    {
        /// <summary>
        /// True when a step-through lab is on under the chunk tools <paramref name="tool"/> belongs to
        /// (the nearest <see cref="LabChunkTarget"/> at or above it), or under <paramref name="tool"/> itself.
        /// </summary>
        /// <remarks>
        /// A plain enabled check rather than isActiveAndEnabled, so it also works
        /// outside Play mode for Scene view overlays.
        /// </remarks>
        public static bool IsOn(Component tool)
        {
            var target = tool.GetComponentInParent<LabChunkTarget>(true);
            Component root = target != null ? target : tool;
            StepThroughLab lab = root.GetComponentInChildren<StepThroughLab>(true);
            return lab != null && lab.enabled && lab.gameObject.activeInHierarchy;
        }
    }
}
