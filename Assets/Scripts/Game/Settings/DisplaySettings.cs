using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>How the game's window is shown (GL462).</summary>
    public enum DisplayMode
    {
        /// <summary>A window with a border and title bar; dragging its edge changes the resolution.</summary>
        Window,

        /// <summary>A window of the picked size with no border or title bar (Windows builds; a plain window elsewhere).</summary>
        BorderlessWindow,

        /// <summary>The whole screen.</summary>
        FullScreen,
    }

    /// <summary>
    /// Resolution presets and the window mode (GL462): 16:9 (1080p, 1440p, 2160p), 21:9 (2560x1080, 3440x1440, 5120x2160) and
    /// 32:9 (3840x1080, 5120x1440, 7680x2160), shown as a window (bordered or borderless) or full screen. Unity keeps the
    /// size and mode between runs itself; the borderless flag is kept in <see cref="PlayerPrefs"/>.
    /// </summary>
    public static class DisplaySettings
    {
        /// <summary>One resolution preset.</summary>
        public readonly struct Preset
        {
            public readonly int Width;
            public readonly int Height;
            public readonly string Label;

            public Preset(int width, int height, string label)
            {
                Width = width;
                Height = height;
                Label = label;
            }
        }

        /// <summary>The aspect groups, in order, with their presets.</summary>
        public static readonly string[] Aspects = { "16:9", "21:9", "32:9" };

        public static readonly Preset[][] Presets =
        {
            new[] { new Preset(1920, 1080, "1080p"), new Preset(2560, 1440, "1440p"), new Preset(3840, 2160, "2160p") },
            new[] { new Preset(2560, 1080, "2560×1080"), new Preset(3440, 1440, "3440×1440"), new Preset(5120, 2160, "5120×2160") },
            new[] { new Preset(3840, 1080, "3840×1080"), new Preset(5120, 1440, "5120×1440"), new Preset(7680, 2160, "7680×2160") },
        };

        private const string BorderlessKey = "display.borderless";

        /// <summary>The mode the window is in now.</summary>
        public static DisplayMode Mode
        {
            get
            {
                if (Screen.fullScreenMode != FullScreenMode.Windowed)
                {
                    return DisplayMode.FullScreen;
                }
                return PlayerPrefs.GetInt(BorderlessKey, 0) != 0 ? DisplayMode.BorderlessWindow : DisplayMode.Window;
            }
        }

        /// <summary>The aspect group a size belongs to, by its width over height; -1 for none of them.</summary>
        public static int AspectOf(int width, int height)
        {
            float ratio = height > 0 ? (float)width / height : 0f;
            for (int i = 0; i < Aspects.Length; i++)
            {
                Preset first = Presets[i][0];
                if (Mathf.Abs(ratio - (float)first.Width / first.Height) < 0.06f)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Shows the game at a size and in a mode.</summary>
        public static void Apply(int width, int height, DisplayMode mode)
        {
            PlayerPrefs.SetInt(BorderlessKey, mode == DisplayMode.BorderlessWindow ? 1 : 0);
            PlayerPrefs.Save();
            FullScreenMode screenMode = mode == DisplayMode.FullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Screen.SetResolution(width, height, screenMode);
            // The border changes once the window has its new size, a frame or two later.
            Runner.Instance.StartCoroutine(SetBorderLater(mode == DisplayMode.BorderlessWindow, width, height));
        }

        /// <summary>At startup: a borderless window comes back without its border (Unity restores the size and mode).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RestoreBorder()
        {
            if (Mode == DisplayMode.BorderlessWindow)
            {
                Runner.Instance.StartCoroutine(SetBorderLater(true, Screen.width, Screen.height));
            }
        }

        private static IEnumerator SetBorderLater(bool borderless, int width, int height)
        {
            yield return null;
            yield return null;
            SetBorder(borderless, width, height);
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private const int StyleIndex = -16;
        private const int Caption = 0x00C00000;
        private const int ThickFrame = 0x00040000;
        private const int SystemMenu = 0x00080000;
        private const int MinimizeBox = 0x00020000;
        private const int MaximizeBox = 0x00010000;
        private const uint FrameChanged = 0x0020;
        private const uint NoZOrder = 0x0004;
        private const uint ShowWindow = 0x0040;

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr window, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr window, int index, int value);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);

        // Takes the border and title bar off the window, or puts them back, keeping it centred on the screen.
        private static void SetBorder(bool borderless, int width, int height)
        {
            if (Screen.fullScreenMode != FullScreenMode.Windowed)
            {
                return;
            }
            IntPtr window = GetActiveWindow();
            if (window == IntPtr.Zero)
            {
                return;
            }
            int frame = Caption | ThickFrame | SystemMenu | MinimizeBox | MaximizeBox;
            int style = GetWindowLong(window, StyleIndex);
            style = borderless ? style & ~frame : style | frame;
            SetWindowLong(window, StyleIndex, style);
            Resolution desktop = Screen.currentResolution;
            int x = Mathf.Max(0, (desktop.width - width) / 2);
            int y = Mathf.Max(0, (desktop.height - height) / 2);
            SetWindowPos(window, IntPtr.Zero, x, y, width, height, FrameChanged | NoZOrder | ShowWindow);
        }
#else
        private static void SetBorder(bool borderless, int width, int height)
        {
        }
#endif

        // Runs the border change a frame later; lives for the whole run.
        private sealed class Runner : MonoBehaviour
        {
            private static Runner instance;

            public static Runner Instance
            {
                get
                {
                    if (instance == null)
                    {
                        var host = new GameObject("Display settings") { hideFlags = HideFlags.HideAndDontSave };
                        DontDestroyOnLoad(host);
                        instance = host.AddComponent<Runner>();
                    }
                    return instance;
                }
            }
        }
    }
}
