using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>GL737: inland cliffs, sea cliffs, the dry lands' canyons and the strata their walls show.</summary>
    public class CliffCanyonTests
    {
        private const float Level = 20f;

        private VoxelMaterial stone;
        private VoxelMaterial dirt;
        private VoxelMaterial grass;
        private VoxelMaterial sand;
        private VoxelMaterial sandstone;
        private VoxelMaterial granite;

        [OneTimeSetUp]
        public void MakeMaterials()
        {
            stone = VoxelMaterial.Create(1, "Stone");
            dirt = VoxelMaterial.Create(2, "Dirt");
            grass = VoxelMaterial.Create(3, "Grass");
            sand = VoxelMaterial.Create(4, "Sand");
            sandstone = VoxelMaterial.Create(40, "Sandstone");
            granite = VoxelMaterial.Create(41, "Granite");
        }

        [OneTimeTearDown]
        public void DestroyMaterials()
        {
            Hydrology.DisposeAll();
            CanyonReach.DisposeAll();
            foreach (VoxelMaterial material in new[] { stone, dirt, grass, sand, sandstone, granite })
            {
                Object.DestroyImmediate(material);
            }
        }

        private TerrainSettings Settings(int seed = 5)
        {
            var settings = new TerrainSettings
            {
                Generator = TerrainGeneratorType.BoundedMap, Seed = seed, SurfaceLevel = Level, Amplitude = 40f,
            };
            settings.Layers.Top = grass;
            settings.Layers.TopDepth = 1f;
            settings.Layers.Band = dirt;
            settings.Layers.BandDepth = 3f;
            settings.Layers.Base = stone;
            settings.Layers.RidgeDepth = 0f;
            settings.Water.Sand = sand;
            settings.Water.Enabled = false;
            settings.Map.Size = 2048f;
            settings.Map.PeakHeight = 150f;
            settings.Map.VoxelFloor = -40f;
            return settings;
        }

        private static Escarpments Cliffs(float coverage = 1f)
        {
            return new Escarpments
            {
                Seed = 11u, Spacing = 400f, RunLength = 300f, Coverage = coverage, MinDrop = 10f, MaxDrop = 10f,
                Face = 4f, SwellHeight = 2f, SwellWidth = 30f, SeaLevel = 0f,
            };
        }

        // A point on a cliff line (within 0.2 m of it), searched along x from a start.
        private static float2 OnALine(Escarpments cliffs, float z)
        {
            float previous = cliffs.LineDistance(-1000f, z);
            for (float x = -999f; x < 1000f; x += 1f)
            {
                float d = cliffs.LineDistance(x, z);
                if (math.sign(d) != math.sign(previous))
                {
                    float a = x - 1f, b = x;
                    for (int i = 0; i < 30; i++)
                    {
                        float m = 0.5f * (a + b);
                        if (math.sign(cliffs.LineDistance(m, z)) == math.sign(cliffs.LineDistance(a, z))) a = m; else b = m;
                    }
                    return new float2(0.5f * (a + b), z);
                }
                previous = d;
            }
            Assert.Fail("no cliff line crossed");
            return default;
        }

        [Test]
        public void AnInlandCliff_IsASharpStep_WithTheLandLevelOnBothSides()
        {
            Escarpments cliffs = Cliffs();
            float2 p = OnALine(cliffs, 37f);
            float dx = cliffs.LineDistance(p.x + 0.5f, p.y) - cliffs.LineDistance(p.x - 0.5f, p.y);
            float uphill = math.sign(dx);
            float Offset(float along) => cliffs.Offset(p.x + uphill * along, p.y, 60f, 50f);

            Assert.That(Offset(-6f), Is.EqualTo(0f).Within(0.05f), "the low side is the land as it was");
            Assert.That(Offset(6f), Is.GreaterThan(10f), "the high side stands the drop higher, and swells up to the edge");
            Assert.That(Offset(6f) - Offset(-6f), Is.GreaterThan(9f), "within 12 m: a cliff, not a slope");
            Assert.That(Offset(60f), Is.EqualTo(10f).Within(0.3f), "back from the edge the swell is gone; the land carries on");
        }

        [Test]
        public void InlandCliffs_ComeAndGo_AndStayOffTheShoreAndTheMountains()
        {
            Escarpments cliffs = Cliffs(0.4f);
            int on = 0, off = 0;
            for (int i = 0; i < 400; i++)
            {
                float run = cliffs.RunAt(i * 23f - 4600f, i * 7f);
                on += run > 0.99f ? 1 : 0;
                off += run < 0.01f ? 1 : 0;
            }
            Assert.That(on, Is.GreaterThan(40), "some of the lines are cliffs");
            Assert.That(off, Is.GreaterThan(100), "and some are open ground: runs of varying length");

            Escarpments full = Cliffs();
            float2 p = OnALine(full, 37f);
            for (float along = -20f; along <= 20f; along += 4f)
            {
                Assert.That(full.Offset(p.x + along, p.y, 0.5f, 0f), Is.Zero, "not at the sea's edge");
                Assert.That(full.Offset(p.x + along, p.y, 300f, 50f), Is.Zero, "not high on a mountain");
            }
        }

        [Test]
        public void SeaCliffs_StandAtTheWater_AndTheLandBehindThemNeverFallsBack()
        {
            TerrainSettings settings = Settings();
            settings.Map.SetShape(MapShapeType.IslandName);
            settings.Map.SetSides(MapBoundaryType.OceanName, MapBoundaryType.OceanName, MapBoundaryType.OceanName, MapBoundaryType.OceanName);
            settings.Map.Cliffs.SeaCliffCoverage = 0f;
            MapShape beaches = settings.Map.Resolve(5, Level).Shape;
            settings.Map.Cliffs.SeaCliffCoverage = 1f;
            MapShape cliffs = settings.Map.Resolve(5, Level).Shape;

            // Out from the land's middle to the sea: the coast is where the land field first falls below 0.
            float2 direction = math.normalize(new float2(1f, 0.3f));
            float2 start = new float2(cliffs.Lands[0].x, cliffs.Lands[0].y);
            float2 coast = default;
            bool found = false;
            for (float r = 0f; r < cliffs.Half * 1.4f && !found; r += 1f)
            {
                float2 p = start + direction * r;
                if (cliffs.CoastAt(p.x, p.y) < 0f)
                {
                    coast = p;
                    found = true;
                }
            }
            Assert.That(found, "the ray reached the sea");
            float2 inland = -direction;
            float Height(MapShape shape, float metres) => shape.Apply(coast.x + inland.x * metres, coast.y + inland.y * metres, Level);

            float sea = cliffs.SeaLevel;
            Assert.That(Height(cliffs, 20f) - sea, Is.GreaterThan(Height(beaches, 20f) - sea + 15f), "a cliff rises within metres of the water");

            // Past the cliff the hills and ranges are the same on both maps, so what the cliff adds is the difference; with
            // the beach's own ground (its rise from the coast and inland) it must only climb, or a lake would pool behind it.
            float previous = float.MinValue, previousLand = float.MinValue;
            int checkedSteps = 0;
            for (float metres = 220f; metres < 1600f; metres += 20f)
            {
                float land = cliffs.CoastAt(coast.x + inland.x * metres, coast.y + inland.y * metres);
                float beachGround = (Level - sea) * (1f - math.exp(-land / 70f)) + cliffs.InlandRise * (1f - math.exp(-land / (cliffs.Half * 0.5f)));
                float ground = beachGround + Height(cliffs, metres) - Height(beaches, metres);
                Assert.That(Height(cliffs, metres), Is.GreaterThanOrEqualTo(Height(beaches, metres) - 0.01f), $"{metres} m inland");
                // Only while the ray goes further inland (it may cross a bay's reach).
                if (land >= previousLand && land > 0f)
                {
                    Assert.That(ground, Is.GreaterThanOrEqualTo(previous - 0.05f), $"{metres} m inland: the ground under the hills only rises");
                    checkedSteps++;
                }
                previous = ground;
                previousLand = land;
            }
            Assert.That(checkedSteps, Is.GreaterThan(20));
        }

        [Test]
        public void CanyonWalls_ClimbInLedges()
        {
            Assert.That(Canyons.Steps(0f, 4), Is.EqualTo(0f));
            Assert.That(Canyons.Steps(1f, 4), Is.EqualTo(1f).Within(1e-5f));
            float previous = 0f;
            for (float t = 0f; t <= 1f; t += 0.01f)
            {
                float v = Canyons.Steps(t, 4);
                Assert.That(v, Is.GreaterThanOrEqualTo(previous - 1e-5f));
                previous = v;
            }
            Assert.That(Canyons.Steps(0.3f, 4), Is.EqualTo(Canyons.Steps(0.26f, 4)).Within(1e-4f), "a level ledge");
            Assert.That(Canyons.Steps(0.49f, 4) - Canyons.Steps(0.42f, 4), Is.GreaterThan(0.15f), "then a steep face");
        }

        [Test]
        public void Canyons_RaiseOnlyDryLand_AwayFromTheWater()
        {
            // A river along x = 0 through an 800 m square of 2 m cells.
            const int size = 400;
            var water = new WaterMap
            {
                Origin = new float2(-400f, -400f), CellSize = 2f, Size = new int2(size, size),
                Kind = new NativeArray<byte>(size * size, Allocator.Persistent),
            };
            for (int z = 0; z < size; z++)
            {
                water.Kind[size / 2 + z * size] = (byte)WaterKind.River;
            }
            Canyons reach = CanyonReach.Build(water);
            try
            {
                Assert.That(reach.ReachAt(1f, 0f), Is.LessThan(3f));
                Assert.That(reach.ReachAt(41f, 0f), Is.EqualTo(40f).Within(3f), "the reach is metres to the water");

                Canyons canyons = new CliffSettings().ToCanyons(5, 1400f);
                canyons.Origin = reach.Origin;
                canyons.CellSize = reach.CellSize;
                canyons.Size = reach.Size;
                canyons.Reach = reach.Reach;

                Assert.That(canyons.Lift(1f, 0f, 0f), Is.Zero, "the river keeps its own ground");
                Assert.That(canyons.Lift(60f, 0f, 1f), Is.Zero, "wet land is left alone");
                float previous = 0f;
                for (float x = 0f; x < 120f; x += 1f)
                {
                    float lift = canyons.Lift(x, 3f, 0f);
                    // The tableland's own height drifts a little with a broad noise (a few cm a metre).
                    Assert.That(lift, Is.GreaterThanOrEqualTo(previous - 0.05f), $"the walls only climb ({x} m)");
                    previous = lift;
                }
                Assert.That(canyons.Lift(80f, 3f, 0f), Is.GreaterThan(canyons.Depth * 0.7f), "the tableland stands over the canyon");
                Assert.That(canyons.Lift(399f, 3f, 0f), Is.LessThan(1f), "and comes down at the grid's edge, which makes no cliff");
            }
            finally
            {
                reach.Dispose();
                water.Kind.Dispose();
            }
        }

        [Test]
        public void Strata_RepeatByHeight()
        {
            var strata = new RockStrata { Depth = 8f };
            strata.Add(sandstone.Id, 2f);
            strata.Add(granite.Id, 1f);
            strata.Add(stone.Id, 0.1f);
            Assert.That(strata.Period, Is.EqualTo(3f), "a band thinner than a voxel is skipped");
            Assert.That(strata.MaterialAt(0f, 0.5f, 0f), Is.EqualTo(sandstone.Id));
            Assert.That(strata.MaterialAt(0f, 2.5f, 0f), Is.EqualTo(granite.Id));
            Assert.That(strata.MaterialAt(0f, 3.5f, 0f), Is.EqualTo(sandstone.Id), "they repeat");
            Assert.That(strata.MaterialAt(0f, -0.5f, 0f), Is.EqualTo(granite.Id), "and go on down");
            for (int i = 0; i < 20; i++)
            {
                strata.Add(stone.Id, 1f);
            }
            Assert.That(strata.Materials.Length, Is.EqualTo(RockStrata.MaxBands));
        }

        [Test]
        public void ALayeredBiome_ShowsTheStrataNearTheSurface_AndStoneDeeper()
        {
            TerrainSettings settings = Settings();
            settings.Map.Cliffs.Strata.Add(new StratumDefinition(sandstone, 1f));
            settings.Map.Cliffs.Strata.Add(new StratumDefinition(granite, 1f));
            var everywhere = new BiomeDefinition(
                "Badlands", new Vector2(-BiomeDefinition.Any, BiomeDefinition.Any), new Vector2(-BiomeDefinition.Any, BiomeDefinition.Any),
                new Vector2(-BiomeDefinition.Any, BiomeDefinition.Any), null, null, 0f, -1f, 0f, 0f, Color.yellow) { LayeredRock = true };
            settings.Map.Biomes.Add(everywhere);
            BoundedMapGenerator map = BoundedMapGenerator.CreateDry(settings);

            // Land near the middle: a 16 m chunk from 12 m under its ground.
            float2 at = new float2(0f, 0f);
            for (float r = 20f; map.Height(at.x, at.y) < map.Layout.SeaLevel + 3f; r += 20f)
            {
                Assert.That(r, Is.LessThan(900f), "land near the middle");
                at = new float2(r, r * 0.5f);
            }
            float ground = Mathf.Floor(map.Height(at.x, at.y));
            var chunk = new Chunk(new Vector3Int(16, 16, 16));
            ChunkGenerator.Fill(chunk, map, new Vector3(Mathf.Floor(at.x), ground - 12f, Mathf.Floor(at.y)), 1f, settings.Layers);

            int banded = 0, deepStone = 0, deepOther = 0;
            for (int z = 0; z <= 16; z++)
            {
                for (int y = 0; y <= 16; y++)
                {
                    for (int x = 0; x <= 16; x++)
                    {
                        float depth = map.Height(Mathf.Floor(at.x) + x, Mathf.Floor(at.y) + z) - (ground - 12f + y);
                        byte material = chunk.GetMaterial(new Vector3Int(x, y, z));
                        if (depth > 4f && depth < 7.5f)
                        {
                            banded += material == sandstone.Id || material == granite.Id ? 1 : 0;
                        }
                        else if (depth > 9f)
                        {
                            deepStone += material == stone.Id ? 1 : 0;
                            deepOther += material != stone.Id ? 1 : 0;
                        }
                    }
                }
            }
            Assert.That(banded, Is.GreaterThan(200), "under the soil, the rock lies in the strata");
            Assert.That(deepStone, Is.GreaterThan(200));
            Assert.That(deepOther, Is.Zero, "deeper down it is the base material, where the ores grow");
        }

        [Test]
        public void TheDesertAndSteppe_AreLayeredByDefault()
        {
            foreach (BiomeDefinition biome in BiomeDefinition.Defaults(sand, dirt, stone))
            {
                bool dry = biome.Name == BiomeDefinition.DesertName || biome.Name == BiomeDefinition.SteppeName;
                Assert.That(biome.LayeredRock, Is.EqualTo(dry), biome.Name);
            }
        }

        [Test]
        public void ACliffFace_IsBareRock_AtAnyHeight()
        {
            var layers = new TerrainLayers { Top = grass, Band = dirt, Base = stone, SteepAngle = 35f, SteepMinHeight = 40f };
            Assert.That(LayerTable.From(layers).IsSteep(3f, 5f), Is.False, "low steep ground keeps its soil without a cliff angle");
            layers.CliffAngle = 58f;
            LayerTable table = LayerTable.From(layers);
            Assert.That(table.IsSteep(3f, 5f), Is.True, "a cliff face under the steep rule's height is rock");
            Assert.That(table.IsSteep(1f, 5f), Is.False, "a 45° bank down low is not");
        }

        [Test]
        public void TheHeightFieldsMoisture_IsTheBiomes()
        {
            TerrainSettings settings = Settings();
            BoundedMapGenerator map = BoundedMapGenerator.CreateDry(settings);
            BiomeField field = map.BiomeField;
            foreach (var p in new[] { new float2(0f, 0f), new float2(310f, -420f), new float2(-900f, 650f) })
            {
                field.Climate(p.x, p.y, 30f, out _, out _, out float moisture);
                Assert.That(BiomeField.MoistureAt(field.Shape, settings.Map.Climate.RegionSize, p.x, p.y), Is.EqualTo(moisture).Within(1e-5f));
            }
        }

        [Test]
        public void TheMapsJobs_AgreeWithItsHeights_WithCliffsAndCanyons()
        {
            TerrainSettings settings = Settings(7);
            settings.Water.Enabled = true;
            settings.Map.Cliffs.CanyonDepth = 30f;
            var generator = (BoundedMapGenerator)TerrainGenerators.Create(settings);
            Assert.That(generator.Ground.Escarpments.IsCreated, Is.True);
            Assert.That(generator.Ground.Canyons.IsCreated, Is.True);

            const int count = 400;
            var points = new NativeArray<float2>(count, Allocator.TempJob);
            var heights = new NativeArray<float>(count, Allocator.TempJob);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    points[i] = new float2((i % 20) * 47f - 470f, (i / 20) * 53f - 530f);
                }
                generator.SchedulePointHeights(points, heights).Complete();
                for (int i = 0; i < count; i++)
                {
                    Assert.That(heights[i], Is.EqualTo(generator.Height(points[i].x, points[i].y)).Within(0.05f), points[i].ToString());
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
