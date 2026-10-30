using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Wisps of steam over clay drying on the ground (GL228), so it's plain the pile is still curing into brick: a
    /// few faint puffs a second rise from the top of what <see cref="DroppedGroundDrying"/> is waiting on, and stop
    /// when it's dry. Only samples with air above them steam, and only near the camera.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class DryingClayEffect : MonoBehaviour
    {
        [SerializeField]
        private DroppedGroundDrying drying;

        [SerializeField]
        private WorldView worldView;

        [Tooltip("Puffs a second from the whole of what's drying (at most).")]
        [SerializeField, Min(0f)]
        private float puffsPerSecond = 14f;

        [Tooltip("Drying samples further from the camera than this, in metres, don't steam.")]
        [SerializeField, Min(1f)]
        private float range = 40f;

        // Seconds between looks at what's drying.
        private const float RefreshSeconds = 0.5f;

        private readonly List<Vector3> sources = new List<Vector3>();
        private ParticleSystem particles;
        private float nextRefresh;
        private float owed;

        private void Awake()
        {
            particles = GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.2f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.34f);
            main.startColor = new Color(0.95f, 0.95f, 0.93f, 0.55f);
            main.maxParticles = 400;
            main.gravityModifier = 0f;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;
            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;
            particles.Play();
        }

        private void Update()
        {
            if (drying == null || worldView == null || !worldView.IsReady || drying.Count == 0)
            {
                sources.Clear();
                owed = 0f;
                return;
            }
            if (Time.time >= nextRefresh)
            {
                nextRefresh = Time.time + RefreshSeconds;
                FindSources();
            }
            if (sources.Count == 0)
            {
                return;
            }
            owed += puffsPerSecond * Time.deltaTime;
            var emit = new ParticleSystem.EmitParams();
            while (owed >= 1f)
            {
                owed -= 1f;
                Vector3 at = sources[Random.Range(0, sources.Count)];
                emit.position = at + new Vector3(Random.Range(-0.12f, 0.12f), 0.05f, Random.Range(-0.12f, 0.12f));
                particles.Emit(emit, 1);
            }
        }

        // The drying samples with air above them, near the camera, in world space.
        private void FindSources()
        {
            sources.Clear();
            World world = worldView.World;
            float iso = worldView.Config.IsoLevel;
            Camera eyes = Camera.main;
            Vector3 camera = eyes != null ? eyes.transform.position : transform.position;
            foreach (var (sample, _, _) in drying.Drying.Waiting)
            {
                float above = world.GetDensity(sample + Vector3Int.up) ?? 0f;
                if (above >= iso)
                {
                    continue;
                }
                Vector3 position = worldView.transform.TransformPoint(world.Grid.SampleToWorld(sample));
                if ((position - camera).sqrMagnitude <= range * range)
                {
                    sources.Add(position);
                }
            }
        }
    }
}
