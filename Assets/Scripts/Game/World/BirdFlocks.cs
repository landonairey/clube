using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Birds in the sky (GL250, a first look at fauna): now and then a group comes over, from a lone bird to a flock of
    /// a few dozen, flies a pattern for a while (<see cref="BirdFlock"/>: passing over, circling high, a swirling flock,
    /// a V) and leaves until it's out of sight. Few at a time and far off, so the sky stays quiet; fewer at dusk and none
    /// at night. Drawn instanced, each bird a small dark silhouette, its wings in one of four poses of the beat.
    /// </summary>
    public class BirdFlocks : MonoBehaviour
    {
        private const int Poses = 4;

        [Tooltip("The birds' material: a dark unlit colour with GPU instancing on.")]
        [SerializeField]
        private Material material;

        [Tooltip("Lights the sky: fewer birds at dusk, none at night. Optional.")]
        [SerializeField]
        private DayNightCycle dayNight;

        [Tooltip("The most groups in the sky at once.")]
        [SerializeField, Range(1, 6)]
        private int maxGroups = 3;

        [Tooltip("Seconds between new groups, at random within this range.")]
        [SerializeField]
        private Vector2 secondsBetween = new Vector2(25f, 80f);

        [Tooltip("How far from the camera a group's pattern is flown, in metres.")]
        [SerializeField]
        private Vector2 distance = new Vector2(70f, 180f);

        [Tooltip("How high above the camera they fly, in metres.")]
        [SerializeField]
        private Vector2 height = new Vector2(25f, 70f);

        [Tooltip("How far out they're gone, in metres: they fly off past this before they're dropped.")]
        [SerializeField, Min(100f)]
        private float goneDistance = 650f;

        [Tooltip("Wingspan of a bird, in metres; a big flock's birds are smaller, a lone circling bird larger.")]
        [SerializeField, Min(0.1f)]
        private float wingspan = 0.9f;

        private readonly List<BirdFlock> groups = new List<BirdFlock>();
        private readonly List<float> sizes = new List<float>();
        private readonly InstanceBatches[] batches = new InstanceBatches[Poses];
        private readonly Mesh[] meshes = new Mesh[Poses];
        private System.Random random;
        private float nextGroup;
        private int drawnFrame = -1;

        /// <summary>Birds in the sky now.</summary>
        public int BirdCount
        {
            get
            {
                int count = 0;
                foreach (BirdFlock group in groups)
                {
                    count += group.Birds.Count;
                }
                return count;
            }
        }

        /// <summary>Groups in the sky now.</summary>
        public int GroupCount => groups.Count;

        /// <summary>Sends a group over now (lab and tests), if the camera is there.</summary>
        public void SpawnNow()
        {
            Camera view = Camera.main;
            if (view != null)
            {
                Spawn(view.transform.position);
            }
        }

        private void Awake()
        {
            random = new System.Random(System.Environment.TickCount);
            for (int pose = 0; pose < Poses; pose++)
            {
                batches[pose] = new InstanceBatches();
                meshes[pose] = BuildBird(pose);
            }
            // The first group comes along soon after the start.
            nextGroup = Time.time + Range(6f, 15f);
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in meshes)
            {
                if (mesh != null)
                {
                    Destroy(mesh);
                }
            }
        }

        private void Update()
        {
            Camera view = Camera.main;
            if (view == null)
            {
                return;
            }
            Vector3 viewer = view.transform.position;
            float daylight = dayNight != null ? dayNight.Daylight : 1f;
            if (Time.time >= nextGroup)
            {
                // Fewer at dusk and dawn, none in the dark.
                if (groups.Count < maxGroups && Range(0f, 1f) < Mathf.InverseLerp(0.2f, 0.8f, daylight))
                {
                    Spawn(viewer);
                }
                nextGroup = Time.time + Range(secondsBetween.x, secondsBetween.y);
            }

            float step = Mathf.Min(Time.deltaTime, 0.1f);
            for (int i = groups.Count - 1; i >= 0; i--)
            {
                groups[i].Step(step);
                if (groups[i].IsGone(viewer, goneDistance))
                {
                    groups.RemoveAt(i);
                    sizes.RemoveAt(i);
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
            foreach (InstanceBatches batch in batches)
            {
                batch.Clear();
            }
            for (int g = 0; g < groups.Count; g++)
            {
                float size = sizes[g];
                foreach (Bird bird in groups[g].Birds)
                {
                    int pose = bird.BeatRate > 0f ? Mathf.FloorToInt(Mathf.Repeat(bird.Beat, 1f) * Poses) % Poses : 1;
                    Vector3 heading = bird.Velocity.sqrMagnitude > 0.01f ? bird.Velocity.normalized : Vector3.forward;
                    Quaternion rotation = Quaternion.LookRotation(heading, Vector3.up);
                    batches[pose].Add(Matrix4x4.TRS(bird.Position, rotation, Vector3.one * size));
                }
            }
            var parameters = new RenderParams(material)
            {
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 1e5f),
            };
            for (int pose = 0; pose < Poses; pose++)
            {
                if (batches[pose].Count > 0)
                {
                    batches[pose].Draw(parameters, meshes[pose]);
                }
            }
        }

        // A group of a random size and pattern: mostly a few birds, now and then one alone or a big flock.
        private void Spawn(Vector3 viewer)
        {
            float roll = Range(0f, 1f);
            int count;
            FlightPattern pattern;
            if (roll < 0.25f)
            {
                count = 1;
                pattern = Range(0f, 1f) < 0.6f ? FlightPattern.Circle : FlightPattern.Pass;
            }
            else if (roll < 0.6f)
            {
                count = Mathf.RoundToInt(Range(2f, 5f));
                pattern = Range(0f, 1f) < 0.3f ? FlightPattern.Circle : FlightPattern.Pass;
            }
            else if (roll < 0.8f)
            {
                count = Mathf.RoundToInt(Range(5f, 11f));
                pattern = FlightPattern.Vee;
            }
            else
            {
                count = Mathf.RoundToInt(Range(14f, 40f));
                pattern = FlightPattern.Swirl;
            }

            float angle = Range(0f, Mathf.PI * 2f);
            var around = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 anchor = viewer + around * Range(distance.x, distance.y) + Vector3.up * Range(height.x, height.y);
            // They come in from one side and leave roughly on across the sky; circling and swirling ones leave anywhere.
            float entryAngle = Range(0f, Mathf.PI * 2f);
            var entry = new Vector3(Mathf.Cos(entryAngle), 0f, Mathf.Sin(entryAngle));
            Vector3 exit = pattern == FlightPattern.Pass || pattern == FlightPattern.Vee
                ? Quaternion.Euler(0f, Range(-25f, 25f), 0f) * -entry
                : Quaternion.Euler(0f, Range(-120f, 120f), 0f) * -entry;
            float stay = pattern == FlightPattern.Pass || pattern == FlightPattern.Vee ? 30f : Range(25f, 60f);
            groups.Add(new BirdFlock(pattern, count, anchor, entry, exit, stay, random.Next(), Range(200f, 320f)));
            sizes.Add(wingspan * (pattern == FlightPattern.Swirl ? 0.6f : pattern == FlightPattern.Circle ? 1.4f : pattern == FlightPattern.Vee ? 1.5f : 1f));
        }

        private float Range(float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }

        // A bird one unit across the wings, facing +z: a slim body and two wings, raised or lowered for the pose
        // (0 up, 1 level, 2 down, 3 coming back up). Double-sided by having both windings.
        private static Mesh BuildBird(int pose)
        {
            float[] lift = { 0.32f, 0.04f, -0.26f, -0.06f };
            float tip = lift[pose];
            float elbow = tip * 0.45f;
            var vertices = new List<Vector3>
            {
                // Body: head to tail.
                new Vector3(0f, 0f, 0.28f), new Vector3(0.035f, 0f, 0f), new Vector3(0f, 0f, -0.3f), new Vector3(-0.035f, 0f, 0f),
                // Right wing: root front, root back, elbow, tip.
                new Vector3(0.03f, 0f, 0.06f), new Vector3(0.03f, 0f, -0.08f), new Vector3(0.26f, elbow, 0.02f), new Vector3(0.5f, tip, -0.1f),
                // Left wing.
                new Vector3(-0.03f, 0f, 0.06f), new Vector3(-0.03f, 0f, -0.08f), new Vector3(-0.26f, elbow, 0.02f), new Vector3(-0.5f, tip, -0.1f),
                // Tail fan.
                new Vector3(0.07f, 0f, -0.38f), new Vector3(-0.07f, 0f, -0.38f),
            };
            var triangles = new List<int>
            {
                0, 1, 2, 0, 2, 3,
                4, 6, 5, 6, 7, 5,
                8, 9, 10, 10, 9, 11,
                2, 12, 13,
            };
            int one = triangles.Count;
            for (int i = 0; i < one; i += 3)
            {
                triangles.Add(triangles[i]);
                triangles.Add(triangles[i + 2]);
                triangles.Add(triangles[i + 1]);
            }
            var mesh = new Mesh { name = $"Bird {pose}" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
