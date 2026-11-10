using UnityEngine;

namespace Clube.Core
{
    /// <summary>When the evening's light is going (GL465): plain math from the sun's height, for the fireflies.</summary>
    public static class DuskLight
    {
        /// <summary>
        /// How much of dusk it is for a sun height (the sine of its angle over the horizon), 0-1: in the evening only, it
        /// starts as the sun nears the horizon, is full just after it sets, and is gone by deep night. Never in the morning.
        /// </summary>
        public static float Glow(float sunHeight, bool evening)
        {
            if (!evening)
            {
                return 0f;
            }
            float coming = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.02f, sunHeight));
            float going = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.4f, -0.2f, sunHeight));
            return coming * going;
        }
    }
}
