using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Clube.Core.Tests
{
    public class LandscapeHeightTests
    {
        private const float Level = 20f;
        private const float MountainHeight = 110f;

        private static LandscapeHeight Field(int seed = 1, float coverage = 0.35f)
        {
            return new LandscapeHeight(seed, Level, MountainHeight, 250f, coverage);
        }

        [Test]
        public void Landscape_HasWidePlainsAndRealMountains_WithinItsRange()
        {
            LandscapeHeight field = Field();
            int plains = 0;
            int mountains = 0;
            int total = 0;
            float highest = float.MinValue;
            for (float z = -1024f; z < 1024f; z += 8f)
            {
                for (float x = -1024f; x < 1024f; x += 8f)
                {
                    float height = field.Height(x, z);
                    Assert.That(height, Is.InRange(Level - 4f, Level + MountainHeight + 20f), $"Height at {x}, {z}.");
                    plains += height < Level + 6f ? 1 : 0;
                    mountains += height > Level + MountainHeight * 0.6f ? 1 : 0;
                    highest = math.max(highest, height);
                    total++;
                }
            }

            Assert.That(plains / (float)total, Is.GreaterThan(0.3f), "A good share of the land is plains.");
            Assert.That(mountains, Is.GreaterThan(0), "Some peaks rise most of the way up.");
            Assert.That(highest - Level, Is.GreaterThan(80f), "Mountains are on the scale of 100 m, not a storey or two.");
        }

        [Test]
        public void Landscape_IsTheSameForASeed_AndDiffersBetweenSeeds()
        {
            Assert.AreEqual(Field(7).Height(123.4f, -56.7f), Field(7).Height(123.4f, -56.7f));
            Assert.AreNotEqual(Field(7).Height(123.4f, -56.7f), Field(8).Height(123.4f, -56.7f));
        }

        [Test]
        public void ColumnHeightsJob_MatchesTheManagedHeight()
        {
            LandscapeHeight field = Field();
            var block = new ColumnBlock(new int2(-40, 90), new int2(34, 34), 0.25f);
            var heights = new NativeArray<float>(block.Length, Allocator.TempJob);
            var range = new NativeArray<float2>(1, Allocator.TempJob);
            try
            {
                new LandscapeGenerator(1, Level, MountainHeight, 250f, 0.35f).ScheduleColumnHeights(block, heights, range).Complete();
                for (int z = 0; z < block.Count.y; z++)
                {
                    for (int x = 0; x < block.Count.x; x++)
                    {
                        float2 position = block.Position(x, z);
                        Assert.AreEqual(field.Height(position.x, position.y), heights[block.Index(x, z)], 1e-3f);
                    }
                }
            }
            finally
            {
                heights.Dispose();
                range.Dispose();
            }
        }
    }
}
