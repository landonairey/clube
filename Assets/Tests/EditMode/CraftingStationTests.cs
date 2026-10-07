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
        private Inventory inventory;

        [SetUp]
        public void SetUp()
        {
            ore = ItemDefinition.Create("copper-ore", "Copper ore");
            bun = ItemDefinition.Create("copper-bun", "Copper bun", maxStack: 4);
            ingot = ItemDefinition.Create("copper-ingot", "Copper ingot");
            smelt = Recipe.Create(StationKind.Furnace, new ItemAmount(bun, 1), RecipeWork.Time, 6f, new ItemAmount(ore, 8));
            hammer = Recipe.Create(StationKind.Anvil, new ItemAmount(ingot, 1), RecipeWork.Strikes, 6f, new ItemAmount(bun, 1));
            inventory = new Inventory(4);
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

        [Test]
        public void Start_TakesTheInputs()
        {
            inventory.Add(ore, 10);
            var furnace = new CraftingStation(StationKind.Furnace);

            Assert.IsTrue(furnace.Start(smelt, inventory));

            Assert.AreEqual(2, inventory.Count(ore));
            Assert.IsTrue(furnace.IsBusy);
        }

        [Test]
        public void Start_RefusesWithoutInputsOnTheWrongStationOrWhileBusy()
        {
            inventory.Add(ore, 7);
            var furnace = new CraftingStation(StationKind.Furnace);
            var anvil = new CraftingStation(StationKind.Anvil);

            Assert.IsFalse(furnace.Start(smelt, inventory), "Seven ore aren't enough.");
            inventory.Add(ore, 9);
            Assert.IsFalse(anvil.Start(smelt, inventory), "An anvil doesn't smelt.");
            Assert.IsTrue(furnace.Start(smelt, inventory));
            Assert.IsFalse(furnace.Start(smelt, inventory), "One recipe at a time.");
            Assert.AreEqual(8, inventory.Count(ore));
        }

        [Test]
        public void TimeRecipe_FinishesAfterItsSeconds()
        {
            inventory.Add(ore, 8);
            var furnace = new CraftingStation(StationKind.Furnace);
            furnace.Start(smelt, inventory);

            furnace.Tick(5f);
            Assert.IsTrue(furnace.IsBusy);
            Assert.AreEqual(5f / 6f, furnace.Progress, 1e-4f);
            Assert.IsFalse(furnace.Strike(), "Smelting isn't struck.");

            furnace.Tick(1f);
            Assert.IsFalse(furnace.IsBusy);
            Assert.AreEqual(bun, furnace.Output.Item);
            Assert.AreEqual(1, furnace.Output.Count);
        }

        [Test]
        public void StrikeRecipe_FinishesAfterItsStrikes()
        {
            inventory.Add(bun, 1);
            var anvil = new CraftingStation(StationKind.Anvil);
            anvil.Start(hammer, inventory);

            anvil.Tick(100f);
            Assert.AreEqual(6, anvil.StrikesLeft, "Time alone doesn't hammer.");
            for (int i = 0; i < 5; i++)
            {
                anvil.Strike();
            }
            Assert.AreEqual(1, anvil.StrikesLeft);

            anvil.Strike();
            Assert.IsFalse(anvil.IsBusy);
            Assert.AreEqual(ingot, anvil.Output.Item);
        }

        [Test]
        public void Output_PilesUpUntilTakenAndBlocksWhenFull()
        {
            inventory.Add(ore, 64);
            var furnace = new CraftingStation(StationKind.Furnace);
            for (int i = 0; i < 4; i++)
            {
                furnace.Start(smelt, inventory);
                furnace.Tick(6f);
            }
            Assert.AreEqual(4, furnace.Output.Count);
            Assert.IsFalse(furnace.CanStart(smelt, inventory), "A fifth bun wouldn't fit on the stack of four.");

            Assert.AreEqual(0, furnace.TakeOutput(inventory));
            Assert.AreEqual(4, inventory.Count(bun));
            Assert.IsTrue(furnace.Output.IsEmpty);
            Assert.IsTrue(furnace.CanStart(smelt, inventory));
        }

        [Test]
        public void TakeOutput_LeavesWhatDoesNotFit()
        {
            inventory.Add(ore, 8);
            var furnace = new CraftingStation(StationKind.Furnace);
            furnace.Start(smelt, inventory);
            furnace.Tick(6f);

            Assert.AreEqual(1, furnace.TakeOutput(new Inventory(0)), "Nowhere to put it: it stays.");
            Assert.AreEqual(1, furnace.Output.Count);
        }
    }
}
