using System.Collections;
using Clube.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Clube.Game
{
    /// <summary>
    /// The loading curtain (GL780: "Loading NN%" on <see cref="LoadCurtainView"/>) while a game scene loads (GL742, GL743), so the last frame of the menu or the
    /// tutorial doesn't sit frozen; the scene's <see cref="LoadCurtain"/> takes over once it is in. GL762: the bounded map's
    /// water, which would otherwise stall the scene's first frame for seconds, is worked out here first, a slice a frame.
    /// </summary>
    public class GameLoadScreen : MonoBehaviour
    {
        // Milliseconds of each frame for working out the water: nothing else is going on.
        private const float PrepareMilliseconds = 50f;

        private AsyncOperation loading;
        private SlicedWork preparing;

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
            if (scene == GameScenes.For(GameMode.Demo))
            {
                yield return PrepareMap();
            }
            loading = SceneManager.LoadSceneAsync(scene);
            yield return loading;
            Destroy(gameObject);
        }

        // The water of the map this game plays on, under the same settings the scene's world will have.
        private IEnumerator PrepareMap()
        {
            var settings = Resources.Load<MapLoadSettings>(MapLoadSettings.ResourcePath);
            if (settings == null || settings.World == null)
            {
                yield break;
            }
            WorldConfig copy = Instantiate(settings.World);
            if (GameSession.IsActive)
            {
                copy.Terrain.Seed = GameSession.Seed;
            }
            preparing = new SlicedWork(BoundedMapGenerator.PrepareSteps(copy.Terrain));
            while (!preparing.Run(PrepareMilliseconds))
            {
                yield return null;
            }
            UnityEngine.Debug.Log($"Load screen: longest step of the water {preparing.LongestStepMilliseconds:0} ms");
            preparing = null;
            Destroy(copy);
        }

        private void OnDestroy()
        {
            preparing?.Dispose();
        }

        // Draws on the load curtain every frame; its scene's LoadCurtain carries on from here (GL780).
        private void Update()
        {
            if (preparing != null)
            {
                LoadCurtainView.Show(LoadProgress.Charting(preparing.Progress), LoadingMessage.Charting);
            }
            else
            {
                // An async load stops at 0.9 until the scene is switched in.
                LoadCurtainView.Show(LoadProgress.SceneLoad(loading != null ? loading.progress / 0.9f : 0f), LoadingMessage.Destination + "…");
            }
        }
    }
}
