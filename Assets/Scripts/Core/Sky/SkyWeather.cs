using UnityEngine;

namespace Clube.Core
{
    /// <summary>The colour of one sunrise or sunset (GL262).</summary>
    public readonly struct SunsetLook
    {
        /// <summary>The colour the low sun lights the land, the sky and the clouds with (sRGB).</summary>
        public readonly Color Glow;

        /// <summary>How strong it is, 0 (a plain sunset, the usual warm light) to 1 (a vivid one).</summary>
        public readonly float Vividness;

        public SunsetLook(Color glow, float vividness)
        {
            Glow = glow;
            Vividness = vividness;
        }
    }

    /// <summary>
    /// The sky's weather from day to day (GL262, GL263), from the world's seed and the game's day, so the same world
    /// has the same days. Each sunrise and sunset has its own colour, from golden through orange and deep red to pink
    /// and purple, and its own strength: some plain, most a little coloured, now and then a vivid one; partly cloudy
    /// skies make the most of it. Each day has its own cloud cover: clear, partly cloudy or heavy, easing from one day's
    /// to the next.
    /// </summary>
    /// <remarks>Plain math.</remarks>
    public static class SkyWeather
    {
        private const int SunsetSalt = 0x5355;
        private const int CoverSalt = 0x434c;

        /// <summary>The sunset colours a sky picks from, golden to purple (sRGB).</summary>
        public static readonly Color[] Palette =
        {
            new Color(1f, 0.78f, 0.36f),   // golden
            new Color(1f, 0.55f, 0.22f),   // orange
            new Color(0.92f, 0.28f, 0.14f), // deep red
            new Color(1f, 0.46f, 0.58f),   // pink
            new Color(0.7f, 0.38f, 0.8f),  // purple
        };

        /// <summary>
        /// The look of the sunrise (<paramref name="evening"/> false) or sunset of game day <paramref name="day"/>, with
        /// <paramref name="cover"/> of the sky clouded (0-1).
        /// </summary>
        public static SunsetLook Sunset(int seed, int day, bool evening, float cover)
        {
            int index = day * 2 + (evening ? 1 : 0);
            float strength = Hash(seed, index, SunsetSalt);
            // A fifth of them plain, most mild, about one in six vivid.
            float vividness = strength < 0.2f ? 0f
                : strength < 0.83f ? Mathf.Lerp(0.2f, 0.6f, (strength - 0.2f) / 0.63f)
                : Mathf.Lerp(0.8f, 1f, (strength - 0.83f) / 0.17f);
            // Broken cloud catches the colour best; a clear sky or a heavy one tones it down.
            float broken = 1f - Mathf.Abs(Mathf.Clamp01(cover) - 0.45f) / 0.45f;
            vividness = Mathf.Clamp01(vividness * Mathf.Lerp(0.75f, 1.25f, Mathf.Clamp01(broken)));
            // A colour between two neighbours of the palette.
            float hue = Hash(seed, index, SunsetSalt + 1) * (Palette.Length - 1);
            int low = Mathf.Min(Palette.Length - 2, (int)hue);
            Color glow = Color.Lerp(Palette[low], Palette[low + 1], hue - low);
            return new SunsetLook(glow, vividness);
        }

        /// <summary>
        /// How much of the sky is clouded, 0-1, <paramref name="days"/> into the game: each day is clear (about a quarter
        /// of them, under 0.15), partly cloudy, or heavy (about three in ten, 0.55 to 0.78). A day's cover holds through
        /// its middle and eases into the next day's over the hours either side of the turn of the day.
        /// </summary>
        public static float Cover(int seed, double days)
        {
            // Each day's cover sits at its middle; between two middles it holds, then eases across.
            double fromMiddle = days - 0.5d;
            int day = (int)System.Math.Floor(fromMiddle);
            float t = (float)(fromMiddle - day);
            float ease = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.7f, t));
            return Mathf.Lerp(DayCover(seed, day), DayCover(seed, day + 1), ease);
        }

        /// <summary>The cover a day settles at.</summary>
        public static float DayCover(int seed, int day)
        {
            float kind = Hash(seed, day, CoverSalt);
            float within = Hash(seed, day, CoverSalt + 1);
            if (kind < 0.25f)
            {
                return Mathf.Lerp(0.04f, 0.15f, within);
            }
            return kind < 0.7f ? Mathf.Lerp(0.28f, 0.5f, within) : Mathf.Lerp(0.55f, 0.78f, within);
        }

        // A number 0-1 from the seed, an index and a salt.
        private static float Hash(int seed, int index, int salt)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u ^ (uint)index * 0x85EBCA77u ^ (uint)salt * 0xC2B2AE3Du;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }
    }
}
