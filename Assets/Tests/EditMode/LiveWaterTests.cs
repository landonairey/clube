using Clube.Core;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>Water that runs into dug holes and gives way to placed ground (GL313).</summary>
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
            for (int z = 0; z <= 8; z++)
            {
                for (int y = 0; y <= 8; y++)
                {
                    for (int x = 0; x <= 8; x++)
                    {
                        world.SetDensity(new Vector3Int(x, y, z), y <= (x < 3 ? 1 : 6) ? 1f : 0f);
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
                bool wet = i % 8 < 3;
                map.Kind[i] = wet ? (byte)WaterKind.Lake : (byte)0;
                map.Surface[i] = wet ? Level : 0f;
                map.Depth[i] = wet ? Level - 1f : 0f;
                map.RiverDistance[i] = WaterMap.MaxDistance;
            }
            water = new LiveWater(world, map, 0.5f);
        }

        [TearDown]
        public void TearDown()
        {
            water.Dispose();
            map.Dispose();
        }

        private void Settle()
        {
            for (int i = 0; i < 40 && water.AwakeCount > 0; i++)
            {
                water.Step();
            }
        }

        [Test]
        public void DugHoleBesideTheLake_FillsToTheLakesLevel()
        {
            world.SetDensity(new Vector3Int(3, 3, 4), 0f);
            world.SetDensity(new Vector3Int(4, 3, 4), 0f);
            world.SetDensity(new Vector3Int(4, 2, 4), 0f);
            Settle();
            Assert.That(water.LevelAt(new Vector3Int(3, 3, 4)), Is.EqualTo(Level));
            Assert.That(water.LevelAt(new Vector3Int(4, 3, 4)), Is.EqualTo(Level), "water runs on through the hole");
            Assert.That(water.LevelAt(new Vector3Int(4, 2, 4)), Is.EqualTo(Level), "and down into it");
        }

        [Test]
        public void HoleAboveTheWater_StaysDry()
        {
            world.SetDensity(new Vector3Int(3, 5, 4), 0f);
            Settle();
            Assert.That(water.LevelAt(new Vector3Int(3, 5, 4)), Is.Null);
        }

        [Test]
        public void HoleNotTouchingWater_StaysDry()
        {
            world.SetDensity(new Vector3Int(6, 3, 4), 0f);
            Settle();
            Assert.That(water.LevelAt(new Vector3Int(6, 3, 4)), Is.Null);
        }

        [Test]
        public void GroundPlacedInWater_TakesItsPlace()
        {
            world.SetDensity(new Vector3Int(3, 3, 4), 0f);
            Settle();
            Assert.That(water.Flooded.ContainsKey(new Vector3Int(3, 3, 4)));
            world.SetDensity(new Vector3Int(3, 3, 4), 1f);
            Settle();
            Assert.That(water.Flooded.ContainsKey(new Vector3Int(3, 3, 4)), Is.False);
            Assert.That(water.LevelAt(new Vector3Int(3, 3, 4)), Is.Null);
        }

        [Test]
        public void LakeBuiltUpToItsSurface_IsFilled()
        {
            int cell = water.CellOf(new Vector3(1.5f, 0f, 1.5f));
            Assert.That(water.IsFilled(cell), Is.False);
            for (int z = 1; z <= 2; z++)
            {
                for (int x = 1; x <= 2; x++)
                {
                    for (int y = 2; y <= 5; y++)
                    {
                        world.SetDensity(new Vector3Int(x, y, z), 1f);
                    }
                }
            }
            Settle();
            Assert.That(water.IsFilled(cell), Is.True);
            Assert.That(water.LevelAt(new Vector3Int(1, 3, 1)), Is.Null, "no water left where the ground is");
        }
    }
}
