using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>The bounded map (GL589-GL592): its definition, edges, ground, sea, biomes and resource survey.</summary>
    public class BoundedMapTests
    {
        private const float Level = 20f;

        private VoxelMaterial stone;
        private VoxelMaterial dirt;
        private VoxelMaterial grass;
        private VoxelMaterial sand;
        private VoxelMaterial copper;
        private VoxelMaterial wood;
        private VoxelMaterial leaves;

        [OneTimeSetUp]
        public void MakeMaterials()
        {
            stone = VoxelMaterial.Create(1, "Stone");
            dirt = VoxelMaterial.Create(2, "Dirt");
            grass = VoxelMaterial.Create(3, "Grass");
            sand = VoxelMaterial.Create(4, "Sand");
            copper = VoxelMaterial.Create(5, "Copper", VoxelMaterialCategory.Ore);
            wood = VoxelMaterial.Create(6, "Wood");
            leaves = VoxelMaterial.Create(7, "Leaves");
        }

        [OneTimeTearDown]
        public void DestroyMaterials()
        {
            Hydrology.DisposeAll();
            foreach (VoxelMaterial material in new[] { stone, dirt, grass, sand, copper, wood, leaves })
            {
                Object.DestroyImmediate(material);
            }
        }

        private TerrainSettings Settings(int seed, float size, bool water)
        {
            var settings = new TerrainSettings
            {
                Generator = TerrainGeneratorType.BoundedMap, Seed = seed, SurfaceLevel = Level, Amplitude = 40f,
            };
            settings.Layers.Top = grass;
            settings.Layers.TopDepth = 2f;
            settings.Layers.Band = dirt;
            settings.Layers.BandDepth = 4f;
            settings.Layers.Base = stone;
            settings.Water.Sand = sand;
            settings.Water.Enabled = water;
            settings.Map.Size = size;
            settings.Map.PeakHeight = 150f;
            // The tests' plains stand 20 m up; a floor well under them leaves the sea its depth.
            settings.Map.VoxelFloor = -40f;
            return settings;
        }

        private TerrainSettings FourKinds(int seed = 5, float size = 4096f)
        {
            TerrainSettings settings = Settings(seed, size, false);
            settings.Map.SetSides(
                MapBoundaryType.OceanName, MapBoundaryType.DesertName, MapBoundaryType.VoidCliffName, MapBoundaryType.MountainRidgeName);
            return settings;
        }

        private TerrainSettings Island(int seed = 5)
        {
            TerrainSettings settings = Settings(seed, 2048f, true);
            settings.Map.SetShape(MapShapeType.IslandName);
            settings.Map.SetSides(
                MapBoundaryType.OceanName, MapBoundaryType.OceanName, MapBoundaryType.OceanName, MapBoundaryType.OceanName);
            return settings;
        }

        // ---- GL589: the definition ----

        [Test]
        public void Resolve_FromTheSeed_IsRepeatable_AndAlwaysHasAnOcean()
        {
            var map = new MapDefinition();
            var seen = new HashSet<string>();
            for (int seed = 0; seed < 200; seed++)
            {
                MapLayout layout = map.Resolve(seed, Level);
                MapLayout again = map.Resolve(seed, Level);
                Assert.That(layout.OceanSides, Is.InRange(1, 4), $"seed {seed}");
                for (int side = 0; side < 4; side++)
                {
                    Assert.That(again.Boundary((MapSide)side).Name, Is.EqualTo(layout.Boundary((MapSide)side).Name));
                    seen.Add(layout.Boundary((MapSide)side).Name);
                }
            }
            Assert.That(seen.Count, Is.EqualTo(4), "every kind of edge turns up over 200 seeds");
        }

        [Test]
        public void Resolve_ByHand_UsesTheNamedSides_AndTheSizeSet()
        {
            MapLayout layout = FourKinds().Map.Resolve(5, Level);

            Assert.That(layout.Boundary(MapSide.West).Name, Is.EqualTo(MapBoundaryType.OceanName));
            Assert.That(layout.Boundary(MapSide.East).Name, Is.EqualTo(MapBoundaryType.DesertName));
            Assert.That(layout.Boundary(MapSide.South).Name, Is.EqualTo(MapBoundaryType.VoidCliffName));
            Assert.That(layout.Boundary(MapSide.North).Name, Is.EqualTo(MapBoundaryType.MountainRidgeName));
            Assert.That(layout.Size, Is.EqualTo(4096f));
            Assert.That(layout.Bounds, Is.EqualTo(new Rect(-2048f, -2048f, 4096f, 4096f)));
            Assert.That(new MapDefinition().Size, Is.EqualTo(5000f));
        }

        [Test]
        public void Resolve_ByHandWithNoOcean_MakesOneSideAnOcean()
        {
            var map = new MapDefinition();
            map.SetSides(MapBoundaryType.DesertName, MapBoundaryType.DesertName, MapBoundaryType.DesertName, MapBoundaryType.DesertName);

            Assert.That(map.Resolve(3, Level).OceanSides, Is.EqualTo(1));
        }

        // ---- GL590: the kinds of edge ----

        [Test]
        public void BoundaryTypes_ANewEntry_ShapesItsSide()
        {
            var map = new MapDefinition();
            map.BoundaryTypes.Add(new MapBoundaryType("Salt flat", false, 2f, 1f, 300f, 1f, 0f, 0f));
            map.SetSides("Salt flat", MapBoundaryType.OceanName, MapBoundaryType.OceanName, MapBoundaryType.OceanName);
            MapLayout layout = map.Resolve(1, Level);
            float x = -layout.Size * 0.5f - 200f;

            Assert.That(layout.Boundary(MapSide.West).Name, Is.EqualTo("Salt flat"));
            Assert.That(layout.BoundaryAt(x, 0f).Name, Is.EqualTo("Salt flat"));
            // Past its edge the land is its flat: 2 m over the plains there, whatever hills the endless land has.
            float flat = layout.Shape.Apply(x, 0f, Level, out float plains);
            Assert.That(flat, Is.EqualTo(plains).Within(0.05f));
            Assert.That(layout.Shape.Apply(x, 0f, Level + 57f), Is.EqualTo(flat).Within(0.05f));
            Assert.That(flat, Is.GreaterThan(layout.SeaLevel + 2f));
        }

        [Test]
        public void EachKindOfEdge_GoesOnPastIt()
        {
            TerrainSettings settings = FourKinds();
            var map = (BoundedMapGenerator)TerrainGenerators.Create(settings);
            float half = map.Layout.Size * 0.5f, sea = map.Layout.SeaLevel;

            foreach (float beyond in new[] { 400f, 3000f, 20000f })
            {
                Assert.That(map.Height(-half - beyond, 0f), Is.LessThan(sea - 15f), "the ocean's floor");
                Assert.That(map.Height(half + beyond, 0f), Is.InRange(sea + 1f, sea + settings.Map.InlandRise + 30f), "the desert's plain");
                Assert.That(map.Height(0f, -half - beyond), Is.LessThan(sea - 500f), "the void");
            }
            // The larger range: crests and valleys, tall on the whole and nowhere down at the plains.
            float sum = 0f;
            int count = 0;
            foreach (float beyond in new[] { 1500f, 3000f, 20000f })
            {
                for (float x = -half * 0.5f; x <= half * 0.5f; x += half * 0.125f, count++)
                {
                    float h = map.Height(x, half + beyond);
                    Assert.That(h, Is.GreaterThan(sea + 60f), $"the range at {x}, {beyond} past the edge");
                    sum += h - sea;
                }
            }
            Assert.That(sum / count, Is.GreaterThan(280f), "the range's mean height");
        }

        [Test]
        public void Corners_BlendTwoKinds_WithoutAStep()
        {
            // Ocean, desert and ridge corners (the void is a cliff by design), on level land so only the shape shows.
            var map = new MapDefinition();
            map.SetSides(MapBoundaryType.OceanName, MapBoundaryType.DesertName, MapBoundaryType.OceanName, MapBoundaryType.MountainRidgeName);
            MapShape shape = map.Resolve(9, Level).Shape;
            float half = shape.Half;

            foreach (Vector2 corner in new[] { new Vector2(-half, half), new Vector2(half, half), new Vector2(half, -half), new Vector2(-half, -half) })
            {
                // Circles round the corner cross both sides' zones and the diagonal between them.
                foreach (float radius in new[] { 100f, 400f, 900f })
                {
                    float last = float.NaN;
                    int steps = Mathf.CeilToInt(2f * Mathf.PI * radius);
                    for (int i = 0; i <= steps; i++)
                    {
                        float angle = 2f * Mathf.PI * i / steps;
                        float h = shape.Apply(corner.x + Mathf.Cos(angle) * radius, corner.y + Mathf.Sin(angle) * radius, Level);
                        if (i > 0)
                        {
                            Assert.That(Mathf.Abs(h - last), Is.LessThan(8f), $"corner {corner}, radius {radius}, step {i}");
                        }
                        last = h;
                    }
                }
            }
        }

        // ---- GL591: the ground ----

        [Test]
        public void WithNoShape_TheGroundIsTheErodedLandscape()
        {
            var land = new LandscapeHeight(4, Level, 60f, 250f, 0.5f).WithSmoothMassifs();
            ErosionFilter erosion = new ErosionSettings().ToFilter(4);
            var eroded = new ErodedLandscapeHeight(land, erosion, Level, 60f);
            var bounded = new BoundedMapHeight(land, erosion, default, Level, 60f);

            for (int i = 0; i < 200; i++)
            {
                float x = i * 37.3f - 2000f, z = i * -21.7f + 900f;
                Assert.That(bounded.Height(x, z), Is.EqualTo(eroded.Height(x, z)).Within(1e-4f));
                Assert.That(bounded.Valley(x, z), Is.EqualTo(eroded.Valley(x, z)).Within(1e-4f));
            }
        }

        [Test]
        public void PointHeightsJob_MatchesTheManagedHeights()
        {
            var map = (BoundedMapGenerator)TerrainGenerators.Create(FourKinds());
            var points = new NativeArray<float2>(256, Allocator.TempJob);
            var heights = new NativeArray<float>(256, Allocator.TempJob);
            try
            {
                for (int i = 0; i < points.Length; i++)
                {
                    points[i] = new float2((i % 16 - 7.5f) * 330f, (i / 16 - 7.5f) * 330f);
                }
                map.SchedulePointHeights(points, heights).Complete();
                // Burst's exp, log and pow round a hair differently from Mono's, and on the larger range's steep wall the
                // erosion turns that into centimetres: the two agree closely on the whole, and nowhere by more than a hand.
                float total = 0f;
                for (int i = 0; i < points.Length; i++)
                {
                    float managed = map.Height(points[i].x, points[i].y);
                    Assert.That(heights[i], Is.EqualTo(managed).Within(0.2f), $"point {points[i]}");
                    total += Mathf.Abs(heights[i] - managed);
                }
                Assert.That(total / points.Length, Is.LessThan(0.01f), "mean difference");
            }
            finally
            {
                points.Dispose();
                heights.Dispose();
            }
        }

        [Test]
        public void TheGreatPeak_IsTheHighestGroundOfAnIsland()
        {
            TerrainSettings settings = Island();
            settings.Water.Enabled = false;
            var map = (BoundedMapGenerator)TerrainGenerators.Create(settings);
            float half = map.Layout.Size * 0.5f;
            Vector2 highest = default;
            float top = float.NegativeInfinity;
            for (float z = -half; z <= half; z += 32f)
            {
                for (float x = -half; x <= half; x += 32f)
                {
                    float h = map.Height(x, z);
                    if (h > top)
                    {
                        top = h;
                        highest = new Vector2(x, z);
                    }
                }
            }

            Assert.That(top, Is.GreaterThan(map.Layout.SeaLevel + 80f));
            Assert.That(Vector2.Distance(highest, map.Layout.PeakPosition), Is.LessThan(settings.Map.PeakRadius));
        }

        [Test]
        public void TheSea_SurroundsAnIsland_GoesOnForever_AndRiversReachIt()
        {
            var map = (BoundedMapGenerator)TerrainGenerators.Create(Island());
            WaterMap water = map.Water;
            float half = map.Layout.Size * 0.5f;

            Assert.That(water.HasSea, Is.True);
            foreach (var point in new[] { new float2(-half, 0f), new float2(half, 0f), new float2(0f, -half), new float2(-30000f, 12000f) })
            {
                Assert.That(water.TrySurfaceAt(point, out float surface, out int cell), Is.True, $"sea at {point}");
                Assert.That(water.IsSea(cell), Is.True);
                Assert.That(surface, Is.EqualTo(map.Layout.SeaLevel).Within(0.01f));
                Assert.That(map.Height(point.x, point.y), Is.LessThan(surface));
            }
            Assert.That(water.TryCell(new float2(map.Layout.PeakPosition.x, map.Layout.PeakPosition.y), out int peak) && water.Kind[peak] != 0, Is.False);

            // A river cell with the sea within a few cells of it: a mouth.
            int rivers = 0, mouths = 0;
            int size = water.Size.x;
            for (int z = 3; z < size - 3; z++)
            {
                for (int x = 3; x < size - 3; x++)
                {
                    if (water.Kind[x + z * size] != (byte)WaterKind.River)
                    {
                        continue;
                    }
                    rivers++;
                    bool mouth = false;
                    for (int dz = -3; dz <= 3 && !mouth; dz++)
                    {
                        for (int dx = -3; dx <= 3 && !mouth; dx++)
                        {
                            mouth = water.IsSea(x + dx + (z + dz) * size);
                        }
                    }
                    mouths += mouth ? 1 : 0;
                }
            }
            Assert.That(rivers, Is.GreaterThan(0), "river cells");
            Assert.That(mouths, Is.GreaterThan(0), "river cells by the sea");
        }

        // ---- GL591: biomes ----

        [Test]
        public void Biomes_FollowElevationAndTheEdges()
        {
            TerrainSettings settings = FourKinds();
            var map = (BoundedMapGenerator)TerrainGenerators.Create(settings);
            BiomeMap biomes = BiomeMap.For(map);
            float half = map.Layout.Size * 0.5f, sea = map.Layout.SeaLevel;

            Assert.That(BiomeMap.For(new FlatGenerator(1f)), Is.Null);
            Assert.That(biomes.Biomes[biomes.IndexAt(0f, 0f, sea - 5f)].Name, Is.EqualTo(BiomeDefinition.OceanName));
            Assert.That(biomes.Biomes[biomes.IndexAt(0f, 0f, sea + 0.5f)].Name, Is.EqualTo(BiomeDefinition.BeachName));
            Assert.That(biomes.Biomes[biomes.IndexAt(0f, 0f, sea + 600f)].Name, Is.EqualTo(BiomeDefinition.AlpineName));
            Assert.That(biomes.At(-half - 500f, 0f).Name, Is.EqualTo(BiomeDefinition.OceanName));
            for (float z = -half * 0.5f; z <= half * 0.5f; z += 100f)
            {
                Assert.That(biomes.At(new Vector3(half + 600f, 0f, z)).Name, Is.EqualTo(BiomeDefinition.DesertName), $"z {z}");
            }
        }

        [Test]
        public void Biomes_AreVariedAcrossADefaultMap()
        {
            TerrainSettings settings = Settings(21, 5000f, false);
            settings.Map.PeakHeight = 320f;
            var map = (BoundedMapGenerator)TerrainGenerators.Create(settings);
            var seen = new HashSet<string>();
            for (float z = -2500f; z <= 2500f; z += 125f)
            {
                for (float x = -2500f; x <= 2500f; x += 125f)
                {
                    seen.Add(map.Biomes.At(x, z).Name);
                }
            }

            Assert.That(seen.Count, Is.GreaterThanOrEqualTo(5), string.Join(", ", seen));
            Assert.That(seen, Does.Contain(BiomeDefinition.AlpineName), "the peak is cold enough for bare rock");
        }

        [Test]
        public void TheFill_LaysEachBiomesSurface()
        {
            TerrainSettings settings = FourKinds();
            var map = (BoundedMapGenerator)TerrainGenerators.Create(settings);
            var world = new World(new Vector3Int(8, 8, 8), 1f, samples => new ByteVoxelStorage(samples));
            float half = map.Layout.Size * 0.5f;

            // In the desert past the east edge the surface is sand; well inland it is the biome's own top.
            foreach (Vector2 column in new[] { new Vector2(half + 603f, 11f), new Vector2(half + 1500f, -205f), new Vector2(100f, 300f), new Vector2(-400f, -350f) })
            {
                int x = Mathf.FloorToInt(column.x), z = Mathf.FloorToInt(column.y);
                float ground = map.Height(x, z);
                int y = Mathf.FloorToInt(ground - 0.05f);
                var coord = new Vector3Int(Mathf.FloorToInt(x / 8f), Mathf.FloorToInt(y / 8f), Mathf.FloorToInt(z / 8f));
                Chunk chunk = world.Load(coord, map, settings.Layers);
                byte laid = chunk.GetMaterial(new Vector3Int(x - coord.x * 8, y - coord.y * 8, z - coord.z * 8));

                BiomeDefinition biome = map.Biomes.At(x, z);
                byte expected = (biome.Top != null ? biome.Top : grass).Id;
                if (laid == stone.Id)
                {
                    continue; // A steep column is bare rock in any biome.
                }
                Assert.That(laid, Is.EqualTo(expected), $"{biome.Name} at {column}");
            }
            Assert.That(map.Biomes.At(half + 603f, 11f).Top, Is.SameAs(sand));
        }

        [Test]
        public void Trees_FollowTheBiomes_OnlyOnABoundedMap()
        {
            TerrainSettings settings = FourKinds();
            settings.Trees.Chance = 0.5f;
            settings.Trees.Wood = wood;
            settings.Trees.Leaves = leaves;
            var map = (BoundedMapGenerator)TerrainGenerators.Create(settings);
            float half = map.Layout.Size * 0.5f;
            float cell = settings.Trees.CellSize;

            Assert.That(settings.Trees.Climate, Is.SameAs(map.Biomes));
            var desert = new Vector2Int(Mathf.FloorToInt((half + 600f) / cell), 0);
            Assert.That(TreeForests.ChanceIn(settings.Seed, desert, settings.Trees), Is.Zero);
            float thickest = 0f;
            for (int i = -40; i <= 40; i++)
            {
                thickest = Mathf.Max(thickest, TreeForests.ChanceIn(settings.Seed, new Vector2Int(i * 3, i * 2), settings.Trees));
            }
            Assert.That(thickest, Is.GreaterThan(0f));

            settings.Generator = TerrainGeneratorType.Flat;
            TerrainGenerators.Create(settings);
            Assert.That(settings.Trees.Climate, Is.Null);
            Assert.That(TreeForests.ChanceIn(settings.Seed, desert, settings.Trees), Is.EqualTo(0.5f));
        }

        // ---- GL592: the survey ----

        private TerrainSettings Stocked()
        {
            TerrainSettings settings = Island();
            settings.Trees.Chance = 0.5f;
            settings.Trees.Wood = wood;
            settings.Trees.Leaves = leaves;
            settings.Ores.Ores.Add(new OreSpec
            {
                Ore = copper, NodesPerCell = 1.5f, MinDepth = 1f, MaxDepth = 14f, PeakProbability = 0.9f, Spread = new Vector3(1.5f, 1f, 1.5f),
            });
            return settings;
        }

        [Test]
        public void Survey_CoversTheMap_AndIsTheSameEveryTime()
        {
            TerrainSettings settings = Stocked();
            ResourceSurvey survey = ResourceSurvey.Build(settings, 40f);
            ResourceSurvey again = ResourceSurvey.Build(settings, 40f);

            Assert.That(survey.Size, Is.EqualTo(52));
            Assert.That(survey.Area, Is.EqualTo(new Rect(-1024f, -1024f, 2048f, 2048f)));
            Assert.That(survey.Ores, Is.EqualTo(new[] { copper }));
            int coast = 0, river = 0, land = 0;
            float woodTotal = 0f, oreTotal = 0f;
            var biomes = new HashSet<int>();
            for (int z = 0; z < survey.Size; z++)
            {
                for (int x = 0; x < survey.Size; x++)
                {
                    SurveyCell cell = survey.Cell(x, z), other = again.Cell(x, z);
                    Assert.That(cell.Wood, Is.EqualTo(other.Wood));
                    Assert.That(cell.Height, Is.EqualTo(other.Height));
                    Assert.That(cell.Biome, Is.EqualTo(other.Biome));
                    Assert.That(survey.Ore(0, x, z), Is.EqualTo(again.Ore(copper, x, z)));
                    Assert.That(cell.Land + cell.Sea + cell.FreshWater, Is.EqualTo(cell.Outside ? cell.Sea : 1f).Within(1e-4f));
                    Assert.That(cell.Flatness, Is.InRange(0f, 1f));
                    coast += cell.Coast ? 1 : 0;
                    river += cell.River ? 1 : 0;
                    land += cell.Land > 0.5f ? 1 : 0;
                    woodTotal += cell.Wood;
                    oreTotal += survey.Ore(0, x, z);
                    biomes.Add(cell.Biome);
                }
            }
            SurveyCell corner = survey.Cell(0, 0);
            Assert.That(corner.Sea, Is.EqualTo(1f));
            Assert.That(survey.Biomes[corner.Biome].Name, Is.EqualTo(BiomeDefinition.OceanName));
            Assert.That(corner.Wood, Is.Zero);
            Assert.That(survey.TryCell(survey.CellCentre(7, 9), out Vector2Int found) && found == new Vector2Int(7, 9), Is.True);
            Assert.That(survey.TryCell(new Vector2(2000f, 0f), out _), Is.False);
            Assert.That(land, Is.GreaterThan(100), "land cells");
            Assert.That(coast, Is.GreaterThan(10), "coast cells");
            Assert.That(river, Is.GreaterThan(0), "river cells");
            Assert.That(woodTotal, Is.GreaterThan(0f));
            Assert.That(oreTotal, Is.GreaterThan(0f));
            Assert.That(biomes.Count, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void Survey_AgreesWithTheOreFieldAndTheTrees()
        {
            TerrainSettings settings = Stocked();
            ITerrainGenerator generator = TerrainGenerators.Create(settings);
            OreField ores = OreField.Create(settings, generator);
            TreeField trees = TreeField.Create(settings, generator);
            ResourceSurvey survey = ResourceSurvey.Build(settings, generator, ores, trees, 40f);

            // A block of cells in the middle of the island (the survey's cell 0 starts at -1024 = 16 * -64 = 10 * -102.4).
            const int from = 22, to = 30;
            float surveyedOre = 0f, surveyedWood = 0f;
            for (int z = from; z < to; z++)
            {
                for (int x = from; x < to; x++)
                {
                    surveyedOre += survey.Ore(0, x, z);
                    surveyedWood += survey.Cell(x, z).Wood;
                }
            }
            var block = Rect.MinMaxRect(-1024f + from * 40f, -1024f + from * 40f, -1024f + to * 40f, -1024f + to * 40f);

            // The ore field's own nodes, placed one by one, in every ore cell the block touches.
            int placed = 0;
            float cell = ores.CellSize;
            for (int cz = Mathf.FloorToInt(block.yMin / cell); cz <= Mathf.FloorToInt(block.yMax / cell); cz++)
            {
                for (int cx = Mathf.FloorToInt(block.xMin / cell); cx <= Mathf.FloorToInt(block.xMax / cell); cx++)
                {
                    for (int cy = -4; cy <= 16; cy++)
                    {
                        foreach (OreNode node in ores.NodesInCellUncached(new Vector3Int(cx, cy, cz)))
                        {
                            placed += block.Contains(new Vector2(node.Centre.x, node.Centre.z)) ? 1 : 0;
                        }
                    }
                }
            }
            Assert.That(placed, Is.GreaterThan(20), "the block holds ore");
            Assert.That(surveyedOre, Is.EqualTo(placed).Within(placed * 0.03f + 1f));

            // The trees the chunks would grow there.
            int grown = 0;
            float treeCell = trees.CellSize;
            for (int tz = Mathf.FloorToInt(block.yMin / treeCell); tz <= Mathf.FloorToInt(block.yMax / treeCell); tz++)
            {
                for (int tx = Mathf.FloorToInt(block.xMin / treeCell); tx <= Mathf.FloorToInt(block.xMax / treeCell); tx++)
                {
                    if (trees.TryGetTree(new Vector2Int(tx, tz), out Vector3 root, out _) && block.Contains(new Vector2(root.x, root.z)))
                    {
                        grown++;
                    }
                }
            }
            Assert.That(grown, Is.GreaterThan(10), "the block holds trees");
            Assert.That(surveyedWood, Is.EqualTo(grown).Within(grown * 0.15f + 3f));
        }

        [Test]
        public void Sea_StaysBesideAVoidCliff()
        {
            // The sea used to drain into the low dry ground under a neighbouring void cliff (found with GL595: no coast at all).
            TerrainSettings settings = Settings(5, 2048f, true);
            settings.Map.SetSides(MapBoundaryType.OceanName, MapBoundaryType.OceanName, MapBoundaryType.VoidCliffName, MapBoundaryType.OceanName);
            WaterMap water = ((BoundedMapGenerator)TerrainGenerators.Create(settings)).Water;

            foreach (var point in new[] { new float2(1010f, 0f), new float2(-1010f, 0f), new float2(0f, 1010f), new float2(1010f, 300f) })
            {
                Assert.That(water.TryCell(point, out int cell) && water.IsSea(cell), $"sea at {point}");
            }
        }

        [Test]
        public void Survey_NeedsABoundedMap()
        {
            var settings = new TerrainSettings { Generator = TerrainGeneratorType.Flat };

            Assert.Throws<System.ArgumentException>(() => ResourceSurvey.Build(settings));
        }

        [Test]
        public void TheDryMapHasTheFinishedMapsGround()
        {
            // GL636: the preview's land is the game's land; only the water is left out.
            TerrainSettings settings = Island();
            BoundedMapGenerator dry = BoundedMapGenerator.CreateDry(settings);
            BoundedMapGenerator wet = BoundedMapGenerator.Create(settings);
            Assert.IsFalse(dry.Water.IsCreated);
            foreach (var p in new[] { new Vector2(0f, 0f), new Vector2(310f, -420f), new Vector2(-900f, 650f) })
            {
                Assert.AreEqual(wet.Height(p.x, p.y), dry.Height(p.x, p.y), 1e-3f);
            }
        }

        [Test]
        public void ContoursFollowTheHeights()
        {
            // A ramp rising 1 m a pixel to the east, from 10 m under the sea: lines every 10 m.
            const int size = 64;
            var heights = new float[size * size];
            for (int i = 0; i < heights.Length; i++)
            {
                heights[i] = i % size - 10f;
            }
            Color32[] pixels = MapContours.Render(heights, size, 0f, 10f);
            int row = 20 * size;
            Assert.AreEqual(MapContours.SeaWash, pixels[row + 4], "under the sea: the wash, no lines");
            Assert.AreEqual(MapContours.ShoreInk, pixels[row + 10], "the shore");
            Assert.AreEqual(MapContours.Paper, pixels[row + 15], "between lines");
            Assert.AreNotEqual(MapContours.Paper, pixels[row + 20], "the 10 m line");
            Assert.AreEqual(MapContours.Ink, pixels[row + 50], "the 40 m line is an index line, full ink");
            Assert.AreEqual(MapContours.Paper, pixels[row + 45]);
        }

        [Test]
        public void ContoursChangeWithTheSeedAndShowAnIslandsSea()
        {
            const int size = 96;
            Color32[] first = MapContours.Render(BoundedMapGenerator.CreateDry(Island(5)), size, margin: 0.1f);
            Color32[] again = MapContours.Render(BoundedMapGenerator.CreateDry(Island(5)), size, margin: 0.1f);
            Color32[] other = MapContours.Render(BoundedMapGenerator.CreateDry(Island(6)), size, margin: 0.1f);
            CollectionAssert.AreEqual(first, again);
            CollectionAssert.AreNotEqual(first, other);
            // An island: sea in every corner, land with lines in the middle.
            foreach (int corner in new[] { 0, size - 1, size * (size - 1), size * size - 1 })
            {
                Assert.AreEqual(MapContours.SeaWash, first[corner]);
            }
            int lined = 0;
            foreach (Color32 pixel in first)
            {
                if (!pixel.Equals(MapContours.Paper) && !pixel.Equals(MapContours.SeaWash))
                {
                    lined++;
                }
            }
            Assert.Greater(lined, size * 4);
        }
    }
}
