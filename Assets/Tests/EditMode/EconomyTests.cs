using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class EconomyTests
    {
        private ItemDefinition ingot;
        private ItemDefinition stone;
        private PriceList prices;

        [SetUp]
        public void SetUp()
        {
            ingot = ItemDefinition.Create("copper-ingot", "Copper ingot");
            stone = ItemDefinition.Create("stone", "Stone");
            prices = PriceList.Create(new ItemPrice(ingot, 25));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(prices);
            Object.DestroyImmediate(ingot);
            Object.DestroyImmediate(stone);
        }

        [Test]
        public void Wallet_EarnsAndSpendsOnlyWhatItHas()
        {
            var wallet = new Wallet();
            int changes = 0;
            wallet.Changed += () => changes++;

            wallet.Earn(30);
            Assert.IsFalse(wallet.Spend(31));
            Assert.IsTrue(wallet.Spend(12));
            wallet.Earn(-5);

            Assert.AreEqual(18, wallet.Coins);
            Assert.AreEqual(2, changes, "Refused spends and non-positive earnings change nothing.");
        }

        [Test]
        public void Sell_PaysTheListedPricePerItem()
        {
            var inventory = new Inventory(4);
            inventory.Add(ingot, 3);
            var wallet = new Wallet();

            Assert.AreEqual(2, prices.Sell(ingot, 2, inventory, wallet));

            Assert.AreEqual(50, wallet.Coins);
            Assert.AreEqual(1, inventory.Count(ingot));
        }

        [Test]
        public void Sell_SellsOnlyWhatThereIs()
        {
            var inventory = new Inventory(4);
            inventory.Add(ingot, 1);
            var wallet = new Wallet();

            Assert.AreEqual(1, prices.Sell(ingot, 10, inventory, wallet));
            Assert.AreEqual(25, wallet.Coins);
            Assert.AreEqual(0, prices.Sell(ingot, 1, inventory, wallet));
        }

        [Test]
        public void Sell_RefusesItemsNotOnTheList()
        {
            var inventory = new Inventory(4);
            inventory.Add(stone, 5);
            var wallet = new Wallet();

            Assert.AreEqual(0, prices.Sell(stone, 5, inventory, wallet));
            Assert.AreEqual(5, inventory.Count(stone));
            Assert.AreEqual(0, wallet.Coins);
        }

        [Test]
        public void Sell_PaysWholeLotsOnly_WhenAPriceIsPerManyUnits()
        {
            using var temp = new TempList(out PriceList bulk, new ItemPrice(stone, 1, 1000));
            var inventory = new Inventory(4);
            inventory.Add(stone, 1);
            var wallet = new Wallet();

            Assert.AreEqual(0, bulk.Sell(stone, 999, inventory, wallet), "Too few for a lot.");
            Assert.AreEqual(0, wallet.Coins);
            Assert.AreEqual(2000, bulk.Sellable(stone, 2500));
        }

        [Test]
        public void EarnFraction_PaysACoinOnceThePartsAddUp()
        {
            var wallet = new Wallet();
            for (int i = 0; i < 9; i++)
            {
                wallet.EarnFraction(0.1f);
            }
            Assert.AreEqual(0, wallet.Coins);
            wallet.EarnFraction(0.1f);
            Assert.AreEqual(1, wallet.Coins, "Ten tenths make a coin.");
            wallet.EarnFraction(2.5f);
            wallet.EarnFraction(0.5f);
            Assert.AreEqual(4, wallet.Coins);
        }

        [Test]
        public void Value_ScalesWithToolWear_AndLotsCanBeByVolume()
        {
            ToolDefinition pickaxe = ToolDefinition.Create("pickaxe", "Pickaxe", 4, 1.5f).WithDurability(100);
            ItemDefinition bar = ItemDefinition.Create("bar", "Bar");
            bar.WithUnitVolume(0.4f);
            using var temp = new TempList(out PriceList list, new ItemPrice(pickaxe, 50), new ItemPrice(bar, 1, 4, byVolume: true));

            Assert.AreEqual(50, list.ValueOf(new ItemStack(pickaxe, 1), 1));
            Assert.AreEqual(25, list.ValueOf(new ItemStack(pickaxe, 1, null, 50), 1));
            Assert.AreEqual(0, list.ValueOf(new ItemStack(pickaxe, 1, null, 100), 1));

            Assert.IsTrue(list.TryGetLot(bar, out _, out int lot));
            Assert.AreEqual(10, lot, "4 volume of 0.4 each is 10 items.");
            Assert.AreEqual(2, list.ValueOf(new ItemStack(bar, 25), 25));
            Object.DestroyImmediate(pickaxe);
            Object.DestroyImmediate(bar);
        }

        // GL130: an item sells at its quality's share; a tool's quality is in its hits.
        [Test]
        public void Price_FollowsQuality()
        {
            ItemDefinition ingot = ItemDefinition.Create("ingot", "Ingot");
            ToolDefinition pick = ToolDefinition.Create("pick", "Pick", 4, 1.5f).WithDurability(100);
            using var temp = new TempList(out PriceList list, new ItemPrice(ingot, 10), new ItemPrice(pick, 50));
            try
            {
                Assert.AreEqual(7f, list.PriceOf(new ItemStack(ingot, 1, null, 0, 0.7f), 1), 1e-4f);
                Assert.AreEqual(35, list.ValueOf(new ItemStack(pick, 1, null, 0, 0.7f), 1), "70% of a full tool's hits.");

                var bag = new Inventory(1);
                bag.Add(ingot, 2, null, 0, 0.5f);
                var wallet = new Wallet();
                Assert.AreEqual(2, list.Sell(ingot, 2, bag, wallet));
                Assert.AreEqual(10, wallet.Coins);
            }
            finally
            {
                Object.DestroyImmediate(ingot);
                Object.DestroyImmediate(pick);
            }
        }

        private sealed class TempList : System.IDisposable
        {
            private readonly PriceList list;

            public TempList(out PriceList made, params ItemPrice[] prices)
            {
                list = PriceList.Create(prices);
                made = list;
            }

            public void Dispose()
            {
                Object.DestroyImmediate(list);
            }
        }
    }
}
