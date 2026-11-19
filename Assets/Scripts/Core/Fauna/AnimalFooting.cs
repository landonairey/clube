using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// GL800: whether an animal may walk the ground ahead ("they cannot scale super steep surfaces"). The ground is looked
    /// at in short steps along its heading; a step that climbs steeper than its species' <see cref="AnimalSpecies.MaxClimbDegrees"/>
    /// is refused, as is ground ahead steeper than that across the heading (a slanting way up a face), unless the way
    /// goes down, and a drop deeper than it would jump down.
    /// </summary>
    public static class AnimalFooting
    {
        /// <summary>Metres of rise allowed on a step whatever its slope: the ground's own bumps.</summary>
        public const float StepAllowance = 0.05f;

        /// <summary>The deepest drop taken over the whole look ahead, in metres plus this much per metre ahead.</summary>
        public const float DropAllowance = 0.3f, DropPerMetre = 0.6f;

        /// <summary>
        /// True when the ground ahead may be walked: <paramref name="from"/> is the animal's height, then the first
        /// <paramref name="count"/> of <paramref name="heights"/> are the ground's every <paramref name="spacing"/> metres
        /// ahead; <paramref name="crossSlope"/> is the ground's rise a metre across the heading at the last of them.
        /// </summary>
        public static bool WayClear(float from, float[] heights, int count, float spacing, float crossSlope, float maxClimbDegrees)
        {
            float limit = Mathf.Tan(Mathf.Clamp(maxClimbDegrees, 0f, 89f) * Mathf.Deg2Rad);
            float climb = limit * spacing + StepAllowance;
            float last = from;
            for (int i = 0; i < count; i++)
            {
                if (heights[i] - last > climb)
                {
                    return false;
                }
                last = heights[i];
            }
            if (from - last > DropAllowance + DropPerMetre * spacing * count)
            {
                return false;
            }
            // The face ahead: no going on along or up ground steeper than the limit, only down off it.
            float along = count > 1 ? (heights[count - 1] - heights[count - 2]) / spacing : (last - from) / spacing;
            bool steep = along * along + crossSlope * crossSlope > limit * limit;
            return !steep || last < from - StepAllowance;
        }

        /// <summary>
        /// True when one frame's step of <paramref name="length"/> metres from ground at <paramref name="from"/> to ground at
        /// <paramref name="to"/> climbs no steeper than the limit (a centimetre allowed for the surface's own roughness).
        /// </summary>
        public static bool StepClear(float from, float to, float length, float maxClimbDegrees)
        {
            return to - from <= Mathf.Tan(Mathf.Clamp(maxClimbDegrees, 0f, 89f) * Mathf.Deg2Rad) * length + 0.01f;
        }

        /// <summary>
        /// True when ground at <paramref name="height"/> is no steeper than the limit towards any of the first
        /// <paramref name="count"/> of <paramref name="around"/>, the ground's heights <paramref name="distance"/> metres
        /// round it: where an animal may be put down (not on a boulder's top or a cliff's face it could never climb to).
        /// </summary>
        public static bool StandsOn(float height, float[] around, int count, float distance, float maxClimbDegrees)
        {
            float most = Mathf.Tan(Mathf.Clamp(maxClimbDegrees, 0f, 89f) * Mathf.Deg2Rad) * distance + StepAllowance;
            for (int i = 0; i < count; i++)
            {
                if (Mathf.Abs(around[i] - height) > most)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
