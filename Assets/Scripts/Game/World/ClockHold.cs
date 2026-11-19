using System.Collections.Generic;

namespace Clube.Game
{
    /// <summary>
    /// The one switch the <see cref="GameClock"/> reads (GL785): while anything holds it, game time stands still, so the time of
    /// day, the day count and everything timed by them (quests, shop hours, villagers' duties, torches) wait until the scene is
    /// settled and the player can play. <see cref="LoadCurtain"/> holds it while it is up, <see cref="ArrivalCutscene"/> while
    /// the cart ride plays. Holders destroyed with an old scene are dropped when the next loads, so they never stop its clock.
    /// </summary>
    public static class ClockHold
    {
        private static readonly HashSet<object> holders = new HashSet<object>();

        /// <summary>True while anything holds the clock.</summary>
        public static bool IsHeld => holders.Count > 0;

        /// <summary>Holds the clock for <paramref name="holder"/> (twice is the same as once).</summary>
        public static void Hold(object holder)
        {
            if (holder != null)
            {
                holders.Add(holder);
            }
        }

        /// <summary>Lets go of <paramref name="holder"/>'s hold, if it had one.</summary>
        public static void Release(object holder)
        {
            if (holder != null)
            {
                holders.Remove(holder);
            }
        }

        /// <summary>Sets <paramref name="holder"/>'s hold on or off.</summary>
        public static void Set(object holder, bool held)
        {
            if (held)
            {
                Hold(holder);
            }
            else
            {
                Release(holder);
            }
        }

        /// <summary>Drops every hold (a new scene, a fresh test).</summary>
        public static void Clear()
        {
            holders.Clear();
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void HookSceneLoads()
        {
            holders.Clear();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // sceneLoaded fires after the new scene's Awake/OnEnable, so only holders destroyed with the old scene are dropped.
        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (mode != UnityEngine.SceneManagement.LoadSceneMode.Single)
            {
                return;
            }
            holders.RemoveWhere(IsGone);
        }

        private static bool IsGone(object holder)
        {
            return holder is UnityEngine.Object unityObject && unityObject == null;
        }
    }
}
