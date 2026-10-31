using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Volumetric clouds (GL252): a layer of cumulus between <see cref="BaseHeight"/> and <see cref="TopHeight"/>,
    /// ray-marched per pixel by <c>Clube/Clouds</c> on a sphere round the camera. The clouds are shaped from a repeating 3D
    /// noise texture (<see cref="Clube.Core.CloudNoise"/>, baked by *Clube → Sky → Build cloud noise*): a large-scale cover
    /// map says where clouds stand and how tall, billowy noise gives them form and finer noise erodes their edges. They
    /// drift with a wind that slowly swings round and boil gently as they go, and the cover changes over the game's days,
    /// clear one day and heavy the next. The scene's directional light lights them (the sun by day, the moon at night, as
    /// <see cref="DayNightCycle"/> sets it): light scatters through them strongly forwards, so their edges glow with the
    /// sun or moon behind them, and the sky's ambient light fills in their shade. The ground hides them where it's in front
    /// (the camera's depth texture), and they fade into the haze far off.
    /// </summary>
    public class CloudLayer : MonoBehaviour
    {
        private static readonly int NoiseId = Shader.PropertyToID("_CloudNoise");
        private static readonly int BaseId = Shader.PropertyToID("_CloudBase");
        private static readonly int TopId = Shader.PropertyToID("_CloudTop");
        private static readonly int PlanetRadiusId = Shader.PropertyToID("_PlanetRadius");
        private static readonly int CoverageId = Shader.PropertyToID("_Coverage");
        private static readonly int ExtinctionId = Shader.PropertyToID("_Extinction");
        private static readonly int ScalesId = Shader.PropertyToID("_Scales");
        private static readonly int ShapeOffsetId = Shader.PropertyToID("_ShapeOffset");
        private static readonly int WeatherId = Shader.PropertyToID("_Weather");
        private static readonly int StepsId = Shader.PropertyToID("_Steps");
        private static readonly int MaxDistanceId = Shader.PropertyToID("_MaxDistance");

        [Tooltip("The Clube/Clouds material.")]
        [SerializeField]
        private Material material;

        [Tooltip("The baked cloud noise (Clube → Sky → Build cloud noise).")]
        [SerializeField]
        private Texture3D noise;

        [Tooltip("The game's days, for the weather changing from day to day. Optional.")]
        [SerializeField]
        private DayNightCycle dayNight;

        [Header("Layer")]
        [Tooltip("Height of the cloud base, in metres.")]
        [SerializeField, Min(0f)]
        private float baseHeight = 1100f;

        [Tooltip("How deep the layer is, in metres: the tallest clouds reach the top.")]
        [SerializeField, Min(50f)]
        private float thickness = 900f;

        [Tooltip("Scales each day's cloud cover (GL263: clear, partly cloudy or heavy by the day, SkyWeather.Cover); 1 as the days have it.")]
        [SerializeField, Range(0f, 2f)]
        private float coverageScale = 1f;

        [Tooltip("Roughly how many game days the wind and the clouds' patterns take to change.")]
        [SerializeField, Min(0.1f)]
        private float weatherDays = 1.5f;

        [Tooltip("How thick the clouds are: light lost per metre through the densest cloud.")]
        [SerializeField, Range(0.002f, 0.2f)]
        private float extinction = 0.035f;

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

        [Tooltip("How fast the clouds boil and change shape as they go, in metres a second.")]
        [SerializeField, Min(0f)]
        private float boil = 1.5f;

        [Header("Shape")]
        [Tooltip("Size of a cloud's billowing shape, in metres (one repeat of the noise).")]
        [SerializeField, Min(100f)]
        private float shapeScale = 3200f;

        [Tooltip("Size of the fine detail on cloud edges, in metres.")]
        [SerializeField, Min(20f)]
        private float detailScale = 520f;

        [Tooltip("Size of the cover map: how far apart clear and cloudy patches are, in metres.")]
        [SerializeField, Min(500f)]
        private float weatherScale = 16000f;

        [Header("Drawing")]
        [Tooltip("Ray-march steps through the layer per pixel: more is smoother and slower.")]
        [SerializeField, Range(8, 128)]
        private int steps = 48;

        [Tooltip("How far out clouds are drawn, in metres; they fade into the haze before it.")]
        [SerializeField, Min(1000f)]
        private float maxDistance = 30000f;

        [Tooltip("The radius the layer curves round, in metres, so it meets the horizon.")]
        [SerializeField, Min(10000f)]
        private float planetRadius = 600000f;

        private GameObject sphere;
        private Material instance;
        private WorldView world;
        private double driftX;
        private double driftZ;
        private double boilOffset;

        /// <summary>Height of the cloud base, in metres.</summary>
        public float BaseHeight => baseHeight;

        /// <summary>Height of the top of the layer, in metres.</summary>
        public float TopHeight => baseHeight + thickness;

        /// <summary>How much of the sky is covered now, 0-1: today's cover (GL263), easing into tomorrow's.</summary>
        public float Coverage => Mathf.Clamp01(SkyWeather.Cover(Seed, GameDays) * coverageScale);

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

        /// <summary>Where the wind blows towards now, a flat world direction.</summary>
        public Vector3 Wind
        {
            get
            {
                float angle = windDirection + windSwing * (2f * Mathf.PerlinNoise(0.71f, WeatherTime * 0.6f) - 1f);
                return Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            }
        }

        // The weather's place in its noise: game days over a spell's length.
        private float WeatherTime => (float)(GameDays / weatherDays % 10000d);

        private void Update()
        {
            float step = Time.deltaTime;
            Vector3 wind = Wind;
            driftX += wind.x * windSpeed * step;
            driftZ += wind.z * windSpeed * step;
            boilOffset += boil * step;
        }

        private void LateUpdate()
        {
            Camera view = Camera.main;
            if (view == null || material == null || noise == null)
            {
                return;
            }
            if (instance == null)
            {
                instance = new Material(material) { name = material.name + " (clouds)" };
                sphere = SkySphere.Create("Clouds", transform, instance);
            }
            SkySphere.Follow(sphere.transform, view, 0.5f);

            instance.SetTexture(NoiseId, noise);
            instance.SetFloat(BaseId, baseHeight);
            instance.SetFloat(TopId, baseHeight + thickness);
            instance.SetFloat(PlanetRadiusId, planetRadius);
            instance.SetFloat(CoverageId, Coverage);
            instance.SetFloat(ExtinctionId, extinction);
            instance.SetVector(ScalesId, new Vector4(shapeScale, detailScale, weatherScale, 0f));
            // Offsets wrap at their noise's repeat, so they stay small however long the game runs.
            instance.SetVector(ShapeOffsetId, new Vector4(
                Wrap(driftX, shapeScale), Wrap(driftZ, shapeScale), Wrap(driftX * 1.15d, detailScale), Wrap(driftZ * 1.15d, detailScale)));
            float weatherTime = WeatherTime;
            instance.SetVector(WeatherId, new Vector4(
                Wrap(driftX, weatherScale), Wrap(driftZ, weatherScale), weatherTime * 0.11f % 1f, Wrap(boilOffset, shapeScale)));
            instance.SetFloat(StepsId, steps);
            instance.SetFloat(MaxDistanceId, Mathf.Min(maxDistance, view.farClipPlane * 0.95f));
        }

        private void OnDisable()
        {
            if (sphere != null)
            {
                sphere.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (sphere != null)
            {
                sphere.SetActive(true);
            }
        }

        private void OnDestroy()
        {
            SkySphere.Destroy(sphere);
            if (instance != null)
            {
                Destroy(instance);
            }
        }

        private static float Wrap(double value, float period)
        {
            double wrapped = value % period;
            return (float)(wrapped < 0d ? wrapped + period : wrapped);
        }
    }
}
