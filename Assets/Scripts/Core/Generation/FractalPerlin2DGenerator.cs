using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Several octaves of 2D Perlin noise added together (K9): each octave's
    /// frequency is multiplied by the lacunarity and its amplitude by the
    /// persistence. With one octave this is plain 2D Perlin.
    /// </summary>
    public sealed class FractalPerlin2DGenerator : HeightfieldGenerator
    {
        private readonly PerlinNoise noise;
        private readonly float level;
        private readonly float amplitude;
        private readonly float frequency;
        private readonly int octaves;
        private readonly float lacunarity;
        private readonly float persistence;
        private readonly float normaliser;

        public FractalPerlin2DGenerator(
            int seed, float level, float amplitude, float frequency, int octaves = 1, float lacunarity = 2f, float persistence = 0.5f)
        {
            noise = new PerlinNoise(seed);
            this.level = level;
            this.amplitude = amplitude;
            this.frequency = frequency;
            this.octaves = Mathf.Max(1, octaves);
            this.lacunarity = lacunarity;
            this.persistence = persistence;

            // Divide by the sum of octave weights so the result stays within about ±amplitude.
            float weight = 1f;
            for (int i = 0; i < this.octaves; i++)
            {
                normaliser += weight;
                weight *= persistence;
            }
        }

        public override float Height(float x, float z)
        {
            float sum = 0f;
            float octaveFrequency = frequency;
            float weight = 1f;
            for (int octave = 0; octave < octaves; octave++)
            {
                // A different offset per octave, so octaves don't line up at the origin
                // (Perlin noise is 0 on every lattice point).
                float offset = 0.31f + octave * 17.17f;
                sum += weight * noise.Sample(x * octaveFrequency + offset, z * octaveFrequency + offset);
                octaveFrequency *= lacunarity;
                weight *= persistence;
            }
            return level + amplitude * sum / normaliser;
        }
    }
}
