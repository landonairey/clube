using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A torch's fire (GL287, GL294): a warm point light that flickers, bright enough to mine by at night, and wisps of
    /// smoke rising from the flame, made on the object it's given. The terrain, grass and held tools take the light
    /// (<c>Assets/Shaders/AdditionalLights.hlsl</c>). Shared by the torch in the hand (<see cref="TorchBurner"/>) and the one
    /// on the ground (<see cref="PlacedTorch"/>).
    /// </summary>
    public sealed class TorchFlame
    {
        private static readonly Color Warm = new Color(1f, 0.62f, 0.28f);
        private readonly GameObject holder;
        private readonly Light light;
        private readonly ParticleSystem smoke;
        private readonly float intensity;
        private readonly float seed;

        public TorchFlame(Transform parent, Vector3 at, float range, float intensity, Material smokeMaterial)
        {
            holder = new GameObject("Torch fire");
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = at;
            light = holder.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Warm;
            light.range = range;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            this.intensity = intensity;
            seed = Random.value * 100f;
            light.intensity = intensity;
            if (smokeMaterial != null)
            {
                smoke = MakeSmoke(holder, smokeMaterial);
            }
        }

        public bool Shown
        {
            get => holder != null && holder.activeSelf;
            set
            {
                if (holder != null && holder.activeSelf != value)
                {
                    holder.SetActive(value);
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
            float t = Time.time * 11f + seed;
            float flicker = 0.75f + 0.18f * Mathf.PerlinNoise(t, seed) + 0.07f * Mathf.Sin(t * 2.7f) + 0.05f * Mathf.Sin(t * 7.3f);
            light.intensity = intensity * flicker * Mathf.Clamp01(strength);
        }

        // Thin grey wisps rising and spreading from the flame, in the world, so a moving torch leaves a trail.
        private static ParticleSystem MakeSmoke(GameObject holder, Material material)
        {
            var go = new GameObject("Smoke");
            go.transform.SetParent(holder.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.startColor = new Color(0.32f, 0.31f, 0.3f, 0.45f);
            main.maxParticles = 60;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 9f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.02f;
            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 2.4f));
            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;
            particles.Play();
            return particles;
        }
    }
}
