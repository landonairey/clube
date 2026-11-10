using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>The wood a felled piece keeps (GL468): capped by Woodcutting level, the end nearest the cut kept.</summary>
    public class FellingYieldTests
    {
        private const byte Dirt = 1;
        private const byte Wood = 11;
        private const byte Leaves = 12;

        // A straight trunk of wood from y = 0 up, cut at the bottom, with a leaf on top.
        private static FelledLimb Trunk(int height)
        {
            var limb = new FelledLimb { Pivot = Vector3Int.zero, Min = Vector3Int.zero, Max = new Vector3Int(0, height, 0) };
            for (int y = 0; y < height; y++)
            {
                limb.Solid.Add(new FelledSample(new Vector3Int(0, y, 0), Wood, 1f, true));
            }
            limb.Solid.Add(new FelledSample(new Vector3Int(0, height, 0), Leaves, 1f, false));
            return limb;
        }

        [Test]
        public void TheCap_IsTenAtLevelZero_AndGrowsTenPercentALevel_Compounding()
        {
            Assert.That(FellingYield.WoodCap(0), Is.EqualTo(10f).Within(1e-4f));
            Assert.That(FellingYield.WoodCap(1), Is.EqualTo(11f).Within(1e-4f));
            Assert.That(FellingYield.WoodCap(2), Is.EqualTo(12.1f).Within(1e-4f));
            Assert.That(FellingYield.WoodCap(10), Is.EqualTo(10f * Mathf.Pow(1.1f, 10)).Within(1e-3f));
            Assert.That(FellingYield.WoodCap(-3), Is.EqualTo(10f), "No level under 0.");
        }

        [Test]
        public void APieceUnderTheCap_KeepsAllItsWood()
        {
            FelledLimb limb = Trunk(8);
            Assert.That(FellingYield.CapWood(limb, FellingYield.WoodCap(0)), Is.EqualTo(0));
            Assert.That(limb.Solid.Count, Is.EqualTo(9));
            Assert.That(limb.WoodLost, Is.EqualTo(0));
        }

        [Test]
        public void APieceOverTheCap_KeepsTheWoodNearestTheCut_AndAllItsLeaves()
        {
            FelledLimb limb = Trunk(30);
            int lost = FellingYield.CapWood(limb, FellingYield.WoodCap(0));
            Assert.That(lost, Is.EqualTo(20));
            Assert.That(limb.WoodLost, Is.EqualTo(20));
            Assert.That(limb.WoodCount, Is.EqualTo(10));
            Assert.That(limb.Solid.FindAll(s => s.IsWood).TrueForAll(s => s.Sample.y < 10), "The bottom 10 m of the trunk stays.");
            Assert.That(limb.Solid.Exists(s => !s.IsWood), "Leaves aren't capped.");
        }

        [Test]
        public void AHigherLevel_KeepsMore()
        {
            FelledLimb limb = Trunk(30);
            FellingYield.CapWood(limb, FellingYield.WoodCap(5));
            Assert.That(limb.WoodCount, Is.EqualTo(16), "10 x 1.1^5 = 16.1.");
        }

        [Test]
        public void AFelledTree_LandsWithOnlyTheCappedWood()
        {
            var world = new World(new Vector3Int(24, 24, 24), 1f);
            world.Load(Vector3Int.zero, null);
            var trees = new TreeMaterials(new[] { Wood }, new[] { Leaves });
            for (int z = 0; z <= 24; z++)
            {
                for (int x = 0; x <= 24; x++)
                {
                    for (int y = 0; y <= 2; y++)
                    {
                        Set(world, new Vector3Int(x, y, z), Dirt);
                    }
                }
            }
            for (int y = 3; y <= 20; y++)
            {
                Set(world, new Vector3Int(4, y, 12), Wood);
            }
            var cut = new Vector3Int(4, 4, 12);
            world.SetDensity(cut, 0f);
            FelledLimb limb = TreeFelling.FindSevered(world, cut, cut, trees, 0.5f);
            Assert.That(limb.WoodCount, Is.EqualTo(16), "y 5 to 20.");
            TreeFelling.Remove(world, limb);
            FellingYield.CapWood(limb, FellingYield.WoodCap(0));
            TreeFelling.Land(world, limb, new Vector3Int(1, 0, 0), trees, 0.5f);

            int wood = 0;
            for (int x = 0; x <= 24; x++)
            {
                for (int y = 3; y <= 24; y++)
                {
                    for (int z = 0; z <= 24; z++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        wood += (world.GetDensity(sample) ?? 0f) >= 0.5f && world.GetMaterial(sample) == Wood ? 1 : 0;
                    }
                }
            }
            Assert.That(wood, Is.EqualTo(10 + 1), "The capped 10 on the ground, and the stump at y = 3.");
        }

        private static void Set(World world, Vector3Int sample, byte material)
        {
            world.SetDensity(sample, 1f);
            world.SetMaterial(sample, material);
        }
    }
}
