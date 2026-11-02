using Clube.Core;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>Falling ground (GL308) and the rivers and lakes worked out from a heightfield (GL307).</summary>
    public class WaterAndFallingTests
    {
        private const byte Stone = 0, Dirt = 1, Sand = 2;

        private static GroundFall FallOf(byte id)
        {
            return id == Dirt ? GroundFall.Drops : id == Sand ? GroundFall.Slides : GroundFall.Stays;
        }

        // One 8³ chunk: a stone floor at y = 0, air above.
        private static World Floor()
        {
            var world = new World(new Vector3Int(8, 8, 8), 1f);
            world.Load(Vector3Int.zero, null);
            for (int z = 0; z <= 8; z++)
            {
                for (int y = 0; y <= 8; y++)
                {
                    for (int x = 0; x <= 8; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        world.SetDensity(sample, y == 0 ? 1f : 0f);
                        world.SetMaterial(sample, Stone);
                    }
                }
            }
            return world;
        }

        private static void Put(World world, Vector3Int sample, byte material)
        {
            world.SetMaterial(sample, material);
            world.SetDensity(sample, 1f);
        }

        private static void Settle(GroundSettling settling)
        {
            for (int i = 0; i < 50 && settling.AwakeCount > 0; i++)
            {
                settling.Step();
            }
        }

        private static int Solid(World world)
        {
            int count = 0;
            for (int z = 0; z <= 8; z++)
            {
                for (int y = 0; y <= 8; y++)
                {
                    for (int x = 0; x <= 8; x++)
                    {
                        if (world.GetDensity(new Vector3Int(x, y, z)) >= 0.5f)
                        {
                            count++;
                        }
                    }
                }
            }
            return count;
        }

        [Test]
        public void Dirt_InTheAir_FallsToTheFloor()
        {
            World world = Floor();
            using var settling = new GroundSettling(world, FallOf, 0.5f);
            Put(world, new Vector3Int(4, 5, 4), Dirt);
            int before = Solid(world);
            Settle(settling);
            Assert.That(world.GetDensity(new Vector3Int(4, 5, 4)), Is.LessThan(0.5f));
            Assert.That(world.GetDensity(new Vector3Int(4, 1, 4)), Is.EqualTo(1f));
            Assert.That(world.GetMaterial(new Vector3Int(4, 1, 4)), Is.EqualTo(Dirt));
            Assert.That(Solid(world), Is.EqualTo(before), "falling keeps the ground's volume");
        }

        [Test]
        public void Stone_InTheAir_Stays()
        {
            World world = Floor();
            using var settling = new GroundSettling(world, FallOf, 0.5f);
            Put(world, new Vector3Int(4, 5, 4), Stone);
            Settle(settling);
            Assert.That(world.GetDensity(new Vector3Int(4, 5, 4)), Is.EqualTo(1f));
        }

        [Test]
        public void DirtOnAPillar_Stays_SandSlidesOff()
        {
            World world = Floor();
            using var settling = new GroundSettling(world, FallOf, 0.5f);
            Put(world, new Vector3Int(4, 1, 4), Stone);
            Put(world, new Vector3Int(4, 2, 4), Dirt);
            Settle(settling);
            Assert.That(world.GetMaterial(new Vector3Int(4, 2, 4)), Is.EqualTo(Dirt));
            Assert.That(world.GetDensity(new Vector3Int(4, 2, 4)), Is.EqualTo(1f), "dirt holds on a pillar");

            Put(world, new Vector3Int(4, 3, 4), Sand);
            Settle(settling);
            Assert.That(world.GetDensity(new Vector3Int(4, 3, 4)), Is.LessThan(0.5f), "sand slides off the dirt");
            int sandOnFloor = 0;
            foreach (Vector3Int side in new[] { Vector3Int.right, Vector3Int.left, Vector3Int.forward, Vector3Int.back })
            {
                Vector3Int foot = new Vector3Int(4, 1, 4) + side;
                if (world.GetMaterial(foot) == Sand && world.GetDensity(foot) >= 0.5f)
                {
                    sandOnFloor++;
                }
            }
            Assert.That(sandOnFloor, Is.EqualTo(1), "the sand ends at the pillar's foot");
        }

        [Test]
        public void UnderminedDirt_FallsWhenTheGroundUnderIsDug()
        {
            World world = Floor();
            Put(world, new Vector3Int(4, 1, 4), Stone);
            Put(world, new Vector3Int(4, 2, 4), Dirt);
            using var settling = new GroundSettling(world, FallOf, 0.5f);
            Settle(settling);
            world.SetDensity(new Vector3Int(4, 1, 4), 0f);
            Settle(settling);
            Assert.That(world.GetMaterial(new Vector3Int(4, 1, 4)), Is.EqualTo(Dirt));
            Assert.That(world.GetDensity(new Vector3Int(4, 1, 4)), Is.EqualTo(1f));
        }

        // A basin: a bowl round the middle of the area, so water drains in and stands as a lake.
        private sealed class Bowl : HeightfieldGenerator<BowlHeight>
        {
            public Bowl() : base(new BowlHeight()) { }
        }

        private struct BowlHeight : IHeightField
        {
            public float Height(float x, float z)
            {
                float r = Mathf.Sqrt(x * x + z * z);
                return 20f + Mathf.Min(r, 60f) * 0.2f - (r > 100f ? (r - 100f) * 0.3f : 0f);
            }
        }

        [Test]
        public void Hydrology_FillsABowlWithALevelLake()
        {
            var settings = new WaterGeneration { AreaSize = 256f, RiverArea = 2000f };
            WaterMap map = Hydrology.Build(new Bowl(), settings, default);
            try
            {
                Assert.That(map.TryCell(new Unity.Mathematics.float2(0.5f, 0.5f), out int middle));
                Assert.That((WaterKind)map.Kind[middle], Is.EqualTo(WaterKind.Lake));
                Assert.That(map.Depth[middle], Is.GreaterThan(1f));
                Assert.That(map.TryCell(new Unity.Mathematics.float2(10.5f, -7.5f), out int other));
                Assert.That(map.Surface[other], Is.EqualTo(map.Surface[middle]).Within(1e-3f), "a lake lies level");
                for (int i = 0; i < map.Carve.Length; i++)
                {
                    Assert.That(map.Carve[i], Is.GreaterThanOrEqualTo(0f));
                }
            }
            finally
            {
                map.Dispose();
            }
        }

        [Test]
        public void Hydrology_KeepsTheDryAreaDry()
        {
            var settings = new WaterGeneration { AreaSize = 256f, RiverArea = 2000f };
            WaterMap map = Hydrology.Build(new Bowl(), settings, new Rect(-20f, -20f, 40f, 40f));
            try
            {
                Assert.That(map.TryCell(new Unity.Mathematics.float2(0.5f, 0.5f), out int middle));
                Assert.That((WaterKind)map.Kind[middle], Is.EqualTo(WaterKind.Dry));
            }
            finally
            {
                map.Dispose();
            }
        }

        [Test]
        public void WaterMap_GivesSandOnALakeBed()
        {
            var settings = new WaterGeneration { AreaSize = 256f, RiverArea = 2000f };
            WaterMap map = Hydrology.Build(new Bowl(), settings, default);
            try
            {
                map.Banks = new WaterBanks { Sand = 24, Clay = 13, SandDepth = 1.25f, ClayDepth = 1f, ClayFloorDepth = 100f, ShoreSand = 2.5f, PatchSize = 3f };
                var bed = new Unity.Mathematics.float2(0.5f, 0.5f);
                Assert.That(map.MaterialAt(bed, 0.5f, 1), Is.EqualTo((byte)24));
                Assert.That(map.MaterialAt(bed, 1.5f, 1), Is.EqualTo((byte)13), "clay under a lake's sand");
                Assert.That(map.MaterialAt(bed, -0.5f, 1), Is.EqualTo(WaterMap.Keep), "nothing above the ground");
            }
            finally
            {
                map.Dispose();
            }
        }
    }
}
