using Clube.Core;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>Water in the voxel data (GL315): generated, running into dug holes, falling down drops, giving way to ground.</summary>
    public class LiveWaterTests
    {
        private const float Level = 4.5f;
        private World world;
        private WaterMap map;
        private LiveWater water;

        // One 8³ chunk of 1 m voxels. A lake over x 0-2 (cells x < 3), its surface at 4.5 and its bed at y = 1; a bank
        // of ground up to y = 6 from x = 3 on.
        [SetUp]
        public void SetUp()
        {
            world = new World(new Vector3Int(8, 8, 8), 1f);
            world.Load(Vector3Int.zero, null);
            Shape((x, y, z) => y <= (x < 3 ? 1 : 6));
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
                bool wet = i % 8 < 3;
                map.Kind[i] = wet ? (byte)WaterKind.Lake : (byte)0;
                map.Surface[i] = wet ? Level : 0f;
                map.Depth[i] = wet ? Level - 1f : 0f;
                map.RiverDistance[i] = WaterMap.MaxDistance;
            }
            WaterFill.Fill(world, Vector3Int.zero, map, 0.5f);
            water = new LiveWater(world, map, 0.5f);
        }

        [TearDown]
        public void TearDown()
        {
            water.Dispose();
            map.Dispose();
        }

        private void Shape(System.Func<int, int, int, bool> solid)
        {
            for (int z = 0; z <= 8; z++)
            {
                for (int y = 0; y <= 8; y++)
                {
                    for (int x = 0; x <= 8; x++)
                    {
                        world.SetDensity(new Vector3Int(x, y, z), solid(x, y, z) ? 1f : 0f);
                    }
                }
            }
        }

        private void Settle(int steps = 400)
        {
            for (int i = 0; i < steps && water.AwakeCount > 0; i++)
            {
                water.Step();
            }
        }

        private float At(int x, int y, int z)
        {
            return world.GetWater(new Vector3Int(x, y, z));
        }

        [Test]
        public void GeneratedLake_FillsAirUnderItsSurface()
        {
            Assert.That(At(1, 3, 4), Is.EqualTo(1f).Within(0.01f));
            Assert.That(At(1, 4, 4), Is.EqualTo(1f).Within(0.01f), "the sample the surface is half a voxel over");
            Assert.That(At(1, 5, 4), Is.EqualTo(0f));
            Assert.That(At(1, 1, 4), Is.EqualTo(0f), "ground holds no water");
            Assert.That(At(4, 7, 4), Is.EqualTo(0f), "dry land holds none");
        }

        [Test]
        public void DugHoleBesideTheLake_FillsToTheLakesLevel()
        {
            world.SetDensity(new Vector3Int(3, 3, 4), 0f);
            world.SetDensity(new Vector3Int(4, 3, 4), 0f);
            world.SetDensity(new Vector3Int(4, 2, 4), 0f);
            Settle();
            Assert.That(At(3, 3, 4), Is.GreaterThan(0.9f));
            Assert.That(At(4, 3, 4), Is.GreaterThan(0.9f), "water runs on through the hole");
            Assert.That(At(4, 2, 4), Is.GreaterThan(0.9f), "and down into it");
        }

        [Test]
        public void HoleAboveTheWater_StaysDry()
        {
            world.SetDensity(new Vector3Int(3, 5, 4), 0f);
            Settle();
            Assert.That(At(3, 5, 4), Is.EqualTo(0f));
        }

        [Test]
        public void HoleNotTouchingWater_StaysDry()
        {
            world.SetDensity(new Vector3Int(6, 3, 4), 0f);
            Settle();
            Assert.That(At(6, 3, 4), Is.EqualTo(0f));
        }

        [Test]
        public void GroundPlacedInWater_TakesItsPlace()
        {
            world.SetDensity(new Vector3Int(1, 3, 4), 1f);
            Settle();
            Assert.That(At(1, 3, 4), Is.EqualTo(0f));
            Assert.That(At(1, 2, 4), Is.GreaterThan(0.9f), "the rest of the lake stays");
        }

        [Test]
        public void WaterFallsDownAShaft()
        {
            // A channel from the lake at y 3-4 to a shaft down to y 1.
            for (int x = 3; x <= 6; x++)
            {
                world.SetDensity(new Vector3Int(x, 4, 4), 0f);
            }
            for (int y = 1; y <= 4; y++)
            {
                world.SetDensity(new Vector3Int(6, y, 4), 0f);
            }
            Settle();
            Assert.That(At(6, 1, 4), Is.GreaterThan(0.9f), "water lands at the bottom of the shaft");
        }

        [Test]
        public void WaterOffALedge_FallsInsteadOfHovering()
        {
            // No lake here: a ledge up to y = 2 for x < 4, low ground up to y = 0 beyond; a pour of water on the ledge.
            Shape((x, y, z) => y <= (x < 4 ? 2 : 0));
            for (int z = 0; z <= 8; z++)
            {
                for (int y = 0; y <= 8; y++)
                {
                    for (int x = 0; x <= 8; x++)
                    {
                        world.SetWater(new Vector3Int(x, y, z), 0f);
                    }
                }
            }
            water.Dispose();
            map.Dispose();
            map = default;
            water = new LiveWater(world, default, 0.5f);
            for (int z = 3; z <= 5; z++)
            {
                world.SetWater(new Vector3Int(2, 3, z), 1f);
                water.Wake(new Vector3Int(2, 3, z));
            }
            Settle();
            float high = 0f, low = 0f;
            for (int z = 0; z <= 8; z++)
            {
                for (int x = 4; x <= 8; x++)
                {
                    for (int y = 2; y <= 8; y++)
                    {
                        high += At(x, y, z);
                    }
                    low += At(x, 1, z);
                }
            }
            Assert.That(high, Is.LessThan(0.05f), "no water hangs in the air over the low ground");
            Assert.That(low, Is.GreaterThan(0.5f), "it lands on the low ground");
        }
    }
}
