using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The game's one typeface (GL732): Uncial Antiqua (SIL OFL, <c>Resources/Fonts</c>), used by every screen, HUD and
    /// signboard. Falls back to the built-in font if the asset is missing.
    /// </summary>
    public static class GameFont
    {
        private static Font font;

        /// <summary>The typeface; loaded once.</summary>
        public static Font Get()
        {
            if (font == null)
            {
                font = Resources.Load<Font>("Fonts/UncialAntiqua");
                if (font == null)
                {
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
            }
            return font;
        }
    }
}
