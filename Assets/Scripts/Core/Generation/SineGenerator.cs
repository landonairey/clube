using UnityEngine;

namespace Clube.Core
{
    /// <summary>Rolling waves: a sine along x times a sine along z (K9).</summary>
    public sealed class SineGenerator : HeightfieldGenerator
    {
        private readonly float level;
        private readonly float amplitude;
        private readonly float angularFrequency;

        public SineGenerator(float level, float amplitude, float frequency)
        {
            this.level = level;
            this.amplitude = amplitude;
            angularFrequency = frequency * 2f * Mathf.PI;
        }

        public override float Height(float x, float z)
        {
            return level + amplitude * Mathf.Sin(x * angularFrequency) * Mathf.Sin(z * angularFrequency);
        }
    }
}
