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
        private readonly Transform flame;
        private readonly float intensity;
        private readonly float seed;

        public TorchFlame(Transform parent, Vector3 at, float range, float intensity, Material smokeMaterial, Material flameMaterial = null)
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
            if (flameMaterial != null)
            {
                flame = MakeFlame(holder, flameMaterial);
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
            if (flame != null)
            {
                // The flame licks up and leans with the flicker (GL304), smaller as the torch burns low.
                float size = Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(strength));
                float tall = 0.8f + 0.45f * Mathf.PerlinNoise(t * 1.3f, seed + 7f);
                float wide = 0.85f + 0.2f * Mathf.PerlinNoise(seed + 3f, t * 1.7f);
                flame.localScale = new Vector3(wide, tall, wide) * size;
                flame.localRotation = Quaternion.Euler((Mathf.PerlinNoise(t, 1f) - 0.5f) * 14f, t * 40f, (Mathf.PerlinNoise(2f, t) - 0.5f) * 14f);
            }
        }

        // A low-poly flame (GL304): two pointed, faceted cones, an outer orange one and an inner yellow core, standing on the
        // torch head; it flickers by its scale in Flicker. Drawn in the flame material (Embers: glowing).
        private static Transform MakeFlame(GameObject holder, Material material)
        {
            var go = new GameObject("Flame");
            go.transform.SetParent(holder.transform, false);
            go.transform.localPosition = new Vector3(0f, -0.12f, 0f);
            var mesh = new Mesh { name = "Torch flame" };
            var vertices = new System.Collections.Generic.List<Vector3>();
            var colors = new System.Collections.Generic.List<Color>();
            var triangles = new System.Collections.Generic.List<int>();
            Cone(0.07f, 0.26f, 6, new Color(1f, 0.55f, 0.15f), vertices, colors, triangles);
            Cone(0.04f, 0.17f, 5, new Color(1f, 0.9f, 0.45f), vertices, colors, triangles);
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        // A cone of flat faces, from a ring up to a point, closed underneath, each face its own vertices.
        private static void Cone(float radius, float height, int sides, Color color, System.Collections.Generic.List<Vector3> vertices,
            System.Collections.Generic.List<Color> colors, System.Collections.Generic.List<int> triangles)
        {
            var tip = new Vector3(0f, height, 0f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                var p0 = new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                var p1 = new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                foreach (Vector3[] face in new[] { new[] { p0, tip, p1 }, new[] { p1, Vector3.zero, p0 } })
                {
                    foreach (Vector3 p in face)
                    {
                        triangles.Add(vertices.Count);
                        vertices.Add(p);
                        colors.Add(color);
                    }
                }
            }
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
