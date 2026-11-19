using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// One honest percentage for the whole wait from "Start" to standing in the world (GL780), so the curtain's number only
    /// ever rises. The stages, by the share of the bar each owns: the map's rivers worked out before its scene loads, the
    /// scene itself, the survey of the map, then the ground round the player (its columns loaded, written and meshed). A
    /// scene with no map survey gives the survey's share to the ground.
    /// </summary>
    public static class LoadProgress
    {
        public const float ChartingEnd = 0.15f;
        public const float SceneEnd = 0.2f;
        public const float SurveyEnd = 0.5f;

        /// <summary>The map's water being worked out before the scene (<paramref name="progress"/> 0 to 1).</summary>
        public static float Charting(float progress) => Band(0f, ChartingEnd, progress);

        /// <summary>The scene loading (<paramref name="progress"/> 0 to 1).</summary>
        public static float SceneLoad(float progress) => Band(ChartingEnd, SceneEnd, progress);

        /// <summary>
        /// Inside the scene. <paramref name="hasSurvey"/>: a bounded map is being surveyed, <paramref name="surveyDone"/>: it has
        /// been, <paramref name="survey"/> and <paramref name="ground"/> are each stage's own 0 to 1.
        /// </summary>
        public static float InScene(bool hasSurvey, bool surveyDone, float survey, float ground)
        {
            if (!hasSurvey)
            {
                return Band(SceneEnd, 1f, ground);
            }
            return surveyDone ? Band(SurveyEnd, 1f, ground) : Band(SceneEnd, SurveyEnd, survey);
        }

        private static float Band(float from, float to, float progress)
        {
            return Mathf.Lerp(from, to, Mathf.Clamp01(progress));
        }
    }
}
