using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Clube.Game
{
    /// <summary>
    /// Blurs the view while the player rests (GL290): a global volume of Gaussian depth of field, from the camera on, faded in
    /// and out. Post-processing is off in the world's camera, so it's turned on only while the blur shows, and back after.
    /// </summary>
    public sealed class RestBlur
    {
        private readonly GameObject holder;
        private readonly Volume volume;
        private readonly VolumeProfile profile;
        private Camera camera;
        private bool cameraHadPost;

        public RestBlur()
        {
            holder = new GameObject("Rest blur");
            Object.DontDestroyOnLoad(holder);
            holder.hideFlags = HideFlags.DontSave;
            volume = holder.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;
            volume.weight = 0f;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            DepthOfField blur = profile.Add<DepthOfField>(true);
            blur.mode.Override(DepthOfFieldMode.Gaussian);
            blur.gaussianStart.Override(0f);
            blur.gaussianEnd.Override(0.5f);
            blur.gaussianMaxRadius.Override(1.5f);
            blur.highQualitySampling.Override(true);
            volume.sharedProfile = profile;
        }

        /// <summary>How blurred the view is, 0 (clear) to 1.</summary>
        public float Amount
        {
            get => volume.weight;
            set
            {
                float amount = Mathf.Clamp01(value);
                volume.weight = amount;
                SetPostProcessing(amount > 0f);
            }
        }

        public void Destroy()
        {
            SetPostProcessing(false);
            Object.Destroy(profile);
            Object.Destroy(holder);
        }

        // The camera draws post-processing only while there's blur to draw; as it was before otherwise.
        private void SetPostProcessing(bool on)
        {
            if (on && camera == null)
            {
                camera = Camera.main;
                if (camera != null && camera.TryGetComponent(out UniversalAdditionalCameraData data))
                {
                    cameraHadPost = data.renderPostProcessing;
                    data.renderPostProcessing = true;
                }
            }
            else if (!on && camera != null)
            {
                if (camera.TryGetComponent(out UniversalAdditionalCameraData data))
                {
                    data.renderPostProcessing = cameraHadPost;
                }
                camera = null;
            }
        }
    }
}
