using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Shows the voxel lab's teaching labels (corner labels, case table, crossed
    /// edges; V9, V21) in the Game view while playing. The Scene view shows the
    /// same labels through an editor overlay, which reads the toggles from here.
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

        [Tooltip("Highlight and number the edges the surface crosses (V9).")]
        [SerializeField]
        private bool showCrossedEdges = true;

        [Tooltip("Camera the Game-view labels are projected with. Defaults to the main camera.")]
        [SerializeField]
        private Camera targetCamera;

        private VoxelCornerEditor corners;

        public bool ShowCornerLabels => showCornerLabels && !IsStepThroughOn;

        public bool ShowCrossedEdges => showCrossedEdges && !IsStepThroughOn;

        public VoxelCornerEditor Corners => corners != null ? corners : GetComponent<VoxelCornerEditor>();

        // Plain enabled check, so it also works outside Play mode for the Scene view overlay.
        private bool IsStepThroughOn
        {
            get
            {
                StepThroughLab stepThrough = GetComponentInChildren<StepThroughLab>(true);
                return stepThrough != null && stepThrough.enabled && stepThrough.gameObject.activeInHierarchy;
            }
        }

        private void Awake()
        {
            corners = GetComponent<VoxelCornerEditor>();
        }

        private void OnGUI()
        {
            bool cornerLabels = ShowCornerLabels;
            bool crossedEdges = ShowCrossedEdges;
            if (Event.current.type != EventType.Repaint || (!cornerLabels && !crossedEdges))
            {
                return;
            }

            Camera viewCamera = targetCamera != null ? targetCamera : Camera.main;
            if (viewCamera == null)
            {
                return;
            }

            VoxelLabelPainter.Draw(corners, cornerLabels, crossedEdges, (Vector3 world, out Vector2 gui) =>
            {
                Vector3 screen = viewCamera.WorldToScreenPoint(world);

                // Screen y grows upwards, GUI y grows downwards.
                gui = new Vector2(screen.x, Screen.height - screen.y);
                return screen.z > 0f;
            });
        }
    }
}
