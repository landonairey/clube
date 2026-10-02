using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Shows the voxel lab's teaching labels (corner labels, case table, crossed
    /// edges, edge numbers; V9, V21) in the Game view while playing. The Scene view
    /// shows the same labels through an editor overlay, which reads the toggles from here.
    /// </summary>
    /// <remarks>
    /// The labels hide while step-through mode is on (a <see cref="StepThroughLab"/>
    /// child is enabled): they show the finished answer, which the animation
    /// reveals one step at a time.
    /// </remarks>
    [RequireComponent(typeof(VoxelCornerEditor))]
    public class VoxelLabels : MonoBehaviour
    {
        [Tooltip("c0-c7 on the corners, and a table of which bit each one sets in the case index (V21).")]
        [SerializeField]
        private bool showCornerLabels = true;

        [Tooltip("Highlight the edges the surface crosses (V9).")]
        [SerializeField]
        private bool showCrossedEdges = true;

        [Tooltip("Number all 12 edges (e0-e11); crossed edges are labelled in the highlight colour.")]
        [SerializeField]
        private bool showEdgeLabels = true;

        [Tooltip("Camera the Game-view labels are projected with. Defaults to the main camera.")]
        [SerializeField]
        private Camera targetCamera;

        private VoxelCornerEditor corners;

        public bool ShowCornerLabels => showCornerLabels && !StepThroughMode.IsOn(this);

        public bool ShowCrossedEdges => showCrossedEdges && !StepThroughMode.IsOn(this);

        public bool ShowEdgeLabels => showEdgeLabels && !StepThroughMode.IsOn(this);

        public VoxelCornerEditor Corners => corners != null ? corners : GetComponent<VoxelCornerEditor>();

        private void Awake()
        {
            corners = GetComponent<VoxelCornerEditor>();
        }

        private void OnGUI()
        {
            bool cornerLabels = ShowCornerLabels;
            bool crossedEdges = ShowCrossedEdges;
            bool edgeLabels = ShowEdgeLabels;
            if (Event.current.type != EventType.Repaint || (!cornerLabels && !crossedEdges && !edgeLabels))
            {
                return;
            }

            Camera viewCamera = targetCamera != null ? targetCamera : Camera.main;
            if (viewCamera == null)
            {
                return;
            }

            VoxelLabelPainter.Draw(corners, cornerLabels, crossedEdges, edgeLabels, (Vector3 world, out Vector2 gui) =>
            {
                Vector3 screen = viewCamera.WorldToScreenPoint(world);

                // Screen y grows upwards, GUI y grows downwards.
                gui = new Vector2(screen.x, Screen.height - screen.y);
                return screen.z > 0f;
            });
        }
    }
}
