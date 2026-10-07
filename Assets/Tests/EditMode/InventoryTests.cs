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
    }
}
