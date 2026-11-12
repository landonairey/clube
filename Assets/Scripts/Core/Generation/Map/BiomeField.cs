using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>A <see cref="BiomeDefinition"/>'s ranges and surface materials as plain values (K35).</summary>
    public struct BiomeRow
    {
        public float2 Elevation;
        public float2 Temperature;
        public float2 Moisture;
        public byte Top;
        public byte Band;
    }

    /// <summary>
    /// Which biome a column is in (GL591), as plain values so the fill jobs read it (K35): the climate (temperature
    /// from the latitude, the height and a wandering noise; moisture from a wider noise; both shifted near the map's
    /// edges, <see cref="MapShape.EdgeClimate"/>) and the biome table, first match wins. The rows sit inline (at most
    /// <see cref="MaxBiomes"/>), so nothing needs disposing.
    /// </summary>
    public struct BiomeField
    {
        public const int MaxBiomes = 18;

        public FixedList512Bytes<BiomeRow> Rows;
        public MapShape Shape;
        public int Seed;
        public float BaseTemperature;
        public float LapseRate;
        public float NorthCooling;
        public float TemperatureNoise;
        public float RegionSize;

        private const int TemperatureSalt = 0x54454D50;
        private const int MoistureSalt = 0x4D4F4953;

        /// <summary>No biomes: the terrain layers alone set the materials.</summary>
        public static BiomeField None => default;

        public bool IsCreated => Rows.Length > 0;

        /// <summary>The field for a map's biome table; ids a biome leaves unset are the terrain layers' own.</summary>
        public static BiomeField From(IReadOnlyList<BiomeDefinition> biomes, MapClimate climate, MapShape shape, LayerTable layers)
        {
            var field = new BiomeField
            {
                Shape = shape,
                Seed = shape.Seed,
                BaseTemperature = climate.BaseTemperature,
                LapseRate = Mathf.Max(0f, climate.LapseRate),
                NorthCooling = climate.NorthCooling,
                TemperatureNoise = Mathf.Max(0f, climate.TemperatureNoise),
                RegionSize = Mathf.Max(50f, climate.RegionSize),
            };
            for (int i = 0; i < biomes.Count && i < MaxBiomes; i++)
            {
                BiomeDefinition biome = biomes[i];
                field.Rows.Add(new BiomeRow
                {
                    Elevation = new float2(biome.Elevation.x, biome.Elevation.y),
                    Temperature = new float2(biome.Temperature.x, biome.Temperature.y),
                    Moisture = new float2(biome.Moisture.x, biome.Moisture.y),
                    Top = biome.Top != null ? biome.Top.Id : layers.Top,
                    Band = biome.Band != null ? biome.Band.Id : layers.Band,
                });
            }
            return field;
        }

        /// <summary>The climate of a column whose ground is at <paramref name="height"/>.</summary>
        /// <param name="elevation">Metres above the sea.</param>
        /// <param name="temperature">Degrees.</param>
        /// <param name="moisture">0 dry to 1 wet.</param>
        public void Climate(float x, float z, float height, out float elevation, out float temperature, out float moisture)
        {
            elevation = height - Shape.SeaLevel;
            Shape.EdgeClimate(x, z, out float edgeTemperature, out float edgeMoisture);
            float wander = ValueNoise.At(Seed, x / (RegionSize * 1.3f), z / (RegionSize * 1.3f), 0, TemperatureSalt) * 2f - 1f;
            float north = Shape.Half > 0f ? math.clamp(z / Shape.Half, -1.5f, 1.5f) : 0f;
            temperature = BaseTemperature - NorthCooling * north - LapseRate * math.max(0f, elevation)
                          + wander * TemperatureNoise + edgeTemperature;
            float noise = 0.65f * ValueNoise.At(Seed, x / RegionSize, z / RegionSize, 0, MoistureSalt)
                          + 0.35f * ValueNoise.At(Seed, x / (RegionSize * 0.4f), z / (RegionSize * 0.4f), 1, MoistureSalt);
            // The octaves' sum gathers round 0.5; stretched so dry and wet regions both turn up.
            moisture = math.saturate(0.5f + (noise - 0.5f) * 2.6f + edgeMoisture);
        }

        /// <summary>The biome's index in the table for a column whose ground is at <paramref name="height"/>; the last when none matches.</summary>
        public int IndexAt(float x, float z, float height)
        {
            Climate(x, z, height, out float elevation, out float temperature, out float moisture);
            for (int i = 0; i < Rows.Length; i++)
            {
                BiomeRow row = Rows[i];
                if (elevation >= row.Elevation.x && elevation < row.Elevation.y
                    && temperature >= row.Temperature.x && temperature < row.Temperature.y
                    && moisture >= row.Moisture.x && moisture < row.Moisture.y)
                {
                    return i;
                }
            }
            return Rows.Length - 1;
        }
    }

    /// <summary>
    /// The main-thread biome query (GL591): which <see cref="BiomeDefinition"/> any position is in. Systems get it from
    /// the generator (<see cref="BoundedMapGenerator.Biomes"/>, or <see cref="For"/>); the trees read it through
    /// <see cref="ITreeClimate"/>, from a coarse grid filled in one job the first time it's asked (a ground height per lookup would
    /// slow the distant trees).
    /// </summary>
    public sealed class BiomeMap : ITreeClimate
    {
        // Size of the tree lookups' cells, in metres, and how far past the map's edges the grid reaches.
        private const float CoarseCell = 16f;
        private const float CoarseMargin = 512f;

        private readonly HeightfieldGenerator ground;
        private readonly List<BiomeDefinition> biomes;
        private byte[] coarse;
        private readonly int coarseSize;
        private readonly float coarseOrigin;
        private BiomeField field;

        public BiomeMap(BiomeField field, List<BiomeDefinition> biomes, HeightfieldGenerator ground)
        {
            this.field = field;
            this.biomes = biomes;
            this.ground = ground;
            foreach (BiomeDefinition biome in biomes)
            {
                MaxTreeDensity = Mathf.Max(MaxTreeDensity, biome.TreeDensity);
            }
            coarseOrigin = -(field.Shape.Half + CoarseMargin);
            coarseSize = Mathf.CeilToInt(-2f * coarseOrigin / CoarseCell);
        }

        /// <summary>The biome query of a bounded map's generator; null for any other generator.</summary>
        public static BiomeMap For(ITerrainGenerator generator)
        {
            return (generator as BoundedMapGenerator)?.Biomes;
        }

        /// <summary>The biome table, in the order indices count.</summary>
        public IReadOnlyList<BiomeDefinition> Biomes => biomes;

        /// <summary>The plain-value field the fill jobs read.</summary>
        public BiomeField Field => field;

        public float MaxTreeDensity { get; }

        /// <summary>The biome at a position (its height doesn't matter: the column's ground decides).</summary>
        public BiomeDefinition At(Vector3 position)
        {
            return biomes[IndexAt(position.x, position.z)];
        }

        public BiomeDefinition At(float x, float z)
        {
            return biomes[IndexAt(x, z)];
        }

        /// <summary>The index in <see cref="Biomes"/> of the biome at (x, z).</summary>
        public int IndexAt(float x, float z)
        {
            return field.IndexAt(x, z, ground.Height(x, z));
        }

        /// <summary>The same for a column whose ground height is already known.</summary>
        public int IndexAt(float x, float z, float groundHeight)
        {
            return field.IndexAt(x, z, groundHeight);
        }

        /// <summary>Elevation over the sea, temperature in degrees and moisture (0-1) at (x, z).</summary>
        public void ClimateAt(float x, float z, out float elevation, out float temperature, out float moisture)
        {
            field.Climate(x, z, ground.Height(x, z), out elevation, out temperature, out moisture);
        }

        public float TreeDensityAt(float x, float z)
        {
            return biomes[CoarseIndexAt(x, z)].TreeDensity;
        }

        public float MapleShareAt(float x, float z, float fallback)
        {
            float share = biomes[CoarseIndexAt(x, z)].MapleShare;
            return share >= 0f ? share : fallback;
        }

        // The biome at the middle of the 16 m cell holding (x, z), remembered.
        private int CoarseIndexAt(float x, float z)
        {
            int cx = Mathf.FloorToInt((x - coarseOrigin) / CoarseCell);
            int cz = Mathf.FloorToInt((z - coarseOrigin) / CoarseCell);
            float mx = coarseOrigin + (cx + 0.5f) * CoarseCell, mz = coarseOrigin + (cz + 0.5f) * CoarseCell;
            if (cx < 0 || cz < 0 || cx >= coarseSize || cz >= coarseSize)
            {
                return IndexAt(mx, mz);
            }
            if (coarse == null)
            {
                FillCoarse();
            }
            return coarse[cx + cz * coarseSize];
        }

        // Every coarse cell's biome, from one job's worth of ground heights (about 120,000 for a 5 km map).
        private void FillCoarse()
        {
            int count = coarseSize * coarseSize;
            var points = new NativeArray<float2>(count, Allocator.TempJob);
            var heights = new NativeArray<float>(count, Allocator.TempJob);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    points[i] = coarseOrigin + (new float2(i % coarseSize, i / coarseSize) + 0.5f) * CoarseCell;
                }
                ground.SchedulePointHeights(points, heights).Complete();
                coarse = new byte[count];
                for (int i = 0; i < count; i++)
                {
                    coarse[i] = (byte)field.IndexAt(points[i].x, points[i].y, heights[i]);
                }
            }
            finally
            {
                points.Dispose();
                heights.Dispose();
            }
        }
    }
}
