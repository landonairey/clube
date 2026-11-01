using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Low-poly clouds (GL277, replacing GL252's ray-marched ones): fluffy puffs of faceted balls with flat bottoms, the same
    /// look as the leaf clusters on the trees, scattered over a grid that drifts with the wind round the camera, out to
    /// <see cref="reach"/>. How many there are and how big follows the day's cover (<see cref="SkyWeather.Cover"/>) and a
    /// slowly changing pattern, so they grow and shrink rather than pop. Drawn instanced by <c>Clube/Low Poly Clouds</c>,
    /// lit by the light <see cref="DayNightCycle"/> gives the clouds (the sun, in the sunrise's or sunset's colour, a while
    /// after it leaves the land; the moon at night), with silver edges towards the sun and the sun's 12-sided outline
    /// shining through any cloud in front of it.
    /// </summary>
    public class CloudLayer : MonoBehaviour
    {
        private const int Variants = 6;

        [Tooltip("The Clube/Low Poly Clouds material (GPU instancing on).")]
        [SerializeField]
        private Material material;

        [Tooltip("The game's days, for the weather changing from day to day. Optional.")]
        [SerializeField]
        private DayNightCycle dayNight;

        [Header("Layer")]
        [Tooltip("Height of the cloud bases, in metres.")]
        [SerializeField, Min(0f)]
        private float baseHeight = 1000f;

        [Tooltip("Scales each day's cloud cover (GL263: clear, partly cloudy or heavy by the day, SkyWeather.Cover); 1 as the days have it.")]
        [SerializeField, Range(0f, 2f)]
        private float coverageScale = 1f;

        [Tooltip("Roughly how many game days the wind and the clouds' patterns take to change.")]
        [SerializeField, Min(0.1f)]
        private float weatherDays = 1.5f;

        [Tooltip("Metres between cloud spots on the grid; at most one cloud each.")]
        [SerializeField, Min(100f)]
        private float cellSize = 1100f;

        [Tooltip("How far out clouds are drawn, in metres.")]
        [SerializeField, Min(1000f)]
        private float reach = 11000f;

        [Tooltip("A cloud's width in metres, smallest and largest.")]
        [SerializeField]
        private Vector2 size = new Vector2(450f, 1100f);

        [Header("Wind")]
        [Tooltip("Wind speed at the clouds, in metres a second.")]
        [SerializeField, Min(0f)]
        private float windSpeed = 9f;

        [Tooltip("The compass direction the wind blows towards, in degrees.")]
        [SerializeField]
        private float windDirection = 70f;

        [Tooltip("How far the wind swings round over the days, in degrees each way.")]
        [SerializeField, Range(0f, 180f)]
        private float windSwing = 35f;

        private readonly InstanceBatches[] batches = new InstanceBatches[Variants];
        private readonly Mesh[] meshes = new Mesh[Variants];
        private WorldView world;
        private double driftX;
        private double driftZ;
        private int drawnFrame = -1;

        /// <summary>How much of the sky is covered now, 0-1: today's cover (GL263), easing into tomorrow's.</summary>
        public float Coverage => Mathf.Clamp01(SkyWeather.Cover(Seed, GameDays) * coverageScale);

        /// <summary>Where the wind blows towards now, a flat world direction.</summary>
        public Vector3 Wind
        {
            get
            {
                float angle = windDirection + windSwing * (2f * Mathf.PerlinNoise(0.71f, WeatherTime * 0.6f) - 1f);
                return Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            }
        }

        /// <summary>Clouds drawn last frame.</summary>
        public int CloudCount { get; private set; }

        // The world's seed, so the same world has the same weather.
        private int Seed
        {
            get
            {
                world ??= GetComponent<WorldView>();
                return world != null && world.Config != null ? world.Config.Terrain.Seed : 0;
            }
        }

        private double GameDays => dayNight != null ? dayNight.Days : Time.time / 2400d;

        // The weather's place in its noise: game days over a spell's length.
        private float WeatherTime => (float)(GameDays / weatherDays % 10000d);

        private void Awake()
        {
            for (int i = 0; i < Variants; i++)
            {
                batches[i] = new InstanceBatches();
                meshes[i] = BuildPuff(i);
            }
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
            Vector3 wind = Wind;
            driftX += wind.x * windSpeed * Time.deltaTime;
            driftZ += wind.z * windSpeed * Time.deltaTime;
        }

        private void LateUpdate()
        {
            Camera view = Camera.main;
            // Once a frame: instanced draws' buffers are only freed when a real frame ends.
            if (view == null || material == null || drawnFrame == Time.frameCount)
            {
                return;
            }
            drawnFrame = Time.frameCount;
            foreach (InstanceBatches batch in batches)
            {
                batch.Clear();
            }

            // The grid drifts with the wind: cells are fixed in the drifting frame, which the camera moves through.
            Vector3 eye = view.transform.position;
            double frameX = eye.x - driftX, frameZ = eye.z - driftZ;
            int fromX = Mathf.FloorToInt((float)((frameX - reach) / cellSize)), toX = Mathf.FloorToInt((float)((frameX + reach) / cellSize));
            int fromZ = Mathf.FloorToInt((float)((frameZ - reach) / cellSize)), toZ = Mathf.FloorToInt((float)((frameZ + reach) / cellSize));
            float cover = Coverage;
            float time = WeatherTime;
            int seed = Seed;
            float reachSquared = reach * reach;
            CloudCount = 0;
            for (int cz = fromZ; cz <= toZ; cz++)
            {
                for (int cx = fromX; cx <= toX; cx++)
                {
                    // The cover here: the day's, thicker and thinner in a slowly changing pattern across the sky.
                    float pattern = Mathf.PerlinNoise(cx * 0.13f + time * 0.4f + seed % 97, cz * 0.13f - time * 0.3f + seed % 89);
                    float here = Mathf.Clamp01(cover * (0.35f + 1.3f * pattern));
                    float roll = VoxelHash.Uniform(seed, cx, 0, cz, 0x434c44);
                    // A cloud grows in as the cover passes its roll, rather than popping in.
                    float grown = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(roll, roll + 0.18f, here));
                    if (grown <= 0.02f)
                    {
                        continue;
                    }
                    float jx = VoxelHash.Uniform(seed, cx, 1, cz, 0x434c44) - 0.5f;
                    float jz = VoxelHash.Uniform(seed, cx, 2, cz, 0x434c44) - 0.5f;
                    float x = (float)((cx + 0.5f + jx * 0.7f) * cellSize + driftX);
                    float z = (float)((cz + 0.5f + jz * 0.7f) * cellSize + driftZ);
                    float dx = x - eye.x, dz = z - eye.z;
                    if (dx * dx + dz * dz > reachSquared)
                    {
                        continue;
                    }
                    float width = Mathf.Lerp(size.x, size.y, VoxelHash.Uniform(seed, cx, 3, cz, 0x434c44)) * (0.4f + 0.6f * grown);
                    float tall = width * Mathf.Lerp(0.45f, 0.8f, grown * here);
                    float height = baseHeight + VoxelHash.Uniform(seed, cx, 4, cz, 0x434c44) * 150f;
                    float yaw = VoxelHash.Uniform(seed, cx, 5, cz, 0x434c44) * 360f;
                    int variant = (int)(VoxelHash.Uniform(seed, cx, 6, cz, 0x434c44) * Variants) % Variants;
                    batches[variant].Add(Matrix4x4.TRS(new Vector3(x, height, z), Quaternion.Euler(0f, yaw, 0f), new Vector3(width, tall, width)));
                    CloudCount++;
                }
            }

            var parameters = new RenderParams(material)
            {
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(eye, Vector3.one * (reach * 2.2f + baseHeight * 4f)),
            };
            for (int i = 0; i < Variants; i++)
            {
                batches[i].Draw(parameters, meshes[i]);
            }
        }

        // A cloud a unit wide and about half a unit tall, standing on its origin: a cluster of lumpy faceted balls, the
        // biggest in the middle, cut flat underneath. Flat shaded; the vertex colour's red is how far up it is, for the
        // shader to shade the underside.
        private static Mesh BuildPuff(int variant)
        {
            var random = new System.Random(1009 + variant * 31);
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            int balls = 5 + random.Next(5);
            for (int b = 0; b < balls; b++)
            {
                float t = b / (float)Mathf.Max(1, balls - 1);
                float radius = Mathf.Lerp(0.3f, 0.16f, Mathf.Abs(t - 0.5f) * 2f) * (0.85f + 0.3f * (float)random.NextDouble());
                var centre = new Vector3(Mathf.Lerp(-0.36f, 0.36f, t) + ((float)random.NextDouble() - 0.5f) * 0.12f,
                    radius * Mathf.Lerp(0.55f, 0.9f, (float)random.NextDouble()),
                    ((float)random.NextDouble() - 0.5f) * 0.36f);
                Ball(centre, radius, random, vertices, normals, colors, triangles);
            }
            var mesh = new Mesh { name = $"Cloud {variant + 1}" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // A once-divided icosahedron, its corners pushed in and out a little, cut flat at the base; flat-shaded faces.
        private static void Ball(Vector3 centre, float radius, System.Random random, List<Vector3> vertices, List<Vector3> normals,
            List<Color> colors, List<int> triangles)
        {
            float g = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var corners = new List<Vector3>
            {
                new Vector3(-1, g, 0), new Vector3(1, g, 0), new Vector3(-1, -g, 0), new Vector3(1, -g, 0),
                new Vector3(0, -1, g), new Vector3(0, 1, g), new Vector3(0, -1, -g), new Vector3(0, 1, -g),
                new Vector3(g, 0, -1), new Vector3(g, 0, 1), new Vector3(-g, 0, -1), new Vector3(-g, 0, 1),
            };
            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            // Split each face in four, with the new corners on the sphere.
            var midpoints = new Dictionary<long, int>();
            int Middle(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (!midpoints.TryGetValue(key, out int index))
                {
                    index = corners.Count;
                    corners.Add(((corners[a] + corners[b]) * 0.5f).normalized * corners[a].magnitude);
                    midpoints.Add(key, index);
                }
                return index;
            }
            var split = new List<int>();
            for (int f = 0; f < faces.Length; f += 3)
            {
                int a = faces[f], b = faces[f + 1], c = faces[f + 2];
                int ab = Middle(a, b), bc = Middle(b, c), ca = Middle(c, a);
                split.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            var points = new Vector3[corners.Count];
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 p = centre + corners[i].normalized * radius * (0.88f + 0.24f * (float)random.NextDouble());
                p.y = Mathf.Max(p.y, 0f);
                points[i] = p;
            }
            for (int f = 0; f < split.Count; f += 3)
            {
                Vector3 a = points[split[f]], b = points[split[f + 1]], c = points[split[f + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-12f)
                {
                    continue;
                }
                normal.Normalize();
                if (Vector3.Dot(normal, (a + b + c) / 3f - centre) < 0f)
                {
                    (b, c) = (c, b);
                    normal = -normal;
                }
                int first = vertices.Count;
                foreach (Vector3 p in new[] { a, b, c })
                {
                    vertices.Add(p);
                    normals.Add(normal);
                    colors.Add(new Color(Mathf.Clamp01(p.y / 0.5f), 0f, 0f, 1f));
                }
                triangles.Add(first);
                triangles.Add(first + 1);
                triangles.Add(first + 2);
            }
        }
    }
}
