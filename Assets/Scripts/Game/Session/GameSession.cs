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

        /// <summary>The world's name from the new-game page (GL206), "Hello World" by default.</summary>
        public static string WorldName { get; private set; } = SaveNames.DefaultWorldName;

        /// <summary>True for a new game that opens with the Mining Corp tutorial (GL479), until the tutorial ends.</summary>
        public static bool Tutorial { get; private set; }

        /// <summary>The character's name, typed for the crew lead in the tutorial (GL514); empty until then.</summary>
        public static string CharacterName { get; set; } = "";

        /// <summary>Coins earned in the tutorial, waiting for the player's wallet in the game scene.</summary>
        public static float CarriedCoins { get; set; }

        /// <summary>The save this game was loaded from, until the game scene has put it back; null for a new game.</summary>
        public static GameSave Loaded { get; private set; }

        /// <summary>A new game in a mode, on a seed, with a world name (the default when empty), with or without the tutorial.</summary>
        public static void StartNew(GameMode mode, int seed, string worldName = null, bool tutorial = false)
        {
            IsActive = true;
            Mode = mode;
            Seed = seed;
            WorldName = NameOrDefault(worldName);
            Tutorial = tutorial;
            CharacterName = "";
            CarriedCoins = 0f;
            Loaded = null;
        }

        /// <summary>The tutorial is over: the game carries on as a normal new game.</summary>
        public static void FinishTutorial()
        {
            Tutorial = false;
        }

        /// <summary>Carries on a saved game: its mode, seed and world name, and everything else once the scene starts.</summary>
        public static void StartLoaded(GameSave save)
        {
            IsActive = true;
            Mode = System.Enum.TryParse(save.Header.Mode, out GameMode mode) ? mode : GameMode.World;
            Seed = save.Header.Seed;
            WorldName = NameOrDefault(save.Header.WorldName);
            Tutorial = false;
            Loaded = save;
        }

        private static string NameOrDefault(string worldName)
        {
            string name = SaveNames.Clean(worldName);
            return name.Length > 0 ? name : SaveNames.DefaultWorldName;
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
            Tutorial = false;
            CarriedCoins = 0f;
            Loaded = null;
        }

        /// <summary>The coins carried out of the tutorial, now cleared.</summary>
        public static float TakeCarriedCoins()
        {
            float coins = CarriedCoins;
            CarriedCoins = 0f;
            return coins;
        }
    }
}
