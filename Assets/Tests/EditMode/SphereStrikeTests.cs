using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    // GL96: the collecting test's area-of-impact sphere.
    public class SphereStrikeTests
    {
        private ItemDefinition dirtItem;
        private VoxelMaterial dirt;
        private MaterialRegistry registry;

        [SetUp]
        public void SetUp()
        {
            dirtItem = ItemDefinition.Create("dirt", "Dirt");
            dirt = VoxelMaterial.Create(1, "Dirt").WithBreaking(0.5f, dirtItem);
            registry = MaterialRegistry.Create(dirt);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(registry);
            Object.DestroyImmediate(dirt);
            Object.DestroyImmediate(dirtItem);
        }

        [Test]
        public void Targets_AreTheSamplesWithGroundInsideTheSphere_NearestFirst()
        {
            World world = SlabWorld();
            var targets = new List<Vector3Int>();
            SphereStrike.FindTargets(world, new Vector3(2f, 1f, 2f), 1.05f, targets);
            Assert.AreEqual(new Vector3Int(2, 1, 2), targets[0]);
            // Within 1.05 of (2, 1, 2) and with ground (y <= 1): the middle, its 4 sides and the one below.
            Assert.AreEqual(6, targets.Count);
            Assert.IsFalse(targets.Contains(new Vector3Int(2, 2, 2)), "Air above isn't a target.");
        }

        // The hand's 0.2 a hit: five hits take a sample's whole unit, and give its one item.
        [Test]
        public void Lower_TakesTheAmount_AndGivesAnItemPerWholeUnit()
        {
            World world = SlabWorld();
            var target = new List<Vector3Int> { new Vector3Int(2, 1, 2) };
            var owed = new Dictionary<VoxelMaterial, float>();
            var collected = new List<ItemDefinition>();
            for (int hit = 1; hit <= 4; hit++)
            {
                SphereStrike.Lower(world, target, 0.2f, registry, owed, collected);
                Assert.AreEqual(1f - 0.2f * hit, world.GetDensity(target[0]).Value, 0.01f);
                Assert.IsEmpty(collected, "Not a whole unit yet.");
            }
            SphereStrike.Lower(world, target, 0.2f, registry, owed, collected);
            Assert.AreEqual(0f, world.GetDensity(target[0]).Value, 0.01f);
            CollectionAssert.AreEqual(new[] { dirtItem }, collected);
        }

        [Test]
        public void Take_ClearsEverySampleAtOnce_AndGivesAnItemPerSolidOne()
        {
            World world = SlabWorld();
            world.SetDensity(new Vector3Int(1, 1, 2), 0.3f);
            var targets = new List<Vector3Int>();
            SphereStrike.FindTargets(world, new Vector3(2f, 1f, 2f), 1.05f, targets);
            var collected = new List<ItemDefinition>();
            int taken = SphereStrike.Take(world, targets, registry, collected);
            Assert.AreEqual(5, taken, "The softened sample is cleared but isn't a whole item.");
            Assert.AreEqual(5, collected.Count);
            foreach (Vector3Int sample in targets)
            {
                Assert.AreEqual(0f, world.GetDensity(sample).Value);
            }
        }

        // One 4-voxel chunk of dirt up to y = 1, air above.
        private static World SlabWorld()
        {
            var world = new World(new Vector3Int(4, 4, 4), 1f);
            world.Load(Vector3Int.zero, null);
            for (int z = 0; z <= 4; z++)
            {
                for (int y = 0; y <= 4; y++)
                {
                    for (int x = 0; x <= 4; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        world.SetDensity(sample, y <= 1 ? 1f : 0f);
                        world.SetMaterial(sample, 1);
                    }
                }
            }
            return world;
        }
    }
}
