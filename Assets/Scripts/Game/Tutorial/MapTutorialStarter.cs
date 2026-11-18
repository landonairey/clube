using UnityEngine;
using UnityEngine.SceneManagement;

namespace Clube.Game
{
    /// <summary>
    /// GL734: plays the opening tutorial on the bounded map, at the quarry settlement ("update tutorial to take place at one
    /// of the settlements in the map"). When a scene with a <see cref="MapSession"/> loads for a new game that chose the
    /// tutorial, it makes the Mining Corp tutorial from its prefab (<see cref="PrefabPath"/>, under Resources) and binds it
    /// to the map's world and player; the tutorial then waits for the quarry's pit and sets the player down there. No
    /// scene holds it, so the map scene keeps no tutorial of its own.
    /// </summary>
    public static class MapTutorialStarter
    {
        /// <summary>The tutorial's prefab, a Resources path: the <see cref="MiningCorpTutorial"/> with its assets, no scene references.</summary>
        public const string PrefabPath = "Tutorial/Mining Corp Tutorial";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void HookSceneLoads()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single && GameSession.IsActive && GameSession.Tutorial)
            {
                Start();
            }
        }

        /// <summary>Makes the tutorial in the open map scene; nothing when the scene has no map or already has a tutorial.</summary>
        public static MiningCorpTutorial Start()
        {
            MapSession session = Object.FindAnyObjectByType<MapSession>();
            if (session == null || session.WorldView == null || Object.FindAnyObjectByType<MiningCorpTutorial>(FindObjectsInactive.Include) != null)
            {
                return null;
            }
            // GL619: the map scene's player is inactive while the scene loads.
            PlayerController player = Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
            var prefab = Resources.Load<MiningCorpTutorial>(PrefabPath);
            if (player == null || prefab == null)
            {
                UnityEngine.Debug.LogWarning($"Map tutorial: no {(player == null ? "player" : "tutorial prefab at Resources/" + PrefabPath)}; the game starts without it.");
                return null;
            }
            MiningCorpTutorial tutorial = Object.Instantiate(prefab);
            tutorial.name = "Mining Corp Tutorial";
            tutorial.Bind(session.WorldView, player);
            return tutorial;
        }
    }
}
