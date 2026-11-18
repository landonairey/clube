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
        private const string WalkArmedKey = "Clube.MapWalkBenchmark.Armed";
        private const int WalkRenderDistance = 64;
        private const string MenuScene = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Clube/Debug/Measure tutorial start")]
        public static void Measure()
        {
            Arm(ArmedKey);
        }

        /// <summary>*Clube → Debug → Measure walking on the map* (GL770): <see cref="MapWalkBenchmark"/>, at the longest render distance; the report is <c>Temp/walkbench.txt</c>.</summary>
        [MenuItem("Clube/Debug/Measure walking on the map")]
        public static void MeasureWalking()
        {
            Arm(WalkArmedKey);
        }

        private static void Arm(string key)
        {
            if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            EditorSceneManager.OpenScene(MenuScene);
            SessionState.SetBool(key, true);
            EditorApplication.isPlaying = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Begin()
        {
            bool tutorial = SessionState.GetBool(ArmedKey, false), walking = SessionState.GetBool(WalkArmedKey, false);
            if (!tutorial && !walking)
            {
                return;
            }
            SessionState.SetBool(ArmedKey, false);
            SessionState.SetBool(WalkArmedKey, false);
            // Each script's Update is only timed while the Profiler records.
            UnityEditorInternal.ProfilerDriver.enabled = true;
            EditorApplication.update += KeepFramesComing;
            if (walking)
            {
                MapWalkBenchmark.Run(WalkRenderDistance);
            }
            else
            {
                TutorialBenchmark.Run();
            }
        }

        private static void KeepFramesComing()
        {
            if (!EditorApplication.isPlaying)
            {
                UnityEditorInternal.ProfilerDriver.enabled = false;
                EditorApplication.update -= KeepFramesComing;
                return;
            }
            EditorApplication.QueuePlayerLoopUpdate();
        }
    }
}
