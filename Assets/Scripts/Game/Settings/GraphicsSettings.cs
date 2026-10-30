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
    /// The player's graphics settings (GL209, first pass of P10): field of view and the frame rate limit. Kept
    /// on the player's machine with <see cref="PlayerPrefs"/>, so they last between runs of the exe, unlike the
    /// <see cref="PlayerSettings"/> asset, which a build can't save. <see cref="FieldOfViewSetting"/> puts the
    /// field of view on the camera; the frame rate is set here (<see cref="ApplyFrameRate"/>).
    /// </summary>
    /// <remarks>
    /// The main menu never runs unlimited: with nothing to draw it would redraw thousands of times a second and
    /// keep several cores busy, so there an unlimited setting draws at 60.
    /// </remarks>
    public static class GraphicsSettings
    {
        public const float MinFieldOfView = 60f;
        public const float MaxFieldOfView = 120f;
        public const float DefaultFieldOfView = 60f;
        public const FrameRateLimit DefaultFrameRate = FrameRateLimit.VSync;

        private const string FieldOfViewKey = "graphics.fieldOfView";
        private const string FrameRateKey = "graphics.frameRate";
        private const int MenuFrameRate = 60;

        private static bool loaded;
        private static float fieldOfView = DefaultFieldOfView;
        private static FrameRateLimit frameRate = DefaultFrameRate;

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
        }
    }
}
