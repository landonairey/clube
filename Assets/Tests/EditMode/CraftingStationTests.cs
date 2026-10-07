using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class CraftingStationTests
    {
        private ItemDefinition ore;
        private ItemDefinition bun;
        private ItemDefinition ingot;
        private Recipe smelt;
        private Recipe hammer;

        [SetUp]
        public void SetUp()
        {
            ore = ItemDefinition.Create("copper-ore", "Copper ore");
            bun = ItemDefinition.Create("copper-bun", "Copper bun", maxStack: 4);
            ingot = ItemDefinition.Create("copper-ingot", "Copper ingot");
            smelt = Recipe.Create(StationKind.Furnace, new ItemAmount(bun, 1), RecipeWork.Time, 6f, new ItemAmount(ore, 8));
            hammer = Recipe.Create(StationKind.Anvil, new ItemAmount(ingot, 1), RecipeWork.Strikes, 6f, new ItemAmount(bun, 1));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(smelt);
            Object.DestroyImmediate(hammer);
            Object.DestroyImmediate(ore);
            Object.DestroyImmediate(bun);
            Object.DestroyImmediate(ingot);
        }

        private CraftingStation Furnace()
        {
            return new CraftingStation(StationKind.Furnace, new[] { smelt, hammer });
        }

        private CraftingStation Anvil()
        {
            return new CraftingStation(StationKind.Anvil, new[] { smelt, hammer });
        }

        [Test]
        public void Station_KeepsOnlyItsOwnKindOfRecipe()
        {
            CollectionAssert.AreEqual(new[] { smelt }, Furnace().Recipes);
            CollectionAssert.AreEqual(new[] { hammer }, Anvil().Recipes);
        }

        [Test]
        public void Furnace_WorksThroughALoadedStack_OneRecipeAtATime()
        {
            CraftingStation furnace = Furnace();
            furnace.Input.Add(ore, 24);

            furnace.Tick(5f);
            Assert.IsTrue(furnace.IsBusy);
            Assert.AreEqual(16, furnace.Input.Count(ore), "The first 8 went in when it started.");
            Assert.AreEqual(5f / 6f, furnace.Progress, 1e-4f);

            furnace.Tick(1f);
            Assert.AreEqual(1, furnace.Output.Count(bun));

            furnace.Tick(12f);
            Assert.AreEqual(3, furnace.Output.Count(bun));
            Assert.AreEqual(0, furnace.Input.Count(ore));
            Assert.IsFalse(furnace.IsBusy);
        }

        [Test]
        public void Furnace_StaysIdleWithoutAFullLoad()
        {
            CraftingStation furnace = Furnace();
            furnace.Input.Add(ore, 7);

            furnace.Tick(60f);

            Assert.IsFalse(furnace.IsBusy);
            Assert.AreEqual(7, furnace.Input.Count(ore));
            Assert.IsTrue(furnace.Output[0].IsEmpty);
            Assert.IsFalse(furnace.Strike(), "Smelting isn't struck.");
        }

        [Test]
        public void Anvil_HammersOneBunPerSixStrikes_AndTimeDoesNothing()
        {
            CraftingStation anvil = Anvil();
            anvil.Input.Add(bun, 2);

            anvil.Tick(100f);
            Assert.IsFalse(anvil.IsBusy, "Time alone doesn't hammer.");

            for (int i = 0; i < 5; i++)
            {
                Assert.IsTrue(anvil.Strike());
            }
            Assert.AreEqual(1, anvil.StrikesLeft);
            Assert.AreEqual(1, anvil.Input.Count(bun), "The first bun is on the anvil.");

            anvil.Strike();
            Assert.AreEqual(1, anvil.Output.Count(ingot));
            for (int i = 0; i < 6; i++)
            {
                anvil.Strike();
            }
            Assert.AreEqual(2, anvil.Output.Count(ingot));
            Assert.IsFalse(anvil.Strike(), "Nothing left to strike.");
        }

        [Test]
        public void FullOutput_StopsTheStation_UntilTaken()
        {
            CraftingStation furnace = Furnace();
            furnace.Input.Add(ore, 48);

            furnace.Tick(600f);
            Assert.AreEqual(4, furnace.Output.Count(bun), "Four buns fill the output stack.");
            Assert.AreEqual(16, furnace.Input.Count(ore));
            Assert.IsFalse(furnace.IsBusy);

            var player = new Inventory(4);
            Assert.AreEqual(4, furnace.Output.MoveTo(0, player));
            furnace.Tick(12f);
            Assert.AreEqual(2, furnace.Output.Count(bun));
            Assert.AreEqual(4, player.Count(bun));
        }

        [Test]
        public void MoveTo_MovesWhatFitsAndLeavesTheRest()
        {
            var player = new Inventory(2);
            player.Add(ore, 64);
            player.Add(ore, 30);
            var input = new Inventory(1);
            input.Add(ore, 50);

            Assert.AreEqual(14, player.MoveTo(0, input), "Only 14 more fit on the 50.");
            Assert.AreEqual(50, player[0].Count);
            Assert.AreEqual(64, input.Count(ore));
        }
    }
}
