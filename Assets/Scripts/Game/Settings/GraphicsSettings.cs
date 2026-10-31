using System;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>How often the game draws a frame (GL209).</summary>
    public enum FrameRateLimit
    {
        Fps30,
        Fps60,
        VSync,
        Unlimited,
    }

    /// <summary>
    /// The player's graphics settings (GL209, first pass of P10): field of view, the frame rate limit, render
    /// distance (GL211) and a frozen time of day (GL227). Kept on the player's machine with
    /// <see cref="PlayerPrefs"/>, so they last between runs of the exe, unlike the <see cref="PlayerSettings"/>
    /// asset, which a build can't save. <see cref="GraphicsSettingsApplier"/> puts them on the game; the frame
    /// rate is set here (<see cref="ApplyFrameRate"/>).
    /// </summary>
    /// <remarks>
    /// The main menu never runs unlimited: with nothing to draw it would redraw thousands of times a second and
    /// keep several cores busy, so there an unlimited setting draws at 60.
    /// </remarks>
    public static class GraphicsSettings
    {
        public const float MinFieldOfView = 60f;
        public const float MaxFieldOfView = 120f;
        public const float DefaultFieldOfView = 80f;
        public const FrameRateLimit DefaultFrameRate = FrameRateLimit.VSync;
        public const int MinRenderDistance = 8;
        public const int MaxRenderDistance = 64;
        public const int DefaultRenderDistance = 32;
        public const float DefaultFrozenHour = 12f;

        private const string FieldOfViewKey = "graphics.fieldOfView";
        private const string FrameRateKey = "graphics.frameRate";
        private const string RenderDistanceKey = "graphics.renderDistance";
        private const string FreezeDayKey = "graphics.freezeDay";
        private const string FrozenHourKey = "graphics.frozenHour";
        private const int MenuFrameRate = 60;

        private static bool loaded;
        private static float fieldOfView = DefaultFieldOfView;
        private static FrameRateLimit frameRate = DefaultFrameRate;
        private static int renderDistance = DefaultRenderDistance;
        private static bool freezeDay;
        private static float frozenHour = DefaultFrozenHour;

        /// <summary>Raised when a setting changes.</summary>
        public static event Action Changed;

        /// <summary>The camera's vertical field of view, in degrees.</summary>
        public static float FieldOfView
        {
            get
            {
                Load();
                return fieldOfView;
            }
            set
            {
                Load();
                float clamped = Mathf.Clamp(Mathf.Round(value), MinFieldOfView, MaxFieldOfView);
                if (Mathf.Approximately(clamped, fieldOfView))
                {
                    return;
                }
                fieldOfView = clamped;
                PlayerPrefs.SetFloat(FieldOfViewKey, fieldOfView);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static FrameRateLimit FrameRate
        {
            get
            {
                Load();
                return frameRate;
            }
            set
            {
                Load();
                if (value == frameRate)
                {
                    return;
                }
                frameRate = value;
                PlayerPrefs.SetInt(FrameRateKey, (int)frameRate);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>How many chunks around the player stay loaded, as a radius in chunks (GL211).</summary>
        public static int RenderDistance
        {
            get
            {
                Load();
                return renderDistance;
            }
            set
            {
                Load();
                int clamped = Mathf.Clamp(value, MinRenderDistance, MaxRenderDistance);
                if (clamped == renderDistance)
                {
                    return;
                }
                renderDistance = clamped;
                PlayerPrefs.SetInt(RenderDistanceKey, renderDistance);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>Whether the sun stays at <see cref="FrozenHour"/> instead of following the day (GL227).</summary>
        public static bool FreezeDay
        {
            get
            {
                Load();
                return freezeDay;
            }
            set
            {
                Load();
                if (value == freezeDay)
                {
                    return;
                }
                freezeDay = value;
                PlayerPrefs.SetInt(FreezeDayKey, freezeDay ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>The hour of the day (0-23.5, in half hours) the sun stays at while <see cref="FreezeDay"/> is on.</summary>
        public static float FrozenHour
        {
            get
            {
                Load();
                return frozenHour;
            }
            set
            {
                Load();
                float clamped = Mathf.Clamp(Mathf.Round(value * 2f) * 0.5f, 0f, 23.5f);
                if (Mathf.Approximately(clamped, frozenHour))
                {
                    return;
                }
                frozenHour = clamped;
                PlayerPrefs.SetFloat(FrozenHourKey, frozenHour);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>An hour of the day as a clock time, e.g. "06:30".</summary>
        public static string ClockTime(float hour)
        {
            hour = Mathf.Repeat(hour, 24f);
            int whole = Mathf.FloorToInt(hour);
            int minutes = Mathf.Min(59, Mathf.FloorToInt((hour - whole) * 60f));
            return $"{whole:00}:{minutes:00}";
        }

        /// <summary>A short label for a limit, e.g. "60 FPS".</summary>
        public static string Label(FrameRateLimit limit)
        {
            switch (limit)
            {
                case FrameRateLimit.Fps30:
                    return "30 FPS";
                case FrameRateLimit.Fps60:
                    return "60 FPS";
                case FrameRateLimit.VSync:
                    return "VSync";
                default:
                    return "Unlimited";
            }
        }

        /// <summary>
        /// Sets the frame rate limit for the game, or for the main menu (<paramref name="inMenu"/>), which never
        /// runs unlimited.
        /// </summary>
        public static void ApplyFrameRate(bool inMenu = false)
        {
            FrameRateLimit limit = FrameRate;
            if (inMenu && limit == FrameRateLimit.Unlimited)
            {
                limit = FrameRateLimit.Fps60;
            }
            QualitySettings.vSyncCount = limit == FrameRateLimit.VSync ? 1 : 0;
            switch (limit)
            {
                case FrameRateLimit.Fps30:
                    Application.targetFrameRate = 30;
                    break;
                case FrameRateLimit.Fps60:
                    Application.targetFrameRate = MenuFrameRate;
                    break;
                default:
                    // VSync paces itself; unlimited draws as fast as it can.
                    Application.targetFrameRate = -1;
                    break;
            }
        }

        private static void Load()
        {
            if (loaded)
            {
                return;
            }
            loaded = true;
            fieldOfView = Mathf.Clamp(PlayerPrefs.GetFloat(FieldOfViewKey, DefaultFieldOfView), MinFieldOfView, MaxFieldOfView);
            int saved = PlayerPrefs.GetInt(FrameRateKey, (int)DefaultFrameRate);
            frameRate = Enum.IsDefined(typeof(FrameRateLimit), saved) ? (FrameRateLimit)saved : DefaultFrameRate;
            renderDistance = Mathf.Clamp(PlayerPrefs.GetInt(RenderDistanceKey, DefaultRenderDistance), MinRenderDistance, MaxRenderDistance);
            freezeDay = PlayerPrefs.GetInt(FreezeDayKey, 0) != 0;
            frozenHour = Mathf.Clamp(PlayerPrefs.GetFloat(FrozenHourKey, DefaultFrozenHour), 0f, 23.5f);
        }
    }
}
