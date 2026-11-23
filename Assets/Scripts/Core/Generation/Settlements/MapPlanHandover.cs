namespace Clube.Core
{
    /// <summary>
    /// GL802: a bounded map's survey and plan, handed from the tutorial (played on the same map, in the quarry) to the game
    /// scene that loads after it, so leaving the tutorial skips seconds of surveying and planning. Held for one map at a
    /// time and taken once: any other load plans afresh.
    /// </summary>
    public static class MapPlanHandover
    {
        private static string heldKey;
        private static ResourceSurvey heldSurvey;
        private static WorldPlan heldPlan;

        /// <summary>True while a survey and plan wait to be taken.</summary>
        public static bool IsHolding => heldPlan != null;

        /// <summary>Keeps a survey and plan for the next load of the map named <paramref name="key"/> (replacing any held).</summary>
        public static void Hold(string key, ResourceSurvey survey, WorldPlan plan)
        {
            heldKey = key;
            heldSurvey = survey;
            heldPlan = plan;
        }

        /// <summary>
        /// The held survey and plan when they are for the map named <paramref name="key"/>; either way nothing is held after.
        /// </summary>
        public static bool TryTake(string key, out ResourceSurvey survey, out WorldPlan plan)
        {
            bool match = heldPlan != null && heldKey == key;
            survey = match ? heldSurvey : null;
            plan = match ? heldPlan : null;
            Clear();
            return match;
        }

        /// <summary>Drops whatever is held.</summary>
        public static void Clear()
        {
            heldKey = null;
            heldSurvey = null;
            heldPlan = null;
        }
    }
}
