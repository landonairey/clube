using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The line of text at the bottom of the screen while the game loads (GL742, GL743): what is being loaded and how far it
    /// has got. <see cref="GameLoadScreen"/> shows it while a scene loads, <see cref="LoadCurtain"/> while the ground does.
    /// </summary>
    public static class LoadingMessage
    {
        public const string Surveying = "Surveying the map…";

        private static GUIStyle style;

        /// <summary>Where the game is going: the quarry, the family's parcel, or the land of a loaded game.</summary>
        public static string Destination => GameSession.Tutorial ? "Loading the quarry"
            : GameSession.ArrivesByCart || ArrivalCutscene.IsPlaying ? "Travelling to your family's parcel"
            : "Loading the land";

        /// <summary>The line while the map's rivers and lakes are worked out (<paramref name="progress"/> 0 to 1), before its scene starts (GL762).</summary>
        public static string Charting(float progress)
        {
            return $"Charting the rivers… {Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f)}%";
        }

        /// <summary>The line while the map is surveyed and planned (<paramref name="progress"/> 0 to 1), so it is seen to move (GL762).</summary>
        public static string Survey(float progress)
        {
            return $"{Surveying} {Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f)}%";
        }

        /// <summary>The line for where the load has got in a scene: the map's survey until it is ready, then the ground (<paramref name="groundProgress"/> 0 to 1).</summary>
        public static string InScene(float groundProgress)
        {
            MapSession session = MapSession.Current;
            return session != null && !session.IsReady ? Survey(session.BuildProgress) : Ground(groundProgress);
        }

        /// <summary>The line while the ground round the player loads (<paramref name="progress"/> 0 to 1).</summary>
        public static string Ground(float progress)
        {
            return $"{Destination}… {Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f)}%";
        }

        /// <summary>Draws the line at the bottom of the screen, from <c>OnGUI</c>.</summary>
        public static void Draw(string text)
        {
            style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = GameUi.Font(18), wordWrap = false };
            float height = style.lineHeight * 2f;
            GUI.Label(new Rect(0f, Screen.height - height * 1.75f, Screen.width, height), text, style);
        }
    }
}
