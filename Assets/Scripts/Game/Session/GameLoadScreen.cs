using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Clube.Game
{
    /// <summary>
    /// A black screen with the loading line while a game scene loads (GL742, GL743), so the last frame of the menu or the
    /// tutorial doesn't sit frozen; the scene's <see cref="LoadCurtain"/> takes over once it is in.
    /// </summary>
    public class GameLoadScreen : MonoBehaviour
    {
        private AsyncOperation loading;

        /// <summary>Loads a scene behind the screen.</summary>
        public static void Load(string scene)
        {
            var screen = new GameObject("Game load screen").AddComponent<GameLoadScreen>();
            DontDestroyOnLoad(screen.gameObject);
            screen.StartCoroutine(screen.Run(scene));
        }

        private IEnumerator Run(string scene)
        {
            // A frame for the screen to be drawn before the load starts.
            yield return null;
            yield return null;
            loading = SceneManager.LoadSceneAsync(scene);
            yield return loading;
            Destroy(gameObject);
        }

        private void OnGUI()
        {
            GUI.depth = -300;
            Color was = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            // An async load stops at 0.9 until the scene is switched in.
            LoadingMessage.Draw(LoadingMessage.Ground(loading != null ? loading.progress / 0.9f * 0.1f : 0f));
            GUI.color = was;
        }
    }
}
