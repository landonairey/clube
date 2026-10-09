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
            Assert.AreEqual(24, furnace.Input.Count(ore), "The ore being smelted stays in until it's done.");
            Assert.AreEqual(5f / 6f, furnace.Progress, 1e-4f);

            furnace.Tick(1f);
            Assert.AreEqual(1, furnace.Output.Count(bun));
            Assert.AreEqual(16, furnace.Input.Count(ore), "Finishing used up the first 8.");

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
            Assert.AreEqual(2, anvil.Input.Count(bun), "The struck bun is still a bun until the last strike.");

            anvil.Strike();
            Assert.AreEqual(1, anvil.Output.Count(ingot));
            Assert.AreEqual(1, anvil.Input.Count(bun), "The last strike turned one bun into the ingot.");
            for (int i = 0; i < 6; i++)
            {
                anvil.Strike();
            }
            Assert.AreEqual(2, anvil.Output.Count(ingot));
            Assert.IsFalse(anvil.Strike(), "Nothing left to strike.");
        }

        [Test]
        public void TakingTheInputOutPartWay_KeepsItAndResetsTheWork()
        {
            CraftingStation anvil = Anvil();
            anvil.Input.Add(bun, 1);
            for (int i = 0; i < 3; i++)
            {
                anvil.Strike();
            }
            Assert.AreEqual(3, anvil.StrikesLeft);

            var player = new Inventory(4);
            Assert.AreEqual(1, anvil.Input.MoveTo(0, player), "The struck bun comes back out whole.");
            Assert.AreEqual(1, player.Count(bun));
            Assert.IsFalse(anvil.IsBusy, "The work is dropped.");

            player.MoveTo(0, anvil.Input);
            anvil.Strike();
            Assert.AreEqual(5, anvil.StrikesLeft, "Put back, it starts over.");
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

        // The worked example from GL33: 1 volume of 80/20 ore → 0.2 copper → a 0.1 bun → a 0.08 ingot.
        [Test]
        public void ByVolume_YieldsMultiplyThroughTheChain()
        {
            VoxelMaterial stone = VoxelMaterial.Create(0, "Stone");
            VoxelMaterial copper = VoxelMaterial.Create(3, "Copper", VoxelMaterialCategory.Ore);
            ItemDefinition smallBun = ItemDefinition.Create("bun", "Bun").With(unitVolume: 0.1f);
            ItemDefinition smallIngot = ItemDefinition.Create("ingot", "Ingot").With(unitVolume: 0.08f);
            Recipe smeltByVolume = Recipe.Create(StationKind.Furnace, new ItemAmount(smallBun, 1), RecipeWork.Time, 1f, new ItemAmount(ore, 1))
                .WithYield(0.5f, copper);
            Recipe hammerByVolume = Recipe.Create(StationKind.Anvil, new ItemAmount(smallIngot, 1), RecipeWork.Strikes, 1f, new ItemAmount(smallBun, 1))
                .WithYield(0.8f, null, CraftSkill.Blacksmithing, 0.95f);
            try
            {
                Composition eightyTwenty = Composition.From(new Content(stone, 0.8f), new Content(copper, 0.2f));
                Assert.AreEqual(0.1f, smeltByVolume.OutputVolume(_ => eightyTwenty, 0f), 1e-5f);
                Assert.AreEqual(0.08f, hammerByVolume.OutputVolume(null, 0f), 1e-5f);
                Assert.AreEqual(0.095f, hammerByVolume.OutputVolume(null, 1f), 1e-5f, "Mastered blacksmithing loses 5%, not 20%.");

                var furnace = new CraftingStation(StationKind.Furnace, new[] { smeltByVolume });
                furnace.Input.Add(ore, 1, eightyTwenty);
                furnace.Tick(1f);
                Assert.AreEqual(1, furnace.Output.Count(smallBun));

                var anvil = new CraftingStation(StationKind.Anvil, new[] { hammerByVolume });
                furnace.Output.MoveTo(0, anvil.Input);
                anvil.Strike();
                Assert.AreEqual(1, anvil.Output.Count(smallIngot));
                Assert.AreEqual(0f, anvil.Held(smallIngot), 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(smeltByVolume);
                Object.DestroyImmediate(hammerByVolume);
                Object.DestroyImmediate(smallBun);
                Object.DestroyImmediate(smallIngot);
                Object.DestroyImmediate(stone);
                Object.DestroyImmediate(copper);
            }
        }

        [Test]
        public void ByVolume_HoldsWhatsShortOfAWholeItem_UntilMoreComesIn()
        {
            VoxelMaterial copper = VoxelMaterial.Create(3, "Copper", VoxelMaterialCategory.Ore);
            ItemDefinition halfBun = ItemDefinition.Create("bun", "Bun").With(unitVolume: 0.5f);
            Recipe smeltByVolume = Recipe.Create(StationKind.Furnace, new ItemAmount(halfBun, 1), RecipeWork.Time, 1f, new ItemAmount(ore, 1))
                .WithYield(0.5f, copper);
            try
            {
                var furnace = new CraftingStation(StationKind.Furnace, new[] { smeltByVolume });
                furnace.Input.Add(ore, 7, Composition.From(new Content(copper, 0.2f), new Content(VoxelMaterial.Create(0, "Stone"), 0.8f)));

                furnace.Tick(4f);
                Assert.AreEqual(0, furnace.Output.Count(halfBun), "0.4 of copper isn't a bun yet.");
                Assert.AreEqual(0.4f, furnace.Held(halfBun), 1e-4f);

                furnace.Tick(3f);
                Assert.AreEqual(1, furnace.Output.Count(halfBun), "The fifth ore makes up the bun.");
                Assert.AreEqual(0.2f, furnace.Held(halfBun), 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(smeltByVolume);
                Object.DestroyImmediate(halfBun);
                Object.DestroyImmediate(copper);
            }
        }

        [Test]
        public void Fuel_WorksOnlyWhileBurning_AndPullsTheNextItemAsLongAsThereIsWork()
        {
            ItemDefinition wood = ItemDefinition.Create("wood", "Wood").With(burnSeconds: 10f);
            ItemDefinition leaves = ItemDefinition.Create("leaves", "Leaves").With(burnSeconds: 1f);
            try
            {
                var furnace = new CraftingStation(StationKind.Furnace, new[] { smelt }, burnsFuel: true);
                furnace.Input.Add(ore, 16);

                furnace.Tick(3f);
                Assert.IsTrue(furnace.NeedsFuel);
                Assert.AreEqual(0f, furnace.Progress, "No fire, no smelting.");

                var player = new Inventory(2);
                player.Add(leaves, 2);
                player.Add(ore, 1);
                Assert.AreEqual(2, furnace.Load(player, 0), "Fuel goes to the fuel slot.");
                Assert.AreEqual(2, furnace.Fuel.Count(leaves));
                Assert.AreEqual(1, furnace.Load(player, 1), "Ore goes to the input.");

                furnace.Tick(3f);
                Assert.AreEqual(2f / 6f, furnace.Progress, 1e-4f, "Two leaves burn a second each.");
                Assert.IsTrue(furnace.NeedsFuel);

                furnace.Fuel.Add(wood, 2);
                furnace.Tick(4f);
                Assert.AreEqual(1, furnace.Output.Count(bun), "The bun finishes on the first log.");
                Assert.AreEqual(1, furnace.Fuel.Count(wood), "One log is burning, the other waits.");
                Assert.AreEqual(10f - 4f, furnace.BurnLeft, 1e-4f);

                furnace.Input.Remove(ore, furnace.Input.Count(ore));
                furnace.Tick(20f);
                Assert.IsFalse(furnace.IsBurning, "The fire burns out with nothing to work.");
                Assert.AreEqual(1, furnace.Fuel.Count(wood), "And no new log is pulled for nothing.");
            }
            finally
            {
                Object.DestroyImmediate(wood);
                Object.DestroyImmediate(leaves);
            }
        }
    }
}
