using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Game
{
    /// <summary>
    /// Day and night (GL227, first pass of PK4): the hour of day follows the <see cref="GameClock"/>, one in-game day
    /// of 40 minutes, the sun up from 06:00 to 18:00 (20 minutes) and down for the other 20. The sun light swings
    /// across the sky, warm and low at the ends of the day; at night the same light is a dim blue moon, and the
    /// ambient light, fog and sky darken with it. The graphics popup can freeze the sky at an hour
    /// (<see cref="GraphicsSettings.FreezeDay"/>); the clock itself, and so drying and smelting, go on.
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

        [Tooltip("The hour of day when the clock reads 0 days: a new game starts in the morning.")]
        [SerializeField, Range(0f, 24f)]
        private float startHour = 8f;

        [Tooltip("The compass direction the sun crosses the sky towards, in degrees.")]
        [SerializeField]
        private float azimuth = 150f;

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

        private Material sky;
        private Material sceneSky;
        private Color ambientSky;
        private Color ambientEquator;
        private Color ambientGround;
        private SphericalHarmonicsL2 ambientProbe;
        private Color fogColor;
        private float skyExposure = 1f;
        private bool captured;

        /// <summary>The hour of day shown now, 0-24: the clock's, or the frozen hour.</summary>
        public float Hour => GraphicsSettings.FreezeDay ? GraphicsSettings.FrozenHour : ClockHour;

        /// <summary>The hour of day on the clock, 0-24.</summary>
        public float ClockHour => clock != null ? Mathf.Repeat((float)(clock.Days % 1d) * 24f + startHour, 24f) : startHour;

        /// <summary>How much daylight there is, 0 at night to 1 by day.</summary>
        public float Daylight { get; private set; } = 1f;

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
            if (sky != null)
            {
                Destroy(sky);
                sky = null;
            }
        }

        private void LateUpdate()
        {
            Apply(Hour);
        }

        private void Apply(float hour)
        {
            // Above the horizon from sunrise to sunset; the height follows a sine, negative at night.
            float sunHeight = Mathf.Sin((hour - SunriseHour) / (SunsetHour - SunriseHour) * Mathf.PI);
            if (hour < SunriseHour || hour > SunsetHour)
            {
                float night = Mathf.Repeat(hour - SunsetHour, 24f) / (24f - (SunsetHour - SunriseHour));
                sunHeight = -Mathf.Sin(night * Mathf.PI);
            }
            Daylight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.12f, 0.25f, sunHeight));

            if (sun != null)
            {
                if (sunHeight >= 0f)
                {
                    // Across the sky from east of the azimuth to west of it.
                    float along = Mathf.Clamp01((hour - SunriseHour) / (SunsetHour - SunriseHour));
                    float elevation = Mathf.Max(3f, sunHeight * noonElevation);
                    sun.transform.rotation = Quaternion.Euler(elevation, azimuth - 90f + along * 180f, 0f);
                    sun.color = Color.Lerp(lowSunColor, noonColor, Mathf.SmoothStep(0f, 1f, sunHeight * 2f));
                    sun.intensity = Mathf.Lerp(moonIntensity, dayIntensity, Daylight);
                }
                else
                {
                    // The moon crosses the other way, lower.
                    float night = Mathf.Repeat(hour - SunsetHour, 24f) / (24f - (SunsetHour - SunriseHour));
                    float elevation = Mathf.Max(3f, -sunHeight * 45f);
                    sun.transform.rotation = Quaternion.Euler(elevation, azimuth + 90f + night * 180f, 0f);
                    sun.color = Color.Lerp(moonColor, lowSunColor, Daylight);
                    sun.intensity = Mathf.Lerp(moonIntensity, dayIntensity, Daylight);
                }
            }

            float ambient = Mathf.Lerp(nightAmbient, 1f, Daylight);
            RenderSettings.ambientSkyColor = ambientSky * ambient;
            RenderSettings.ambientEquatorColor = ambientEquator * ambient;
            RenderSettings.ambientGroundColor = ambientGround * ambient;
            RenderSettings.ambientProbe = ambientProbe * ambient;
            RenderSettings.fogColor = Color.Lerp(fogColor * 0.15f, fogColor, Daylight);
            if (sky != null && sky.HasProperty("_Exposure"))
            {
                sky.SetFloat("_Exposure", Mathf.Lerp(0.18f, skyExposure, Daylight));
            }
        }
    }
}
