using UnityEngine;
using UnityEngine.SceneManagement;

namespace Clube.Debug
{
    /// <summary>
    /// Moves between the demo exe's scenes (K34): the <c>DemoMenu</c> welcome screen and
    /// the <c>ChunkLab</c> scene it launches. Both must be in the build's scene list.
    /// </summary>
    public static class DemoScenes
    {
        public const string Menu = "DemoMenu";
        public const string Lab = "ChunkLab";

        /// <summary>
        /// True when the menu scene can be loaded, i.e. it's in the build's scene list.
        /// The lab scene hides its Exit button otherwise.
        /// </summary>
        public static bool HasMenu => Application.CanStreamedLevelBeLoaded(Menu);

        public static void LoadMenu()
        {
            SceneManager.LoadScene(Menu);
        }

        public static void LoadLab()
        {
            SceneManager.LoadScene(Lab);
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
