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
    /// angle between it and the sun, drawn by <see cref="MoonDisc"/>. The one directional light is the sun while it's up
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

        [Tooltip("The hour of day when the clock reads 0 days: a new game starts in the morning.")]
        [SerializeField, Range(0f, 24f)]
        private float startHour = 8f;

        [Tooltip("The compass direction of north in the world, in degrees: the sun is opposite it at noon.")]
        [SerializeField]
        private float azimuth = 150f;

        [Tooltip("The moon's age when the clock reads 0 days, in days after a new moon (GL248): 5 is a crescent in the evening sky.")]
        [SerializeField, Range(0f, 29.5f)]
        private float startMoonAge = 5f;

        [Tooltip("Draws the moon (GL248). Optional.")]
        [SerializeField]
        private MoonDisc moon;

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
        private string sunDiscKeyword = "_SUNDISK_HIGH_QUALITY";
        private bool sunDiscShown = true;

        /// <summary>The hour of day shown now, 0-24: the clock's, or the frozen hour.</summary>
        public float Hour => GraphicsSettings.FreezeDay ? GraphicsSettings.FrozenHour : ClockHour;

        /// <summary>The hour of day on the clock, 0-24.</summary>
        public float ClockHour => clock != null ? Mathf.Repeat((float)(clock.Days % 1d) * 24f + startHour, 24f) : startHour;

        /// <summary>How much daylight there is, 0 at night to 1 by day.</summary>
        public float Daylight { get; private set; } = 1f;

        /// <summary>Days since a new moon, the clock's (or the frozen hour's on the clock's day).</summary>
        public double MoonAge
        {
            get
            {
                double days = clock != null ? clock.Days : 0d;
                if (GraphicsSettings.FreezeDay)
                {
                    days = System.Math.Floor(days) + (GraphicsSettings.FrozenHour - startHour) / 24d;
                }
                return days + startMoonAge;
            }
        }

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
            if (sun != null)
            {
                if (sunsLight)
                {
                    sun.transform.rotation = Quaternion.LookRotation(-sunDirection);
                    sun.color = Color.Lerp(lowSunColor, noonColor, Mathf.SmoothStep(0f, 1f, sunHeight * 2f));
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
                SetSunDisc(sunsLight);
            }
            if (moon != null)
            {
                moon.Show(moonDirection, sunDirection, Daylight);
            }

            // A bright moon lights the night a little more than a new one.
            float ambient = Mathf.Lerp(nightAmbient * Mathf.Lerp(0.7f, 1.15f, illumination), 1f, Daylight);
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
