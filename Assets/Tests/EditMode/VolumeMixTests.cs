using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    // GL99-GL101, GL104: the volume inventory's mix.
    public class VolumeMixTests
    {
        private ItemDefinition stoneItem;
        private ItemDefinition clayItem;
        private ItemDefinition oreItem;
        private VoxelMaterial stone;
        private VoxelMaterial looseStone;
        private VoxelMaterial clay;
        private VoxelMaterial ore;
        private MaterialRegistry registry;

        [SetUp]
        public void SetUp()
        {
            stoneItem = ItemDefinition.Create("stone", "Stone");
            clayItem = ItemDefinition.Create("clay", "Clay");
            oreItem = ItemDefinition.Create("cassiterite", "Cassiterite");
            looseStone = VoxelMaterial.Create(7, "Loose stone").WithBreaking(1f, stoneItem);
            stone = VoxelMaterial.Create(0, "Stone").WithBreaking(1f, stoneItem, looseStone);
            clay = VoxelMaterial.Create(13, "Clay").WithBreaking(1f, clayItem);
            ore = VoxelMaterial.Create(19, "Cassiterite").WithBreaking(1f, oreItem);
            registry = MaterialRegistry.Create(stone, looseStone, clay, ore);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object made in new Object[] { registry, stone, looseStone, clay, ore, stoneItem, clayItem, oreItem })
            {
                Object.DestroyImmediate(made);
            }
        }

        [Test]
        public void Mix_KeepsShares_AndLowGradeOreStaysLowGrade()
        {
            var mix = new VolumeMix();
            mix.Add(stone, 9f);
            mix.Add(ore, 1f, Composition.From(new Content(stone, 0.9f), new Content(ore, 0.1f)));
            Assert.AreEqual(10f, mix.Total, 1e-4f);
            Assert.AreEqual(0.1f, mix.ShareOf(ore), 1e-4f, "A tenth of what was mined is ore...");
            Assert.AreEqual(0.01f, mix.Contents().FractionOf(ore), 1e-4f, "...but only a hundredth of it is cassiterite.");
            Assert.AreEqual(0.1f, mix.Entries[1].FractionOf(ore), 1e-4f, "The ore keeps its own 90/10.");
        }

        [Test]
        public void Take_CutsTheWholeMix_InItsShares()
        {
            var mix = new VolumeMix();
            mix.Add(clay, 76f);
            mix.Add(stone, 24f);
            VolumeMix taken = mix.Take(50f);
            Assert.AreEqual(50f, taken.Total, 1e-3f);
            Assert.AreEqual(0.76f, taken.ShareOf(clay), 1e-3f);
            Assert.AreEqual(50f, mix.Total, 1e-3f);
            Assert.AreEqual(0.76f, mix.ShareOf(clay), 1e-3f, "What's left is the same mix.");
        }

        // GL101: a drop picks each sample's material by the mix, so a roll low in the range lands on the
        // biggest share and one at the top on the smallest.
        [Test]
        public void TakeOne_PicksByWeight()
        {
            var mix = new VolumeMix();
            mix.Add(clay, 3f);
            mix.Add(stone, 1f);
            Assert.IsTrue(mix.TakeOne(0.1f, out VoxelMaterial first, out _));
            Assert.AreEqual(clay, first);
            Assert.IsTrue(mix.TakeOne(0.99f, out VoxelMaterial last, out _));
            Assert.AreEqual(stone, last);
            Assert.AreEqual(2f, mix.Total, 1e-4f);
            Assert.AreEqual(1f, mix.ShareOf(clay), 1e-4f);
        }

        // GL120: what hits aimed at clay gather goes to a clay stack, mixed with the stone that came with
        // it, at clay's share as its quality; and what's built from it keeps that.
        [Test]
        public void Gathering_SortsByTarget_AndCraftsAtItsShare()
        {
            PlaceableDefinition furnace = PlaceableDefinition.Create("clay-furnace", "Clay furnace");
            Recipe build = Recipe.Create(StationKind.Worktop, new ItemAmount(furnace, 1), RecipeWork.Time, 1f, new ItemAmount(clayItem, 10));
            try
            {
                var gathering = new Gathering();
                gathering.Add(clay, clay, 7.6f);
                gathering.Add(clay, stone, 2.4f);
                gathering.Add(stone, stone, 0.5f);
                var ready = new List<GatheredStack>();
                gathering.TakeWhole(registry, ready);
                Assert.AreEqual(1, ready.Count, "Stone aimed at hasn't made a whole item yet.");
                Assert.AreEqual(clayItem, ready[0].Item);
                Assert.AreEqual(10, ready[0].Count);
                Assert.AreEqual(0.76f, ready[0].Quality, 1e-3f);
                Assert.AreEqual(0.5f, gathering.PendingFor(stone), 1e-4f);
                Assert.AreEqual(0f, gathering.PendingFor(clay), 1e-4f);

                var station = new CraftingStation(StationKind.Worktop, new[] { build });
                station.Input.Add(ready[0].Item, ready[0].Count, ready[0].Contents, 0, ready[0].Quality);
                station.Tick(2f);
                ItemStack made = station.Output[0];
                Assert.AreEqual(furnace, made.Item);
                Assert.AreEqual(0.76f, registry.Quality(made.Contents, clayItem), 1e-3f);
            }
            finally
            {
                Object.DestroyImmediate(build);
                Object.DestroyImmediate(furnace);
            }
        }

        // A part of a sample waits for the next hit, aimed at the same material.
        [Test]
        public void Gathering_KeepsWhatsShortOfAWholeItem()
        {
            var gathering = new Gathering();
            var ready = new List<GatheredStack>();
            gathering.Add(clay, clay, 0.6f);
            gathering.TakeWhole(registry, ready);
            Assert.AreEqual(0, ready.Count);
            gathering.Add(clay, clay, 0.6f);
            gathering.TakeWhole(registry, ready);
            Assert.AreEqual(1, ready.Count);
            Assert.AreEqual(1, ready[0].Count);
            Assert.AreEqual(0.2f, gathering.PendingFor(clay), 1e-4f);
        }

        // Stone mined whole and its loose stage both count as stone.
        [Test]
        public void ShareOfAnItem_CountsEveryStageThatGivesIt()
        {
            var mix = new VolumeMix();
            mix.Add(stone, 2f);
            mix.Add(looseStone, 2f);
            mix.Add(clay, 4f);
            Assert.AreEqual(0.5f, registry.ShareOf(mix, stoneItem), 1e-4f);
        }

        // GL99: the volume inventory's tool slot takes tools only.
        [Test]
        public void ToolSlot_TakesOnlyTools()
        {
            ToolDefinition pick = ToolDefinition.Create("pick", "Pick", 1, 1f);
            try
            {
                var slots = new Inventory(3) { Accepts = (slot, item) => slot != 0 || item is ToolDefinition };
                slots.Add(clayItem, 5);
                Assert.IsTrue(slots[0].IsEmpty, "Clay skips the tool slot.");
                Assert.AreEqual(clayItem, slots[1].Item);
                slots.Add(pick, 1);
                Assert.AreEqual(pick, slots[0].Item);
                slots.Move(1, 0);
                Assert.AreEqual(pick, slots[0].Item, "Clay can't be moved onto it either.");
            }
            finally
            {
                Object.DestroyImmediate(pick);
            }
        }
    }
}
