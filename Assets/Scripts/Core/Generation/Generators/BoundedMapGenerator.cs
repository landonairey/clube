using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The bounded map's generator (GL589-GL591): a heightfield like the others, so the fill jobs, the coarse terrain
    /// and the far terrain use it unchanged, plus what only a map has: its <see cref="Layout"/> (size, sides, sea
    /// level, peak) and its biomes (<see cref="Biomes"/> to ask on the main thread, <see cref="BiomeField"/> for jobs).
    /// </summary>
    public sealed class BoundedMapGenerator : HeightfieldGenerator<BoundedMapHeight>
    {
        private readonly List<BiomeDefinition> biomeTable;

        public BoundedMapGenerator(BoundedMapHeight field, MapLayout layout, List<BiomeDefinition> biomes, BiomeField biomeField)
            : base(field)
        {
            Water = field.Water;
            Layout = layout;
            biomeTable = biomes;
            BiomeField = biomeField;
            Biomes = new BiomeMap(biomeField, biomes, this);
        }

        /// <summary>The map: its square, each side's boundary, the sea level, where the peak is.</summary>
        public MapLayout Layout { get; }

        /// <summary>Which biome any position is in (main thread).</summary>
        public BiomeMap Biomes { get; }

        /// <summary>The biomes as plain values, for jobs (<see cref="ChunkFillSettings.Biomes"/>).</summary>
        public BiomeField BiomeField { get; }

        public override WaterMap Water { get; }

        public override bool HasValleys => true;

        /// <summary>The same map with rivers, lakes and the sea.</summary>
        public BoundedMapGenerator WithWater(WaterMap water)
        {
            return new BoundedMapGenerator(Field.WithWater(water), Layout, biomeTable, BiomeField);
        }

        /// <summary>
        /// The generator for a terrain's settings: the eroded landscape's land (its amplitude, feature size, coverage
        /// and erosion settings), shaped by <see cref="TerrainSettings.Map"/>, with the sea, lakes and rivers when the
        /// terrain's water is on. The sea needs the water: without it the sea floor is dry.
        /// </summary>
        public static BoundedMapGenerator Create(TerrainSettings settings)
        {
            MapDefinition map = settings.Map;
            MapLayout layout = map.Resolve(settings.Seed, settings.SurfaceLevel);
            LandscapeHeight land = new LandscapeHeight(
                settings.Seed, settings.SurfaceLevel, settings.Amplitude, settings.FeatureSize, settings.MountainCoverage,
                settings.Erosion.BaseOctaves, settings.Lacunarity, settings.Persistence).WithSmoothMassifs();
            var field = new BoundedMapHeight(
                land, settings.Erosion.ToFilter(settings.Seed), layout.Shape, settings.SurfaceLevel,
                Mathf.Max(settings.Amplitude, map.PeakHeight));

            LayerTable layers = LayerTable.From(settings.Layers);
            List<BiomeDefinition> biomes = map.Biomes.Count > 0
                ? map.Biomes
                : BiomeDefinition.Defaults(settings.Water.Sand, settings.Layers?.Band, settings.Layers?.Base);
            BiomeField biomeField = Clube.Core.BiomeField.From(biomes, map.Climate, layout.Shape, layers);
            var dry = new BoundedMapGenerator(field, layout, biomes, biomeField);
            if (!settings.Water.Enabled)
            {
                return dry;
            }

            MapShape shape = layout.Shape;
            var extent = new WaterExtent
            {
                AreaSize = layout.Size + 2f * map.WaterMargin,
                FlowCellSize = map.FlowCellSize,
                CellSize = map.WaterCellSize,
                SeaLevel = layout.SeaLevel,
                IsSea = point => shape.IsSeaSide(point.x, point.y),
            };
            string key = $"bounded|{settings.Seed}|{settings.SurfaceLevel}|{settings.Amplitude}|{settings.FeatureSize}|"
                + $"{settings.MountainCoverage}|{settings.Lacunarity}|{settings.Persistence}|{settings.Erosion.Key}|{map.Key(settings.Seed)}";
            Rect keepDry = settings.StartArea.Enabled ? settings.StartArea.Area : default;
            WaterMap water = Hydrology.For(
                key, dry, settings.Water, TerrainGenerators.BanksFor(settings), keepDry, settings.Erosion.MinPondArea, extent);
            return dry.WithWater(water);
        }

        public override JobHandle ScheduleValleys(NativeArray<float2> points, NativeArray<float> valleys, JobHandle dependsOn = default)
        {
            return new PointValleysJob { Field = Field, Points = points, Valleys = valleys }.ScheduleParallel(points.Length, 64, dependsOn);
        }

        [BurstCompile]
        private struct PointValleysJob : IJobFor
        {
            public BoundedMapHeight Field;
            [ReadOnly] public NativeArray<float2> Points;
            [WriteOnly] public NativeArray<float> Valleys;

            public void Execute(int index)
            {
                float2 p = Points[index];
                Valleys[index] = Field.Valley(p.x, p.y);
            }
        }
    }

    /// <summary>
    /// The area a bounded map's water is worked out over (<see cref="Hydrology.Build"/>): wider than the settings'
    /// limit, on coarser cells, and with a sea.
    /// </summary>
    public sealed class WaterExtent
    {
        /// <summary>Length of the area's sides in metres, centred on the origin.</summary>
        public float AreaSize;

        /// <summary>Size of the cells the rivers' courses are found on.</summary>
        public float FlowCellSize = 8f;

        /// <summary>Size of the finished water map's cells; 1 or more.</summary>
        public float CellSize = 1f;

        public float SeaLevel;

        /// <summary>True where ground below <see cref="SeaLevel"/> and joined to the area's edge is sea (x, z); null for no sea.</summary>
        public System.Func<Vector2, bool> IsSea;

        public string Key => $"{AreaSize}|{FlowCellSize}|{CellSize}|{SeaLevel}|{IsSea != null}";
    }
}
