using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// *Clube → Debug → Measure tutorial start* (GL759): runs <see cref="TutorialBenchmark"/> in Play mode from the main menu
    /// scene, keeping frames coming while the Editor is unfocused. The report is written to <c>Temp/tutorialbench.txt</c>.
    /// </summary>
    public static class TutorialBenchmarkMenu
    {
        private const string ArmedKey = "Clube.TutorialBenchmark.Armed";
        private const string MenuScene = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Clube/Debug/Measure tutorial start")]
        public static void Measure()
        {
            if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            EditorSceneManager.OpenScene(MenuScene);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.isPlaying = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Begin()
        {
            if (!SessionState.GetBool(ArmedKey, false))
            {
                return;
            }
            SessionState.SetBool(ArmedKey, false);
            EditorApplication.update += KeepFramesComing;
            TutorialBenchmark.Run();
        }

        private static void KeepFramesComing()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.update -= KeepFramesComing;
                return;
            }
            EditorApplication.QueuePlayerLoopUpdate();
        }
    }
}
