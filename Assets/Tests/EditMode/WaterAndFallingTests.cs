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

        // GL327: dirt hangs 2 samples out, stone 5 and a sample more for each solid one over it (up to 10).
        private static GroundHold HoldOf(byte id)
        {
            return id == Dirt ? new GroundHold(GroundFall.Drops, 2)
                : id == Stone ? new GroundHold(GroundFall.Drops, 5, 10)
                : new GroundHold(GroundFall.Slides);
        }

        // One 16³ chunk of 1 m samples, all air but a stone floor at y = 0 (the unloaded ground under it holds it up).
        private static World Room()
        {
            var world = new World(new Vector3Int(16, 16, 16), 1f);
            world.Load(Vector3Int.zero, null);
            for (int z = 0; z <= 16; z++)
            {
                for (int y = 0; y <= 16; y++)
                {
                    for (int x = 0; x <= 16; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        world.SetDensity(sample, y == 0 ? 1f : 0f);
                        world.SetMaterial(sample, Stone);
                    }
                }
            }
            return world;
        }

        private static void Settle(GroundSettling settling, int steps)
        {
            for (int i = 0; i < steps && settling.AwakeCount > 0; i++)
            {
                settling.Step();
            }
        }

        // A stone wall at x = 0 up to y = 4 and a lip of the material at y = 4 reaching out x = 1 .. length.
        private static World Lip(byte material, int length)
        {
            World world = Room();
            for (int y = 1; y <= 4; y++)
            {
                Put(world, new Vector3Int(0, y, 8), Stone);
            }
            for (int x = 1; x <= length; x++)
            {
                Put(world, new Vector3Int(x, 4, 8), material);
            }
            return world;
        }

        [Test]
        public void DirtLip_HoldsTwoOut_TheThirdFalls()
        {
            World world = Lip(Dirt, 3);
            using var settling = new GroundSettling(world, HoldOf, 0.5f);
            settling.Wake(new Vector3Int(1, 4, 8));
            Settle(settling, 100);
            Assert.That(world.GetDensity(new Vector3Int(2, 4, 8)), Is.EqualTo(1f), "two out holds");
            Assert.That(world.GetDensity(new Vector3Int(3, 4, 8)), Is.LessThan(0.5f), "the third lets go");
            Assert.That(world.GetMaterial(new Vector3Int(3, 1, 8)), Is.EqualTo(Dirt), "and lands on the floor");
        }

        [Test]
        public void StoneLip_HoldsFiveOut_TheSixthFalls()
        {
            World world = Lip(Stone, 6);
            using var settling = new GroundSettling(world, HoldOf, 0.5f);
            settling.Wake(new Vector3Int(1, 4, 8));
            Settle(settling, 100);
            Assert.That(world.GetDensity(new Vector3Int(5, 4, 8)), Is.EqualTo(1f), "five out holds");
            Assert.That(world.GetDensity(new Vector3Int(6, 4, 8)), Is.LessThan(0.5f), "the sixth lets go");
            Assert.That(world.GetDensity(new Vector3Int(6, 1, 8)), Is.EqualTo(1f));
        }

        [Test]
        public void DiggingUnderALip_DropsWhatItNoLongerHolds()
        {
            // A dirt shelf on a stone step: standing at first, then the step is dug out from under its far end.
            World world = Room();
            for (int x = 0; x <= 4; x++)
            {
                for (int y = 1; y <= 3; y++)
                {
                    Put(world, new Vector3Int(x, y, 8), x == 0 ? Stone : Dirt);
                }
                Put(world, new Vector3Int(x, 4, 8), Dirt);
            }
            using var settling = new GroundSettling(world, HoldOf, 0.5f);
            Settle(settling, 100);
            for (int x = 1; x <= 4; x++)
            {
                world.SetDensity(new Vector3Int(x, 3, 8), 0f);
                world.SetDensity(new Vector3Int(x, 2, 8), 0f);
                world.SetDensity(new Vector3Int(x, 1, 8), 0f);
            }
            Settle(settling, 100);
            Assert.That(world.GetDensity(new Vector3Int(2, 4, 8)), Is.EqualTo(1f), "two out of the wall still holds");
            Assert.That(world.GetDensity(new Vector3Int(3, 4, 8)), Is.LessThan(0.5f));
            Assert.That(world.GetDensity(new Vector3Int(4, 4, 8)), Is.LessThan(0.5f), "the far end, never touched, falls too");
        }

        // A stone tunnel along z: walls at x = 0 and x = width + 1 from y = 1, a ceiling at y = 3 and rock over it up to
        // y = 3 + thickness.
        private static World Tunnel(int width, int thickness)
        {
            World world = Room();
            for (int z = 0; z <= 16; z++)
            {
                for (int x = 0; x <= width + 1; x++)
                {
                    bool wall = x == 0 || x == width + 1;
                    for (int y = 1; y <= 3 + thickness; y++)
                    {
                        if (wall || y >= 3)
                        {
                            Put(world, new Vector3Int(x, y, z), Stone);
                        }
                    }
                }
            }
            return world;
        }

        [Test]
        public void ThickRockCeiling_Holds_AThinOneFalls()
        {
            // 13 wide: the middle is 7 from a wall, past stone's 5 but within 5 + the rock over it.
            World thick = Tunnel(13, 4);
            using (var settling = new GroundSettling(thick, HoldOf, 0.5f))
            {
                settling.Wake(new Vector3Int(7, 3, 8));
                Settle(settling, 100);
            }
            Assert.That(thick.GetDensity(new Vector3Int(7, 3, 8)), Is.EqualTo(1f), "a cave under thick rock keeps its ceiling");

            World thin = Tunnel(13, 0);
            using (var settling = new GroundSettling(thin, HoldOf, 0.5f))
            {
                settling.Wake(new Vector3Int(7, 3, 8));
                Settle(settling, 100);
            }
            Assert.That(thin.GetDensity(new Vector3Int(7, 3, 8)), Is.LessThan(0.5f), "a thin slab breaks in the middle");
            Assert.That(thin.GetDensity(new Vector3Int(3, 3, 8)), Is.EqualTo(1f), "but holds near the walls");
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
