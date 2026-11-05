using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The player in water (GL397): a splash and a spray of droplets on going in, a slosh every stride while wading or
    /// swimming, and the pull and drip of breaking free on coming out. Goes by how much of the capsule is under water
    /// (<see cref="PlayerController.Submersion"/>, set by <see cref="PlayerSwimming"/>); footsteps stop while wading
    /// (<see cref="PlayerFootsteps"/>).
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerWaterSounds : MonoBehaviour
    {
        [SerializeField]
        private PlayerSounds sounds;

        [Tooltip("Plays the sounds; the player's 2D source.")]
        [SerializeField]
        private AudioSource source;

        [Tooltip("The droplets' material (soft round particles); none for no droplets.")]
        [SerializeField]
        private Material dropletMaterial;

        [Tooltip("Share of the capsule under water that counts as being in it.")]
        [SerializeField, Range(0.02f, 0.6f)]
        private float inDepth = 0.12f;

        [Tooltip("Share of the capsule under water below which the player has left it.")]
        [SerializeField, Range(0f, 0.3f)]
        private float outDepth = 0.04f;

        [Tooltip("Metres moved through water between sloshes.")]
        [SerializeField, Min(0.2f)]
        private float strideLength = 1.4f;

        [Tooltip("Slower than this (metres per second) makes no slosh.")]
        [SerializeField, Min(0f)]
        private float minSpeed = 0.6f;

        private PlayerController player;
        private ParticleSystem droplets;
        private Vector3 lastPosition;
        private float moved;
        private bool inWater;

        /// <summary>True while the player is in water (past <see cref="inDepth"/>, until under <see cref="outDepth"/>).</summary>
        public bool InWater => inWater;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            lastPosition = transform.position;
            if (dropletMaterial != null)
            {
                droplets = MakeDroplets(dropletMaterial);
            }
        }

        private void Update()
        {
            Vector3 position = transform.position;
            Vector3 step = position - lastPosition;
            lastPosition = position;
            if (!player.enabled)
            {
                inWater = false;
                return;
            }

            float depth = player.Submersion;
            if (!inWater && depth > inDepth)
            {
                inWater = true;
                moved = 0f;
                // Harder the faster the player came down.
                float fall = Time.deltaTime > 0f ? Mathf.Max(0f, -step.y / Time.deltaTime) : 0f;
                Play(sounds != null ? sounds.Splash : null, Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(fall / 8f)));
                Spray(SurfaceAt(depth), Mathf.RoundToInt(Mathf.Lerp(18f, 50f, Mathf.Clamp01(fall / 8f))), Mathf.Lerp(1.6f, 3.4f, Mathf.Clamp01(fall / 8f)));
                return;
            }
            if (inWater && depth < outDepth)
            {
                inWater = false;
                Play(sounds != null ? sounds.Emerge : null, 0.7f);
                Spray(SurfaceAt(Mathf.Max(depth, outDepth)), 10, 1.1f);
                return;
            }
            if (!inWater)
            {
                return;
            }

            float distance = new Vector2(step.x, step.z).magnitude;
            if (Time.deltaTime <= 0f || distance / Time.deltaTime < minSpeed || distance > strideLength)
            {
                return;
            }
            moved += distance;
            if (moved >= strideLength)
            {
                moved -= strideLength;
                Play(sounds != null ? sounds.Wade : null, 0.55f);
                if (depth < 0.6f)
                {
                    // Water kicked up at the surface by the legs.
                    Spray(SurfaceAt(depth) + new Vector3(step.x, 0f, step.z).normalized * 0.3f, 6, 1f);
                }
            }
        }

        private void Play(SoundSet sound, float volume)
        {
            if (sounds != null && sounds.EffectsOn && sound != null)
            {
                sounds.Play(sound, source, volume);
            }
        }

        // Where the water's surface meets the capsule, in world space.
        private Vector3 SurfaceAt(float depth)
        {
            CharacterController body = player.Body;
            Vector3 feet = transform.TransformPoint(body.center) - Vector3.up * (body.height * 0.5f);
            return feet + Vector3.up * (body.height * depth);
        }

        private void Spray(Vector3 at, int count, float speed)
        {
            if (droplets == null || count <= 0)
            {
                return;
            }
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = true };
            for (int i = 0; i < count; i++)
            {
                Vector2 ring = Random.insideUnitCircle * 0.35f;
                emit.position = at + new Vector3(ring.x, 0.02f, ring.y);
                Vector3 outward = new Vector3(ring.x, 0f, ring.y).normalized;
                emit.velocity = (Vector3.up * Random.Range(0.7f, 1.2f) + outward * Random.Range(0.15f, 0.6f)) * speed * Random.Range(0.6f, 1.1f);
                emit.startSize = Random.Range(0.025f, 0.06f);
                emit.startLifetime = Random.Range(0.5f, 0.9f);
                droplets.Emit(emit, 1);
            }
        }

        // Pale blue-white droplets thrown up and falling back, in the world (not following the player).
        private ParticleSystem MakeDroplets(Material material)
        {
            var go = new GameObject("Water droplets");
            go.transform.SetParent(transform, false);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1f;
            main.startColor = new Color(0.82f, 0.9f, 1f, 0.85f);
            main.maxParticles = 200;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.4f));
            return particles;
        }
    }
}
