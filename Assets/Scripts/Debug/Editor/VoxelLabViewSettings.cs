using UnityEditor;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Scene-view overlay toggles for the voxel lab, shared by the Inspector that
    /// sets them and the gizmo drawer that reads them. Kept per editor session.
    /// </summary>
    internal static class VoxelLabViewSettings
    {
        private const string CornerLabelsKey = "Clube.VoxelLab.ShowCornerLabels";
        private const string CrossedEdgesKey = "Clube.VoxelLab.ShowCrossedEdges";

        /// <summary>Corner index, bit and solid/empty labels (V21).</summary>
        public static bool ShowCornerLabels
        {
            get => SessionState.GetBool(CornerLabelsKey, true);
            set => SessionState.SetBool(CornerLabelsKey, value);
        }

        /// <summary>Highlight of the edges the surface crosses (V9).</summary>
        public static bool ShowCrossedEdges
        {
            get => SessionState.GetBool(CrossedEdgesKey, true);
            set => SessionState.SetBool(CrossedEdgesKey, value);
        }
    }
}
