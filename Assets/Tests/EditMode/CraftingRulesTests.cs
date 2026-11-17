using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>The W1 crafting and inventory rules (GL698-GL707).</summary>
    public class CraftingRulesTests
    {
        private ItemDefinition clay;
        private ItemDefinition copperBun;
        private ItemDefinition copperIngot;
        private ItemDefinition tinBun;
        private ItemDefinition tinIngot;
        private ItemDefinition bronzeBun;
        private ItemDefinition bronzeIngot;
        private ItemDefinition coal;
        private ItemDefinition ironBun;
        private ItemDefinition ironIngot;
        private ItemDefinition steelBun;
        private ItemDefinition steelIngot;
        private ItemDefinition hammer;
        private VoxelMaterial claySample;
        private VoxelMaterial dirt;

        [SetUp]
        public void SetUp()
        {
            clay = ItemDefinition.Create("clay", "Clay", maxStack: 16);
            copperIngot = ItemDefinition.Create("copper-ingot", "Copper ingot", maxStack: 100);
            tinIngot = ItemDefinition.Create("tin-ingot", "Tin ingot", maxStack: 100);
            bronzeIngot = ItemDefinition.Create("bronze-ingot", "Bronze ingot", maxStack: 100);
            ironIngot = ItemDefinition.Create("iron-ingot", "Iron ingot", maxStack: 100);
            steelIngot = ItemDefinition.Create("steel-ingot", "Steel ingot", maxStack: 100);
            copperBun = ItemDefinition.Create("copper-bun", "Copper bun", maxStack: 32).WithIngotForm(copperIngot);
            tinBun = ItemDefinition.Create("tin-bun", "Tin bun", maxStack: 32).WithIngotForm(tinIngot);
            bronzeBun = ItemDefinition.Create("bronze-bun", "Bronze bun", maxStack: 32).WithIngotForm(bronzeIngot);
            ironBun = ItemDefinition.Create("iron-bun", "Iron bun", maxStack: 32).WithIngotForm(ironIngot);
            steelBun = ItemDefinition.Create("steel-bun", "Steel bun", maxStack: 32).WithIngotForm(steelIngot);
            coal = ItemDefinition.Create("coal", "Coal", maxStack: 64);
            hammer = ItemDefinition.Create("stone-hammer", "Stone hammer", maxStack: 1).WithUpgrade(new StationUpgrade(StationKind.FlatRock, 2f));
            claySample = VoxelMaterial.Create(1, "Clay");
            dirt = VoxelMaterial.Create(2, "Dirt");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object made in new Object[] { clay, copperBun, copperIngot, tinBun, tinIngot, bronzeBun, bronzeIngot, coal, ironBun, ironIngot, steelBun, steelIngot, hammer, claySample, dirt })
            {
                Object.DestroyImmediate(made);
            }
        }

        private Composition Pure => Composition.From(new Content(claySample, 1f));

        private Composition Dirty => Composition.From(new Content(claySample, 0.6f), new Content(dirt, 0.4f));

        // GL698
        [Test]
        public void PureStack_IsNotTopUpByLowerGradeMaterial_ButTakesItDraggedOnByHand()
        {
            var items = new Inventory(3);
            items.Add(clay, 4, Pure, 0, 1f);

            // Digging dirty clay: it starts its own stack.
            items.Add(clay, 3, Dirty, 0, 0.6f);
            Assert.AreEqual(4, items[0].Count);
            Assert.AreEqual(3, items[1].Count);
            Assert.AreEqual(0.6f, items[1].Quality, 0.01f);

            // A hand drag still mixes it in.
            items.Move(1, 0);
            Assert.AreEqual(7, items[0].Count);
            Assert.Less(items[0].Quality, 1f);
        }

        [Test]
        public void PureStack_StaysApartFromDirtyOnesWhenSorting()
        {
            var items = new Inventory(6);
            items.Add(clay, 4, Pure, 0, 1f);
            items.Add(clay, 3, Dirty, 0, 0.6f);
            items.Sort(0, 6);

            int stacks = 0;
            for (int i = 0; i < 6; i++)
            {
                stacks += items[i].IsEmpty ? 0 : 1;
            }
            Assert.AreEqual(2, stacks, "Sort leaves the pure stack apart from the dirty one.");
        }

        // GL707
        [Test]
        public void MoveInto_FillsAPartialStackExactly_PartsIncluded()
        {
            var items = new Inventory(4);
            items.Add(coal, 60, null, 0, 1f, 0.4f);
            items.Move(0, 3);
            items.Add(coal, 3, null, 0, 1f, 0.5f);
            Assert.AreEqual(3.5f, items[0].Amount, 0.01f);

            items.MoveInto(3, 0, 1);

            Assert.AreEqual(64f, items[0].Amount, 0.01f, "The hotbar stack is full to exactly its maximum.");
            Assert.AreEqual(0f, items[0].Part, 0.01f);
            Assert.AreEqual(60.4f + 3.5f - 64f, items[3].Amount, 0.01f, "Only the rest stays behind.");
        }

        // GL706
        [Test]
        public void MoveToSlot_PutsItInTheChosenSlot_TopsUpOrSwaps()
        {
            var mine = new Inventory(3);
            var chest = new Inventory(4);
            mine.Add(coal, 10);
            Assert.AreEqual(10, mine.MoveToSlot(0, chest, 2, 10));
            Assert.IsTrue(mine[0].IsEmpty);
            Assert.AreEqual(10, chest[2].Count);

            mine.Add(coal, 5);
            Assert.AreEqual(5, mine.MoveToSlot(0, chest, 2, 5), "Onto the same item: topped up.");
            Assert.AreEqual(15, chest[2].Count);

            mine.Add(clay, 2);
            Assert.AreEqual(2, mine.MoveToSlot(0, chest, 2, 2), "The whole stack onto another item swaps them.");
            Assert.AreEqual(clay, chest[2].Item);
            Assert.AreEqual(coal, mine[0].Item);
        }

        // GL699
        [Test]
        public void Strikes_NeededFallWithBlacksmithing()
        {
            var skills = new SkillSet();
            Recipe hammering = Recipe.Create(StationKind.Anvil, new ItemAmount(copperIngot, 1), RecipeWork.Strikes, 10, new ItemAmount(copperBun, 1))
                .WithExperience(SkillType.Blacksmithing, 3f);
            var anvil = new CraftingStation(StationKind.Anvil, new[] { hammering }) { Learner = skills };

            Assert.AreEqual(10, StrikesToFinish(anvil, copperBun));

            skills.SetLevel(SkillType.Blacksmithing, 10);
            Assert.AreEqual(5, StrikesToFinish(anvil, copperBun), "Level 10 halves them.");
        }

        // GL703
        [Test]
        public void HammerFittedToTheFlatRock_HalvesTheStrikes_AndOnlyOnStationsThatTakeUpgrades()
        {
            Recipe hammering = Recipe.Create(StationKind.FlatRock, new ItemAmount(copperIngot, 1), RecipeWork.Strikes, 12, new ItemAmount(copperBun, 1))
                .WithExperience(SkillType.Blacksmithing, 3f);
            var bare = new CraftingStation(StationKind.FlatRock, new[] { hammering });
            Assert.IsNull(bare.Upgrades);

            var rock = new CraftingStation(StationKind.FlatRock, new[] { hammering }, upgradable: true);
            Assert.AreEqual(12, StrikesToFinish(rock, copperBun));

            var mine = new Inventory(2);
            mine.Add(hammer, 1);
            Assert.AreEqual(1, mine.MoveTo(0, rock.Upgrades, 1));
            Assert.AreEqual(6, StrikesToFinish(rock, copperBun));

            mine.Add(coal, 1);
            Assert.AreEqual(0, mine.MoveTo(0, rock.Upgrades, 1), "Only an upgrade for this station fits the slot.");
        }

        private static int StrikesToFinish(CraftingStation station, ItemDefinition input)
        {
            station.Output.TakeAll(0);
            station.Input.Add(input, 1);
            int strikes = 0;
            while (station.Output[0].IsEmpty && strikes < 100)
            {
                Assert.IsTrue(station.Strike());
                strikes++;
            }
            station.Output.TakeAll(0);
            return strikes;
        }

        // GL700
        [Test]
        public void Alloys_TakeBunsOrIngots_AndMakeTheLesserForm()
        {
            Recipe bronze = Recipe.Create(StationKind.Furnace, new ItemAmount(bronzeBun, 4), RecipeWork.Time, 3, new ItemAmount(copperBun, 1), new ItemAmount(tinBun, 3)).WithIngotForms();
            var furnace = new CraftingStation(StationKind.Furnace, new[] { bronze }, burnsFuel: true);
            Assert.AreEqual(4, furnace.Recipes.Count, "The recipe and its three bun/ingot variants.");

            Recipe Variant(ItemDefinition copper, ItemDefinition tin)
            {
                return furnace.Recipes.Single(r => r.Inputs[0].Item == copper && r.Inputs[1].Item == tin);
            }

            Assert.AreSame(bronzeBun, Variant(copperBun, tinBun).Output.Item);
            Assert.AreSame(bronzeBun, Variant(copperIngot, tinBun).Output.Item, "A copper ingot with a tin bun makes a bun.");
            Assert.AreSame(bronzeBun, Variant(copperBun, tinIngot).Output.Item);
            Assert.AreSame(bronzeIngot, Variant(copperIngot, tinIngot).Output.Item, "Two ingots make an ingot.");
            Assert.AreEqual(4, Variant(copperIngot, tinIngot).Output.Count);
            Assert.AreEqual(3, Variant(copperIngot, tinIngot).Inputs[1].Count);
        }

        // GL700, GL701
        [Test]
        public void Steel_TakesCoalAndAnIronBunOrIngot()
        {
            Recipe steel = Recipe.Create(StationKind.Furnace, new ItemAmount(steelBun, 1), RecipeWork.Time, 12, new ItemAmount(coal, 4), new ItemAmount(ironBun, 1)).WithIngotForms();
            var furnace = new CraftingStation(StationKind.Furnace, new[] { steel }, burnsFuel: true);
            Assert.AreEqual(2, furnace.Recipes.Count);
            Recipe ingots = furnace.Recipes.Single(r => r.Output.Item == steelIngot);
            Assert.AreSame(ironIngot, ingots.Inputs[1].Item);
            Assert.AreSame(coal, ingots.Inputs[0].Item, "Coal has no ingot form.");
        }
    }
}
