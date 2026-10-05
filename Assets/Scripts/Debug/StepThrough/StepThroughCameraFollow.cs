using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Optional camera follow for step-through (K22): while it's on, each time playback
    /// moves to a new voxel the free-fly camera glides to frame it, keeping its
    /// rotation, the same glide as F on a selected voxel (K6). Flying the camera
    /// yourself still works between voxels; the next voxel takes it back.
    /// </summary>
    [RequireComponent(typeof(StepThroughLab))]
    public class StepThroughCameraFollow : MonoBehaviour
    {
        [Tooltip("Follow the current voxel with the camera. C toggles it.")]
        [SerializeField]
        private bool follow;

        [Tooltip("How far from the voxel the camera sits, in voxels.")]
        [SerializeField, Min(0.5f)]
        private float distance = 4f;

        [Tooltip("Camera to move. Defaults to the main camera; it needs a FreeFlyCamera.")]
        [SerializeField]
        private Camera targetCamera;

        private StepThroughLab lab;
        private LabChunkTarget target;
        private Vector3Int? followed;

        public bool Follow
        {
            get => follow;
            set
            {
                follow = value;
                // Turning it on frames the current voxel straight away.
                followed = null;
            }
        }

        private void Awake()
        {
            lab = GetComponent<StepThroughLab>();
            target = GetComponentInParent<LabChunkTarget>();
        }

        // After the lab's Update has moved playback on, so the camera never lags a voxel.
        private void LateUpdate()
        {
            if (!follow || !lab.enabled || target == null)
            {
                return;
            }

            Vector3Int? voxel = lab.CurrentVoxel;
            if (voxel == null || voxel == followed)
            {
                return;
            }

            Camera viewCamera = targetCamera != null ? targetCamera : Camera.main;
            var flyCamera = viewCamera != null ? viewCamera.GetComponent<FreeFlyCamera>() : null;
            if (flyCamera == null)
            {
                return;
            }

            followed = voxel;
            float size = target.VoxelSize;
            Transform chunk = target.transform;
            Vector3 centre = chunk.TransformPoint(((Vector3)voxel.Value + Vector3.one * 0.5f) * size);
            flyCamera.Focus(centre, distance * size * chunk.lossyScale.x);
        }
    }
}
