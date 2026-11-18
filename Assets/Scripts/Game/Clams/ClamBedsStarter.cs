using Clube.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Clube.Game
{
    /// <summary>
    /// GL735: when a scene with a world and a player loads, makes the <see cref="ClamBeds"/> for it and puts a
    /// <see cref="ClamPanel"/> on the player, so no scene has to hold them. A scene with no player gets nothing.
    /// </summary>
    public static class ClamBedsStarter
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void HookSceneLoads()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single)
            {
                return;
            }
            var view = Object.FindAnyObjectByType<WorldView>();
            PlayerController player = Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (view == null || player == null || Object.FindAnyObjectByType<ClamBeds>() != null)
            {
                return;
            }
            if (player.GetComponent<ClamPanel>() == null)
            {
                player.gameObject.AddComponent<ClamPanel>();
            }
            var beds = new GameObject("Clam Beds").AddComponent<ClamBeds>();
            beds.Bind(view, player);
        }
    }
}
