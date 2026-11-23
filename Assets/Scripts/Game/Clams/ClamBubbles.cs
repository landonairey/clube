using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The burst of bubbles a clam leaves when its pearl is taken (GL808): a short-lived particle system of small round
    /// bubbles drifting up and wobbling, and a bubbling sound heard from the spot. Cleans itself up.
    /// </summary>
    public static class ClamBubbles
    {
        private const int Count = 26;
        private const float Life = 2.5f;

        /// <summary>Bubbles at a spot; no particles without a material, no sound without the sounds.</summary>
        public static void Play(Vector3 at, Material material, PlayerSounds sounds)
        {
            var go = new GameObject("Clam bubbles");
            go.transform.position = at;
            if (material != null)
            {
                ParticleSystem particles = Make(go, material);
                var emit = new ParticleSystem.EmitParams();
                for (int i = 0; i < Count; i++)
                {
                    Vector2 ring = Random.insideUnitCircle * 0.1f;
                    emit.position = at + new Vector3(ring.x, Random.Range(0f, 0.06f), ring.y);
                    emit.velocity = new Vector3(ring.x * 1.5f, Random.Range(0.25f, 0.7f), ring.y * 1.5f);
                    emit.startSize = Random.Range(0.015f, 0.05f);
                    emit.startLifetime = Random.Range(0.9f, Life);
                    particles.Emit(emit, 1);
                }
            }
            if (sounds != null && sounds.EffectsOn && sounds.Bubbles.HasClips)
            {
                AudioSource source = go.AddComponent<AudioSource>();
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.maxDistance = 25f;
                sounds.Bubbles.Play(source, 0.8f);
            }
            Object.Destroy(go, Life + 0.5f);
        }

        // Pale bubbles that rise (negative gravity), wobble a little and shrink away near the end of their life.
        private static ParticleSystem Make(GameObject go, Material material)
        {
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.08f;
            main.startColor = new Color(0.85f, 0.95f, 1f, 0.75f);
            main.maxParticles = Count * 2;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;
            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 1.5f;
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.2f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0f)));
            return particles;
        }
    }
}
