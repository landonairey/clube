namespace Clube.Core
{
    /// <summary>Plains, foothills and ridged mountains at a real scale (GL26); see <see cref="LandscapeHeight"/>.</summary>
    public sealed class LandscapeGenerator : HeightfieldGenerator<LandscapeHeight>
    {
        public LandscapeGenerator(
            int seed, float level, float mountainHeight, float featureSize, float coverage,
            int octaves = 5, float lacunarity = 2f, float persistence = 0.5f, DistantRanges distant = default)
            : base(new LandscapeHeight(seed, level, mountainHeight, featureSize, coverage, octaves, lacunarity, persistence, distant))
        {
        }

        private LandscapeGenerator(LandscapeHeight field)
            : base(field)
        {
            Water = field.Water;
        }

        /// <summary>The same land with rivers and lakes (GL307) cut into it.</summary>
        public LandscapeGenerator WithWater(WaterMap water)
        {
            return new LandscapeGenerator(Field.WithWater(water));
        }

        public override WaterMap Water { get; }
    }
}
