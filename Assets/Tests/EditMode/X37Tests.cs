using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    // GL840-GL846 (batch X37): pure stacks, switching recipes, whole crafted tools, combining in a station's input.
    public class X37Tests
    {
        [Test]
        public void PureItemsClickedIntoTheBag_OnlyJoinPureStacks()
        {
            VoxelMaterial clay = VoxelMaterial.Create(13, "Clay");
            VoxelMaterial dirt = VoxelMaterial.Create(1, "Dirt");
            ItemDefinition item = ItemDefinition.Create("clay", "Clay", 1024);
            try
            {
                var items = new Inventory(4);
                Composition mixed = Composition.From(new Content(clay, 0.6f), new Content(dirt, 0.4f));
                Composition pure = Composition.From(new Content(clay, 1f));
                items.Replace(0, new ItemStack(item, 10, mixed, 0, 0.6f));
                items.Replace(3, new ItemStack(item, 5, pure, 0, 1f));

                items.MoveInto(3, 0, 3);
                Assert.AreEqual(10, items[0].Count, "The pure stack doesn't sink into the 60% one (GL840).");
                Assert.AreEqual(5, items[1].Count, "It takes an empty slot instead.");
                Assert.AreEqual(1f, items[1].Quality, 1e-4f);

                items.Add(item, 2, pure, 0, 1f);
                Assert.AreEqual(7, items[1].Count, "Pure joins pure by itself.");

                items.Move(1, 0);
                Assert.AreEqual(17, items[0].Count, "Dragged by hand, pure still combines with the lower grade.");
            }
            finally
            {
                Object.DestroyImmediate(clay);
                Object.DestroyImmediate(dirt);
                Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void SwitchingRecipe_DropsTheWorkUnderWay()
        {
            ItemDefinition stone = ItemDefinition.Create("stone", "Stone");
            ItemDefinition spear = ItemDefinition.Create("spear", "Spear");
            ItemDefinition hammer = ItemDefinition.Create("hammer", "Hammer");
            Recipe spearRecipe = Recipe.Create(StationKind.Worktop, new ItemAmount(spear, 1), RecipeWork.Strikes, 4f, new ItemAmount(stone, 1));
            Recipe hammerRecipe = Recipe.Create(StationKind.Worktop, new ItemAmount(hammer, 1), RecipeWork.Strikes, 6f, new ItemAmount(stone, 1));
            var station = new CraftingStation(StationKind.Worktop, new[] { spearRecipe, hammerRecipe });
            try
            {
                station.Input.Add(stone, 1);
                station.Chosen = spearRecipe;
                Assert.IsTrue(station.Strike());
                Assert.AreSame(spearRecipe, station.Active);
                Assert.AreEqual(3, station.StrikesLeft);

                station.Chosen = null;
                Assert.AreSame(spearRecipe, station.Active, "Clearing the pick (reopening the panel) keeps the work.");

                station.Chosen = hammerRecipe;
                Assert.IsNull(station.Active, "Another recipe picked: the spear's work is dropped (GL841).");
                Assert.AreEqual(0f, station.Progress);
                Assert.IsTrue(station.Strike());
                Assert.AreSame(hammerRecipe, station.Active, "The next strike starts the new recipe.");
                Assert.AreEqual(5, station.StrikesLeft, "Its strike counter starts from the top.");
                Assert.AreEqual(1, station.Input.Count(stone), "The inputs were never used.");
            }
            finally
            {
                Object.DestroyImmediate(spearRecipe);
                Object.DestroyImmediate(hammerRecipe);
                Object.DestroyImmediate(stone);
                Object.DestroyImmediate(spear);
                Object.DestroyImmediate(hammer);
            }
        }

        [Test]
        public void ToolsAndThingsToBuild_ComeOutWhole_TheYieldGoingIntoTheirQuality()
        {
            ItemDefinition stone = ItemDefinition.Create("stone", "Stone");
            ItemDefinition ingot = ItemDefinition.Create("iron-ingot", "Iron ingot");
            ToolDefinition hammer = ToolDefinition.Create("stone-hammer", "Stone hammer", 1, 1f);
            PlaceableDefinition anvil = PlaceableDefinition.Create("anvil", "Anvil");
            Recipe hammerRecipe = Recipe.Create(StationKind.Worktop, new ItemAmount(hammer, 1), RecipeWork.Strikes, 1f, new ItemAmount(stone, 1));
            Recipe anvilRecipe = Recipe.Create(StationKind.Worktop, new ItemAmount(anvil, 1), RecipeWork.Strikes, 1f, new ItemAmount(ingot, 1));
            var stump = new CraftingStation(StationKind.Worktop, new[] { hammerRecipe, anvilRecipe }) { Quality = 0.8f };
            try
            {
                stump.Input.Add(stone, 1);
                stump.Chosen = hammerRecipe;
                Assert.IsTrue(stump.Strike());
                Assert.AreEqual(hammer, stump.Output[0].Item);
                Assert.AreEqual(1, stump.Output[0].Count, "One whole hammer (GL842)...");
                Assert.AreEqual(0f, stump.Output[0].Part, 1e-4f);
                Assert.AreEqual(0.8f, stump.Output[0].Quality, 1e-4f, "...at the station's 80%.");
                stump.Output.TakeAll(0);

                stump.Quality = 1f;
                stump.Input.Add(ingot, 1, null, 0, 0.7f);
                stump.Chosen = anvilRecipe;
                Assert.AreEqual(0.7f, stump.ResultQuality(anvilRecipe), 1e-4f);
                Assert.IsTrue(stump.Strike());
                Assert.AreEqual(1, stump.Output[0].Count, "One whole anvil (GL843)...");
                Assert.AreEqual(0.7f, stump.Output[0].Quality, 1e-4f, "...as good as its ingot.");
            }
            finally
            {
                Object.DestroyImmediate(hammerRecipe);
                Object.DestroyImmediate(anvilRecipe);
                Object.DestroyImmediate(stone);
                Object.DestroyImmediate(ingot);
                Object.DestroyImmediate(hammer);
                Object.DestroyImmediate(anvil);
            }
        }

        [Test]
        public void DroppingOnAStationInputStack_CombinesAtAnyQuality()
        {
            ItemDefinition sticks = ItemDefinition.Create("stick", "Stick", 64);
            try
            {
                var bag = new Inventory(2);
                var input = new Inventory(3);
                bag.Add(sticks, 4, null, 0, 0.6f);
                input.Add(sticks, 4, null, 0, 1f);
                Assert.AreEqual(4, bag.MoveToSlot(0, input, 0, int.MaxValue));
                Assert.AreEqual(8, input[0].Count, "The 60% sticks join the 100% stack in the slot (GL845).");
                Assert.AreEqual(0.8f, input[0].Quality, 1e-4f);
                Assert.IsTrue(input[1].IsEmpty);
            }
            finally
            {
                Object.DestroyImmediate(sticks);
            }
        }

        [Test]
        public void APartItem_IsUsedUpWhenOneIsTakenByAmount()
        {
            PlaceableDefinition anvil = PlaceableDefinition.Create("anvil", "Anvil");
            try
            {
                var bag = new Inventory(1);
                bag.Replace(0, new ItemStack(anvil, 0, null, 0, 1f, 0.85f));
                bag.RemoveAmount(0, System.Math.Min(1f, bag[0].Amount));
                Assert.IsTrue(bag[0].IsEmpty, "An old 0.85 anvil built on the ground leaves the inventory (GL843).");
            }
            finally
            {
                Object.DestroyImmediate(anvil);
            }
        }
    }
}
