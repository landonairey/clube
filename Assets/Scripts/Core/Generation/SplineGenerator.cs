using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Fractal 2D Perlin noise remapped through a height curve (K9). The noise picks
    /// a point from 0 to 1 along the curve's horizontal axis, and the curve says how
    /// high that point is, from 0 (the surface level minus the amplitude) to 1 (the
    /// level plus it). Flat stretches of the curve make plateaus, steep ones cliffs;
    /// a straight diagonal gives back plain fractal noise. The shape is <see cref="SplineHeight"/>.
    /// </summary>
    /// <remarks>
    /// The curve is baked into a <see cref="CurveTable"/> when the generator is made, so
    /// generation jobs can read it (K35); editing the curve regenerates the world, which
    /// makes a new generator.
    /// </remarks>
    public sealed class SplineGenerator : HeightfieldGenerator<SplineHeight>
    {
        public SplineGenerator(
            AnimationCurve curve, int seed, float level, float amplitude, float frequency,
            int octaves = 1, float lacunarity = 2f, float persistence = 0.5f)
            : base(new SplineHeight(CurveTable.Bake(curve), seed, level, amplitude, frequency, octaves, lacunarity, persistence))
        {
        }
    }
}
