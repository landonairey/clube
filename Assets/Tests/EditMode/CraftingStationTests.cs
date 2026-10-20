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
        public void ChosenRecipe_IsTheOnlyOneThatStarts_WhenRecipesShareInputs()
        {
            ItemDefinition spear = ItemDefinition.Create("spear", "Spear");
            Recipe spearRecipe = Recipe.Create(StationKind.Anvil, new ItemAmount(spear, 1), RecipeWork.Strikes, 1f, new ItemAmount(bun, 2));
            var station = new CraftingStation(StationKind.Anvil, new[] { hammer, spearRecipe });
            try
            {
                station.Input.Add(bun, 2);
                Assert.AreSame(spearRecipe, station.Ready(RecipeWork.Strikes), "Unchosen: the recipe that uses the most of what's loaded (GL81).");

                station.Chosen = hammer;
                Assert.AreSame(hammer, station.Ready(RecipeWork.Strikes));
                for (int i = 0; i < 6; i++)
                {
                    Assert.IsTrue(station.Strike());
                }
                Assert.AreEqual(ingot, station.Output[0].Item);

                station.Chosen = smelt;
                Assert.IsNull(station.Chosen, "A recipe the station doesn't make can't be chosen.");
            }
            finally
            {
                Object.DestroyImmediate(spearRecipe);
                Object.DestroyImmediate(spear);
            }
        }

        // GL81: sticks and stone loaded, nothing picked: the stone pick, not the pointy stick.
        [Test]
        public void Unchosen_TheRecipeUsingTheMostOfWhatsLoadedStarts()
        {
            ItemDefinition stick = ItemDefinition.Create("stick", "Stick");
            ItemDefinition stone = ItemDefinition.Create("stone", "Stone");
            ItemDefinition pointy = ItemDefinition.Create("pointy", "Pointy stick", maxStack: 1);
            ItemDefinition pick = ItemDefinition.Create("pick", "Stone pick", maxStack: 1);
            Recipe whittle = Recipe.Create(StationKind.Worktop, new ItemAmount(pointy, 1), RecipeWork.Strikes, 1f, new ItemAmount(stick, 1));
            Recipe makePick = Recipe.Create(StationKind.Worktop, new ItemAmount(pick, 1), RecipeWork.Strikes, 1f, new ItemAmount(stick, 1), new ItemAmount(stone, 2));
            try
            {
                var stump = new CraftingStation(StationKind.Worktop, new[] { whittle, makePick });
                stump.Input.Add(stick, 1);
                stump.Input.Add(stone, 2);
                Assert.AreSame(makePick, stump.Upcoming);
                Assert.IsTrue(stump.Strike());
                Assert.AreEqual(pick, stump.Output[0].Item);
            }
            finally
            {
                foreach (Object o in new Object[] { whittle, makePick, stick, stone, pointy, pick })
                {
                    Object.DestroyImmediate(o);
                }
            }
        }

        // GL80: picking a recipe loads one batch from the player, sending back what it doesn't take.
        [Test]
        public void LoadFor_TopsUpEachInputToOneBatch_AndReturnsWhatTheRecipeDoesntTake()
        {
            var player = new Inventory(4);
            player.Add(ore, 20);
            CraftingStation furnace = Furnace();
            furnace.Input.Add(bun, 2);
            furnace.Input.Add(ore, 3);

            Assert.AreEqual(5, furnace.LoadFor(smelt, player), "Up to the recipe's 8 ore.");
            Assert.AreEqual(8, furnace.Input.Count(ore));
            Assert.AreEqual(0, furnace.Input.Count(bun), "The buns went back.");
            Assert.AreEqual(2, player.Count(bun));
            Assert.AreEqual(15, player.Count(ore));
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

        // GL61: a fixed count in, a fixed count out, and nothing held between runs.
        [Test]
        public void FixedBatch_LeavesOnlyWholeLeftoverInputs()
        {
            Recipe five = Recipe.Create(StationKind.Furnace, new ItemAmount(bun, 1), RecipeWork.Time, 1f, new ItemAmount(ore, 5));
            try
            {
                var furnace = new CraftingStation(StationKind.Furnace, new[] { five });
                furnace.Input.Add(ore, 7);
                furnace.Tick(10f);
                Assert.AreEqual(1, furnace.Output.Count(bun));
                Assert.AreEqual(2, furnace.Input.Count(ore), "Two ore short of a batch stay as ore.");
                Assert.IsFalse(furnace.IsBusy);
            }
            finally
            {
                Object.DestroyImmediate(five);
            }
        }

        // The campfire bug behind GL61: tin left from an earlier run mustn't stop a different ore for good.
        [Test]
        public void DifferentResultWaitingInTheOutput_HoldsTheNextRecipe_UntilTaken()
        {
            ItemDefinition tinOre = ItemDefinition.Create("tin-ore", "Cassiterite");
            ItemDefinition tinBun = ItemDefinition.Create("tin-bun", "Tin bun");
            Recipe smeltTin = Recipe.Create(StationKind.Furnace, new ItemAmount(tinBun, 1), RecipeWork.Time, 1f, new ItemAmount(tinOre, 3));
            var player = new Inventory(4);
            try
            {
                var furnace = new CraftingStation(StationKind.Furnace, new[] { smeltTin, smelt });
                furnace.Input.Add(tinOre, 3);
                furnace.Tick(2f);
                Assert.AreEqual(1, furnace.Output.Count(tinBun));

                furnace.Input.Add(ore, 8);
                furnace.Tick(10f);
                Assert.AreSame(smelt, furnace.Upcoming, "The copper is what's next.");
                Assert.IsFalse(furnace.OutputFits(smelt), "The tin bun is in the way.");
                Assert.AreEqual(8, furnace.Input.Count(ore));

                furnace.Output.MoveTo(0, player);
                furnace.Tick(10f);
                Assert.AreEqual(1, furnace.Output.Count(bun));
                Assert.AreEqual(0, furnace.Input.Count(ore));
            }
            finally
            {
                Object.DestroyImmediate(smeltTin);
                Object.DestroyImmediate(tinOre);
                Object.DestroyImmediate(tinBun);
            }
        }

        [Test]
        public void Upcoming_IsTheChosenRecipe_OrTheFirstWithItsInputsLoaded()
        {
            CraftingStation anvil = Anvil();
            Assert.IsNull(anvil.Upcoming, "Nothing loaded.");
            anvil.Input.Add(bun, 1);
            Assert.AreSame(hammer, anvil.Upcoming);
            anvil.Chosen = hammer;
            anvil.Input.RemoveAt(0, 1);
            Assert.AreSame(hammer, anvil.Upcoming, "A chosen recipe shows before its inputs are in.");
        }

        // GL60: the anvil only works while used, so its leftovers go back; the furnace works on.
        [Test]
        public void ReturnInputs_MovesWhatFits_AndOnlySmeltersWorkUnattended()
        {
            CraftingStation anvil = Anvil();
            Assert.IsFalse(anvil.WorksUnattended);
            Assert.IsTrue(Furnace().WorksUnattended);

            anvil.Input.Add(bun, 3);
            var player = new Inventory(1);
            player.Add(bun, 2);
            Assert.AreEqual(2, anvil.ReturnInputs(player), "Only 2 more fit on the player's 2 (stacks of 4).");
            Assert.AreEqual(1, anvil.Input.Count(bun));
            Assert.AreEqual(4, player.Count(bun));
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
