using UnityEngine;
using UnityEngine.SceneManagement;

namespace Clube.Debug
{
    /// <summary>
    /// Moves between the demo exe's scenes (K34, V22): the <c>DemoMenu</c> welcome screen
    /// and the two labs it launches, <c>VoxelLab</c> (a single voxel) and <c>ChunkLab</c>
    /// (a single chunk). All of them must be in the build's scene list.
    /// </summary>
    public static class DemoScenes
    {
        public const string Menu = "DemoMenu";
        public const string VoxelLab = "VoxelLab";
        public const string ChunkLab = "ChunkLab";

        /// <summary>
        /// True when the menu scene can be loaded, i.e. it's in the build's scene list.
        /// A lab scene hides its Exit button otherwise.
        /// </summary>
        public static bool HasMenu => Application.CanStreamedLevelBeLoaded(Menu);

        public static void LoadMenu()
        {
            SceneManager.LoadScene(Menu);
        }

        /// <summary>Loads a lab scene by name, e.g. <see cref="VoxelLab"/> or <see cref="ChunkLab"/>.</summary>
        public static void LoadLab(string scene)
        {
            SceneManager.LoadScene(scene);
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
