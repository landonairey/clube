using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Whether step-through mode is on for a chunk: a <see cref="StepThroughLab"/>
    /// under it is enabled. Lab tools that would show the finished answer (labels,
    /// normals, density spheres) or share its keys check this and stand aside.
    /// </summary>
    public static class StepThroughMode
    {
        /// <summary>True when a step-through lab on or under <paramref name="chunk"/> is on.</summary>
        /// <remarks>
        /// A plain enabled check rather than isActiveAndEnabled, so it also works
        /// outside Play mode for Scene view overlays.
        /// </remarks>
        public static bool IsOn(Component chunk)
        {
            StepThroughLab lab = chunk.GetComponentInChildren<StepThroughLab>(true);
            return lab != null && lab.enabled && lab.gameObject.activeInHierarchy;
        }
    }
}
