using Clube.Core;

namespace Clube.Game
{
    /// <summary>The ways to play the demo (GL203); only <see cref="World"/> is playable so far.</summary>
    public enum GameMode
    {
        Voxel,
        Chunk,
        World,
        Procedural,
    }

    /// <summary>
    /// The game being played, carried from the main menu into the game scene (GL202, GL203): its mode, its
    /// world seed, and the save it was loaded from, if any. Static, because it outlives the menu scene. Inactive
    /// when a scene is played straight from the Editor, which then plays as it always has.
    /// </summary>
    public static class GameSession
    {
        /// <summary>True between the menu starting a game and the game returning to the menu.</summary>
        public static bool IsActive { get; private set; }

        public static GameMode Mode { get; private set; }

        public static int Seed { get; private set; }

        /// <summary>The save this game was loaded from, until the game scene has put it back; null for a new game.</summary>
        public static GameSave Loaded { get; private set; }

        /// <summary>A new game in a mode, on a seed.</summary>
        public static void StartNew(GameMode mode, int seed)
        {
            IsActive = true;
            Mode = mode;
            Seed = seed;
            Loaded = null;
        }

        /// <summary>Carries on a saved game: its mode and seed, and everything else once the scene starts.</summary>
        public static void StartLoaded(GameSave save)
        {
            IsActive = true;
            Mode = System.Enum.TryParse(save.Header.Mode, out GameMode mode) ? mode : GameMode.World;
            Seed = save.Header.Seed;
            Loaded = save;
        }

        /// <summary>The scene has put the save back: drop it, so it isn't held in memory for the whole game.</summary>
        public static void ForgetLoaded()
        {
            Loaded = null;
        }

        /// <summary>Back at the main menu.</summary>
        public static void End()
        {
            IsActive = false;
            Loaded = null;
        }
    }
}
