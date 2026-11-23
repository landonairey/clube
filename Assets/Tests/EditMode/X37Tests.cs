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
    }
}
