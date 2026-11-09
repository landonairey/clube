using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// River trout that now and then leap and splash (GL466). A leap starts on a deep river cell of the
    /// <see cref="WaterMap"/> a few metres to a few dozen from the camera (still wet, by <see cref="WaterQuery"/>), arcs
    /// up and slightly upstream, and lands with a splash and a spray of droplets. How often is <see cref="TroutJumps"/>'s:
    /// most in the morning, least in the afternoon, none at night. Trout are drawn instanced; the splash is heard in 3D.
    /// </summary>
    public class RiverTrout : MonoBehaviour
    {
        [Tooltip("The trout's material: an unlit silvery olive with GPU instancing on.")]
        [SerializeField]
        private Material material;

        [SerializeField]
        private WorldView worldView;

        [SerializeField]
        private DayNightCycle dayNight;

        [Tooltip("The splash sound lives here.")]
        [SerializeField]
        private PlayerSounds sounds;

        [Tooltip("The droplets' material (soft round particles); none for no spray.")]
        [SerializeField]
        private Material dropletMaterial;

        [Tooltip("How near and far from the camera a jump is, in metres.")]
        [SerializeField]
        private Vector2 range = new Vector2(4f, 40f);

        [Tooltip("The trout's length, in metres.")]
        [SerializeField, Min(0.05f)]
        private float length = 0.35f;

        private struct Leap
        {
            public Vector3 Start;
            public Vector3 Velocity;
            public float Age;
            public float Duration;
        }

        private readonly List<Leap> leaps = new List<Leap>();
        private readonly InstanceBatches batch = new InstanceBatches();
        private Mesh fish;
        private ParticleSystem droplets;
        private int drawnFrame = -1;

        /// <summary>Trout in the air now.</summary>
        public int AirborneCount => leaps.Count;

        /// <summary>Jumps so far (for the lab and tests).</summary>
        public int JumpCount { get; private set; }

        /// <summary>Makes a trout jump at a river spot near the camera, whatever the hour (lab and tests).</summary>
        public bool JumpNow()
        {
            Camera view = Camera.main;
            if (view == null || worldView == null || !worldView.IsReady)
            {
                return false;
            }
            for (int tries = 0; tries < 200; tries++)
            {
                if (TryStart(view.transform.position))
                {
                    return true;
                }
            }
            return false;
        }

        private void Awake()
        {
            fish = BuildFish();
            if (dropletMaterial != null)
            {
                droplets = MakeDroplets(dropletMaterial);
            }
        }

        private void OnDestroy()
        {
            if (fish != null)
            {
                Destroy(fish);
            }
            batch.Dispose();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            Camera view = Camera.main;
            if (view != null && worldView != null && worldView.IsReady && dayNight != null)
            {
                float chance = TroutJumps.ChanceIn(Mathf.Min(dt, 0.1f), dayNight.Hour, dayNight.Daylight);
                if (chance > 0f && Random.value < chance)
                {
                    TryStart(view.transform.position);
                }
            }
            for (int i = leaps.Count - 1; i >= 0; i--)
            {
                Leap leap = leaps[i];
                leap.Age += dt;
                if (leap.Age >= leap.Duration)
                {
                    Splash(PositionOf(leap, leap.Duration), 1f);
                    leaps.RemoveAt(i);
                }
                else
                {
                    leaps[i] = leap;
                }
            }
        }

        private void LateUpdate()
        {
            // Once a frame: instanced draws' buffers are only freed when a real frame ends.
            if (material == null || drawnFrame == Time.frameCount)
            {
                return;
            }
            drawnFrame = Time.frameCount;
            batch.Clear();
            foreach (Leap leap in leaps)
            {
                Vector3 position = PositionOf(leap, leap.Age);
                Vector3 velocity = leap.Velocity + Physics.gravity * leap.Age;
                // Nose first, so it pitches over at the top of the arc.
                Quaternion rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
                batch.Add(Matrix4x4.TRS(position, rotation, Vector3.one * length));
            }
            if (batch.Count > 0)
            {
                var parameters = new RenderParams(material)
                {
                    shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                    receiveShadows = false,
                    worldBounds = new Bounds(Vector3.zero, Vector3.one * 1e5f),
                };
                batch.Draw(parameters, fish);
            }
        }

        private static Vector3 PositionOf(Leap leap, float age)
        {
            return leap.Start + leap.Velocity * age + 0.5f * age * age * Physics.gravity;
        }

        // Picks a river spot in the ring round the viewer and sends a trout up from it; false when it isn't deep river.
        private bool TryStart(Vector3 viewer)
        {
            if (!(worldView.Generator is HeightfieldGenerator heightfield))
            {
                return false;
            }
            float angle = Random.value * Mathf.PI * 2f;
            float distance = Mathf.Lerp(range.x, range.y, Random.value);
            if (!TroutJumps.TryRiverSpot(heightfield.Water, viewer.x + Mathf.Cos(angle) * distance,
                    viewer.z + Mathf.Sin(angle) * distance, out Vector3 start, out Vector2 flow))
            {
                return false;
            }
            // The live water must still stand there (a dug-out or drained river has none).
            if (!WaterQuery.IsUnderwater(worldView.World, start - Vector3.up * 0.2f))
            {
                return false;
            }
            // Up and a little against the current, like a trout in a riffle.
            Vector3 upstream = flow.sqrMagnitude > 1e-4f
                ? -new Vector3(flow.x, 0f, flow.y).normalized
                : new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            float rise = Random.Range(2.6f, 3.6f);
            float duration = 2f * rise / -Physics.gravity.y;
            Vector3 horizontal = upstream * Random.Range(0.6f, 1.4f) / duration;
            leaps.Add(new Leap
            {
                Start = start,
                Velocity = horizontal + Vector3.up * rise,
                Age = 0f,
                Duration = duration,
            });
            JumpCount++;
            Splash(start, 0.5f);
            return true;
        }

        // A spray of droplets and a splash heard from the spot, softer the farther it is.
        private void Splash(Vector3 at, float strength)
        {
            if (droplets != null)
            {
                var emit = new ParticleSystem.EmitParams { applyShapeToPosition = true };
                int count = Mathf.RoundToInt(12f * strength);
                for (int i = 0; i < count; i++)
                {
                    Vector2 ring = Random.insideUnitCircle * 0.15f;
                    emit.position = at + new Vector3(ring.x, 0.02f, ring.y);
                    emit.velocity = new Vector3(ring.x * 4f, Random.Range(0.8f, 1.6f), ring.y * 4f);
                    emit.startSize = Random.Range(0.02f, 0.05f);
                    emit.startLifetime = Random.Range(0.4f, 0.8f);
                    droplets.Emit(emit, 1);
                }
            }
            if (sounds != null && sounds.Splash.HasClips)
            {
                var go = new GameObject("Trout splash");
                go.transform.position = at;
                AudioSource source = go.AddComponent<AudioSource>();
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.maxDistance = 60f;
                sounds.Splash.Play(source, 0.35f * strength);
                Destroy(go, 3f);
            }
        }

        // Pale blue-white droplets thrown up and falling back, in the world.
        private ParticleSystem MakeDroplets(Material dropMaterial)
        {
            var go = new GameObject("Trout spray");
            go.transform.SetParent(transform, false);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = dropMaterial;
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1f;
            main.startColor = new Color(0.82f, 0.9f, 1f, 0.85f);
            main.maxParticles = 300;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;
            return particles;
        }

        // A trout one unit long facing +z: a slim spindle body with a forked tail, in two windings.
        private static Mesh BuildFish()
        {
            var vertices = new List<Vector3>
            {
                new Vector3(0f, 0f, 0.5f),
                new Vector3(0.07f, 0f, 0.15f),
                new Vector3(0f, 0.09f, 0.15f),
                new Vector3(-0.07f, 0f, 0.15f),
                new Vector3(0f, -0.08f, 0.15f),
                new Vector3(0f, 0f, -0.3f),
                new Vector3(0.12f, 0.04f, -0.5f),
                new Vector3(-0.12f, 0.04f, -0.5f),
                new Vector3(0f, 0.05f, -0.1f),
                new Vector3(0f, 0.15f, -0.2f),
            };
            var triangles = new List<int>
            {
                0, 2, 1, 0, 3, 2, 0, 4, 3, 0, 1, 4,
                5, 1, 2, 5, 2, 3, 5, 3, 4, 5, 4, 1,
                5, 6, 7,
                2, 9, 8,
            };
            int one = triangles.Count;
            for (int i = 0; i < one; i += 3)
            {
                triangles.Add(triangles[i]);
                triangles.Add(triangles[i + 2]);
                triangles.Add(triangles[i + 1]);
            }
            var mesh = new Mesh { name = "Trout" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
