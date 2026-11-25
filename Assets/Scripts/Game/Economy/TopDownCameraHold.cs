using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Borrows the player's camera for a straight-down orthographic view (GL863: buying land) and gives it back exactly as it
    /// was: its parent, pose, projection and the scene's fog. The same camera is moved rather than a second one made, because
    /// the far terrain, distant trees and buildings follow <c>Camera.main</c>.
    /// </summary>
    public sealed class TopDownCameraHold
    {
        private Camera camera;
        private Transform parent;
        private Vector3 localPosition;
        private Quaternion localRotation;
        private bool orthographic;
        private float orthographicSize;
        private float nearClip;
        private float farClip;
        private bool fog;

        public bool IsHeld => camera != null;

        public Camera Camera => camera;

        /// <summary>Takes the camera; false when there is none or one is already held.</summary>
        public bool Take(Camera eyes)
        {
            if (eyes == null || camera != null)
            {
                return false;
            }
            camera = eyes;
            Transform t = eyes.transform;
            parent = t.parent;
            localPosition = t.localPosition;
            localRotation = t.localRotation;
            orthographic = eyes.orthographic;
            orthographicSize = eyes.orthographicSize;
            nearClip = eyes.nearClipPlane;
            farClip = eyes.farClipPlane;
            fog = RenderSettings.fog;
            t.SetParent(null, true);
            eyes.orthographic = true;
            // Looking down from high over a wide area, the fog would grey the whole view.
            RenderSettings.fog = false;
            return true;
        }

        /// <summary>
        /// Looks straight down at <paramref name="centre"/> from <paramref name="height"/> metres over it, showing
        /// <paramref name="across"/> metres from the bottom of the screen to the top, with <paramref name="north"/> up the screen.
        /// </summary>
        public void Look(Vector3 centre, Vector3 up, Vector3 north, float across, float height)
        {
            if (camera == null)
            {
                return;
            }
            camera.transform.SetPositionAndRotation(centre + up * height, Quaternion.LookRotation(-up, north));
            camera.orthographicSize = across * 0.5f;
            camera.nearClipPlane = 0.5f;
            camera.farClipPlane = height * 2f + 200f;
        }

        /// <summary>Puts the camera back as it was taken.</summary>
        public void Release()
        {
            if (camera == null)
            {
                return;
            }
            Transform t = camera.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localRotation = localRotation;
            camera.orthographic = orthographic;
            camera.orthographicSize = orthographicSize;
            camera.nearClipPlane = nearClip;
            camera.farClipPlane = farClip;
            RenderSettings.fog = fog;
            camera = null;
            parent = null;
        }
    }
}
