using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The graphics popup (GL209), opened from the pause popup: resolution presets by aspect and window or full screen
    /// (GL462, <see cref="DisplaySettings"/>), field of view on a slider (60-120 degrees,
    /// vertical), render distance in chunks (GL211), the frame rate limit (30, 60, VSync or unlimited) and the
    /// time of day frozen at an hour (GL227). Changes apply at once and are kept (<see cref="GraphicsSettings"/>);
    /// Back returns to the pause popup.
    /// </summary>
    public sealed class GraphicsSettingsPage
    {
        public const float Width = 560f;
        public const float Height = 700f;

        private static readonly FrameRateLimit[] Limits = { FrameRateLimit.Fps30, FrameRateLimit.Fps60, FrameRateLimit.VSync, FrameRateLimit.Unlimited };
        private static string[] limitLabels;
        private static GUIStyle toggleStyle;
        private static readonly string[] ModeLabels = { "Window", "Full screen" };
        private int shownAspect = -1;

        /// <summary>Draws the popup's contents in the current layout area; true when Back was pressed.</summary>
        public bool Draw(GUIStyle title, GUIStyle label, GUIStyle hint, GUIStyle button)
        {
            if (limitLabels == null)
            {
                limitLabels = new string[Limits.Length];
                for (int i = 0; i < Limits.Length; i++)
                {
                    limitLabels[i] = GraphicsSettings.Label(Limits[i]);
                }
            }

            GUILayout.Label("Video", title);
            GUILayout.Space(8f);

            DrawDisplay(label, hint, button);
            GUILayout.Space(10f);

            GUILayout.Label($"Field of view  {GraphicsSettings.FieldOfView:0}°", label);
            GraphicsSettings.FieldOfView = GUILayout.HorizontalSlider(GraphicsSettings.FieldOfView, GraphicsSettings.MinFieldOfView, GraphicsSettings.MaxFieldOfView);
            GUILayout.Space(10f);

            int distance = GraphicsSettings.RenderDistance;
            GUILayout.Label($"Render distance  {distance} chunks ({distance * 4} m)", label);
            GraphicsSettings.RenderDistance = Mathf.RoundToInt(GUILayout.HorizontalSlider(distance, GraphicsSettings.MinRenderDistance, GraphicsSettings.MaxRenderDistance));
            GUILayout.Space(10f);

            GUILayout.Label("Frame rate", label);
            int current = System.Array.IndexOf(Limits, GraphicsSettings.FrameRate);
            int picked = GUILayout.Toolbar(current, limitLabels, button, GUILayout.Height(32f));
            if (picked != current && picked >= 0)
            {
                GraphicsSettings.FrameRate = Limits[picked];
            }
            GUILayout.Label(Describe(GraphicsSettings.FrameRate), hint);
            GUILayout.Space(8f);

            // The skin's toggle, which draws the box; a label style would hide it.
            toggleStyle ??= new GUIStyle(GUI.skin.toggle) { fontSize = label.fontSize, normal = { textColor = label.normal.textColor }, onNormal = { textColor = label.normal.textColor } };
            GraphicsSettings.FreezeDay = GUILayout.Toggle(GraphicsSettings.FreezeDay, " Freeze the time of day", toggleStyle);
            bool wasEnabled = GUI.enabled;
            GUI.enabled = GraphicsSettings.FreezeDay;
            GUILayout.Label($"Frozen at  {GraphicsSettings.ClockTime(GraphicsSettings.FrozenHour)}", label);
            GraphicsSettings.FrozenHour = GUILayout.HorizontalSlider(GraphicsSettings.FrozenHour, 0f, 23.5f);
            GUI.enabled = wasEnabled;
            GUILayout.Label("Only the sky stops: drying and smelting go on.", hint);

            GUILayout.FlexibleSpace();
            return GUILayout.Button("Back", button, GUILayout.Height(36f));
        }

        // The resolution presets by aspect, and window or full screen (GL462). Dragging a window's edge changes its size too.
        private void DrawDisplay(GUIStyle label, GUIStyle hint, GUIStyle button)
        {
            DisplayMode mode = DisplaySettings.Mode;
            GUILayout.Label($"Resolution  {Screen.width} × {Screen.height}", label);
            int aspect = DisplaySettings.AspectOf(Screen.width, Screen.height);
            shownAspect = shownAspect < 0 ? Mathf.Max(0, aspect) : shownAspect;
            shownAspect = GUILayout.Toolbar(shownAspect, DisplaySettings.Aspects, button, GUILayout.Height(30f));
            DisplaySettings.Preset[] presets = DisplaySettings.Presets[shownAspect];
            GUILayout.BeginHorizontal();
            foreach (DisplaySettings.Preset preset in presets)
            {
                bool current = Screen.width == preset.Width && Screen.height == preset.Height;
                if (GUILayout.Toggle(current, preset.Label, button, GUILayout.Height(30f)) && !current)
                {
                    DisplaySettings.Apply(preset.Width, preset.Height, mode);
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);

            int pickedMode = GUILayout.Toolbar(mode == DisplayMode.FullScreen ? 1 : 0, ModeLabels, button, GUILayout.Height(30f));
            DisplayMode wanted = pickedMode == 1 ? DisplayMode.FullScreen : mode == DisplayMode.FullScreen ? DisplayMode.Window : mode;
            if (wanted == DisplayMode.Window || wanted == DisplayMode.BorderlessWindow)
            {
                toggleStyle ??= new GUIStyle(GUI.skin.toggle) { fontSize = label.fontSize, normal = { textColor = label.normal.textColor }, onNormal = { textColor = label.normal.textColor } };
                bool borderless = GUILayout.Toggle(wanted == DisplayMode.BorderlessWindow, " Borderless window", toggleStyle);
                wanted = borderless ? DisplayMode.BorderlessWindow : DisplayMode.Window;
            }
            if (wanted != mode)
            {
                DisplaySettings.Apply(Screen.width, Screen.height, wanted);
            }
            GUILayout.Label(mode == DisplayMode.FullScreen ? "Full screen draws at the picked size, scaled to the screen." : "Drag the window's edge to change its size.", hint);
        }

        private static string Describe(FrameRateLimit limit)
        {
            switch (limit)
            {
                case FrameRateLimit.Fps30:
                    return "At most 30 frames a second: least power and heat.";
                case FrameRateLimit.Fps60:
                    return "At most 60 frames a second.";
                case FrameRateLimit.VSync:
                    return "In step with the monitor's refresh: no tearing.";
                default:
                    return "As fast as the computer can draw (the main menu still stops at 60).";
            }
        }
    }
}
