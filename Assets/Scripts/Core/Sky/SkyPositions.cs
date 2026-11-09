using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Where the sun and moon are in the sky (GL248), worked out the way they move round the Earth, scaled to the
    /// game's day: the sun crosses from east to west and goes under the horizon at night; the moon goes round once a
    /// synodic month (<see cref="SynodicMonthDays"/>), so it rises about 50 minutes (of game time) later each day, and its
    /// phase is how far it has drifted from the sun. Its path tilts with the ecliptic and its own 5° orbit, so it rides
    /// high some nights and low on others. The stars turn about the celestial pole once a sidereal day
    /// (<see cref="SiderealAngle"/>), the pole standing as high over the north horizon as the latitude.
    /// </summary>
    /// <remarks>
    /// Plain math. Directions are in the sky's frame: x east, y up, z north. The sun keeps to the equinox (no seasons), so
    /// it rises at 04:00 and sets at 20:00 every day (GL464): 16 hours of light and 8 of dark, as near a midsummer solstice.
    /// </remarks>
    public static class SkyPositions
    {
        /// <summary>New moon to new moon, in days.</summary>
        public const double SynodicMonthDays = 29.530589;

        /// <summary>Node to node of the moon's orbit (its tilt against the ecliptic comes round), in days.</summary>
        public const double DraconicMonthDays = 27.212221;

        /// <summary>The tilt of the Earth's axis to its orbit, in degrees: the ecliptic's tilt to the equator.</summary>
        public const float AxialTilt = 23.44f;

        /// <summary>The tilt of the moon's orbit to the ecliptic, in degrees.</summary>
        public const float MoonOrbitTilt = 5.14f;

        /// <summary>
        /// The direction of a body with this hour angle and declination, seen from <paramref name="latitude"/> (degrees).
        /// Hour angle 0 is due south at its highest (for a northern latitude); it grows westward, 15° an hour.
        /// </summary>
        public static Vector3 Direction(float hourAngleDegrees, float declinationDegrees, float latitude)
        {
            float h = hourAngleDegrees * Mathf.Deg2Rad;
            float d = declinationDegrees * Mathf.Deg2Rad;
            float p = latitude * Mathf.Deg2Rad;
            float east = -Mathf.Cos(d) * Mathf.Sin(h);
            float north = Mathf.Cos(p) * Mathf.Sin(d) - Mathf.Sin(p) * Mathf.Cos(d) * Mathf.Cos(h);
            float up = Mathf.Sin(p) * Mathf.Sin(d) + Mathf.Cos(p) * Mathf.Cos(d) * Mathf.Cos(h);
            return new Vector3(east, up, north).normalized;
        }

        /// <summary>When the sun rises, in hours (GL464).</summary>
        public const float SunriseHour = 4f;

        /// <summary>When the sun sets, in hours (GL464): 16 hours of daylight.</summary>
        public const float SunsetHour = 20f;

        /// <summary>
        /// The sun's hour angle at an hour of the day (0-24), in degrees: 0 at noon, -90 at sunrise, 90 at sunset. The day
        /// takes the half-turn over 16 hours and the night the other half over 8, so the sun runs faster in the dark.
        /// </summary>
        public static float SunHourAngle(float hour)
        {
            const float day = SunsetHour - SunriseHour;
            const float night = 24f - day;
            if (hour >= SunriseHour && hour <= SunsetHour)
            {
                return (hour - 12f) * 180f / day;
            }
            // After sunset 90 grows to 270 (= -90) at the next sunrise.
            float sinceSunset = hour > SunsetHour ? hour - SunsetHour : hour + 24f - SunsetHour;
            return 90f + sinceSunset * 180f / night;
        }

        /// <summary>Where the sun is at an hour of the day.</summary>
        public static Vector3 Sun(float hour, float latitude)
        {
            return Direction(SunHourAngle(hour), 0f, latitude);
        }

        /// <summary>
        /// How far the moon has drifted east of the sun, 0-1 of a turn: 0 new, 0.25 first quarter, 0.5 full, 0.75 last quarter.
        /// <paramref name="days"/> counts from a new moon.
        /// </summary>
        public static float MoonPhase(double days)
        {
            double turns = days / SynodicMonthDays;
            return (float)(turns - System.Math.Floor(turns));
        }

        /// <summary>How much of the moon's face is lit, 0 at new moon to 1 at full.</summary>
        public static float MoonIllumination(float phase)
        {
            return 0.5f * (1f - Mathf.Cos(phase * 2f * Mathf.PI));
        }

        /// <summary>
        /// The moon's declination, in degrees: it follows the ecliptic (the sun at the equinox puts it as far north as it
        /// gets at first quarter) and its own orbit's tilt on top.
        /// </summary>
        public static float MoonDeclination(double days)
        {
            float elongation = MoonPhase(days) * 2f * Mathf.PI;
            double nodeTurns = days / DraconicMonthDays;
            float node = (float)(nodeTurns - System.Math.Floor(nodeTurns)) * 2f * Mathf.PI;
            return Mathf.Asin(Mathf.Sin(AxialTilt * Mathf.Deg2Rad) * Mathf.Sin(elongation)) * Mathf.Rad2Deg
                   + MoonOrbitTilt * Mathf.Sin(node);
        }

        /// <summary>
        /// Where the moon is <paramref name="days"/> after a new moon, at an hour of the day: it trails the sun across the
        /// sky by its phase, a full moon opposite it.
        /// </summary>
        public static Vector3 Moon(double days, float hour, float latitude)
        {
            float hourAngle = SunHourAngle(hour) - MoonPhase(days) * 360f;
            return Direction(hourAngle, MoonDeclination(days), latitude);
        }

        /// <summary>
        /// How far the stars have turned, in degrees (the local sidereal time, GL253): the sun's hour angle plus its
        /// drift east through the stars, once a year. So the stars turn once a sidereal day, about 4 minutes (of 24 game
        /// hours) shorter than the sun's, and the same stars rise a little earlier each night.
        /// </summary>
        public static float SiderealAngle(double days, float hour)
        {
            double years = days / TropicalYearDays;
            return SunHourAngle(hour) + (float)((years - System.Math.Floor(years)) * 360d);
        }

        /// <summary>
        /// Where a star is, from its place on the sky (right ascension and declination, degrees), <paramref name="days"/>
        /// into the game at an hour of the day.
        /// </summary>
        public static Vector3 Star(float rightAscension, float declination, double days, float hour, float latitude)
        {
            return Direction(SiderealAngle(days, hour) - rightAscension, declination, latitude);
        }

        /// <summary>
        /// The moon illusion (GL254): how much bigger the moon looks at a height (the sine of its angle over the horizon)
        /// than high in the sky, as a factor: 1 + <paramref name="growth"/> on the horizon, easing back to 1 by the
        /// height <paramref name="band"/>.
        /// </summary>
        public static float MoonIllusion(float height, float growth, float band)
        {
            float low = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(height / Mathf.Max(band, 1e-3f)));
            return 1f + growth * low;
        }

        /// <summary>The year, equinox to equinox, in days: how long the sun takes to go once round the stars.</summary>
        public const double TropicalYearDays = 365.24219;

        /// <summary>The stars' day as a share of the sun's: once round in 23 h 56 min.</summary>
        public const double SiderealDayShare = TropicalYearDays / (TropicalYearDays + 1d);
    }
}
