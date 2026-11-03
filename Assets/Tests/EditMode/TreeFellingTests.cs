using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>Felling (GL344): what comes off when a limb is cut through, and how it lands.</summary>
    public class TreeFellingTests
    {
        private const byte Dirt = 1;
        private const byte Wood = 11;
        private const byte Leaves = 12;
        private World world;
        private TreeMaterials trees;

        // One 24³ chunk of 1 m voxels: ground up to y = 2; a trunk at x = 6, z = 12 from y = 3 to 12, with leaves round its
        // top; a branch off it at y = 9 out along +x to x = 11, with a ball of leaves round its end.
        [SetUp]
        public void SetUp()
        {
            world = new World(new Vector3Int(24, 24, 24), 1f);
            world.Load(Vector3Int.zero, null);
            trees = new TreeMaterials(new[] { Wood }, new[] { Leaves });
            for (int z = 0; z <= 24; z++)
            {
                for (int x = 0; x <= 24; x++)
                {
                    for (int y = 0; y <= 2; y++)
                    {
                        Set(x, y, z, Dirt);
                    }
                }
            }
            for (int y = 3; y <= 12; y++)
            {
                Set(6, y, 12, Wood);
            }
            for (int x = 7; x <= 11; x++)
            {
                Set(x, 9, 12, Wood);
            }
            Ball(new Vector3Int(11, 10, 12), 2);
            Ball(new Vector3Int(6, 13, 12), 2);
        }

        private void Set(int x, int y, int z, byte material)
        {
            var sample = new Vector3Int(x, y, z);
            world.SetDensity(sample, 1f);
            world.SetMaterial(sample, material);
        }

        // Leaves in every air sample within a radius.
        private void Ball(Vector3Int centre, int radius)
        {
            for (int z = -radius; z <= radius; z++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    for (int x = -radius; x <= radius; x++)
                    {
                        Vector3Int sample = centre + new Vector3Int(x, y, z);
                        if (x * x + y * y + z * z <= radius * radius && (world.GetDensity(sample) ?? 1f) < 0.5f)
                        {
                            Set(sample.x, sample.y, sample.z, Leaves);
                        }
                    }
                }
            }
        }

        private FelledLimb Chop(Vector3Int sample)
        {
            world.SetDensity(sample, 0f);
            return TreeFelling.FindSevered(world, sample, sample, trees, 0.5f);
        }

        private bool Contains(FelledLimb limb, int x, int y, int z)
        {
            var sample = new Vector3Int(x, y, z);
            return limb.Solid.Exists(s => s.Sample == sample);
        }

        [Test]
        public void ChoppingWithoutCuttingThrough_FellsNothing()
        {
            // A leaf off the branch's end: every piece of wood still reaches the ground.
            Assert.That(Chop(new Vector3Int(11, 12, 12)), Is.Null);
            Assert.That(TreeFelling.FindSevered(world, new Vector3Int(6, 5, 12), new Vector3Int(6, 5, 12), trees, 0.5f), Is.Null);
        }

        [Test]
        public void CuttingABranch_FellsItWithItsLeaves_AndNotTheCrown()
        {
            FelledLimb limb = Chop(new Vector3Int(7, 9, 12));
            Assert.That(limb, Is.Not.Null);
            Assert.That(Contains(limb, 11, 9, 12), "The branch's far end falls.");
            Assert.That(Contains(limb, 11, 11, 12), "Its leaves fall with it.");
            Assert.That(Contains(limb, 6, 12, 12), Is.False, "The trunk stays.");
            Assert.That(Contains(limb, 6, 14, 12), Is.False, "The crown's leaves stay.");
            Assert.That(limb.WoodCount, Is.EqualTo(4), "x 8 to 11 of the branch.");
            Assert.That(limb.Pivot, Is.EqualTo(new Vector3Int(8, 9, 12)), "It turns about the cut.");
        }

        [Test]
        public void CuttingTheTrunk_FellsEverythingAbove()
        {
            FelledLimb limb = Chop(new Vector3Int(6, 4, 12));
            Assert.That(limb, Is.Not.Null);
            Assert.That(Contains(limb, 6, 12, 12) && Contains(limb, 11, 9, 12) && Contains(limb, 6, 14, 12) && Contains(limb, 11, 11, 12));
            Assert.That(Contains(limb, 6, 3, 12), Is.False, "The stump below the cut stays.");
        }

        [Test]
        public void Turn_TipsUpTowardsTheFall_AndTheFallDown()
        {
            var east = new Vector3Int(1, 0, 0);
            Assert.That(TreeFelling.Turn(Vector3Int.up, east), Is.EqualTo(east));
            Assert.That(TreeFelling.Turn(east, east), Is.EqualTo(Vector3Int.down));
            var north = new Vector3Int(0, 0, 1);
            Assert.That(TreeFelling.Turn(new Vector3Int(0, 3, 0), north), Is.EqualTo(new Vector3Int(0, 0, 3)));
            Assert.That(TreeFelling.Turn(east, north), Is.EqualTo(east), "Across the fall stays as it is.");
        }

        [Test]
        public void ALandedTrunk_LiesFlatOnTheGround_WoodUnderLeaves_AllTheWoodKept()
        {
            FelledLimb limb = Chop(new Vector3Int(6, 4, 12));
            TreeFelling.Remove(world, limb);
            int placed = TreeFelling.Land(world, limb, new Vector3Int(1, 0, 0), trees, 0.5f);
            Assert.That(placed, Is.LessThan(limb.Solid.Count), "Leaves crush as they land.");
            int wood = 0;
            for (int x = 0; x <= 24; x++)
            {
                for (int z = 0; z <= 24; z++)
                {
                    bool leavesSeen = false;
                    int leafDepth = 0;
                    for (int y = 3; y <= 24; y++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        if ((world.GetDensity(sample) ?? 0f) < 0.5f)
                        {
                            continue;
                        }
                        byte material = world.GetMaterial(sample) ?? 0;
                        if (material == Leaves)
                        {
                            leavesSeen = true;
                            leafDepth++;
                        }
                        else if (material == Wood && !(x == 6 && y == 3))
                        {
                            wood++;
                            Assert.That(leavesSeen, Is.False, $"Wood under leaves in column {x}, {z}.");
                        }
                    }
                    Assert.That(leafDepth, Is.LessThanOrEqualTo(TreeFelling.MaxLeafPile), $"Leaves lie flat in column {x}, {z}.");
                }
            }
            Assert.That(wood, Is.EqualTo(limb.WoodCount), "Every sample of wood lands.");
            Assert.That(world.GetDensity(new Vector3Int(6, 10, 12)), Is.LessThan(0.5f), "The trunk no longer stands.");
            Assert.That(world.GetMaterial(new Vector3Int(13, 3, 12)), Is.EqualTo(Wood), "The trunk lies along the ground, its top 7 m out.");
        }

        [Test]
        public void AFallingLimb_PassesThroughAnotherTree()
        {
            // Another trunk where the felled one lands: its wood is stepped over and still stands.
            for (int y = 3; y <= 8; y++)
            {
                Set(12, y, 12, Wood);
            }
            FelledLimb limb = Chop(new Vector3Int(6, 4, 12));
            TreeFelling.Remove(world, limb);
            int placed = TreeFelling.Land(world, limb, new Vector3Int(1, 0, 0), trees, 0.5f);
            Assert.That(placed, Is.GreaterThanOrEqualTo(limb.WoodCount));
            Assert.That(world.GetMaterial(new Vector3Int(12, 8, 12)), Is.EqualTo(Wood), "The other trunk stands.");
            Assert.That(world.GetDensity(new Vector3Int(12, 9, 12)), Is.GreaterThanOrEqualTo(0.5f), "The fallen wood lies over it there.");
        }
    }
}
