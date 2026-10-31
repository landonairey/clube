using UnityEngine;

namespace Clube.Game
{
    /// <summary>The main menu's text and button styles (GL201-GL203), made once from the IMGUI skin.</summary>
    public sealed class MenuStyles
    {
        public static readonly Color Ink = new Color(0.95f, 0.93f, 0.88f);
        public static readonly Color Dim = new Color(0.72f, 0.72f, 0.7f);

        public MenuStyles()
        {
            Title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 64,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Ink },
            };
            Subtitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = Dim },
            };
            Heading = new GUIStyle(Title) { fontSize = 30 };
            Label = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true, normal = { textColor = Ink } };
            Caption = new GUIStyle(Label) { fontSize = 12, alignment = TextAnchor.MiddleCenter, normal = { textColor = Dim } };
            Corner = new GUIStyle(Caption) { alignment = TextAnchor.LowerRight };
            Button = new GUIStyle(GUI.skin.button) { fontSize = 19 };
            ModeButton = new GUIStyle(GUI.skin.button) { fontSize = 16, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(14, 10, 6, 6), richText = true };
            Field = new GUIStyle(GUI.skin.textField) { fontSize = 18, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(8, 8, 4, 4) };
        }

        public GUIStyle Title { get; }
        public GUIStyle Subtitle { get; }
        public GUIStyle Heading { get; }
        public GUIStyle Label { get; }
        public GUIStyle Caption { get; }
        public GUIStyle Corner { get; }
        public GUIStyle Button { get; }
        public GUIStyle ModeButton { get; }
        public GUIStyle Field { get; }
    }
}
