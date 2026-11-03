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
        public void LakeBedLowerThanTheMapSays_HoldsNoDryPocket()
        {
            // The map puts the bed at 3.5 (depth 1); the ground is really at 1, so 2 and 3 must be water too (GL318).
            for (int i = 0; i < 64; i++)
            {
                map.Depth[i] = map.Kind[i] != 0 ? 1f : 0f;
            }
            WaterFill.Fill(world, Vector3Int.zero, map, 0.5f);
            Assert.That(At(1, 2, 4), Is.EqualTo(1f).Within(0.01f));
        }

        [Test]
        public void ShoreLowerThanTheLake_IsUnderWater()
        {
            // The first dry column (x = 3) only reaches y = 3, under the lake's 4.5: the water runs on over it (GL318).
            Shape((x, y, z) => y <= (x < 3 ? 1 : x == 3 ? 3 : 6));
            WaterFill.Fill(world, Vector3Int.zero, map, 0.5f);
            Assert.That(At(3, 4, 4), Is.EqualTo(1f).Within(0.01f));
            Assert.That(At(4, 4, 4), Is.EqualTo(0f), "the bank beyond is ground");
        }

        [Test]
        public void Bucket_ScoopsAndPoursTheSameVolume()
        {
            float before = Total();
            int taken = WaterScoop.Take(world, new Vector3(1f, 3f, 4f), 1.5f, 4, null);
            Assert.That(taken, Is.EqualTo(4));
            Assert.That(Total(), Is.EqualTo(before - 4f).Within(0.05f));
            int poured = WaterScoop.Pour(world, new Vector3(1f, 5f, 4f), 1.5f, 4, 0.5f, null);
            Assert.That(poured, Is.EqualTo(4));
            Assert.That(Total(), Is.EqualTo(before).Within(0.05f));
        }

        private float Total()
        {
            float sum = 0f;
            for (int z = 0; z <= 8; z++)
            {
                for (int y = 0; y <= 8; y++)
                {
                    for (int x = 0; x <= 8; x++)
                    {
                        sum += At(x, y, z);
                    }
                }
            }
            return sum;
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

        // No lake: dry ground of the given shape and a flow with no sources.
        private void DryWorld(System.Func<int, int, int, bool> solid)
        {
            Shape(solid);
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
        }

        [Test]
        public void HeldPour_PoolsInsteadOfStacking()
        {
            // Many small pours at one point with no flow between them, as holding Place does (GL324).
            DryWorld((x, y, z) => y <= 1);
            for (int i = 0; i < 30; i++)
            {
                Assert.That(WaterScoop.Pour(world, new Vector3(4f, 5f, 4f), 3f, 1, 0.5f, null), Is.EqualTo(1));
            }
            float above = 0f;
            for (int z = 0; z <= 8; z++)
            {
                for (int x = 0; x <= 8; x++)
                {
                    for (int y = 4; y <= 8; y++)
                    {
                        above += At(x, y, z);
                    }
                }
            }
            Assert.That(above, Is.LessThan(0.01f), "30 units over a 3 m reach stay within two samples of the ground");
            Assert.That(Total(), Is.EqualTo(30f).Within(0.05f));
        }

        [Test]
        public void TrickleDownSteps_LeavesNoSpecks()
        {
            // Steps down from y = 6 at x = 0 to y = 2 at x = 8; one unit of water at the top.
            DryWorld((x, y, z) => y <= 6 - x / 2);
            world.SetWater(new Vector3Int(0, 7, 4), 1f);
            water.Wake(new Vector3Int(0, 7, 4));
            Settle(2000);
            int specks = 0;
            for (int z = 0; z <= 8; z++)
            {
                for (int y = 0; y <= 8; y++)
                {
                    for (int x = 0; x <= 8; x++)
                    {
                        float a = At(x, y, z);
                        specks += a > 0f && a < LiveWater.MinSpread ? 1 : 0;
                    }
                }
            }
            Assert.That(specks, Is.EqualTo(0), "no film left behind on the steps");
            Assert.That(Total(), Is.EqualTo(1f).Within(0.02f), "the water is all still there");
        }

        [Test]
        public void HoleDugUnderWater_TakesItInOneStep()
        {
            // Water in a one-sample pit at y = 7 in ground up to y = 7; a shaft dug under it down to y = 1 (GL326).
            DryWorld((x, y, z) => y <= 6 || (y == 7 && (x != 4 || z != 4)));
            world.SetWater(new Vector3Int(4, 7, 4), 1f);
            for (int y = 1; y <= 6; y++)
            {
                world.SetDensity(new Vector3Int(4, y, 4), 0f);
            }
            water.Step();
            Assert.That(At(4, 1, 4), Is.EqualTo(1f).Within(0.01f), "the water drops to the bottom of the shaft at once");
            Assert.That(At(4, 7, 4), Is.EqualTo(0f));
        }

        [Test]
        public void TrickleDownADiagonalSlope_RunsInOneVein()
        {
            // Ground falling both ways (x and z), so every sample has two ways down: the water keeps to one line rather
            // than fanning out over the slope (GL325).
            DryWorld((x, y, z) => y <= 6 - (x + z) / 2);
            world.SetWater(new Vector3Int(0, 7, 0), 1f);
            water.Wake(new Vector3Int(0, 7, 0));
            var wetColumns = new System.Collections.Generic.HashSet<Vector2Int>();
            for (int i = 0; i < 2000 && water.AwakeCount > 0; i++)
            {
                water.Step();
                for (int z = 0; z <= 8; z++)
                {
                    for (int x = 0; x <= 8; x++)
                    {
                        for (int y = 0; y <= 8; y++)
                        {
                            // The upper slope only: at the foot the water pools, as it should.
                            if (x + z <= 8 && At(x, y, z) > 0f)
                            {
                                wetColumns.Add(new Vector2Int(x, z));
                            }
                        }
                    }
                }
            }
            // A vein down the upper slope crosses 9 columns (x + z from 0 to 8); a fan covers the 45 there.
            Assert.That(wetColumns.Count, Is.LessThanOrEqualTo(13), "the water ran in a vein, not a fan");
            Assert.That(Total(), Is.EqualTo(1f).Within(0.02f));
        }
    }
}
