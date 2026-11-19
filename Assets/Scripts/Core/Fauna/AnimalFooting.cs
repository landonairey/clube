using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// GL800: whether an animal may walk the ground ahead ("they cannot scale super steep surfaces"). The ground is looked
    /// at in short steps along its heading; a step that climbs steeper than its species' <see cref="AnimalSpecies.MaxClimbDegrees"/>
    /// is refused, as is a drop deeper than it would jump down.
    /// </summary>
    public static class AnimalFooting
    {
        /// <summary>Metres of rise allowed on a step whatever its slope: the ground's own bumps.</summary>
        public const float StepAllowance = 0.05f;

        /// <summary>The deepest drop taken over the whole look ahead, in metres plus this much per metre ahead.</summary>
        public const float DropAllowance = 0.3f, DropPerMetre = 0.6f;

        /// <summary>
        /// True when the ground ahead may be walked: <paramref name="from"/> is the animal's height, then the first
        /// <paramref name="count"/> of <paramref name="heights"/> are the ground's every <paramref name="spacing"/> metres ahead.
        /// </summary>
        public static bool WayClear(float from, float[] heights, int count, float spacing, float maxClimbDegrees)
        {
            float climb = Mathf.Tan(Mathf.Clamp(maxClimbDegrees, 0f, 89f) * Mathf.Deg2Rad) * spacing + StepAllowance;
            float last = from;
            for (int i = 0; i < count; i++)
            {
                if (heights[i] - last > climb)
                {
                    return false;
                }
                last = heights[i];
            }
            return from - last <= DropAllowance + DropPerMetre * spacing * count;
        }
    }
}
