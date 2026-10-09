using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>Mixed ore contents (GL32) and how inventories carry and blend them.</summary>
    public class CompositionTests
    {
        private VoxelMaterial stone;
        private VoxelMaterial copper;
        private VoxelMaterial silver;
        private ItemDefinition ore;

        [SetUp]
        public void SetUp()
        {
            stone = VoxelMaterial.Create(0, "Stone");
            copper = VoxelMaterial.Create(3, "Copper", VoxelMaterialCategory.Ore);
            silver = VoxelMaterial.Create(4, "Silver", VoxelMaterialCategory.Ore);
            ore = ItemDefinition.Create("copper-ore", "Copper ore", 256);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(stone);
            Object.DestroyImmediate(copper);
            Object.DestroyImmediate(silver);
            Object.DestroyImmediate(ore);
        }

        [Test]
        public void From_ScalesSharesToOne_LargestFirst_AndMergesRepeats()
        {
            Composition mix = Composition.From(new Content(copper, 1f), new Content(stone, 3f), new Content(stone, 1f));

            Assert.AreEqual(2, mix.Parts.Count);
            Assert.AreSame(stone, mix.Parts[0].Material);
            Assert.AreEqual(0.8f, mix.FractionOf(stone), 1e-5f);
            Assert.AreEqual(0.2f, mix.FractionOf(copper), 1e-5f);
            Assert.AreEqual(0f, mix.FractionOf(silver));
            Assert.AreEqual("80% Stone, 20% Copper", mix.ToString());
        }

        [Test]
        public void Blend_WeightsByVolume()
        {
            Composition rich = Composition.From(new Content(stone, 0.6f), new Content(copper, 0.4f));
            Composition poor = Composition.From(new Content(stone, 0.9f), new Content(copper, 0.1f));

            Composition mixed = Composition.Blend(rich, 1f, poor, 3f);

            Assert.AreEqual((0.4f + 0.3f) / 4f, mixed.FractionOf(copper), 1e-5f);
            Assert.AreSame(rich, Composition.Blend(rich, 2f, null, 5f), "Nothing known about the other lot: keep what is.");
        }

        [Test]
        public void Inventory_ToppingUpAStack_BlendsItsContents_AndMovingKeepsThem()
        {
            Composition rich = Composition.From(new Content(stone, 0.6f), new Content(copper, 0.4f));
            Composition poor = Composition.From(new Content(stone, 0.9f), new Content(copper, 0.1f));
            var bag = new Inventory(2);

            bag.Add(ore, 1, rich);
            bag.Add(ore, 1, poor);

            Assert.AreEqual(2, bag[0].Count);
            Assert.AreEqual(0.25f, bag[0].Contents.FractionOf(copper), 1e-5f);
            Assert.AreEqual(2f, bag[0].Volume, 1e-5f);

            var station = new Inventory(1);
            bag.MoveTo(0, station);
            Assert.AreEqual(0.25f, station.ContentsOf(ore).FractionOf(copper), 1e-5f);
        }

        [Test]
        public void Spec_ContentsFor_IsTheGradeOfOre_ExtrasInRange_AndGangueForTheRest()
        {
            var spec = new OreSpec { Ore = copper, Grade = new Vector2(0.1f, 0.3f), Gangue = stone };
            spec.ExtraContents.Add(new OreContentRange(silver, 0f, 0.1f));

            Composition low = spec.ContentsFor(0f, i => 0f);
            Composition high = spec.ContentsFor(1f, i => 1f);

            Assert.AreEqual(0.1f, low.FractionOf(copper), 1e-5f);
            Assert.AreEqual(0.9f, low.FractionOf(stone), 1e-5f);
            Assert.AreEqual(0.3f, high.FractionOf(copper), 1e-5f);
            Assert.AreEqual(0.1f, high.FractionOf(silver), 1e-5f);
            Assert.AreEqual(0.6f, high.FractionOf(stone), 1e-5f);
        }
    }
}
