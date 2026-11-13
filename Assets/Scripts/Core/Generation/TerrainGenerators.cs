using System;

namespace Clube.Core
{
    /// <summary>Builds the generator a <see cref="TerrainSettings"/> selects (K9), once per fill.</summary>
    public static class TerrainGenerators
    {
        /// <summary>The sand and clay laid by rivers and lakes (GL307).</summary>
        public static WaterBanks BanksFor(TerrainSettings settings)
        {
            WaterGeneration water = settings.Water;
            VoxelMaterial clay = settings.Layers?.Clay;
            return new WaterBanks
            {
                Sand = water.Sand != null ? water.Sand.Id : (byte)0,
                Clay = clay != null ? clay.Id : (byte)0,
                SandDepth = water.Sand != null ? water.SandDepth : 0f,
                ClayDepth = clay != null ? water.ClayDepth : 0f,
                ClayFloorDepth = water.ClayFloorDepth,
                ShoreSand = water.ShoreSand,
                BankClay = water.BankClay,
                BankClayCover = water.BankClayCover,
                PatchSize = 3f,
            };
        }

        /// <exception cref="InvalidOperationException">
        /// The heightmap generator is selected but no usable heightmap is assigned.
        /// </exception>
        public static ITerrainGenerator Create(TerrainSettings settings)
        {
            ITerrainGenerator generator = Build(settings);
            // The trees follow the map's biomes (GL591), or the tree settings alone on any other ground.
            if (settings.Trees != null)
            {
                settings.Trees.Climate = BiomeMap.For(generator);
            }
            return generator;
        }

        private static ITerrainGenerator Build(TerrainSettings settings)
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
                case TerrainGeneratorType.Landscape:
                {
                    var land = new LandscapeGenerator(
                        settings.Seed, settings.SurfaceLevel, settings.Amplitude, settings.FeatureSize, settings.MountainCoverage,
                        settings.Octaves, settings.Lacunarity, settings.Persistence,
                        new DistantRanges(settings.DistantRangeHeight, settings.DistantRangeWidth, settings.DistantRangeStart));
                    if (!settings.Water.Enabled)
                    {
                        return land;
                    }
                    string shape = $"landscape|{settings.Seed}|{settings.SurfaceLevel}|{settings.Amplitude}|{settings.FeatureSize}|"
                        + $"{settings.MountainCoverage}|{settings.Octaves}|{settings.Lacunarity}|{settings.Persistence}";
                    UnityEngine.Rect dry = settings.StartArea.Enabled ? settings.StartArea.Area : default;
                    return land.WithWater(Hydrology.For(shape, land, settings.Water, BanksFor(settings), dry));
                }
                case TerrainGeneratorType.ErodedLandscape:
                {
                    var land = new LandscapeHeight(
                        settings.Seed, settings.SurfaceLevel, settings.Amplitude, settings.FeatureSize, settings.MountainCoverage,
                        settings.Erosion.BaseOctaves, settings.Lacunarity, settings.Persistence,
                        new DistantRanges(settings.DistantRangeHeight, settings.DistantRangeWidth, settings.DistantRangeStart)).WithSmoothMassifs();
                    var eroded = new ErodedLandscapeGenerator(new ErodedLandscapeHeight(
                        land, settings.Erosion.ToFilter(settings.Seed), settings.SurfaceLevel, settings.Amplitude));
                    if (!settings.Water.Enabled)
                    {
                        return eroded;
                    }
                    string shape = $"eroded|{settings.Seed}|{settings.SurfaceLevel}|{settings.Amplitude}|{settings.FeatureSize}|"
                        + $"{settings.MountainCoverage}|{settings.Octaves}|{settings.Lacunarity}|{settings.Persistence}|"
                        + $"{settings.DistantRangeHeight}|{settings.DistantRangeWidth}|{settings.DistantRangeStart}|{settings.Erosion.Key}";
                    UnityEngine.Rect dry = settings.StartArea.Enabled ? settings.StartArea.Area : default;
                    return eroded.WithWater(Hydrology.For(shape, eroded, settings.Water, BanksFor(settings), dry, settings.Erosion.MinPondArea));
                }
                case TerrainGeneratorType.BoundedMap:
                    return BoundedMapGenerator.Create(settings);
                default:
                    throw new ArgumentOutOfRangeException(nameof(settings.Generator), settings.Generator, null);
            }
        }
    }
}
