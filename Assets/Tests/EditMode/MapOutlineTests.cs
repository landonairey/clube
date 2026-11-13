using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// The bounded map's natural land and organic edges (GL637-GL640): the outlines, the wandering playable edge, the
    /// sides' hold on the land, the ranges, and the limit that ends the playable area.
    /// </summary>
    public class MapOutlineTests
    {
        private const float Level = 36f;
        private const float Size = 4096f;

        private static TerrainSettings Settings(int seed)
        {
            var settings = new TerrainSettings
            {
                Generator = TerrainGeneratorType.BoundedMap, Seed = seed, SurfaceLevel = Level, Amplitude = 110f,
            };
            settings.Water.Enabled = false;
            settings.Map.Size = Size;
            return settings;
        }

        private static TerrainSettings FourKinds(int seed = 5)
        {
            TerrainSettings settings = Settings(seed);
            settings.Map.SetSides(
                MapBoundaryType.OceanName, MapBoundaryType.DesertName, MapBoundaryType.VoidCliffName, MapBoundaryType.MountainRidgeName);
            return settings;
        }

        // The point on a ray from the middle where the playable edge is crossed.
        private static Vector2 EdgePoint(MapShape shape, float angle)
        {
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float near = 0f, far = shape.Half * 1.5f;
            for (int i = 0; i < 40; i++)
            {
                float middle = (near + far) * 0.5f;
                if (shape.EdgeInside(direction.x * middle, direction.y * middle) > 0f)
                {
                    near = middle;
                }
                else
                {
                    far = middle;
                }
            }
            return direction * near;
        }

        // ---- GL638: the outlines ----

        [Test]
        public void EveryOutline_TurnsUp_WithAsManySeaSidesAsItWants()
        {
            var map = new MapDefinition();
            var seen = new HashSet<string>();
            for (int seed = 0; seed < 200; seed++)
            {
                MapLayout layout = map.Resolve(seed, Level);
                seen.Add(layout.ShapeType.Name);
                Vector2Int wanted = layout.ShapeType.OceanSides;
                Assert.That(layout.OceanSides, Is.InRange(wanted.x, wanted.y), $"seed {seed}, {layout.ShapeType.Name}");
                Assert.That(map.Resolve(seed, Level).ShapeType, Is.SameAs(layout.ShapeType));
            }
            Assert.That(seen.Count, Is.EqualTo(map.ShapeTypes.Count), string.Join(", ", seen));
        }

        [Test]
        public void ANewOutline_IsAnEntry_AndSidesSetByHandGetOneThatSuitsThem()
        {
            var map = new MapDefinition();
            map.ShapeTypes.Add(new MapShapeType(
                "Atoll", 1f, new Vector2Int(4, 4), new Vector2(0.05f, 0.1f), new Vector2Int(1, 1), new Vector2(0.3f, 0.3f), 0.1f,
                Vector2Int.zero, Vector2Int.zero, new Vector2(0.04f, 0.05f), new Vector2Int(1, 1), 1f, Vector2Int.zero));
            map.SetShape("Atoll");
            Assert.That(map.Resolve(3, Level).ShapeType.Name, Is.EqualTo("Atoll"));

            // One sea side by hand: only the continent wants so few.
            var byHand = new MapDefinition();
            byHand.SetSides(MapBoundaryType.OceanName, MapBoundaryType.DesertName, MapBoundaryType.DesertName, MapBoundaryType.MountainRidgeName);
            for (int seed = 0; seed < 20; seed++)
            {
                Assert.That(byHand.Resolve(seed, Level).ShapeType.Name, Is.EqualTo(MapShapeType.ContinentName));
            }
        }

        [Test]
        public void Outlines_GiveDifferentLands()
        {
            // Land inside the playable edge on a coarse grid: its share, and how many separate pieces it is in.
            void Measure(string shape, int seed, out float share, out int pieces)
            {
                TerrainSettings settings = Settings(seed);
                settings.Map.SetShape(shape);
                if (shape == MapShapeType.ContinentName)
                {
                    settings.Map.SetSides(MapBoundaryType.OceanName, MapBoundaryType.MountainRidgeName, MapBoundaryType.OceanName, MapBoundaryType.DesertName);
                }
                else
                {
                    settings.Map.SetSides(MapBoundaryType.OceanName, MapBoundaryType.OceanName, MapBoundaryType.OceanName, MapBoundaryType.OceanName);
                }
                BoundedMapGenerator map = BoundedMapGenerator.CreateDry(settings);
                const int n = 72;
                float step = Size / n;
                var land = new bool[n * n];
                int inside = 0, dry = 0;
                for (int i = 0; i < n * n; i++)
                {
                    float x = -Size * 0.5f + (i % n + 0.5f) * step, z = -Size * 0.5f + (i / n + 0.5f) * step;
                    if (!map.Layout.Contains(x, z))
                    {
                        continue;
                    }
                    inside++;
                    land[i] = map.Height(x, z) > map.Layout.SeaLevel;
                    dry += land[i] ? 1 : 0;
                }
                share = dry / (float)inside;
                pieces = 0;
                var open = new Stack<int>();
                for (int i = 0; i < n * n; i++)
                {
                    if (!land[i])
                    {
                        continue;
                    }
                    int cells = 0;
                    land[i] = false;
                    open.Push(i);
                    while (open.Count > 0)
                    {
                        int cell = open.Pop();
                        cells++;
                        int cx = cell % n, cz = cell / n;
                        for (int d = 0; d < 4; d++)
                        {
                            int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0), nz = cz + (d == 2 ? 1 : d == 3 ? -1 : 0);
                            if (nx >= 0 && nz >= 0 && nx < n && nz < n && land[nx + nz * n])
                            {
                                land[nx + nz * n] = false;
                                open.Push(nx + nz * n);
                            }
                        }
                    }
                    // A piece of a few cells is a rock, not an island.
                    pieces += cells >= 4 ? 1 : 0;
                }
            }

            float continents = 0f, archipelagos = 0f, islands = 0f;
            int archipelagoPieces = 0;
            const int seeds = 4;
            for (int seed = 1; seed <= seeds; seed++)
            {
                Measure(MapShapeType.ContinentName, seed, out float continent, out _);
                Measure(MapShapeType.ArchipelagoName, seed, out float archipelago, out int pieces);
                Measure(MapShapeType.IslandName, seed, out float island, out _);
                continents += continent / seeds;
                archipelagos += archipelago / seeds;
                islands += island / seeds;
                archipelagoPieces += pieces;
                Assert.That(island, Is.InRange(0.12f, 0.8f), $"seed {seed}: an island has sea all round it");
            }
            Assert.That(continents, Is.GreaterThan(0.5f), "a continent is mostly land");
            Assert.That(continents, Is.GreaterThan(islands + 0.1f));
            Assert.That(archipelagos, Is.InRange(0.1f, 0.65f));
            Assert.That(archipelagoPieces, Is.GreaterThanOrEqualTo(seeds * 3), "an archipelago is many islands");
        }

        // ---- GL639: the edge and the sides ----

        [Test]
        public void ThePlayableEdge_Wanders_AndStaysInsideTheSquare()
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                MapShape shape = new MapDefinition { Size = Size }.Resolve(seed, Level).Shape;
                // Along the middle half of the east side a straight edge would hold one x; this one wanders.
                float least = float.MaxValue, most = float.MinValue;
                for (int i = 0; i < 720; i++)
                {
                    Vector2 point = EdgePoint(shape, i * Mathf.PI / 360f);
                    Assert.That(Mathf.Max(Mathf.Abs(point.x), Mathf.Abs(point.y)), Is.LessThanOrEqualTo(shape.Half), $"seed {seed}");
                    Assert.That(Mathf.Abs(shape.EdgeInside(point.x, point.y)), Is.LessThan(0.5f));
                    if (point.x > 0f && Mathf.Abs(point.y) < shape.Half * 0.5f)
                    {
                        least = Mathf.Min(least, point.x);
                        most = Mathf.Max(most, point.x);
                    }
                }
                Assert.That(most - least, Is.GreaterThan(60f), $"seed {seed}: the east edge is no straight line");
                Assert.That(shape.Half - least, Is.LessThan(shape.Half * 0.3f), "and the map is not eaten away by it");
            }
        }

        [Test]
        public void SeaSides_KeepTheLandOffTheEdge_AndTheOthersKeepLandAtIt()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                BoundedMapGenerator map = BoundedMapGenerator.CreateDry(FourKinds(seed));
                MapShape shape = map.Layout.Shape;
                float sea = map.Layout.SeaLevel;
                for (int i = 0; i < 360; i++)
                {
                    Vector2 point = EdgePoint(shape, i * Mathf.PI / 180f);
                    float4 weights = shape.SideWeights(point.x, point.y);
                    string where = $"seed {seed}, edge point {point}";
                    if (weights[(int)MapSide.West] > 0.97f)
                    {
                        Assert.That(map.Height(point.x, point.y), Is.LessThan(sea), where + ": sea at an ocean's edge");
                        Assert.That(shape.CoastAt(point.x, point.y), Is.LessThan(-100f), where);
                    }
                    else if (weights[(int)MapSide.East] > 0.97f || weights[(int)MapSide.North] > 0.97f)
                    {
                        Assert.That(map.Height(point.x, point.y), Is.GreaterThan(sea), where + ": land at a desert's or a range's edge");
                    }
                }
                Assert.That(map.Layout.BoundaryAt(-shape.Half, 0f).Name, Is.EqualTo(MapBoundaryType.OceanName));
                Assert.That(map.Layout.BoundaryAt(0f, shape.Half).Name, Is.EqualTo(MapBoundaryType.MountainRidgeName));
            }
        }

        [Test]
        public void SideWeights_ShareEveryPoint_AndTheirBordersWander()
        {
            MapShape shape = new MapDefinition { Size = Size }.Resolve(4, Level).Shape;
            // Along a line well outside the map, north of east: the border between the two sides is crossed once, off the diagonal.
            float crossing = float.NaN;
            for (float t = 0.2f; t <= 1.8f; t += 0.002f)
            {
                float x = shape.Half * 1.2f * (2f - t), z = shape.Half * 1.2f * t;
                float4 weights = shape.SideWeights(x, z);
                Assert.That(math.csum(weights), Is.EqualTo(1f).Within(1e-4f));
                Assert.That(math.cmin(weights), Is.GreaterThanOrEqualTo(0f));
                if (float.IsNaN(crossing) && weights[(int)MapSide.North] > weights[(int)MapSide.East])
                {
                    crossing = t;
                }
            }
            Assert.That(crossing, Is.InRange(0.5f, 1.5f));
            Assert.That(Mathf.Abs(crossing - 1f), Is.GreaterThan(0.004f), "not on the square's own diagonal");
        }

        // ---- GL637: the ranges ----

        [Test]
        public void TheSummit_IsOnARange_NotACone()
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                MapLayout layout = new MapDefinition { Size = Size }.Resolve(seed, Level);
                MapShape shape = layout.Shape;
                Vector2 summit = layout.PeakPosition;
                Assert.That(layout.Contains(summit.x, summit.y), Is.True, $"seed {seed}: the summit stands inside the edge");
                Assert.That(shape.RangeAt(summit.x, summit.y), Is.EqualTo(shape.PeakHeight).Within(shape.PeakHeight * 0.08f), $"seed {seed}");

                // Round the summit the range is far higher along its crest than across it.
                float least = float.MaxValue, most = float.MinValue;
                for (int i = 0; i < 36; i++)
                {
                    float angle = i * Mathf.PI / 18f;
                    float h = shape.RangeAt(summit.x + Mathf.Cos(angle) * shape.PeakRadius * 0.7f, summit.y + Mathf.Sin(angle) * shape.PeakRadius * 0.7f);
                    least = Mathf.Min(least, h);
                    most = Mathf.Max(most, h);
                }
                Assert.That(most, Is.GreaterThan(least * 1.6f + 10f), $"seed {seed}: a crest runs through the summit");
            }
        }

        [Test]
        public void ARange_CutsAcrossTheMap_AndGoesOnPastItsSide()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                MapLayout layout = FourKinds(seed).Map.Resolve(seed, Level);
                MapShape shape = layout.Shape;
                // Walking out past the north edge (the range's side) the range is met: its crest leaves the map there.
                float past = 0f, within = 0f;
                for (float x = -shape.Half; x <= shape.Half; x += 40f)
                {
                    past = Mathf.Max(past, shape.RangeAt(x, shape.Half * 1.15f));
                    for (float z = -shape.Half * 0.5f; z <= shape.Half * 0.5f; z += 40f)
                    {
                        within = Mathf.Max(within, shape.RangeAt(x, z));
                    }
                }
                Assert.That(past, Is.GreaterThan(shape.PeakHeight * 0.12f), $"seed {seed}: the range goes on past the north edge");
                Assert.That(within, Is.GreaterThan(shape.PeakHeight * 0.6f), $"seed {seed}: and stands in the middle of the map");
            }
        }

        // ---- GL640: the limit ----

        [Test]
        public void ReachableGround_StaysInsideTheVoxelWorld()
        {
            foreach (int seed in new[] { 2, 5, 9 })
            {
                TerrainSettings settings = seed == 5 ? FourKinds(seed) : Settings(seed);
                BoundedMapGenerator map = BoundedMapGenerator.CreateDry(settings);
                MapShape shape = map.Layout.Shape;
                int reached = 0, beyond = 0;
                float lowestBeyond = float.MaxValue, highestBeyond = float.MinValue;
                for (float z = -Size * 0.62f; z <= Size * 0.62f; z += 37f)
                {
                    for (float x = -Size * 0.62f; x <= Size * 0.62f; x += 37f)
                    {
                        float h = map.Height(x, z);
                        if (shape.LimitAt(x, z) < 1f)
                        {
                            reached++;
                            Assert.That(h, Is.InRange(shape.Floor - 0.5f, shape.Ceiling + 0.5f), $"seed {seed} at ({x}, {z})");
                        }
                        else
                        {
                            beyond++;
                            lowestBeyond = Mathf.Min(lowestBeyond, h);
                            highestBeyond = Mathf.Max(highestBeyond, h);
                        }
                    }
                }
                Assert.That(reached, Is.GreaterThan(5000));
                Assert.That(beyond, Is.GreaterThan(500));
                if (seed == 5)
                {
                    Assert.That(lowestBeyond, Is.LessThan(shape.Floor - 100f), "the void is free to fall away past the stop line");
                    Assert.That(highestBeyond, Is.GreaterThan(shape.Ceiling + 20f), "and the larger range to stand over the voxel world");
                }
            }
        }

        [Test]
        public void TheLimit_GrowsPastTheEdge_AndStopsTheWalk()
        {
            MapLayout layout = FourKinds().Map.Resolve(5, Level);
            MapShape shape = layout.Shape;
            // West: the ocean. South: the void. East: the desert. North: the range.
            foreach (Vector2 direction in new[] { Vector2.left, Vector2.down, Vector2.right, Vector2.up })
            {
                Assert.That(layout.LimitAt(direction.x * shape.Half * 0.5f, direction.y * shape.Half * 0.5f), Is.Zero);

                // A walker heading straight out, a step at a time, as the player's own limit holds them.
                Vector2 position = direction * shape.Half * 0.6f;
                float last = 0f;
                for (int i = 0; i < 6000; i++)
                {
                    var step = direction * 0.5f;
                    position += MapLimit.Held(layout, position + step, step);
                    float limit = layout.LimitAt(position.x, position.y);
                    Assert.That(limit, Is.GreaterThanOrEqualTo(last - 0.1f), $"{direction}: the limit only grows on the way out");
                    last = Mathf.Max(last, limit);
                }
                Assert.That(last, Is.GreaterThan(0.8f), $"{direction}: the walker comes right up to the stop line");
                Assert.That(Mathf.Max(Mathf.Abs(position.x), Mathf.Abs(position.y)), Is.LessThan(shape.Half + 260f), $"{direction}: and is stopped near the edge");
                Vector2 before = position;
                for (int i = 0; i < 200; i++)
                {
                    var step = direction * 0.5f;
                    position += MapLimit.Held(layout, position + step, step);
                }
                Assert.That(Vector2.Dot(position - before, direction), Is.LessThan(1.5f), $"{direction}: 100 m of walking gains nothing");

                // Turning back is never held.
                Vector2 back = -direction * 0.5f;
                Assert.That(MapLimit.Held(layout, position + back, back), Is.EqualTo(back));
                Assert.That(layout.BoundaryAt(position.x, position.y).LimitMessage, Is.Not.Empty);
            }

            // The void's stop line is short of the cliff's lip: the ground there is still the land's.
            Vector2 lip = Vector2.zero;
            for (float z = 0f; z > -shape.Half * 1.2f; z -= 0.5f)
            {
                if (layout.LimitAt(0f, z) >= 1f)
                {
                    lip = new Vector2(0f, z);
                    break;
                }
            }
            Assert.That(shape.Apply(lip.x, lip.y, Level), Is.GreaterThan(layout.SeaLevel - 3f), "stopped before the drop");
            Assert.That(shape.Apply(lip.x, lip.y - 60f, Level), Is.LessThan(layout.SeaLevel - 100f), "which is just past it");
        }

        [Test]
        public void TheLimitsHold_GivesWayGently()
        {
            Assert.That(MapLimit.Resistance(0f), Is.Zero);
            Assert.That(MapLimit.Resistance(0.1f), Is.Zero);
            Assert.That(MapLimit.Resistance(1f), Is.EqualTo(1f));
            Assert.That(MapLimit.Resistance(0.6f), Is.InRange(0.2f, 0.8f));

            // Only the outward part of a step is held; the part along the line is kept whole.
            Vector2 held = MapLimit.Held(new Vector2(1f, 1f), Vector2.right, 1f);
            Assert.That(held.x, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(held.y, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(MapLimit.Held(new Vector2(-1f, 0.5f), Vector2.right, 1f), Is.EqualTo(new Vector2(-1f, 0.5f)));
        }

        [Test]
        public void TheSurvey_HoldsNothingPastThePlayableEdge()
        {
            TerrainSettings settings = Settings(4);
            settings.Map.Size = 2048f;
            settings.Map.SetSides(MapBoundaryType.OceanName, MapBoundaryType.DesertName, MapBoundaryType.OceanName, MapBoundaryType.MountainRidgeName);
            var map = (BoundedMapGenerator)TerrainGenerators.Create(settings);
            ResourceSurvey survey = ResourceSurvey.Build(settings, map, null, null, 40f);
            int outside = 0, land = 0;
            for (int z = 0; z < survey.Size; z++)
            {
                for (int x = 0; x < survey.Size; x++)
                {
                    SurveyCell cell = survey.Cell(x, z);
                    Vector2 centre = survey.CellCentre(x, z);
                    Assert.That(cell.Outside, Is.EqualTo(!map.Layout.WalksFreely(centre.x, centre.y)));
                    if (cell.Outside)
                    {
                        outside++;
                        Assert.That(cell.Land + cell.FreshWater + cell.Wood + cell.Grazing + cell.Game, Is.Zero, $"cell {x}, {z}");
                    }
                    else
                    {
                        land += cell.Land > 0.9f ? 1 : 0;
                    }
                }
            }
            Assert.That(outside, Is.GreaterThan(50), "the desert's and the range's land past the edge");
            Assert.That(land, Is.GreaterThan(100));
        }
    }
}
