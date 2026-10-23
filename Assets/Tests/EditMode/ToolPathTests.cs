using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core.Tests
{
    // The twentieth pass: tool tiers, crushing, stones in the dirt, maple sticks.
    public class ToolPathTests
    {
        private ItemDefinition stoneItem;
        private VoxelMaterial stone;
        private VoxelMaterial dirt;
        private VoxelMaterial ore;
        private MaterialRegistry registry;

        [SetUp]
        public void SetUp()
        {
            stoneItem = ItemDefinition.Create("stone", "Stone");
            stone = VoxelMaterial.Create(0, "Stone").WithBreaking(1f, stoneItem).WithPickUp(3).WithToolTier(1);
            dirt = VoxelMaterial.Create(1, "Dirt").WithSoft();
            ore = VoxelMaterial.Create(19, "Cassiterite", VoxelMaterialCategory.Ore).WithToolTier(1);
            registry = MaterialRegistry.Create(stone, dirt, ore);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object made in new Object[] { registry, stone, dirt, ore, stoneItem })
            {
                Object.DestroyImmediate(made);
            }
        }

        // GL145, GL152: a tool works what its tier reaches.
        [Test]
        public void Tools_WorkUpToTheirTier()
        {
            ToolDefinition hand = ToolDefinition.Create("hand", "Hand", 1, 1f).WithTier(0);
            ToolDefinition handAxe = ToolDefinition.Create("hand-axe", "Hand axe", 1, 1f).WithTier(1, 0.3f);
            VoxelMaterial maple = VoxelMaterial.Create(21, "Maple wood", VoxelMaterialCategory.Plant).WithToolTier(3, SkillType.Woodcutting, 3);
            try
            {
                Assert.IsTrue(hand.CanWork(dirt));
                Assert.IsFalse(hand.CanWork(stone), "Bare hands don't take stone.");
                Assert.IsTrue(handAxe.CanWork(stone));
                Assert.IsFalse(handAxe.CanWork(maple), "Maple needs a bronze tool.");
                Assert.AreEqual(0.3f, handAxe.Efficiency, 1e-4f);
                Assert.AreEqual(SkillType.Woodcutting, maple.RequiredSkill);
                Assert.AreEqual(3, maple.RequiredLevel);
            }
            finally
            {
                Object.DestroyImmediate(hand);
                Object.DestroyImmediate(handAxe);
                Object.DestroyImmediate(maple);
            }
        }

        // GL148: crushing keeps the ore and knocks off half the rock.
        [Test]
        public void Crushing_HalvesTheRock()
        {
            Composition raw = Composition.From(new Content(ore, 0.18f), new Content(stone, 0.82f));
            Composition crushed = Crushing.Crush(raw, out float knockedOff);
            Assert.AreEqual(0.41f, knockedOff, 1e-4f);
            Assert.AreEqual(0.18f / 0.59f, crushed.FractionOf(ore), 1e-4f);
            Assert.IsNull(Crushing.Crush(null, out float none));
            Assert.AreEqual(0f, none);
        }

        // GL149: a stone in the dirt comes out by hand; one against ore doesn't.
        [Test]
        public void HandPickup_TakesAStoneBuriedInDirt_ButNotOneAgainstOre()
        {
            World world = DirtWorld();
            var buried = new Vector3Int(2, 1, 2);
            world.SetMaterial(buried, stone.Id);
            var collected = new List<ItemDefinition>();
            Assert.AreEqual(1, HandPickup.TakeStone(world, new List<Vector3Int> { buried }, new Vector3(2f, 1f, 2f), registry, collected));
            Assert.AreEqual(0f, world.GetDensity(buried));

            var againstOre = new Vector3Int(1, 1, 1);
            world.SetMaterial(againstOre, stone.Id);
            world.SetMaterial(againstOre + Vector3Int.right, ore.Id);
            Assert.AreEqual(0, HandPickup.TakeStone(world, new List<Vector3Int> { againstOre }, new Vector3(1f, 1f, 1f), registry, collected));
        }

        // GL149: buried stones are 1-3 samples, the same for every chunk that asks.
        [Test]
        public void BuriedStones_AreSmallAndRepeatable()
        {
            int stones = 0;
            for (int x = 0; x < 64; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    for (int z = 0; z < 64; z++)
                    {
                        var sample = new int3(x, y, z);
                        bool here = ChunkFillKernel.IsBuriedStone(7, sample, 0.01f);
                        Assert.AreEqual(here, ChunkFillKernel.IsBuriedStone(7, sample, 0.01f));
                        stones += here ? 1 : 0;
                    }
                }
            }
            // About 1% of samples start one, each 1-3 samples: 1x to 2x the starts on average.
            Assert.Greater(stones, 64 * 16 * 64 * 0.01f * 0.8f);
            Assert.Less(stones, 64 * 16 * 64 * 0.01f * 2.5f);
            Assert.IsFalse(ChunkFillKernel.IsBuriedStone(7, int3.zero, 0f));
        }

        // GL151: maple whittles into sticks better than ordinary, and tools made of them last longer.
        [Test]
        public void QualityAboveOrdinary_MakesToolsLastLonger()
        {
            ItemDefinition mapleWood = ItemDefinition.Create("maple-wood", "Maple wood");
            ItemDefinition sticks = ItemDefinition.Create("stick", "Stick");
            ToolDefinition pick = ToolDefinition.Create("stone-pick", "Stone pick", 1, 1f).WithDurability(100);
            Recipe whittle = Recipe.Create(StationKind.Worktop, new ItemAmount(sticks, 4), RecipeWork.Time, 1f, new ItemAmount(mapleWood, 1)).WithQualityFactor(1.4f);
            Recipe make = Recipe.Create(StationKind.Worktop, new ItemAmount(pick, 1), RecipeWork.Time, 1f, new ItemAmount(sticks, 1));
            try
            {
                var stump = new CraftingStation(StationKind.Worktop, new[] { whittle, make });
                stump.Chosen = whittle;
                stump.Input.Add(mapleWood, 1);
                stump.Tick(2f);
                Assert.AreEqual(1.4f, stump.Output[0].Quality, 1e-4f, "Maple sticks at 140%.");
                stump.Output.MoveTo(0, stump.Input);
                stump.Chosen = make;
                stump.Tick(2f);
                Assert.AreEqual(140, stump.Output[0].MaxHits, "A pick of maple lasts 140 hits, not 100.");
            }
            finally
            {
                foreach (Object made in new Object[] { whittle, make, mapleWood, sticks, pick })
                {
                    Object.DestroyImmediate(made);
                }
            }
        }

        // Solid dirt up to y = 2 in one chunk.
        private World DirtWorld()
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
                        world.SetDensity(sample, y <= 2 ? 1f : 0f);
                        world.SetMaterial(sample, dirt.Id);
                    }
                }
            }
            return world;
        }
    }
}
