using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Fractal 2D Perlin noise remapped through a height curve (K9). The noise picks
    /// a point from 0 to 1 along the curve's horizontal axis, and the curve says how
    /// high that point is, from 0 (the surface level minus the amplitude) to 1 (the
    /// level plus it). Flat stretches of the curve make plateaus, steep ones cliffs;
    /// a straight diagonal gives back plain fractal noise.
    /// </summary>
    /// <remarks>
    /// <see cref="AnimationCurve"/> can only be evaluated on the main thread, so a
    /// Jobs/Burst version (K12, P3) will need the curve baked into a lookup table.
    /// </remarks>
    public sealed class SplineGenerator : HeightfieldGenerator
    {
        private readonly FractalPerlin2DGenerator noise;
        private readonly AnimationCurve curve;
        private readonly float lowest;
        private readonly float range;

        public SplineGenerator(
            AnimationCurve curve, int seed, float level, float amplitude, float frequency,
            int octaves = 1, float lacunarity = 2f, float persistence = 0.5f)
        {
            // Unit noise around 0, so it can be mapped onto the curve's 0-1 input.
            noise = new FractalPerlin2DGenerator(seed, 0f, 1f, frequency, octaves, lacunarity, persistence);
            this.curve = curve;
            lowest = level - amplitude;
            range = 2f * amplitude;
        }

        public override float Height(float x, float z)
        {
            float alongCurve = Mathf.Clamp01((noise.Height(x, z) + 1f) * 0.5f);
            return lowest + range * curve.Evaluate(alongCurve);
        }
    }
}
