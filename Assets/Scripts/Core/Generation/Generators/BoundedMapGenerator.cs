using System.Collections;
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

        // Not readonly: passed by reference to the Burst-compiled height (GL762).
        private BoundedMapHeight heightField;

        public BoundedMapGenerator(BoundedMapHeight field, MapLayout layout, List<BiomeDefinition> biomes, BiomeField biomeField)
            : base(field)
        {
            heightField = field;
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

        /// <summary>The height field itself: its shape, inland cliffs and canyons (GL737).</summary>
        public BoundedMapHeight Ground => Field;

        public override bool HasValleys => true;

        /// <summary>The ground's height at (x, z), through Burst (GL762): settlements, roads and people ask it point by point.</summary>
        public override float Height(float x, float z)
        {
            return BoundedMapHeightCalls.At(ref heightField, x, z);
        }

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
            BoundedMapGenerator dry = CreateDry(settings);
            if (!settings.Water.Enabled)
            {
                return dry;
            }

            WaterMap water = WaterOf(settings, dry, out string key);
            return dry.WithWater(water).WithCanyons(CanyonsOf(settings, key, water));
        }

        /// <summary>
        /// Works out the water <see cref="Create"/> will need, a step at a time (GL762, <see cref="SlicedWork"/>): it is kept,
        /// so a scene can be started behind a load screen that keeps drawing, and <see cref="Create"/> then takes a moment.
        /// </summary>
        public static IEnumerator PrepareSteps(TerrainSettings settings)
        {
            if (!settings.Water.Enabled)
            {
                yield break;
            }
            BoundedMapGenerator dry = CreateDry(settings);
            yield return Hydrology.PrepareSteps(WaterKey(settings), dry, settings.Water, TerrainGenerators.BanksFor(settings), KeepDry(settings),
                settings.Erosion.MinPondArea, ExtentOf(settings, dry.Layout));
            CanyonsOf(settings, WaterKey(settings), WaterOf(settings, dry, out _));
        }

        private static WaterMap WaterOf(TerrainSettings settings, BoundedMapGenerator dry, out string key)
        {
            key = WaterKey(settings);
            return Hydrology.For(key, dry, settings.Water, TerrainGenerators.BanksFor(settings), KeepDry(settings), settings.Erosion.MinPondArea,
                ExtentOf(settings, dry.Layout));
        }

        // The dry lands' canyons are cut round the water once it is known (GL737).
        private static Canyons CanyonsOf(TerrainSettings settings, string key, WaterMap water)
        {
            MapDefinition map = settings.Map;
            return CanyonReach.For(key + "|canyons " + map.Cliffs.CanyonKey(), map.Cliffs.ToCanyons(settings.Seed, map.Climate.RegionSize), water);
        }

        /// <summary>GL802: a key naming the map these settings make (the same key, the same ground and water).</summary>
        public static string MapKey(TerrainSettings settings) => WaterKey(settings);

        private static string WaterKey(TerrainSettings settings)
        {
            return $"bounded|{settings.Seed}|{settings.SurfaceLevel}|{settings.Amplitude}|{settings.FeatureSize}|"
                + $"{settings.MountainCoverage}|{settings.Lacunarity}|{settings.Persistence}|{settings.Erosion.Key}|{settings.Map.Key(settings.Seed)}";
        }

        private static Rect KeepDry(TerrainSettings settings)
        {
            return settings.StartArea.Enabled ? settings.StartArea.Area : default;
        }

        private static WaterExtent ExtentOf(TerrainSettings settings, MapLayout layout)
        {
            MapDefinition map = settings.Map;
            MapShape shape = layout.Shape;
            return new WaterExtent
            {
                AreaSize = layout.Size + 2f * map.WaterMargin,
                FlowCellSize = map.FlowCellSize,
                CellSize = map.WaterCellSize,
                SeaLevel = layout.SeaLevel,
                IsSea = point => shape.IsSeaSide(point.x, point.y),
                IsDry = point => IsDesert(shape, point),
            };
        }

        /// <summary>The same map with its dry lands raised into tablelands round their water (GL737).</summary>
        public BoundedMapGenerator WithCanyons(Canyons canyons)
        {
            return new BoundedMapGenerator(Field.WithCanyons(canyons), Layout, biomeTable, BiomeField);
        }


        // True where an edge has dried the climate right out (the desert and its margin): no lake stands in its hollows.
        private static bool IsDesert(MapShape shape, Vector2 point)
        {
            shape.EdgeClimate(point.x, point.y, out _, out float moisture);
            return moisture < -0.4f;
        }

        /// <summary>
        /// The map's land alone, with no rivers, lakes or sea water worked out (GL636): its heights are the finished
        /// map's, but it builds in a moment, where the water takes seconds. The new-game preview draws from it.
        /// </summary>
        public static BoundedMapGenerator CreateDry(TerrainSettings settings)
        {
            MapDefinition map = settings.Map;
            MapLayout layout = map.Resolve(settings.Seed, settings.SurfaceLevel);
            LandscapeHeight land = new LandscapeHeight(
                settings.Seed, settings.SurfaceLevel, settings.Amplitude, settings.FeatureSize, settings.MountainCoverage,
                settings.Erosion.BaseOctaves, settings.Lacunarity, settings.Persistence).WithSmoothMassifs();
            var field = new BoundedMapHeight(
                land, settings.Erosion.ToFilter(settings.Seed), layout.Shape, settings.SurfaceLevel,
                Mathf.Max(settings.Amplitude, map.PeakHeight))
                .WithEscarpments(map.Cliffs.ToEscarpments(settings.Seed, layout.SeaLevel));

            LayerTable layers = LayerTable.From(settings.Layers);
            List<BiomeDefinition> biomes = map.Biomes.Count > 0
                ? map.Biomes
                : BiomeDefinition.Defaults(settings.Water.Sand, settings.Layers?.Band, settings.Layers?.Base);
            BiomeField biomeField = Clube.Core.BiomeField.From(biomes, map.Climate, layout.Shape, layers, map.Cliffs.ToStrata(settings.Seed));
            return new BoundedMapGenerator(field, layout, biomes, biomeField);
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

    /// <summary>One height of a bounded map as a Burst-compiled call: about the cost of a job's, from managed code (GL762).</summary>
    [BurstCompile]
    internal static class BoundedMapHeightCalls
    {
        [BurstCompile]
        public static float At(ref BoundedMapHeight field, float x, float z)
        {
            return field.Height(x, z);
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

        /// <summary>True where the ground soaks water up, so a hollow there holds no lake (a desert's dunes); null for nowhere.</summary>
        public System.Func<Vector2, bool> IsDry;

        public string Key => $"{AreaSize}|{FlowCellSize}|{CellSize}|{SeaLevel}|{IsSea != null}|{IsDry != null}";
    }
}
