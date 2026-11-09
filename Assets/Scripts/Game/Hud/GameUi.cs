using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The game's shared HUD and menu sizes (GL427): how much bigger text is drawn than the sizes the screens were laid out
    /// for, and the size of an inventory slot. Every player screen and HUD calls <see cref="Apply"/> at the start of its
    /// <c>OnGUI</c>, which sets the default skin's text size once, so text drawn in the plain skin grows with the rest.
    /// </summary>
    public static class GameUi
    {
        /// <summary>Text is drawn this much bigger than the sizes the screens name.</summary>
        public const float FontScale = 1.4f;

        /// <summary>The side of one inventory slot, in pixels, in every inventory, station, chest and merchant grid.</summary>
        public const float SlotSize = 112f;

        /// <summary>The space between slots.</summary>
        public const float SlotGap = 6f;

        // The default skin's text is 13 px (font size 0).
        private const int DefaultFontSize = 13;

        private static GUISkin applied;

        /// <summary>A text size named for the old layout, scaled.</summary>
        public static int Font(int size)
        {
            return Mathf.RoundToInt(size * FontScale);
        }

        /// <summary>
        /// A label with a dark outline (GL435, GL436), so white text reads over light icons, sand and the build preview.
        /// </summary>
        public static void OutlinedLabel(Rect rect, string text, GUIStyle style)
        {
            Color color = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx != 0 || dy != 0)
                    {
                        GUI.Label(new Rect(rect.x + dx * 1.5f, rect.y + dy * 1.5f, rect.width, rect.height), text, style);
                    }
                }
            }
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
        }

        /// <summary>Sets the current skin's default text size, once per skin. Call at the start of OnGUI.</summary>
        public static void Apply()
        {
            GUISkin skin = GUI.skin;
            if (skin == applied)
            {
                return;
            }
            applied = skin;
            int size = Font(DefaultFontSize);
            foreach (GUIStyle style in new[] { skin.label, skin.button, skin.box, skin.textField, skin.textArea, skin.toggle, skin.window })
            {
                if (style.fontSize == 0)
                {
                    style.fontSize = size;
                }
            }
        }
    }
}
