using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>Rivers fed by the soil water in their chunk (WT1): water comes from somewhere and goes somewhere.</summary>
    public class SoilWaterTests
    {
        private const float Level = 4.5f;
        private World world;
        private WaterMap map;
        private LiveWater water;
        private Chunk chunk;

        // One 8³ chunk of 1 m voxels: ground up to y = 6, with a river channel along z = 4 (bed at y = 1, water to 4.5).
        [SetUp]
        public void SetUp()
        {
            world = new World(new Vector3Int(8, 8, 8), 1f);
            world.Load(Vector3Int.zero, null);
            world.TryGetChunk(Vector3Int.zero, out chunk);
            for (int z = 0; z <= 8; z++)
            {
                for (int y = 0; y <= 8; y++)
                {
                    for (int x = 0; x <= 8; x++)
                    {
                        bool channel = z == 4 && y >= 2;
                        world.SetDensity(new Vector3Int(x, y, z), y <= 6 && !channel ? 1f : 0f);
                    }
                }
            }
            int cells = 8 * 8;
            map = new WaterMap
            {
                Origin = float2.zero,
                CellSize = 1f,
                Size = new int2(8, 8),
                Carve = new NativeArray<float>(cells, Allocator.Persistent),
                Surface = new NativeArray<float>(cells, Allocator.Persistent),
                Depth = new NativeArray<float>(cells, Allocator.Persistent),
                Kind = new NativeArray<byte>(cells, Allocator.Persistent),
                Flow = new NativeArray<float2>(cells, Allocator.Persistent),
                LakeDistance = new NativeArray<byte>(cells, Allocator.Persistent),
                RiverDistance = new NativeArray<byte>(cells, Allocator.Persistent),
            };
            for (int i = 0; i < cells; i++)
            {
                bool river = i / 8 == 4;
                map.Kind[i] = river ? (byte)WaterKind.River : (byte)0;
                map.Surface[i] = river ? Level : 0f;
                map.Depth[i] = river ? Level - 1.5f : 0f;
                map.LakeDistance[i] = WaterMap.MaxDistance;
                map.RiverDistance[i] = river ? (byte)0 : (byte)1;
            }
            WaterFill.Fill(world, Vector3Int.zero, map, 0.5f);
            water = new LiveWater(world, map, 0.5f) { HoldSoilWaterFull = false };
        }

        [TearDown]
        public void TearDown()
        {
            water.Dispose();
            map.Dispose();
        }

        // A pit beside the river at x = 3, z = 5, from y = 2 up: the river runs into it.
        private void DigPit()
        {
            for (int y = 2; y <= 6; y++)
            {
                world.SetDensity(new Vector3Int(3, y, 5), 0f);
            }
        }

        private void Settle(int steps = 3000)
        {
            for (int i = 0; i < steps && water.AwakeCount > 0; i++)
            {
                water.Step();
            }
        }

        private float Surface()
        {
            float total = 0f;
            for (int z = 0; z <= 8; z++)
            {
                for (int y = 0; y <= 8; y++)
                {
                    for (int x = 0; x <= 8; x++)
                    {
                        total += world.GetWater(new Vector3Int(x, y, z));
                    }
                }
            }
            return total;
        }

        [Test]
        public void RiverFillingAPit_DrawsItsWaterFromTheSoil()
        {
            float soil = water.SoilOf(chunk);
            float before = Surface();
            DigPit();
            Settle();
            float gained = Surface() - before;
            Assert.That(gained, Is.GreaterThan(2f), "the pit filled from the river");
            Assert.That(soil - chunk.SoilWater, Is.EqualTo(gained).Within(0.01f), "every drop of it came out of the soil");
            Assert.That(world.GetWater(new Vector3Int(3, 4, 4)), Is.EqualTo(1f).Within(0.01f), "and the river is back at its level");
        }

        [Test]
        public void RiverOverDrySoil_RunsDown()
        {
            water.SoilWater = 0f;
            float before = Surface();
            DigPit();
            Settle();
            Assert.That(Surface(), Is.EqualTo(before).Within(0.01f), "no water is made");
            Assert.That(world.GetWater(new Vector3Int(3, 3, 5)), Is.GreaterThan(0.5f), "the pit took river water");
            Assert.That(world.GetWater(new Vector3Int(3, 4, 4)), Is.LessThan(1f), "which the river couldn't replace");
        }

        [Test]
        public void SoilKeptFull_IsToppedBackUpEachStep()
        {
            water.HoldSoilWaterFull = true;
            float capacity = water.SoilCapacity(chunk);
            DigPit();
            Settle();
            water.Step();
            Assert.That(chunk.SoilWater, Is.EqualTo(capacity).Within(0.001f));
            Assert.That(water.SoilWaterRefilled, Is.EqualTo(water.SoilWaterDrawn).Within(0.01f), "the stand-in put back what was drawn");
        }

        [Test]
        public void WaterOverARiversLevel_SoaksIntoTheSoil()
        {
            // The river's level drops a quarter of a sample under the water filled for it: its top samples now hold more
            // than the river keeps.
            for (int x = 0; x < 8; x++)
            {
                map.Surface[x + 4 * 8] = Level - 0.25f;
            }
            float soil = water.SoilOf(chunk);
            float before = Surface();
            for (int x = 0; x <= 8; x++)
            {
                water.Wake(new Vector3Int(x, 4, 4));
            }
            Settle();
            float lost = before - Surface();
            Assert.That(lost, Is.GreaterThan(1f), "the water over the river's level left the surface");
            Assert.That(chunk.SoilWater - soil, Is.EqualTo(lost).Within(0.01f), "into the soil");
        }
    }
}
