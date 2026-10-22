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

        // GL114: materials sort into the kinds tools are made for.
        [Test]
        public void Kinds_ByMaterial()
        {
            Assert.AreEqual(MaterialKind.Rock, stone.Kind);
            Assert.AreEqual(MaterialKind.Soil, dirt.Kind);
        }

        // GL118: purity carries through crafting: wood gathered 60/40 with leaves makes 60% sticks, which make a 60%
        // pointy stick that lasts 60% of its hits.
        [Test]
        public void Quality_CarriesFromGatheredWoodToTool()
        {
            ItemDefinition woodItem = ItemDefinition.Create("wood", "Wood");
            ItemDefinition leafItem = ItemDefinition.Create("leaves", "Leaves");
            ItemDefinition sticks = ItemDefinition.Create("sticks", "Sticks");
            ToolDefinition pointy = ToolDefinition.Create("pointy-stick", "Pointy stick", 1, 1f).WithDurability(60);
            VoxelMaterial wood = VoxelMaterial.Create(11, "Wood", VoxelMaterialCategory.Plant).WithBreaking(1f, woodItem);
            VoxelMaterial leaves = VoxelMaterial.Create(12, "Leaves", VoxelMaterialCategory.Plant).WithBreaking(1f, leafItem);
            MaterialRegistry plants = MaterialRegistry.Create(wood, leaves);
            Recipe whittle = Recipe.Create(StationKind.Worktop, new ItemAmount(sticks, 2), RecipeWork.Time, 1f, new ItemAmount(woodItem, 1));
            Recipe sharpen = Recipe.Create(StationKind.Worktop, new ItemAmount(pointy, 1), RecipeWork.Time, 1f, new ItemAmount(sticks, 2));
            try
            {
                var gathering = new Gathering();
                gathering.Add(wood, wood, 0.6f);
                gathering.Add(wood, leaves, 0.4f);
                var ready = new List<GatheredStack>();
                gathering.TakeWhole(plants, ready);
                Assert.AreEqual(woodItem, ready[0].Item, "Aimed at wood: a wood stack.");
                var stump = new CraftingStation(StationKind.Worktop, new[] { whittle });
                stump.Input.Add(ready[0].Item, ready[0].Count, ready[0].Contents, 0, ready[0].Quality);
                stump.Tick(2f);
                Assert.AreEqual(0.6f, stump.Output[0].Quality, 1e-3f, "Sticks as good as the wood was pure.");

                var bench = new CraftingStation(StationKind.Worktop, new[] { sharpen });
                stump.Output.MoveTo(0, bench.Input);
                bench.Tick(2f);
                ItemStack made = bench.Output[0];
                Assert.AreEqual(pointy, made.Item);
                Assert.AreEqual(0.6f, made.Quality, 1e-3f);
                Assert.AreEqual(36, made.MaxHits, "60% of 60 hits.");

                var hands = new Inventory(1);
                hands.Add(made.Item, 1, made.Contents, 0, made.Quality);
                for (int hit = 1; hit < 36; hit++)
                {
                    Assert.IsFalse(hands.Wear(0, pointy.MaxHits));
                }
                Assert.IsTrue(hands.Wear(0, pointy.MaxHits), "Breaks at its 36th hit.");
            }
            finally
            {
                foreach (Object made in new Object[] { whittle, sharpen, plants, wood, leaves, woodItem, leafItem, sticks, pointy })
                {
                    Object.DestroyImmediate(made);
                }
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
