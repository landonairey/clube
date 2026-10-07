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
    }
}
