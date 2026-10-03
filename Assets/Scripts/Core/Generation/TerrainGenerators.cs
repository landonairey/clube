using System;

namespace Clube.Core
{
    /// <summary>Builds the generator a <see cref="TerrainSettings"/> selects (K9), once per fill.</summary>
    public static class TerrainGenerators
    {
        public static ITerrainGenerator Create(TerrainSettings settings)
        {
            switch (settings.Generator)
            {
                case TerrainGeneratorType.Flat:
                    return new FlatGenerator(settings.SurfaceLevel);
                case TerrainGeneratorType.Sine:
                    return new SineGenerator(settings.SurfaceLevel, settings.Amplitude, settings.Frequency);
                case TerrainGeneratorType.Perlin2D:
                    return new FractalPerlin2DGenerator(settings.Seed, settings.SurfaceLevel, settings.Amplitude, settings.Frequency);
                case TerrainGeneratorType.Perlin3D:
                    return new Perlin3DGenerator(settings.Seed, settings.SurfaceLevel, settings.Amplitude, settings.Frequency);
                case TerrainGeneratorType.FractalPerlin2D:
                    return new FractalPerlin2DGenerator(
                        settings.Seed, settings.SurfaceLevel, settings.Amplitude, settings.Frequency,
                        settings.Octaves, settings.Lacunarity, settings.Persistence);
                default:
                    throw new ArgumentOutOfRangeException(nameof(settings.Generator), settings.Generator, null);
            }
        }
    }
}
