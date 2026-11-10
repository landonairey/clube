using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Fireflies at dusk (GL465): a small fixed crowd of glowing specks drifting and blinking a metre or two over the
    /// grass close to the camera, only while the evening's light is going (a little before the sun is down until well
    /// after, fading in and out) and never by day or deep night. Cheap: a pool of <see cref="count"/> specks, each
    /// re-seated on grass in a ring round the camera when it falls out of range, drawn instanced as camera-facing quads.
    /// </summary>
    public class Fireflies : MonoBehaviour
    {
        [Tooltip("An unlit, bright yellow-green material with GPU instancing on.")]
        [SerializeField]
        private Material material;

        [SerializeField]
        private WorldView worldView;

        [SerializeField]
        private DayNightCycle dayNight;

        [Tooltip("The grass material: fireflies keep to ground that shows it. Found in the world's registry as 'Grass' when empty.")]
        [SerializeField]
        private VoxelMaterial grass;

        [Tooltip("How many fireflies there can be.")]
        [SerializeField, Range(0, 200)]
        private int count = 60;

        [Tooltip("How near and far from the camera one is seated, in metres.")]
        [SerializeField]
        private Vector2 ring = new Vector2(3f, 20f);

        [Tooltip("How high over the ground they drift, in metres.")]
        [SerializeField]
        private Vector2 height = new Vector2(0.5f, 2.2f);

        [Tooltip("Width of a firefly's glow, in metres.")]
        [SerializeField, Min(0.005f)]
        private float size = 0.12f;

        private struct Firefly
        {
            public Vector3 Home;
            public Vector3 Phase;
            public Vector3 Rate;
            public float Blink;
            public float BlinkRate;
            public bool Seated;
        }

        private static readonly Vector3[] QuadCorners =
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
        };

        private Firefly[] flies;
        private readonly InstanceBatches batch = new InstanceBatches();
        private Mesh quad;
        private System.Random random;
        private int drawnFrame = -1;
        private byte grassId;
        private bool grassKnown;

        /// <summary>How much the fireflies are out now, 0 by day to 1 at dusk.</summary>
        public float Glow { get; private set; }

        /// <summary>Fireflies showing now.</summary>
        public int ShownCount { get; private set; }


        private void Awake()
        {
            random = new System.Random(System.Environment.TickCount);
            flies = new Firefly[count];
            quad = new Mesh { name = "Firefly" };
            quad.vertices = QuadCorners;
            quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            quad.RecalculateBounds();
        }

        private void OnDestroy()
        {
            if (quad != null)
            {
                Destroy(quad);
            }
            batch.Dispose();
        }

        private void Update()
        {
            float sunHeight = dayNight != null ? dayNight.SunDirection.y : 1f;
            bool evening = dayNight != null && dayNight.Hour >= 12f;
            Glow = DuskLight.Glow(sunHeight, evening);
            if (Glow <= 0.01f || worldView == null || !worldView.IsReady || Camera.main == null)
            {
                ShownCount = 0;
                return;
            }
            Vector3 viewer = Camera.main.transform.position;
            int reseats = 0;
            for (int i = 0; i < flies.Length; i++)
            {
                Firefly fly = flies[i];
                bool far = (fly.Home - viewer).sqrMagnitude > ring.y * ring.y * 1.2f;
                if ((!fly.Seated || far) && reseats < 6)
                {
                    reseats++;
                    flies[i] = TrySeat(viewer);
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
            ShownCount = 0;
            Camera view = Camera.main;
            if (Glow > 0.01f && view != null)
            {
                Quaternion facing = view.transform.rotation;
                float time = Time.time;
                for (int i = 0; i < flies.Length; i++)
                {
                    Firefly fly = flies[i];
                    if (!fly.Seated)
                    {
                        continue;
                    }
                    // Blinks: on for about a third of each cycle, easing in and out, and the dusk's fade scales all.
                    float cycle = Mathf.Repeat(time * fly.BlinkRate + fly.Blink, 1f);
                    float on = Mathf.Sin(Mathf.Clamp01(cycle * 3f) * Mathf.PI);
                    float scale = size * on * Glow;
                    if (scale < 0.004f)
                    {
                        continue;
                    }
                    Vector3 drift = new Vector3(
                        Mathf.Sin(time * fly.Rate.x + fly.Phase.x),
                        0.5f * Mathf.Sin(time * fly.Rate.y + fly.Phase.y),
                        Mathf.Sin(time * fly.Rate.z + fly.Phase.z)) * 0.9f;
                    batch.Add(Matrix4x4.TRS(fly.Home + drift, facing, Vector3.one * scale));
                    ShownCount++;
                }
            }
            if (batch.Count > 0)
            {
                var parameters = new RenderParams(material)
                {
                    shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                    receiveShadows = false,
                    worldBounds = new Bounds(Vector3.zero, Vector3.one * 1e5f),
                };
                batch.Draw(parameters, quad);
            }
        }

        // A firefly over grass at a random spot in the ring round the viewer; unseated when the spot isn't grass.
        private Firefly TrySeat(Vector3 viewer)
        {
            var fly = new Firefly();
            float angle = Range(0f, Mathf.PI * 2f);
            float distance = Mathf.Lerp(ring.x, ring.y, Mathf.Sqrt((float)random.NextDouble()));
            float x = viewer.x + Mathf.Cos(angle) * distance;
            float z = viewer.z + Mathf.Sin(angle) * distance;
            if (worldView.Generator == null)
            {
                return fly;
            }
            float top = worldView.World.Grid.ChunkWorldSize.y * worldView.Config.WorldHeightInChunks;
            float ground = TerrainSurface.HeightAt(worldView.Generator, x, z, 0f, top);
            if (!IsGrass(new Vector3(x, ground - 0.15f, z)) || ground < viewer.y - 25f || ground > viewer.y + 25f)
            {
                return fly;
            }
            fly.Home = new Vector3(x, ground + Range(height.x, height.y), z);
            fly.Phase = new Vector3(Range(0f, 6.3f), Range(0f, 6.3f), Range(0f, 6.3f));
            fly.Rate = new Vector3(Range(0.3f, 0.8f), Range(0.5f, 1.1f), Range(0.3f, 0.8f));
            fly.Blink = Range(0f, 1f);
            fly.BlinkRate = Range(0.25f, 0.5f);
            fly.Seated = true;
            return fly;
        }

        private bool IsGrass(Vector3 point)
        {
            if (!grassKnown)
            {
                grassKnown = true;
                MaterialRegistry registry = worldView.Config.Materials;
                if (grass == null && registry != null)
                {
                    grass = registry.Find("Grass");
                }
                if (grass != null && registry != null)
                {
                    for (int id = 0; id < MaterialRegistry.MaxMaterials; id++)
                    {
                        if (registry.Get((byte)id) == grass)
                        {
                            grassId = (byte)id;
                        }
                    }
                }
            }
            if (grass == null)
            {
                return false;
            }
            Vector3Int sample = worldView.World.Grid.WorldToNearestSample(point);
            byte? found = worldView.World.GetMaterial(sample);
            return found.HasValue && found.Value == grassId;
        }

        private float Range(float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }
    }
}
