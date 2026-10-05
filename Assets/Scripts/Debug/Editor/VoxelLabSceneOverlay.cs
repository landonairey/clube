using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Shows the voxel lab's teaching labels in the Scene view, drawn by the same
    /// <see cref="VoxelLabelPainter"/> the Game view uses, with the toggles from
    /// each <see cref="VoxelLabels"/> component. Works outside Play mode too.
    /// </summary>
    /// <remarks>
    /// Drawn from <see cref="SceneView.duringSceneGui"/> rather than as a gizmo:
    /// Unity skips an object's gizmos once its renderer bounds leave the view, and
    /// before Play mode the voxel's renderer has no mesh, so its bounds are a point
    /// at corner c0. Panning c0 off screen made all the labels vanish.
    /// </remarks>
    [InitializeOnLoad]
    internal static class VoxelLabSceneOverlay
    {
        static VoxelLabSceneOverlay()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            foreach (VoxelLabels labels in Object.FindObjectsByType<VoxelLabels>(FindObjectsSortMode.None))
            {
                // Not isActiveAndEnabled: that only becomes true once OnEnable has run,
                // which never happens outside Play mode for a regular MonoBehaviour.
                if (!labels.enabled || !labels.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Handles.BeginGUI();
                VoxelLabelPainter.Draw(
                    labels.Corners, labels.ShowCornerLabels, labels.ShowCornerValues, labels.ShowCrossedEdges, labels.ShowEdgeLabels, WorldToGui);
                Handles.EndGUI();
            }
        }

        private static bool WorldToGui(Vector3 world, out Vector2 gui)
        {
            Vector3 point = HandleUtility.WorldToGUIPointWithDepth(world);
            gui = point;
            return point.z > 0f;
        }
    }
}
