using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>The eroded landscape (P12): gullies on the mountains, plains left alone, rivers starting in the valleys.</summary>
    public class ErodedLandscapeTests
    {
        private const float Level = 20f;
        private const float MountainHeight = 110f;

        private static LandscapeHeight Land(float coverage)
        {
            return new LandscapeHeight(1, Level, MountainHeight, 250f, coverage, 3).WithSmoothMassifs();
        }

        private static ErodedLandscapeHeight Eroded(float coverage = 0.35f, ErosionSettings settings = null)
        {
            settings ??= new ErosionSettings();
            return new ErodedLandscapeHeight(Land(coverage), settings.ToFilter(1), Level, MountainHeight);
        }

        [Test]
        public void Plains_AreLeftAlone_AndMountainsAreCut()
        {
            LandscapeHeight land = Land(0.35f);
            ErodedLandscapeHeight eroded = Eroded();
            int plains = 0;
            int cut = 0;
            float highest = float.MinValue;
            for (float z = -600f; z < 600f; z += 6f)
            {
                for (float x = -600f; x < 600f; x += 6f)
                {
                    float before = land.RawHeight(x, z);
                    float after = eroded.Height(x, z);
                    highest = math.max(highest, after);
                    if (before < Level + 0.5f)
                    {
                        Assert.AreEqual(before, after, 1e-4f, $"Plains at {x}, {z} keep their height.");
                        plains++;
                    }
                    else if (math.abs(after - before) > 2f)
                    {
                        cut++;
                    }
                }
            }
            Assert.That(plains, Is.GreaterThan(1000), "The test area has plains.");
            Assert.That(cut, Is.GreaterThan(1000), "The mountains have gullies metres deep.");
            Assert.That(highest, Is.LessThan(160f), "Peaks stay under the world labs' 160 m top.");
        }

        [Test]
        public void Eroded_IsTheSameForASeed()
        {
            Assert.AreEqual(Eroded(0.9f).Height(123.4f, -56.7f), Eroded(0.9f).Height(123.4f, -56.7f));
        }

        [Test]
        public void NoStrength_IsTheLandUnchanged()
        {
            var none = new ErosionSettings { Strength = 0f };
            LandscapeHeight land = Land(0.9f);
            ErodedLandscapeHeight eroded = Eroded(0.9f, none);
            for (float x = -200f; x < 200f; x += 17f)
            {
                Assert.AreEqual(land.RawHeight(x, 31f), eroded.Height(x, 31f), 1e-5f);
            }
        }

        [Test]
        public void ColumnHeightsJob_MatchesTheManagedHeight()
        {
            ErodedLandscapeHeight field = Eroded(0.9f);
            var generator = new ErodedLandscapeGenerator(field);
            var block = new ColumnBlock(new int2(-200, 300), new int2(34, 34), 0.25f);
            var heights = new NativeArray<float>(block.Length, Allocator.TempJob);
            var range = new NativeArray<float2>(1, Allocator.TempJob);
            try
            {
                generator.ScheduleColumnHeights(block, heights, range).Complete();
                for (int z = 0; z < block.Count.y; z++)
                {
                    for (int x = 0; x < block.Count.x; x++)
                    {
                        float2 position = block.Position(x, z);
                        // Burst-compiled, the erosion steps drift up to a few centimetres from the managed sum.
                        Assert.AreEqual(field.Height(position.x, position.y), heights[block.Index(x, z)], 5e-2f);
                    }
                }
            }
            finally
            {
                heights.Dispose();
                range.Dispose();
            }
        }

        [Test]
        public void ValleysJob_MatchesTheManagedValley()
        {
            ErodedLandscapeHeight field = Eroded(0.9f);
            var generator = new ErodedLandscapeGenerator(field);
            var points = new NativeArray<float2>(200, Allocator.TempJob);
            var valleys = new NativeArray<float>(200, Allocator.TempJob);
            try
            {
                for (int i = 0; i < points.Length; i++)
                {
                    points[i] = new float2(i * 7.3f - 700f, i * 3.1f - 300f);
                }
                generator.ScheduleValleys(points, valleys).Complete();
                for (int i = 0; i < points.Length; i++)
                {
                    // Burst-compiled, valley strength drifts by up to a few hundredths.
                    Assert.AreEqual(field.Valley(points[i].x, points[i].y), valleys[i], 5e-2f);
                }
            }
            finally
            {
                points.Dispose();
                valleys.Dispose();
            }
        }

        [Test]
        public void ValleyRivers_StartStreamsUpInTheMountains()
        {
            var generator = new ErodedLandscapeGenerator(Eroded(0.9f));
            var settings = new WaterGeneration { AreaSize = 320f };
            int with = MountainRiverCells(generator, settings);
            settings.ValleyRivers = false;
            int without = MountainRiverCells(generator, settings);
            Assert.That(with, Is.GreaterThan(without + 100), $"Valleys start streams ({with} river cells against {without}).");
        }

        [Test]
        public void ValleyRivers_JoinUpIntoStreams()
        {
            var generator = new ErodedLandscapeGenerator(Eroded(0.9f));
            WaterMap map = Hydrology.Build(generator, new WaterGeneration { AreaSize = 320f }, default);
            try
            {
                // A river cell with no water beside it is a stray speck, not part of a stream.
                int size = map.Size.x;
                int stranded = 0;
                int rivers = 0;
                for (int z = 1; z < size - 1; z++)
                {
                    for (int x = 1; x < size - 1; x++)
                    {
                        int i = x + z * size;
                        if (map.Kind[i] != (byte)WaterKind.River)
                        {
                            continue;
                        }
                        rivers++;
                        bool joined = false;
                        for (int dz = -1; dz <= 1 && !joined; dz++)
                        {
                            for (int dx = -1; dx <= 1 && !joined; dx++)
                            {
                                int j = x + dx + (z + dz) * size;
                                joined = j != i && map.Kind[j] != (byte)WaterKind.Dry;
                            }
                        }
                        stranded += joined ? 0 : 1;
                    }
                }
                Assert.That(rivers, Is.GreaterThan(0));
                Assert.That(stranded / (float)rivers, Is.LessThan(0.05f), "River cells join up into streams.");
            }
            finally
            {
                map.Dispose();
            }
        }

        private static int MountainRiverCells(HeightfieldGenerator generator, WaterGeneration settings)
        {
            WaterMap map = Hydrology.Build(generator, settings, default);
            try
            {
                int count = 0;
                for (int i = 0; i < map.Kind.Length; i++)
                {
                    if (map.Kind[i] == (byte)WaterKind.River && map.Surface[i] - map.Depth[i] > Level + 15f)
                    {
                        count++;
                    }
                }
                return count;
            }
            finally
            {
                map.Dispose();
            }
        }
    }
}
