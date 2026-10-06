using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Fractal 2D Perlin noise remapped through a height curve (K9). The noise picks a point
    /// from 0 to 1 along the curve, and the curve says how high that point is, from 0 (the
    /// surface level minus the amplitude) to 1 (the level plus it). Flat stretches of the
    /// curve make plateaus, steep ones cliffs; a straight diagonal gives back plain fractal noise.
    /// </summary>
    public struct SplineHeight : IHeightField
    {
        private FractalNoiseHeight unitNoise;
        private CurveTable curve;
        private float lowest;
        private float range;

        public SplineHeight(
            CurveTable curve, int seed, float level, float amplitude, float frequency,
            int octaves = 1, float lacunarity = 2f, float persistence = 0.5f)
        {
            // Unit noise around 0, so it can be mapped onto the curve's 0-1 input.
            unitNoise = new FractalNoiseHeight(seed, 0f, 1f, frequency, octaves, lacunarity, persistence);
            this.curve = curve;
            lowest = level - amplitude;
            range = 2f * amplitude;
        }

        public float Height(float x, float z)
        {
            float alongCurve = math.saturate((unitNoise.Height(x, z) + 1f) * 0.5f);
            return lowest + range * curve.Evaluate(alongCurve);
        }
    }

    /// <summary>
    /// An <see cref="AnimationCurve"/> over 0-1 baked into evenly spaced samples, read with
    /// linear interpolation. An <c>AnimationCurve</c> can only be evaluated on the main thread,
    /// so generation jobs (K35) read this instead. Stored inline (4 KB), so it copies into a
    /// job with the struct holding it and needs no disposing.
    /// </summary>
    public struct CurveTable
    {
        /// <summary>Samples across 0-1; 1,000 intervals, close enough that no curve shows the steps.</summary>
        public const int SampleCount = 1001;

        private FixedList4096Bytes<float> samples;

        /// <summary>Samples the curve at <see cref="SampleCount"/> even steps from 0 to 1.</summary>
        public static CurveTable Bake(AnimationCurve curve)
        {
            var table = new CurveTable { samples = new FixedList4096Bytes<float>() };
            for (int i = 0; i < SampleCount; i++)
            {
                float t = i / (float)(SampleCount - 1);
                table.samples.Add(curve != null ? curve.Evaluate(t) : t);
            }
            return table;
        }

        /// <summary>The curve's value at <paramref name="t"/> (clamped to 0-1).</summary>
        public float Evaluate(float t)
        {
            float position = math.saturate(t) * (SampleCount - 1);
            int below = math.min((int)position, SampleCount - 2);
            return math.lerp(samples[below], samples[below + 1], position - below);
        }

        /// <summary>The lowest and highest value the curve reaches (it may overshoot 0-1 between keys).</summary>
        public float2 Range
        {
            get
            {
                var range = new float2(float.MaxValue, float.MinValue);
                for (int i = 0; i < samples.Length; i++)
                {
                    range = new float2(math.min(range.x, samples[i]), math.max(range.y, samples[i]));
                }
                return range;
            }
        }
    }
}
