namespace Clube.Core
{
    /// <summary>Plains, foothills and ridged mountains at a real scale (GL26); see <see cref="LandscapeHeight"/>.</summary>
    public sealed class LandscapeGenerator : HeightfieldGenerator<LandscapeHeight>
    {
        public LandscapeGenerator(
            int seed, float level, float mountainHeight, float featureSize, float coverage,
            int octaves = 5, float lacunarity = 2f, float persistence = 0.5f)
            : base(new LandscapeHeight(seed, level, mountainHeight, featureSize, coverage, octaves, lacunarity, persistence))
        {
        }
    }
}
