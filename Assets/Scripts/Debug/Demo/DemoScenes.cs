using Clube.Game;
using UnityEngine.SceneManagement;

namespace Clube.Debug
{
    /// <summary>
    /// Moves between a lab scene and the demo's main menu (K34, GL200): the labs opened on their own
    /// (VoxelLab, ChunkLab) use it for their Exit button.
    /// </summary>
    public static class DemoScenes
    {
        public const string VoxelLab = "VoxelLab";
        public const string ChunkLab = "ChunkLab";

        /// <summary>
        /// True when the main menu can be loaded, i.e. it's in the build's scene list.
        /// A lab scene hides its Exit button otherwise.
        /// </summary>
        public static bool HasMenu => GameScenes.HasMainMenu;

        public static void LoadMenu()
        {
            GameScenes.LoadMainMenu();
        }

        /// <summary>Loads a lab scene by name, e.g. <see cref="VoxelLab"/> or <see cref="ChunkLab"/>.</summary>
        public static void LoadLab(string scene)
        {
            SceneManager.LoadScene(scene);
        }

        /// <summary>Closes the exe, or stops Play mode in the Editor.</summary>
        public static void Quit()
        {
            GameScenes.Quit();
        }
    }
}
