using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    // The sixteenth pass: stones by hand, slivers after any hit, amounts by material, bins by kind.
    public class CollectingByKindTests
    {
        private ItemDefinition stoneItem;
        private ItemDefinition dirtItem;
        private VoxelMaterial stone;
        private VoxelMaterial dirt;
        private MaterialRegistry registry;

        [SetUp]
        public void SetUp()
        {
            stoneItem = ItemDefinition.Create("stone", "Stone");
            dirtItem = ItemDefinition.Create("dirt", "Dirt");
            stone = VoxelMaterial.Create(0, "Stone").WithBreaking(1f, stoneItem).WithPickUp(6);
            dirt = VoxelMaterial.Create(1, "Dirt").WithBreaking(1f, dirtItem).WithSoft();
            registry = MaterialRegistry.Create(stone, dirt);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object made in new Object[] { registry, stone, dirt, stoneItem, dirtItem })
            {
                Object.DestroyImmediate(made);
            }
        }

        // GL109: a stone lying on dirt comes up whole with up to 2 of its own samples.
        [Test]
        public void HandPickup_TakesTheStoneAndTwoNeighbours()
        {
            World world = SlabWorld();
            // A stone of four samples in a row on the dirt surface (y = 2).
            for (int x = 1; x <= 4; x++)
            {
                world.SetDensity(new Vector3Int(x, 2, 2), 1f);
                world.SetMaterial(new Vector3Int(x, 2, 2), stone.Id);
            }
            var collected = new List<ItemDefinition>();
            var targets = new List<Vector3Int> { new Vector3Int(2, 2, 2), new Vector3Int(2, 1, 2) };
            int taken = HandPickup.TakeStone(world, targets, new Vector3(2f, 2.4f, 2f), registry, collected);
            Assert.AreEqual(3, taken);
            CollectionAssert.AreEqual(new[] { stoneItem, stoneItem, stoneItem }, collected);
            Assert.AreEqual(0f, world.GetDensity(new Vector3Int(2, 2, 2)));
            Assert.AreEqual(1f, world.GetDensity(new Vector3Int(4, 2, 2)), "Only two neighbours come with it.");
            Assert.AreEqual(1f, world.GetDensity(new Vector3Int(2, 1, 2)), "The dirt under it stays.");
            Assert.AreEqual(0, HandPickup.TakeStone(world, new List<Vector3Int> { new Vector3Int(2, 1, 2) }, Vector3.zero, registry, collected), "Dirt isn't a stone.");
        }

        // GL112: a sample left hanging after a hit comes away with it.
        [Test]
        public void ClearSliversAround_TakesWhatsLeftFloating()
        {
            World world = SlabWorld();
            var floating = new Vector3Int(2, 3, 2);
            world.SetDensity(floating, 1f);
            world.SetDensity(new Vector3Int(2, 2, 2), 1f);
            // A hit empties the sample holding it up.
            world.SetDensity(new Vector3Int(2, 2, 2), 0f);
            var collected = new List<ItemDefinition>();
            int cleared = ToolStrike.ClearSliversAround(world, new[] { new Vector3Int(2, 2, 2) }, registry, new StrikeDamage(), collected);
            Assert.AreEqual(1, cleared);
            Assert.AreEqual(0f, world.GetDensity(floating));
        }

        // GL107: how much a hit takes depends on each sample's material.
        [Test]
        public void Lower_TakesByMaterial()
        {
            World world = SlabWorld();
            world.SetMaterial(new Vector3Int(1, 1, 1), stone.Id);
            var targets = new List<Vector3Int> { new Vector3Int(1, 1, 1), new Vector3Int(2, 1, 1) };
            SphereStrike.Lower(world, targets, material => material == dirt ? 1f : 0.2f, registry,
                new Dictionary<VoxelMaterial, float>(), new List<ItemDefinition>());
            Assert.AreEqual(0.8f, world.GetDensity(new Vector3Int(1, 1, 1)).Value, 0.01f, "Stone gives up 0.2.");
            Assert.AreEqual(0f, world.GetDensity(new Vector3Int(2, 1, 1)).Value, 0.01f, "Dirt gives up all of it.");
        }

        // GL114: materials sort into kinds; GL111: a bin sells for its contents.
        [Test]
        public void Kinds_AndABinsValue()
        {
            Assert.AreEqual(MaterialKind.Rock, stone.Kind);
            Assert.AreEqual(MaterialKind.Soil, dirt.Kind);
            PriceList prices = PriceList.Create(new ItemPrice(stoneItem, 2), new ItemPrice(dirtItem, 1, 10));
            try
            {
                var bin = new VolumeMix();
                bin.Add(stone, 5f);
                bin.Add(dirt, 20f);
                Assert.AreEqual(5f * 2f + 20f * 0.1f, prices.ValueOf(bin), 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(prices);
            }
        }

        // One 4-voxel chunk of dirt up to y = 1, air above.
        private World SlabWorld()
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
                        world.SetMaterial(sample, dirt.Id);
                    }
                }
            }
            return world;
        }
    }
}
