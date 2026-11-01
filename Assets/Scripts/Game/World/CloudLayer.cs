using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Low-poly clouds (GL277, replacing GL252's ray-marched ones), faceted like the leaf clusters on the trees, of three kinds
    /// (GL283): fluffy cumulus of every size at varied heights, on a grid that drifts with the wind round the camera, out to
    /// <see cref="reach"/>; long flat, see-through
    /// stratus sheets low down on overcast days; and thin see-through cirrus streaks high up, lined up with the wind, on most days. How
    /// many there are and how big follows the day's cover (<see cref="SkyWeather.Cover"/>) and slowly changing patterns, so
    /// they grow and shrink rather than pop. Drawn instanced by <c>Clube/Low Poly Clouds</c>, lit by the light
    /// <see cref="DayNightCycle"/> gives the clouds (the sun, in the sunrise's or sunset's colour, a while after it leaves
    /// the land; the moon at night), with silver edges towards the sun and the sun's 12-sided outline shining through any
    /// cloud in front of it.
    /// </summary>
    public class CloudLayer : MonoBehaviour
    {
        private const int Puffs = 6;
        private const int Sheets = 3;
        private const int Wisps = 4;
        private const int Variants = Puffs + Sheets + Wisps;
        private const int SheetStart = Puffs;
        private const int WispStart = SheetStart + Sheets;
        private const int Salt = 0x434c44;

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

        [Tooltip("A cumulus cloud's width in metres, smallest and largest; most are nearer the small end.")]
        [SerializeField]
        private Vector2 size = new Vector2(250f, 1900f);

        [Tooltip("How far above the base heights the cumulus spread, in metres.")]
        [SerializeField, Min(0f)]
        private float heightSpread = 700f;

        [Header("Other kinds")]
        [Tooltip("Height of the stratus sheets, in metres; they come on overcast days.")]
        [SerializeField, Min(0f)]
        private float sheetHeight = 550f;

        [Tooltip("Height of the cirrus wisps, in metres.")]
        [SerializeField, Min(0f)]
        private float wispHeight = 5200f;

        [Tooltip("How often a day has cirrus, 0-1.")]
        [SerializeField, Range(0f, 1f)]
        private float wispDays = 0.7f;

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
        private double lastDays = -1d;
        private Material translucent;

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
                meshes[i] = i < SheetStart ? BuildPuff(i)
                    : i < WispStart ? BuildSheet(i - SheetStart)
                    : BuildWisp(i - WispStart);
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
            float seconds = GameSecondsPassed();
            driftX += wind.x * windSpeed * seconds;
            driftZ += wind.z * windSpeed * seconds;
        }

        // The wind blows by game time, so hours rested (GL290) carry the clouds hours along; real time without a clock.
        private float GameSecondsPassed()
        {
            GameClock clock = dayNight != null ? dayNight.Clock : null;
            if (clock == null)
            {
                return Time.deltaTime;
            }
            double days = clock.Days;
            float seconds = lastDays < 0d ? 0f : (float)System.Math.Max(0d, (days - lastDays) * clock.DayLengthSeconds);
            lastDays = days;
            return seconds;
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

            Vector3 eye = view.transform.position;
            float cover = Coverage;
            float time = WeatherTime;
            int seed = Seed;
            CloudCount = 0;
            AddCumulus(eye, cover, time, seed);
            // Overcast days lay a low grey sheet under the rest.
            float overcast = Mathf.InverseLerp(0.5f, 0.85f, cover);
            if (overcast > 0f)
            {
                AddSheets(eye, overcast, time, seed);
            }
            // Cirrus comes and goes over the days on its own, clear days included.
            float wisps = Mathf.InverseLerp(1f - wispDays, 1f, Mathf.PerlinNoise(seed % 53 + 0.37f, time * 0.45f + 3.1f) * 1.25f);
            if (wisps > 0f)
            {
                AddWisps(eye, wisps, time, seed);
            }

            var parameters = new RenderParams(material)
            {
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(eye, Vector3.one * (reach * 2.2f + wispHeight * 4f)),
            };
            // Puffs solid; sheets and streaks see-through (GL301).
            translucent ??= Translucent(material);
            var seeThrough = parameters;
            seeThrough.material = translucent;
            for (int i = 0; i < Variants; i++)
            {
                batches[i].Draw(i < SheetStart ? parameters : seeThrough, meshes[i]);
            }
        }

        // Cumulus: at most one a cell, most of them small and some very large, at heights spread over a few hundred metres;
        // (GL301: no more cumulonimbus towers.)
        private void AddCumulus(Vector3 eye, float cover, float time, int seed)
        {
            ForCells(eye, cellSize, 0, (cx, cz, x, z) =>
            {
                // The cover here: the day's, thicker and thinner in a slowly changing pattern across the sky.
                float pattern = Mathf.PerlinNoise(cx * 0.13f + time * 0.4f + seed % 97, cz * 0.13f - time * 0.3f + seed % 89);
                float here = Mathf.Clamp01(cover * (0.35f + 1.3f * pattern));
                float roll = VoxelHash.Uniform(seed, cx, 0, cz, Salt);
                // A cloud grows in as the cover passes its roll, rather than popping in.
                float grown = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(roll, roll + 0.18f, here));
                if (grown <= 0.02f)
                {
                    return;
                }
                float yaw = VoxelHash.Uniform(seed, cx, 5, cz, Salt) * 360f;
                float pick = VoxelHash.Uniform(seed, cx, 6, cz, Salt);
                // Width: most small, a few large (the square of a roll), all growing in with the cover.
                float share = VoxelHash.Uniform(seed, cx, 3, cz, Salt);
                float width = Mathf.Lerp(size.x, size.y, share * share) * (0.4f + 0.6f * grown);
                float tall = width * Mathf.Lerp(0.45f, 0.8f, grown * here);
                // Small ones float anywhere in the spread; large ones keep to the lower bases.
                float height = baseHeight + VoxelHash.Uniform(seed, cx, 4, cz, Salt) * heightSpread * (1f - 0.6f * share);
                Place((int)(pick * Puffs) % Puffs, new Vector3(x, height, z), yaw, new Vector3(width, tall, width));
            });
        }

        // Stratus: long, flat sheets low down, turned along the wind, spreading as the day gets more overcast.
        private void AddSheets(Vector3 eye, float overcast, float time, int seed)
        {
            float cell = cellSize * 2.4f;
            float along = Mathf.Atan2(Wind.x, Wind.z) * Mathf.Rad2Deg;
            ForCells(eye, cell, 1, (cx, cz, x, z) =>
            {
                float pattern = Mathf.PerlinNoise(cx * 0.21f - time * 0.25f + seed % 61, cz * 0.21f + time * 0.2f + seed % 67);
                float roll = VoxelHash.Uniform(seed, cx, 0, cz, Salt + 1);
                float grown = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(roll, roll + 0.25f, overcast * (0.5f + pattern)));
                if (grown <= 0.02f)
                {
                    return;
                }
                float length = cell * Mathf.Lerp(0.55f, 1f, VoxelHash.Uniform(seed, cx, 3, cz, Salt + 1)) * (0.5f + 0.5f * grown);
                float height = sheetHeight + VoxelHash.Uniform(seed, cx, 4, cz, Salt + 1) * 150f;
                float yaw = along + (VoxelHash.Uniform(seed, cx, 5, cz, Salt + 1) - 0.5f) * 30f;
                int variant = SheetStart + (int)(VoxelHash.Uniform(seed, cx, 6, cz, Salt + 1) * Sheets) % Sheets;
                Place(variant, new Vector3(x, height, z), yaw, new Vector3(length * 0.45f, length * 0.12f * grown, length));
            });
        }

        // Cirrus: thin wisps high up, streaming along the wind, in patches.
        private void AddWisps(Vector3 eye, float amount, float time, int seed)
        {
            float cell = cellSize * 1.6f;
            float along = Mathf.Atan2(Wind.x, Wind.z) * Mathf.Rad2Deg;
            ForCells(eye, cell, 2, (cx, cz, x, z) =>
            {
                float pattern = Mathf.PerlinNoise(cx * 0.17f + time * 0.5f + seed % 71, cz * 0.17f + seed % 73);
                float roll = VoxelHash.Uniform(seed, cx, 0, cz, Salt + 2);
                float grown = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(roll, roll + 0.2f, amount * (0.2f + 1.1f * pattern)));
                if (grown <= 0.02f)
                {
                    return;
                }
                float length = cell * Mathf.Lerp(0.9f, 1.7f, VoxelHash.Uniform(seed, cx, 3, cz, Salt + 2)) * (0.6f + 0.4f * grown);
                float height = wispHeight + VoxelHash.Uniform(seed, cx, 4, cz, Salt + 2) * 1200f;
                float yaw = along + (VoxelHash.Uniform(seed, cx, 5, cz, Salt + 2) - 0.5f) * 40f;
                int variant = WispStart + (int)(VoxelHash.Uniform(seed, cx, 6, cz, Salt + 2) * Wisps) % Wisps;
                Place(variant, new Vector3(x, height, z), yaw, new Vector3(length * 0.35f, length * 0.05f, length * 1.6f));
            });
        }

        // A copy of the cloud material that blends over the sky without writing depth (GL301).
        private static Material Translucent(Material solid)
        {
            var copy = new Material(solid) { name = solid.name + " (see-through)" };
            copy.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            copy.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            copy.SetFloat("_ZWrite", 0f);
            copy.SetFloat("_Alpha", 0.62f);
            copy.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 300;
            return copy;
        }

        // Every cell of a drifting grid within reach: the grid moves with the wind, and the camera through it.
        private void ForCells(Vector3 eye, float cell, int layer, System.Action<int, int, float, float> each)
        {
            double frameX = eye.x - driftX, frameZ = eye.z - driftZ;
            int fromX = Mathf.FloorToInt((float)((frameX - reach) / cell)), toX = Mathf.FloorToInt((float)((frameX + reach) / cell));
            int fromZ = Mathf.FloorToInt((float)((frameZ - reach) / cell)), toZ = Mathf.FloorToInt((float)((frameZ + reach) / cell));
            float reachSquared = reach * reach;
            int seed = Seed;
            for (int cz = fromZ; cz <= toZ; cz++)
            {
                for (int cx = fromX; cx <= toX; cx++)
                {
                    float jx = VoxelHash.Uniform(seed, cx, 1, cz, Salt + 16 * layer) - 0.5f;
                    float jz = VoxelHash.Uniform(seed, cx, 2, cz, Salt + 16 * layer) - 0.5f;
                    float x = (float)((cx + 0.5f + jx * 0.7f) * cell + driftX);
                    float z = (float)((cz + 0.5f + jz * 0.7f) * cell + driftZ);
                    float dx = x - eye.x, dz = z - eye.z;
                    if (dx * dx + dz * dz <= reachSquared)
                    {
                        each(cx, cz, x, z);
                    }
                }
            }
        }

        private void Place(int variant, Vector3 at, float yaw, Vector3 scale)
        {
            batches[variant].Add(Matrix4x4.TRS(at, Quaternion.Euler(0f, yaw, 0f), scale));
            CloudCount++;
        }

        // A cloud a unit wide and about half a unit tall, standing on its origin: a cluster of lumpy faceted balls, the
        // biggest in the middle, cut flat underneath. Flat shaded; the vertex colour's red is how far up it is, for the
        // shader to shade the underside.
        private static Mesh BuildPuff(int variant)
        {
            var random = new System.Random(1009 + variant * 31);
            var parts = new MeshParts();
            int balls = 5 + random.Next(5);
            for (int b = 0; b < balls; b++)
            {
                float t = b / (float)Mathf.Max(1, balls - 1);
                float radius = Mathf.Lerp(0.3f, 0.16f, Mathf.Abs(t - 0.5f) * 2f) * (0.85f + 0.3f * (float)random.NextDouble());
                var centre = new Vector3(Mathf.Lerp(-0.36f, 0.36f, t) + ((float)random.NextDouble() - 0.5f) * 0.12f,
                    radius * Mathf.Lerp(0.55f, 0.9f, (float)random.NextDouble()),
                    ((float)random.NextDouble() - 0.5f) * 0.36f);
                Ball(centre, radius, Vector3.one, true, 0.5f, random, parts);
            }
            return parts.Build($"Cloud {variant + 1}");
        }

        // A stratus sheet about a unit long (along Z), a unit wide and flat (GL301): many low faceted lumps of different
        // sizes side by side, a few riding higher, so its top reads as low-poly billows. Drawn see-through.
        private static Mesh BuildSheet(int variant)
        {
            var random = new System.Random(3001 + variant * 41);
            var parts = new MeshParts();
            for (int i = 0; i < 18; i++)
            {
                float t = i / 17f;
                float radius = 0.16f + 0.12f * (float)random.NextDouble();
                var centre = new Vector3(((float)random.NextDouble() - 0.5f) * 0.7f, 0.18f + (float)random.NextDouble() * 0.12f,
                    Mathf.Lerp(-0.42f, 0.42f, t) + ((float)random.NextDouble() - 0.5f) * 0.08f);
                Ball(centre, radius, new Vector3(1.4f, 0.5f, 1.2f), true, 0.45f, random, parts);
            }
            return parts.Build($"Stratus {variant + 1}");
        }

        // A cirrus streak about a unit long (along Z, GL301): a few long, thin trails side by side, each a chain of
        // stretched shards thinning to nothing at both ends and bending a little, like mares' tails. Undivided, so its
        // facets are few and angular; lit evenly (no darker underside); drawn see-through.
        private static Mesh BuildWisp(int variant)
        {
            var random = new System.Random(4001 + variant * 43);
            var parts = new MeshParts();
            int strands = 3 + random.Next(3);
            for (int s = 0; s < strands; s++)
            {
                float offset = ((float)random.NextDouble() - 0.5f) * 0.3f;
                float bend = ((float)random.NextDouble() - 0.5f) * 0.25f;
                float start = -0.5f + (float)random.NextDouble() * 0.25f, end = 0.5f - (float)random.NextDouble() * 0.25f;
                int shards = 12 + random.Next(6);
                for (int i = 0; i < shards; i++)
                {
                    float t = i / (float)(shards - 1);
                    float z = Mathf.Lerp(start, end, t);
                    float x = offset + bend * t * t;
                    float thick = Mathf.Sin(t * Mathf.PI) * (0.6f + 0.5f * (float)random.NextDouble());
                    Ball(new Vector3(x, 0.5f, z), 0.025f * thick + 0.008f, new Vector3(1.2f, 0.3f, 6f), false, 0f, random, parts, false);
                }
            }
            return parts.Build($"Cirrus {variant + 1}");
        }

        // A faceted ball: an icosahedron, divided once unless asked not to, its corners pushed in and out a little and
        // stretched per axis about its centre; cut flat at the base when asked. Flat-shaded faces; the vertex colour's red is
        // the height over shadeHeight (1 everywhere when shadeHeight is 0).
        private static void Ball(Vector3 centre, float radius, Vector3 stretch, bool flatBase, float shadeHeight, System.Random random,
            MeshParts parts, bool divide = true)
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
            var split = new List<int>();
            if (divide)
            {
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
                for (int f = 0; f < faces.Length; f += 3)
                {
                    int a = faces[f], b = faces[f + 1], c = faces[f + 2];
                    int ab = Middle(a, b), bc = Middle(b, c), ca = Middle(c, a);
                    split.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
            }
            else
            {
                split.AddRange(faces);
            }
            var points = new Vector3[corners.Count];
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 offset = corners[i].normalized * radius * (0.88f + 0.24f * (float)random.NextDouble());
                Vector3 p = centre + Vector3.Scale(offset, stretch);
                if (flatBase)
                {
                    p.y = Mathf.Max(p.y, 0f);
                }
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
                parts.Face(a, b, c, normal, shadeHeight);
            }
        }

        // The faces of one cloud mesh as they're built.
        private sealed class MeshParts
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Color> colors = new List<Color>();
            private readonly List<int> triangles = new List<int>();

            public void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 normal, float shadeHeight)
            {
                int first = vertices.Count;
                foreach (Vector3 p in new[] { a, b, c })
                {
                    vertices.Add(p);
                    normals.Add(normal);
                    colors.Add(new Color(shadeHeight > 0f ? Mathf.Clamp01(p.y / shadeHeight) : 1f, 0f, 0f, 1f));
                }
                triangles.Add(first);
                triangles.Add(first + 1);
                triangles.Add(first + 2);
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                if (vertices.Count > 65000)
                {
                    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                }
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetColors(colors);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
