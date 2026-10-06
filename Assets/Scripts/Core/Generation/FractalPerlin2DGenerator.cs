namespace Clube.Core
{
    /// <summary>
    /// Several octaves of 2D Perlin noise added together (K9): each octave's
    /// frequency is multiplied by the lacunarity and its amplitude by the
    /// persistence. With one octave this is plain 2D Perlin. The shape is
    /// <see cref="FractalNoiseHeight"/>.
    /// </summary>
    public sealed class FractalPerlin2DGenerator : HeightfieldGenerator<FractalNoiseHeight>
    {
        public FractalPerlin2DGenerator(
            int seed, float level, float amplitude, float frequency, int octaves = 1, float lacunarity = 2f, float persistence = 0.5f)
            : base(new FractalNoiseHeight(seed, level, amplitude, frequency, octaves, lacunarity, persistence))
        {
        }
    }
}
