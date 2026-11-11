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
        public void PourIntoTheLake_ComesToRest()
        {
            // Small pours onto the lake, as a held bucket does (GL333): the flow must go quiet, not hand the poured water on
            // across the surface for ever.
            float before = Total();
            var changed = new System.Collections.Generic.List<Vector3Int>();
            for (int i = 0; i < 4; i++)
            {
                changed.Clear();
                Assert.That(WaterScoop.Pour(world, new Vector3(1f, 6f, 2f + i), 1.5f, 3, 0.5f, changed), Is.EqualTo(3));
                foreach (Vector3Int sample in changed)
                {
                    water.Wake(sample);
                }
            }
            Settle(3000);
            Assert.That(water.AwakeCount, Is.EqualTo(0), "the water comes to rest");
            Assert.That(Total(), Is.EqualTo(before + 12f).Within(0.01f), "and the lake keeps what was poured");
        }

        [Test]
        public void ScoopingTheLake_LowersIt()
        {
            // A lake is finite water (GL333): what a bucket takes doesn't flow back in from nowhere.
            float before = Total();
            var changed = new System.Collections.Generic.List<Vector3Int>();
            for (int i = 0; i < 5; i++)
            {
                changed.Clear();
                Assert.That(WaterScoop.Take(world, new Vector3(1f, 3.5f, 4f), 1.5f, 4, changed), Is.EqualTo(4));
                foreach (Vector3Int sample in changed)
                {
                    water.Wake(sample);
                }
                Settle(3000);
            }
            Assert.That(Total(), Is.EqualTo(before - 20f).Within(0.01f));
        }

        [Test]
        public void LakeWithARiverOut_DrainsOnlyToTheRiversLevel()
        {
            // A river leaves the lake along z = 4 (x 3-7, its bed at y = 3), its water 0.2 under the lake's (GL333). Lakes are
            // finite, so once disturbed the lake runs into the river, but a river keeps its level: the lake drops to it and
            // stops, rather than draining away.
            for (int x = 3; x <= 7; x++)
            {
                for (int y = 4; y <= 6; y++)
                {
                    world.SetDensity(new Vector3Int(x, y, 4), 0f);
                }
                int cell = x + 4 * 8;
                map.Kind[cell] = (byte)WaterKind.River;
                map.Surface[cell] = Level - 0.2f;
                map.Depth[cell] = Level - 0.2f - 3.5f;
            }
            WaterFill.Fill(world, Vector3Int.zero, map, 0.5f);
            water.Dispose();
            water = new LiveWater(world, map, 0.5f);
            for (int z = 0; z <= 8; z++)
            {
                water.Wake(new Vector3Int(2, 4, z));
            }
            Settle(5000);
            Assert.That(water.AwakeCount, Is.EqualTo(0), "the flow comes to rest");
            float river = water.SourceAt(new Vector3Int(3, 4, 4));
            Assert.That(At(1, 4, 0), Is.GreaterThanOrEqualTo(river - LiveWater.Quantum - 0.001f), "the lake's top drops no lower than the river");
            Assert.That(At(2, 4, 4), Is.LessThan(1f), "next to the outflow it ran off down to the river's level");
            Assert.That(At(1, 3, 0), Is.EqualTo(1f).Within(0.001f), "and the lake under it is still full");
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

        [Test]
        public void FedTrench_RunsWithoutGaps()
        {
            // A trench one sample wide along x at z = 4, its floor stepping down one sample at x = 5, fed at x = 0 every
            // step like a river (GL330): the water runs along the floor as one stream, never as beads with dry floor
            // between them.
            DryWorld((x, y, z) => z != 4 ? y <= 7 : y <= (x < 5 ? 3 : 2));
            for (int i = 0; i < 120; i++)
            {
                world.SetWater(new Vector3Int(0, 4, 4), 1f);
                world.SetWater(new Vector3Int(0, 5, 4), 1f);
                water.Wake(new Vector3Int(0, 4, 4));
                water.Wake(new Vector3Int(0, 5, 4));
                water.Step();
                int front = 0;
                for (int x = 0; x <= 8; x++)
                {
                    if (At(x, x < 5 ? 4 : 3, 4) > 0f)
                    {
                        front = x;
                    }
                }
                for (int x = 0; x <= front; x++)
                {
                    Assert.That(At(x, x < 5 ? 4 : 3, 4), Is.GreaterThan(0f), $"step {i}: the floor at x = {x} is dry behind the front at {front}");
                }
            }
            Assert.That(At(7, 3, 4), Is.GreaterThan(0.5f), "the stream reaches the far end and fills it");
        }

        [Test]
        public void ManyAwakeFarOff_WaterByTheFocusStillMoves()
        {
            // 30,000 awake samples far away (in no loaded chunk) and one unit of water in the air by the focus (GL560): a
            // short step looks at the nearest first and leaves the far ones waiting.
            DryWorld((x, y, z) => y <= 1);
            for (int i = 0; i < 30000; i++)
            {
                water.Wake(new Vector3Int(1000 + i % 100, i / 100 % 30, i / 3000 * 2));
            }
            world.SetWater(new Vector3Int(4, 5, 4), 1f);
            water.Wake(new Vector3Int(4, 5, 4));
            water.Focus = new Vector3Int(4, 2, 4);
            int awake = water.AwakeCount;
            water.Step(max: 200);
            Assert.That(At(4, 5, 4), Is.LessThan(1f), "the water by the focus fell");
            Assert.That(water.AwakeCount, Is.GreaterThan(awake - 400), "most far samples wait for later steps");
            Settle(2000);
            Assert.That(water.AwakeCount, Is.EqualTo(0), "the far ones are looked at in the end");
            Assert.That(Total(), Is.EqualTo(1f).Within(0.01f));
        }

        [Test]
        public void WakeChunk_WakesOnlyWaterThatCanMove()
        {
            // The lake chunk woken again (as when it streams back in with edits): its wet samples wake, its dry land
            // doesn't (GL560). Counted in the chunk itself: its border samples are the unloaded neighbours' to the world.
            Assert.That(world.TryGetChunk(Vector3Int.zero, out Chunk chunk));
            int wet = 0;
            for (int z = 0; z <= 8; z++)
            {
                for (int y = 0; y <= 8; y++)
                {
                    for (int x = 0; x <= 8; x++)
                    {
                        wet += chunk.GetWater(new Vector3Int(x, y, z)) > 0f ? 1 : 0;
                    }
                }
            }
            water.WakeChunk(Vector3Int.zero);
            Assert.That(wet, Is.GreaterThan(0));
            Assert.That(water.AwakeCount, Is.EqualTo(wet));
        }
    }
}
