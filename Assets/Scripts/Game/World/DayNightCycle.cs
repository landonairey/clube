using Clube.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Game
{
    /// <summary>
    /// Day and night (GL227, first pass of PK4): the hour of day follows the <see cref="GameClock"/>, one in-game day
    /// of 40 minutes, the sun up from 06:00 to 18:00 (20 minutes) and down for the other 20. The sun rises in the east,
    /// crosses the sky warm and low at the ends of the day, and goes down under the horizon (GL248). The moon has its own
    /// orbit (<see cref="SkyPositions"/>): a month of 29.5 days, rising about 50 minutes later each day, its phase the
    /// angle between it and the sun, drawn by <see cref="MoonDisc"/>. The stars (<see cref="StarSky"/>) turn about the
    /// celestial pole once a sidereal day and come out after sunset (GL253). The one directional light is the sun while it's up
    /// or just under, and the moon after dusk, as bright as its phase; the ambient light, fog and sky darken at night.
    /// The graphics popup can freeze the sky at an hour (<see cref="GraphicsSettings.FreezeDay"/>); the clock itself,
    /// and so drying and smelting, go on.
    /// </summary>
    /// <remarks>
    /// Works on copies: the skybox material is instanced and the scene's ambient colours and fog are read once and
    /// put back when this is switched off, so nothing it changes is saved into the scene or the material.
    /// </remarks>
    public class DayNightCycle : MonoBehaviour
    {
        public const float SunriseHour = 6f;
        public const float SunsetHour = 18f;

        [SerializeField]
        private GameClock clock;

        [Tooltip("The scene's directional light: the sun by day, the moon by night.")]
        [SerializeField]
        private Light sun;

        [Tooltip("The hour of day when the clock reads 0 days: a new game starts at 7 in the morning (GL428).")]
        [SerializeField, Range(0f, 24f)]
        private float startHour = 7f;

        [Tooltip("The compass direction of north in the world, in degrees: the sun is opposite it at noon.")]
        [SerializeField]
        private float azimuth = 150f;

        [Tooltip("The moon's age when the clock reads 0 days, in days after a new moon (GL248): 5 is a crescent in the evening sky.")]
        [SerializeField, Range(0f, 29.5f)]
        private float startMoonAge = 5f;

        [Tooltip("Draws the moon (GL248). Optional.")]
        [SerializeField]
        private MoonDisc moon;

        [Tooltip("Draws the stars, turned with the sky (GL253). Optional.")]
        [SerializeField]
        private StarSky stars;

        [Tooltip("Draws the sun as a low-poly ball of light (GL277), in place of the skybox's. Optional: without it the skybox draws it.")]
        [SerializeField]
        private SunDisc sunDisc;

        [Tooltip("How high the sun gets at noon, in degrees.")]
        [SerializeField, Range(10f, 90f)]
        private float noonElevation = 62f;

        [SerializeField]
        private float dayIntensity = 1.15f;

        [SerializeField]
        private Color noonColor = new Color(1f, 0.96f, 0.9f);

        [SerializeField]
        private Color lowSunColor = new Color(1f, 0.62f, 0.38f);

        [SerializeField]
        private float moonIntensity = 0.22f;

        [SerializeField]
        private Color moonColor = new Color(0.55f, 0.66f, 1f);

        [Tooltip("The ambient light at midnight, as a share of the day's.")]
        [SerializeField, Range(0f, 1f)]
        private float nightAmbient = 0.3f;

        [Tooltip("How strongly sunrises and sunsets take their colour (GL262): 0 the plain warm light every day, 1 as the day's weather has it.")]
        [SerializeField, Range(0f, 2f)]
        private float sunsetStrength = 1f;

        private static readonly int SkyTintId = Shader.PropertyToID("_SkyTint");
        private static readonly Color TwilightTint = new Color(0.4f, 0.4f, 0.75f);
        private static readonly int AtmosphereId = Shader.PropertyToID("_AtmosphereThickness");
        private static readonly int CloudLightDirId = Shader.PropertyToID("_CloudLightDir");
        private static readonly int CloudLightColorId = Shader.PropertyToID("_CloudLightColor");

        private Material sky;
        private Color skyTint = Color.gray;
        private float atmosphere = 1f;
        private CloudLayer clouds;
        private WorldView world;
        private Material sceneSky;
        private Color ambientSky;
        private Color ambientEquator;
        private Color ambientGround;
        private SphericalHarmonicsL2 ambientProbe;
        private Color fogColor;
        private float skyExposure = 1f;
        private bool captured;
        private string sunDiscKeyword = "_SUNDISK_HIGH_QUALITY";
        private bool sunDiscShown = true;

        /// <summary>The game's clock, or null.</summary>
        public GameClock Clock => clock;

        /// <summary>The hour of day shown now, 0-24: the clock's, or the frozen hour.</summary>
        public float Hour => GraphicsSettings.FreezeDay ? GraphicsSettings.FrozenHour : ClockHour;

        /// <summary>The hour of day on the clock, 0-24.</summary>
        public float ClockHour => clock != null ? Mathf.Repeat((float)(clock.Days % 1d) * 24f + startHour, 24f) : startHour;

        /// <summary>How much daylight there is, 0 at night to 1 by day.</summary>
        public float Daylight { get; private set; } = 1f;

        /// <summary>Days on the clock, or the frozen hour's time on the clock's day.</summary>
        public double Days
        {
            get
            {
                double days = clock != null ? clock.Days : 0d;
                if (GraphicsSettings.FreezeDay)
                {
                    days = System.Math.Floor(days) + (GraphicsSettings.FrozenHour - startHour) / 24d;
                }
                return days;
            }
        }

        /// <summary>The day the game is on by the clock, from 1; a day starts at midnight (GL259).</summary>
        public int Day => (int)System.Math.Floor((clock != null ? clock.Days : 0d) + startHour / 24d) + 1;

        /// <summary>Days since a new moon.</summary>
        public double MoonAge => Days + startMoonAge;

        /// <summary>The moon's phase, 0-1 of its month: 0 new, 0.5 full (GL248).</summary>
        public float MoonPhase => SkyPositions.MoonPhase(MoonAge);

        /// <summary>Where the sun is, a world direction towards it.</summary>
        public Vector3 SunDirection { get; private set; } = Vector3.up;

        /// <summary>Where the moon is, a world direction towards it.</summary>
        public Vector3 MoonDirection { get; private set; } = Vector3.down;

        /// <summary>
        /// Sets the clock to an hour of the day, <paramref name="laterDays"/> days on from today (the lab's time cheat, GL248):
        /// for looking at the sky and the moon's phases. Everything on the clock (drying, smelting) moves with it.
        /// </summary>
        public void SetTime(float hour, double laterDays = 0d)
        {
            if (clock == null)
            {
                return;
            }
            double day = System.Math.Floor(clock.Days) + System.Math.Max(0d, laterDays);
            clock.Restore(day + Mathf.Repeat(hour - startHour, 24f) / 24d);
        }

        // The latitude the sky is seen from: the sun's noon height sets it.
        private float Latitude => 90f - noonElevation;

        private void OnEnable()
        {
            if (sun == null)
            {
                sun = RenderSettings.sun;
            }
            sceneSky = RenderSettings.skybox;
            ambientSky = RenderSettings.ambientSkyColor;
            ambientEquator = RenderSettings.ambientEquatorColor;
            ambientGround = RenderSettings.ambientGroundColor;
            ambientProbe = RenderSettings.ambientProbe;
            fogColor = RenderSettings.fogColor;
            if (sceneSky != null)
            {
                sky = new Material(sceneSky) { name = sceneSky.name + " (day and night)" };
                skyExposure = sky.HasProperty("_Exposure") ? sky.GetFloat("_Exposure") : 1f;
                skyTint = sky.HasProperty(SkyTintId) ? sky.GetColor(SkyTintId) : Color.gray;
                atmosphere = sky.HasProperty(AtmosphereId) ? sky.GetFloat(AtmosphereId) : 1f;
                sunDiscKeyword = sky.IsKeywordEnabled("_SUNDISK_SIMPLE") ? "_SUNDISK_SIMPLE" : "_SUNDISK_HIGH_QUALITY";
                sunDiscShown = true;
                RenderSettings.skybox = sky;
            }
            if (sun != null)
            {
                RenderSettings.sun = sun;
            }
            captured = true;
        }

        private void OnDisable()
        {
            if (!captured)
            {
                return;
            }
            captured = false;
            RenderSettings.skybox = sceneSky;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.ambientProbe = ambientProbe;
            RenderSettings.fogColor = fogColor;
            Shader.SetGlobalVector(CloudLightColorId, Vector4.zero);
            if (sky != null)
            {
                Destroy(sky);
                sky = null;
            }
        }

        /// <summary>
        /// The look of the sunrise or sunset nearest the hour (GL262): the morning's before noon, the evening's after,
        /// with the clouds' cover now.
        /// </summary>
        public SunsetLook SunsetNow(float hour)
        {
            world ??= GetComponent<WorldView>();
            clouds ??= GetComponent<CloudLayer>();
            int seed = world != null && world.Config != null ? world.Config.Terrain.Seed : 0;
            float cover = clouds != null && clouds.isActiveAndEnabled ? clouds.Coverage : 0.4f;
            SunsetLook look = SkyWeather.Sunset(seed, Day, hour >= 12f, cover);
            return new SunsetLook(look.Glow, Mathf.Clamp01(look.Vividness * sunsetStrength));
        }

        private void LateUpdate()
        {
            Apply(Hour);
        }

        private void Apply(float hour)
        {
            Quaternion compass = Quaternion.Euler(0f, azimuth, 0f);
            Vector3 sunDirection = compass * SkyPositions.Sun(hour, Latitude);
            Vector3 moonDirection = compass * SkyPositions.Moon(MoonAge, hour, Latitude);
            SunDirection = sunDirection;
            MoonDirection = moonDirection;
            float sunHeight = sunDirection.y;
            Daylight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.12f, 0.25f, sunHeight));

            // The sun's light fades out as it goes under (it's gone about 4 degrees down); the moon's comes in as dusk
            // deepens, as bright as its phase and its height allow. Both are near nothing when the light changes over.
            float sunLight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.07f, 0.06f, sunHeight));
            float illumination = SkyPositions.MoonIllumination(MoonPhase);
            float moonLight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.07f, -0.16f, sunHeight))
                              * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.02f, 0.12f, moonDirection.y))
                              * Mathf.Lerp(0.15f, 1f, illumination);
            bool sunsLight = sunHeight > -0.07f;

            // Sunrise and sunset (GL262): today's colour, strongest with the sun at the horizon, gone by about 15° up or
            // 10° under. The low sun lights the land in it, the sky takes a tint and a thicker haze, and the clouds catch
            // it most of all, still lit from under the horizon after the land has gone dark.
            SunsetLook look = SunsetNow(hour);
            float twilight = sunHeight >= 0f
                ? 1f - Mathf.SmoothStep(0f, 1f, sunHeight / 0.26f)
                : 1f - Mathf.SmoothStep(0f, 1f, -sunHeight / 0.18f);
            float glow = look.Vividness * twilight;
            Color lowSun = Color.Lerp(lowSunColor, look.Glow, look.Vividness * 0.85f);
            Color sunColour = Color.Lerp(lowSun, noonColor, Mathf.SmoothStep(0f, 1f, sunHeight * 2f));
            if (sun != null)
            {
                if (sunsLight)
                {
                    sun.transform.rotation = Quaternion.LookRotation(-sunDirection);
                    sun.color = sunColour;
                    sun.intensity = dayIntensity * sunLight;
                }
                else
                {
                    sun.transform.rotation = Quaternion.LookRotation(-moonDirection);
                    sun.color = moonColor;
                    sun.intensity = moonIntensity * moonLight;
                }
            }
            if (sky != null)
            {
                // The sky draws the sun's disc where the light comes from: none while the light is the moon's.
                // The skybox draws the sun only without a SunDisc, and only while the light is the sun's.
                SetSunDisc(sunsLight && sunDisc == null);
            }
            if (moon != null)
            {
                moon.Show(moonDirection, sunDirection, Daylight);
            }
            if (stars != null)
            {
                // The celestial frame: right ascension 0 and 90 on the equator, and the pole, where they are now.
                // They come out once the sun is under the horizon, all of them by nautical dusk (12 degrees down).
                double days = Days;
                float latitude = Latitude;
                // A bright moon high up washes out the fainter ones.
                float night = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.02f, -0.2f, sunHeight))
                              * (1f - 0.45f * illumination * Mathf.Clamp01(moonDirection.y * 2f));
                stars.Show(compass * SkyPositions.Star(0f, 0f, days, hour, latitude),
                    compass * SkyPositions.Star(90f, 0f, days, hour, latitude),
                    compass * SkyPositions.Star(0f, 90f, days, hour, latitude),
                    night);
            }

            // A bright moon lights the night a little more than a new one.
            float ambient = Mathf.Lerp(nightAmbient * Mathf.Lerp(0.7f, 1.15f, illumination), 1f, Daylight);
            RenderSettings.ambientSkyColor = ambientSky * ambient;
            RenderSettings.ambientEquatorColor = ambientEquator * ambient;
            RenderSettings.ambientGroundColor = ambientGround * ambient;
            RenderSettings.ambientProbe = ambientProbe * ambient;
            Color fog = Color.Lerp(fogColor * 0.15f, fogColor, Daylight);
            RenderSettings.fogColor = Color.Lerp(fog, look.Glow * fog.maxColorComponent * 1.4f, glow * 0.3f);
            if (sky != null && sky.HasProperty("_Exposure"))
            {
                sky.SetFloat("_Exposure", Mathf.Lerp(0.18f, skyExposure, Daylight));
            }
            if (sky != null && sky.HasProperty(SkyTintId))
            {
                // The procedural sky turns olive just after sunset under a thick atmosphere, whatever its tint; a thin one
                // keeps the clear sky between the clouds a deep twilight blue, so the colour is the clouds' and the haze's.
                sky.SetColor(SkyTintId, Color.Lerp(skyTint, TwilightTint, twilight * 0.5f));
                sky.SetFloat(AtmosphereId, Mathf.Lerp(atmosphere, 0.75f, twilight));
            }
            LightClouds(sunDirection, sunHeight, sunColour, look, glow, moonDirection, moonLight);
            if (sunDisc != null)
            {
                sunDisc.Show(sunDirection, sunColour, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.05f, 0.03f, sunHeight)));
            }
        }

        // The clouds' light (GL262), for Clube/Clouds: the sun's while it can reach them, which at their height is a
        // while after it has left the land, in the sunset's colour near the horizon; then the moon's.
        private void LightClouds(Vector3 sunDirection, float sunHeight, Color sunColour, SunsetLook look, float glow, Vector3 moonDirection, float moonLight)
        {
            float sunOnClouds = dayIntensity * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.17f, 0.05f, sunHeight));
            Color onClouds = Color.Lerp(sunColour, look.Glow, glow * 0.8f) * (1f + 0.6f * glow);
            float moonOnClouds = moonIntensity * moonLight;
            bool sunLights = sunOnClouds * onClouds.maxColorComponent >= moonOnClouds * moonColor.maxColorComponent;
            Vector3 direction = sunLights ? sunDirection : moonDirection;
            Color light = sunLights ? onClouds.linear * sunOnClouds : moonColor.linear * moonOnClouds;
            Shader.SetGlobalVector(CloudLightDirId, direction.normalized);
            Shader.SetGlobalVector(CloudLightColorId, new Vector4(light.r, light.g, light.b, 1f));
        }

        // The procedural skybox's sun disc is a keyword set (none, simple, high quality); the material is this cycle's copy.
        private void SetSunDisc(bool shown)
        {
            if (shown == sunDiscShown)
            {
                return;
            }
            sunDiscShown = shown;
            if (shown)
            {
                sky.DisableKeyword("_SUNDISK_NONE");
                sky.EnableKeyword(sunDiscKeyword);
            }
            else
            {
                sky.DisableKeyword(sunDiscKeyword);
                sky.EnableKeyword("_SUNDISK_NONE");
            }
        }
    }
}
