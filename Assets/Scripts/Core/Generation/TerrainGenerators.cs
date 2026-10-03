using System;

namespace Clube.Core
{
    /// <summary>Builds the generator a <see cref="TerrainSettings"/> selects (K9), once per fill.</summary>
    public static class TerrainGenerators
    {
        /// <exception cref="InvalidOperationException">
        /// The heightmap generator is selected but no usable heightmap is assigned.
        /// </exception>
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
                case TerrainGeneratorType.Spline:
                    return new SplineGenerator(
                        settings.HeightCurve, settings.Seed, settings.SurfaceLevel, settings.Amplitude, settings.Frequency,
                        settings.Octaves, settings.Lacunarity, settings.Persistence);
                case TerrainGeneratorType.Heightmap:
                    Heightmap heightmap = settings.LoadHeightmap()
                        ?? throw new InvalidOperationException("The heightmap generator needs a heightmap image or RAW file.");
                    return new HeightmapGenerator(heightmap, settings.SurfaceLevel, settings.Amplitude, settings.HeightmapUnitsPerPixel);
                default:
                    throw new ArgumentOutOfRangeException(nameof(settings.Generator), settings.Generator, null);
            }
        }
    }
}
