using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Where the sun and moon are in the sky (GL248), worked out the way they move round the Earth, scaled to the
    /// game's day: the sun crosses from east to west and goes under the horizon at night; the moon goes round once a
    /// synodic month (<see cref="SynodicMonthDays"/>), so it rises about 50 minutes (of game time) later each day, and its
    /// phase is how far it has drifted from the sun. Its path tilts with the ecliptic and its own 5° orbit, so it rides
    /// high some nights and low on others.
    /// </summary>
    /// <remarks>
    /// Plain math. Directions are in the sky's frame: x east, y up, z north. The sun keeps to the equinox (no seasons), so
    /// it rises at 06:00 and sets at 18:00 every day.
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

        /// <summary>The sun's hour angle at an hour of the day (0-24), in degrees: 0 at noon.</summary>
        public static float SunHourAngle(float hour)
        {
            return (hour - 12f) * 15f;
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
    }
}
