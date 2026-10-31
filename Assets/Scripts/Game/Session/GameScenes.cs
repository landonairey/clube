using UnityEngine;
using UnityEngine.SceneManagement;

namespace Clube.Game
{
    /// <summary>
    /// The demo's scenes by name (GL200): the main menu, and the scene each game mode plays in. All of them must
    /// be in the build's scene list.
    /// </summary>
    public static class GameScenes
    {
        public const string MainMenu = "MainMenu";

        /// <summary>True when the main menu can be loaded (it's in the build's scene list).</summary>
        public static bool HasMainMenu => Application.CanStreamedLevelBeLoaded(MainMenu);

        /// <summary>The scene a mode plays in.</summary>
        public static string For(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.Voxel:
                    return "VoxelLab";
                case GameMode.Chunk:
                    return "ChunkLab";
                case GameMode.Procedural:
                    return "ProceduralWorldLab";
                default:
                    return "WorldLab";
            }
        }

        /// <summary>Starts the session's game in its mode's scene.</summary>
        public static void LoadGame()
        {
            SceneManager.LoadScene(For(GameSession.Mode));
        }

        /// <summary>Back to the main menu, ending the session; quits when the menu isn't in the build.</summary>
        public static void LoadMainMenu()
        {
            GameSession.End();
            if (HasMainMenu)
            {
                SceneManager.LoadScene(MainMenu);
            }
            else
            {
                Quit();
            }
        }

        /// <summary>Closes the exe, or stops Play mode in the Editor, where Application.Quit does nothing.</summary>
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
