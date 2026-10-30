using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class InventoryTests
    {
        private ItemDefinition ore;
        private ItemDefinition stone;
        private ToolDefinition pickaxe;

        [SetUp]
        public void SetUp()
        {
            ore = ItemDefinition.Create("copper-ore", "Copper ore", maxStack: 10);
            stone = ItemDefinition.Create("stone", "Stone", maxStack: 10);
            pickaxe = ToolDefinition.Create("pickaxe", "Pickaxe", 3, 1.5f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(ore);
            Object.DestroyImmediate(stone);
            Object.DestroyImmediate(pickaxe);
        }

        [Test]
        public void Add_FillsAStackToItsMaximumThenTheNextSlot()
        {
            var inventory = new Inventory(3);

            Assert.AreEqual(0, inventory.Add(ore, 14));

            Assert.AreEqual(10, inventory[0].Count);
            Assert.AreEqual(4, inventory[1].Count);
            Assert.IsTrue(inventory[2].IsEmpty);
            Assert.AreEqual(14, inventory.Count(ore));
        }

        [Test]
        public void Add_TopsUpAnExistingStackBeforeUsingAnEmptySlot()
        {
            var inventory = new Inventory(3);
            inventory.Add(stone, 1);
            inventory.Add(ore, 3);
            inventory.Move(1, 2);

            inventory.Add(ore, 2);

            Assert.IsTrue(inventory[1].IsEmpty, "The ore went onto its stack in slot 2, not into the empty slot 1.");
            Assert.AreEqual(5, inventory[2].Count);
        }

        [Test]
        public void Add_ReturnsWhatDoesNotFit()
        {
            var inventory = new Inventory(2);

            Assert.AreEqual(5, inventory.Add(ore, 25));
            Assert.AreEqual(20, inventory.Count(ore));
        }

        [Test]
        public void Tools_TakeASlotEach()
        {
            var inventory = new Inventory(3);

            inventory.Add(pickaxe, 2);

            Assert.AreEqual(1, inventory[0].Count);
            Assert.AreEqual(1, inventory[1].Count);
        }

        [Test]
        public void Remove_TakesFromTheLastStacksAndEmptiesThem()
        {
            var inventory = new Inventory(3);
            inventory.Add(ore, 14);

            Assert.IsTrue(inventory.Remove(ore, 6));

            Assert.AreEqual(8, inventory[0].Count);
            Assert.IsTrue(inventory[1].IsEmpty);
        }

        [Test]
        public void Remove_RefusesWhenThereAreNotEnoughAndChangesNothing()
        {
            var inventory = new Inventory(2);
            inventory.Add(ore, 3);
            int changes = 0;
            inventory.Changed += () => changes++;

            Assert.IsFalse(inventory.Remove(ore, 4));
            Assert.AreEqual(3, inventory.Count(ore));
            Assert.AreEqual(0, changes);
        }

        [Test]
        public void RemoveAt_TakesFromThatSlotOnly()
        {
            var inventory = new Inventory(3);
            inventory.Add(ore, 14);

            Assert.AreEqual(3, inventory.RemoveAt(1, 3));
            Assert.AreEqual(1, inventory.RemoveAt(1, 10), "Only what the slot still holds.");
            Assert.IsTrue(inventory[1].IsEmpty);
            Assert.AreEqual(10, inventory[0].Count);
        }

        [Test]
        public void Move_SwapsDifferentItemsAndMergesTheSameItem()
        {
            var inventory = new Inventory(3);
            inventory.Add(ore, 10);
            inventory.Add(stone, 2);
            inventory.Add(ore, 7);

            inventory.Move(0, 1);
            Assert.AreEqual(stone, inventory[0].Item);
            Assert.AreEqual(ore, inventory[1].Item);

            // 7 ore onto a full stack of 10: nothing fits, so they swap.
            inventory.Move(2, 1);
            Assert.AreEqual(7, inventory[1].Count);
            Assert.AreEqual(10, inventory[2].Count);

            // 10 onto 7 tops it up to 10 and leaves 7.
            inventory.Move(2, 1);
            Assert.AreEqual(10, inventory[1].Count);
            Assert.AreEqual(7, inventory[2].Count);
        }

        [Test]
        public void Changes_RaiseChanged()
        {
            var inventory = new Inventory(2);
            int changes = 0;
            inventory.Changed += () => changes++;

            inventory.Add(ore, 2);
            inventory.Remove(ore, 1);
            inventory.Add(stone, 1);
            inventory.Move(0, 1);

            Assert.AreEqual(4, changes);
        }

        [Test]
        public void Wear_BreaksTheToolAfterItsHits_AndTravelsWithTheStack()
        {
            ToolDefinition worn = ToolDefinition.Create("worn", "Worn", 3, 1.5f).WithDurability(3);
            var inventory = new Inventory(4);
            inventory.Add(worn);

            Assert.IsFalse(inventory.Wear(0, worn.MaxHits));
            inventory.Move(0, 2);
            Assert.AreEqual(1, inventory[2].Wear, "Wear moves with the tool.");
            Assert.IsFalse(inventory.Wear(2, worn.MaxHits));
            Assert.IsTrue(inventory.Wear(2, worn.MaxHits), "The third hit breaks it.");
            Assert.IsTrue(inventory[2].IsEmpty);
            Object.DestroyImmediate(worn);
        }

        [Test]
        public void ReachesWideOn_FollowsTheGroundTheToolIsMadeFor()
        {
            VoxelMaterial dirt = VoxelMaterial.Create(1, "Dirt").WithSoft();
            VoxelMaterial stone = VoxelMaterial.Create(0, "Stone");
            ToolDefinition shovel = ToolDefinition.Create("shovel", "Shovel", 4, 1.5f).WithWideOn(ReachOn.Soft);
            ToolDefinition axe = ToolDefinition.Create("axe", "Axe", 4, 1.5f).WithWideOn(ReachOn.Hard);
            ToolDefinition hand = ToolDefinition.Create("hand", "Hand", 1, 1f);

            Assert.IsTrue(shovel.ReachesWideOn(dirt));
            Assert.IsFalse(shovel.ReachesWideOn(stone));
            Assert.IsFalse(axe.ReachesWideOn(dirt));
            Assert.IsTrue(axe.ReachesWideOn(stone));
            Assert.IsTrue(axe.ReachesWideOn(null), "Unknown ground counts as hard.");
            Assert.IsTrue(hand.ReachesWideOn(dirt) && hand.ReachesWideOn(stone));

            VoxelMaterial wood = VoxelMaterial.Create(11, "Wood", VoxelMaterialCategory.Plant);
            ToolDefinition hatchet = ToolDefinition.Create("hatchet", "Hatchet", 3, 1.2f).WithWideOn(ReachOn.Wood);
            Assert.IsTrue(hatchet.ReachesWideOn(wood));
            Assert.IsFalse(hatchet.ReachesWideOn(stone));
            Assert.IsFalse(axe.ReachesWideOn(wood), "Wood isn't hard ground: the pickaxe hits one voxel of it.");
            Object.DestroyImmediate(wood);
            Object.DestroyImmediate(hatchet);

            foreach (Object o in new Object[] { dirt, stone, shovel, axe, hand })
            {
                Object.DestroyImmediate(o);
            }
        }

        // GL71: a click in the backpack sends the stack to the hotbar (slots 0-2 here), and back.
        [Test]
        public void MoveInto_TopsUpTheSameItemFirst_ThenTheFirstEmptySlot_AndLeavesWhatDoesntFit()
        {
            var inventory = new Inventory(6);
            inventory.Add(stone, 10);
            inventory.Add(ore, 17);
            inventory.Move(2, 4);
            inventory.RemoveAt(1, 1);

            // Hotbar: stone 10, ore 9, empty. Backpack: empty, ore 7, empty.
            Assert.AreEqual(7, inventory.MoveInto(4, 0, 3));
            Assert.AreEqual(10, inventory[1].Count, "One tops up the 9 first.");
            Assert.AreEqual(6, inventory[2].Count, "The other 6 take the first empty hotbar slot.");
            Assert.IsTrue(inventory[4].IsEmpty);

            Assert.AreEqual(10, inventory.MoveInto(0, 3, 6), "Back to the backpack: its first empty slot.");
            Assert.AreEqual(stone, inventory[3].Item);

            var full = new Inventory(2);
            full.Add(ore, 15);
            Assert.AreEqual(0, full.MoveInto(1, 0, 1), "No room: nothing moves.");
            Assert.AreEqual(5, full[1].Count);
        }

        // GL90: a copy of an item under another id keeps its name but stacks apart from it.
        [Test]
        public void Variant_KeepsTheName_ButNeverSharesAStack()
        {
            ItemDefinition silver = ore.Variant("unknown-silver");
            ItemDefinition gold = ore.Variant("unknown-gold");
            try
            {
                Assert.AreEqual(ore.DisplayName, silver.DisplayName);
                Assert.AreEqual(ore.MaxStack, gold.MaxStack);
                var inventory = new Inventory(3);
                inventory.Add(silver, 2);
                inventory.Add(gold, 3);
                Assert.AreSame(silver, inventory[0].Item);
                Assert.AreSame(gold, inventory[1].Item, "Gold took its own slot.");
                Assert.AreEqual(2, inventory.Count(silver));
            }
            finally
            {
                Object.DestroyImmediate(silver);
                Object.DestroyImmediate(gold);
            }
        }
        // GL131: crafted items of another quality make their own stack; gathered mixes still blend.
        [Test]
        public void Add_KeepsDifferentQualitiesApart_ButBlendsMixes()
        {
            var bag = new Inventory(3);
            bag.Add(stone, 2, null, 0, 0.97f);
            bag.Add(stone, 2, null, 0, 0.98f);
            Assert.AreEqual(2, bag[0].Count);
            Assert.AreEqual(2, bag[1].Count, "98% sticks stay apart from 97%.");
            bag.Add(stone, 1, null, 0, 0.97f);
            Assert.AreEqual(3, bag[0].Count, "The same quality still stacks.");

            var mixes = new Inventory(2);
            VoxelMaterial rock = VoxelMaterial.Create(0, "Stone");
            try
            {
                mixes.Add(ore, 2, Composition.From(new Content(rock, 1f)), 0, 0.5f);
                mixes.Add(ore, 2, Composition.From(new Content(rock, 1f)), 0, 0.7f);
                Assert.AreEqual(4, mixes[0].Count, "Gathered ore blends.");
                Assert.AreEqual(0.6f, mixes[0].Quality, 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(rock);
            }
        }

        // GL136: part items add up into whole ones; recipes and counts see whole items only.
        [Test]
        public void PartItems_AddUp()
        {
            var bag = new Inventory(2);
            bag.Add(ore, 0, null, 0, 1f, 0.6f);
            Assert.AreEqual(0, bag.Count(ore));
            Assert.AreEqual(0.6f, bag[0].Amount, 1e-4f);
            bag.Add(ore, 1, null, 0, 1f, 0.6f);
            Assert.AreEqual(2, bag.Count(ore), "0.6 + 1.6 = 2.2");
            Assert.AreEqual(0.2f, bag[0].Part, 1e-4f);

            var other = new Inventory(1);
            Assert.AreEqual(2, bag.MoveTo(0, other));
            Assert.AreEqual(2.2f, other[0].Amount, 1e-4f, "The part goes with the stack.");
            Assert.IsTrue(bag[0].IsEmpty);
        }

        // GL174: sorting merges what stacks and orders the rest, tools first; slots before the run stay put.
        [Test]
        public void Sort_MergesStacksAndOrdersThem_LeavingSlotsBeforeTheRun()
        {
            var bag = new Inventory(6);
            bag.Add(stone, 1);
            bag.Replace(1, new ItemStack(stone, 3));
            bag.Replace(2, new ItemStack(ore, 4));
            bag.Replace(4, new ItemStack(stone, 5));
            bag.Replace(5, new ItemStack(pickaxe, 1));

            bag.Sort(1, 6);

            Assert.AreEqual(1, bag[0].Count, "the slot before the run is left alone");
            Assert.AreEqual(pickaxe, bag[1].Item, "tools first");
            Assert.AreEqual(ore, bag[2].Item, "then by name");
            Assert.AreEqual(stone, bag[3].Item);
            Assert.AreEqual(8, bag[3].Count, "3 + 5 stones merged");
            Assert.IsTrue(bag[4].IsEmpty && bag[5].IsEmpty, "empty slots last");
        }

        // GL173: the trash empties a slot for good.
        [Test]
        public void TakeAll_EmptiesTheSlot()
        {
            var bag = new Inventory(2);
            bag.Add(ore, 7, null, 0, 1f, 0.4f);
            ItemStack gone = bag.TakeAll(0);
            Assert.AreEqual(7.4f, gone.Amount, 1e-4f);
            Assert.IsTrue(bag[0].IsEmpty);
            Assert.AreEqual(0, bag.Count(ore));
        }

        // GL176: part of a stack into a station; the rest stays in its slot.
        [Test]
        public void MoveTo_WithACount_MovesThatManyAndKeepsTheRest()
        {
            var bag = new Inventory(1);
            bag.Add(ore, 9);
            var station = new Inventory(2);

            Assert.AreEqual(5, bag.MoveTo(0, station, 5));
            Assert.AreEqual(4, bag[0].Count);
            Assert.AreEqual(5, station[0].Count);
            Assert.AreEqual(4, bag.MoveTo(0, station, 100), "more than there is moves the whole stack");
            Assert.IsTrue(bag[0].IsEmpty);
        }

        // GL172: a part added to a gathered stack blends its quality in by amount.
        [Test]
        public void APartItem_BlendsItsQualityIntoTheStack()
        {
            VoxelMaterial clay = VoxelMaterial.Create(13, "Clay");
            try
            {
                var bag = new Inventory(1);
                Composition contents = Composition.From(new Content(clay, 1f));
                bag.Add(ore, 1, contents, 0, 1f);
                bag.Add(ore, 0, contents, 0, 0.5f, 0.5f);
                Assert.AreEqual(1.5f, bag[0].Amount, 1e-4f);
                Assert.AreEqual((1f * 1f + 0.5f * 0.5f) / 1.5f, bag[0].Quality, 1e-3f);
            }
            finally
            {
                Object.DestroyImmediate(clay);
            }
        }

        // GL188: a part item takes a place of its own, so a stack never holds more than its MaxStack in all.
        [Test]
        public void PartItem_NeverPassesMaxStack()
        {
            var inventory = new Inventory(3);
            inventory.Add(ore, 10);
            inventory.Add(ore, 0, null, 0, 1f, 0.7f);
            Assert.AreEqual(10f, inventory[0].Amount, 1e-4f, "The full stack takes no part.");
            Assert.AreEqual(0.7f, inventory[1].Amount, 1e-4f, "The part goes to a slot of its own.");
            Assert.AreEqual(0, inventory[0].Space);

            var second = new Inventory(2);
            second.Add(ore, 9);
            second.Add(ore, 0, null, 0, 1f, 0.5f);
            Assert.AreEqual(9.5f, second[0].Amount, 1e-4f);
            Assert.AreEqual(0, second[0].Space, "9 whole and a part fill 10 places.");
            second.Add(ore, 0, null, 0, 1f, 0.5f);
            Assert.AreEqual(10f, second[0].Amount, 1e-4f, "Two halves make the tenth whole one.");
        }

        // GL191: splitting a stack, and moving part of one onto another slot.
        [Test]
        public void Split_AndMoveACount()
        {
            var inventory = new Inventory(3);
            inventory.Add(ore, 6);
            Assert.AreEqual(1, inventory.Split(0, 3));
            Assert.AreEqual(3, inventory[0].Count);
            Assert.AreEqual(3, inventory[1].Count);
            Assert.AreEqual(2, inventory.Move(0, 1, 2));
            Assert.AreEqual(1, inventory[0].Count);
            Assert.AreEqual(5, inventory[1].Count);
            Assert.AreEqual(1, inventory.Move(0, 2, 1), "The last one moves whole.");
            Assert.IsTrue(inventory[0].IsEmpty);
            Assert.AreEqual(-1, inventory.Split(1, 5), "Nothing to split off a whole stack.");
        }
    }
}
