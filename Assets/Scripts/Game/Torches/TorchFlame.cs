using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A torch's light (GL287): a warm point light that flickers, made on the object it's given. Shared by the torch held in
    /// the hand (<see cref="TorchBurner"/>) and the one on the ground (<see cref="PlacedTorch"/>).
    /// </summary>
    public sealed class TorchFlame
    {
        private static readonly Color Warm = new Color(1f, 0.66f, 0.32f);
        private readonly Light light;
        private readonly float intensity;
        private readonly float seed;

        public TorchFlame(Transform parent, Vector3 at, float range = 10f, float intensity = 1.8f)
        {
            var holder = new GameObject("Torch light");
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = at;
            light = holder.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Warm;
            light.range = range;
            light.shadows = LightShadows.None;
            this.intensity = intensity;
            seed = Random.value * 100f;
            light.intensity = intensity;
        }

        public bool Shown
        {
            get => light != null && light.enabled;
            set
            {
                if (light != null)
                {
                    light.enabled = value;
                }
            }
        }

        /// <summary>Flickers the light; <paramref name="strength"/> dims it as the torch burns low.</summary>
        public void Flicker(float strength = 1f)
        {
            if (light == null)
            {
                return;
            }
            float t = Time.time * 9f + seed;
            float flicker = 0.82f + 0.12f * Mathf.PerlinNoise(t, seed) + 0.06f * Mathf.Sin(t * 2.7f);
            light.intensity = intensity * flicker * Mathf.Clamp01(strength);
        }
    }
}
