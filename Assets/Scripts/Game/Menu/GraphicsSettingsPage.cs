using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The graphics popup (GL209), opened from the pause popup: field of view on a slider (60-120 degrees,
    /// vertical), render distance in chunks (GL211), the frame rate limit (30, 60, VSync or unlimited) and the
    /// time of day frozen at an hour (GL227). Changes apply at once and are kept (<see cref="GraphicsSettings"/>);
    /// Back returns to the pause popup.
    /// </summary>
    public sealed class GraphicsSettingsPage
    {
        public const float Width = 420f;
        public const float Height = 440f;

        private static readonly FrameRateLimit[] Limits = { FrameRateLimit.Fps30, FrameRateLimit.Fps60, FrameRateLimit.VSync, FrameRateLimit.Unlimited };
        private static string[] limitLabels;

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

            GUILayout.Label("Graphics", title);
            GUILayout.Space(8f);

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

            GraphicsSettings.FreezeDay = GUILayout.Toggle(GraphicsSettings.FreezeDay, "  Freeze the time of day", label);
            bool wasEnabled = GUI.enabled;
            GUI.enabled = GraphicsSettings.FreezeDay;
            GUILayout.Label($"Frozen at  {GraphicsSettings.ClockTime(GraphicsSettings.FrozenHour)}", label);
            GraphicsSettings.FrozenHour = GUILayout.HorizontalSlider(GraphicsSettings.FrozenHour, 0f, 23.5f);
            GUI.enabled = wasEnabled;
            GUILayout.Label("Only the sky stops: drying and smelting go on.", hint);

            GUILayout.FlexibleSpace();
            return GUILayout.Button("Back", button, GUILayout.Height(36f));
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
