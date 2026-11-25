using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class InventoryListingTests
    {
        private ItemDefinition stone;
        private ItemDefinition clay;
        private ToolDefinition pick;

        [SetUp]
        public void SetUp()
        {
            stone = ItemDefinition.Create("stone", "Stone", maxStack: 1024);
            clay = ItemDefinition.Create("clay", "Clay", maxStack: 1024);
            pick = ToolDefinition.Create("stone-pick", "Stone Pick", 3, 1.5f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(stone);
            Object.DestroyImmediate(clay);
            Object.DestroyImmediate(pick);
        }

        private static List<ListRow> Build(Inventory items, ICollection<ItemDefinition> expanded = null, ListSort sort = ListSort.Name, bool descending = false)
        {
            return InventoryListing.Build(items, null, slot => items[slot].Quality * 10f, slot => items[slot].Quality, expanded, sort, descending);
        }

        [Test]
        public void TwoStacksOfOneItem_FormOneCollectionWithAveragesAndTotal()
        {
            var items = new Inventory(4);
            items.Set(0, new ItemStack(stone, 57, null, 0, 0.5f));
            items.Set(2, new ItemStack(stone, 129, null, 0, 1f));

            List<ListRow> rows = Build(items);

            Assert.AreEqual(1, rows.Count);
            Assert.IsTrue(rows[0].IsCollection);
            Assert.AreEqual(186f, rows[0].Amount, 1e-3f);
            Assert.AreEqual((57f * 0.5f + 129f) / 186f, rows[0].Quality, 1e-4f);
            CollectionAssert.AreEqual(new[] { 0, 2 }, rows[0].Slots);
        }

        [Test]
        public void AnOpenedCollection_ListsEachStackUnderIt()
        {
            var items = new Inventory(4);
            items.Set(0, new ItemStack(pick, 1, null, 0, 1f));
            items.Set(1, new ItemStack(pick, 1, null, 0, 0.5f));
            items.Set(3, new ItemStack(clay, 3));

            List<ListRow> rows = Build(items, new HashSet<ItemDefinition> { pick });

            Assert.AreEqual(4, rows.Count);
            Assert.AreEqual(clay, rows[0].Item);
            Assert.IsTrue(rows[1].IsCollection);
            Assert.IsTrue(rows[2].IsChild && rows[3].IsChild);
            Assert.AreEqual(0.75f, rows[1].Quality, 1e-4f);
        }

        [Test]
        public void SortingByValueDescending_PutsTheDearestFirst()
        {
            var items = new Inventory(3);
            items.Set(0, new ItemStack(clay, 3, null, 0, 0.2f));
            items.Set(1, new ItemStack(stone, 3, null, 0, 0.9f));

            List<ListRow> rows = Build(items, null, ListSort.Value, true);

            Assert.AreEqual(stone, rows[0].Item);
            Assert.AreEqual(clay, rows[1].Item);
        }
    }
}
