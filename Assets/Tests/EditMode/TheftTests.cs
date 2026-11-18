using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>GL729: goods taken from a cart or vessel lower the disposition of the shop they are bound for.</summary>
    public class TheftTests
    {
        [Test]
        public void Lower_TakesPointsOff_DownToZero_AndLosesWhatWasCarried()
        {
            var disposition = new Disposition(30, 50f);
            Assert.AreEqual(12, disposition.Lower(12));
            Assert.AreEqual(18, disposition.Value);
            Assert.AreEqual(0f, disposition.Carried);
            Assert.AreEqual(18, disposition.Lower(40), "it stops at 0");
            Assert.AreEqual(0, disposition.Value);
            Assert.AreEqual(0, disposition.Lower(5), "nothing lower than 0");
        }

        [Test]
        public void AnyTaking_CostsAtLeastFivePoints_AndAPointForEveryTenCoins()
        {
            Assert.AreEqual(0, Theft.PointsFor(0f));
            Assert.AreEqual(Theft.LeastPoints, Theft.PointsFor(0.5f));
            Assert.AreEqual(Theft.LeastPoints, Theft.PointsFor(50f));
            Assert.AreEqual(8, Theft.PointsFor(75f));
            Assert.AreEqual(25, Theft.PointsFor(250f));
        }

        [Test]
        public void Worth_SpreadsALotsPriceOverTheLot()
        {
            var ingot = ScriptableObject.CreateInstance<ItemDefinition>();
            var plank = ScriptableObject.CreateInstance<ItemDefinition>();
            PriceList prices = PriceList.Create(new ItemPrice(ingot, 12), new ItemPrice(plank, 5, 10));
            Assert.AreEqual(36f, Theft.Worth(prices, ingot, 3f), 1e-4f);
            Assert.AreEqual(2.5f, Theft.Worth(prices, plank, 5f), 1e-4f);
            Assert.AreEqual(0f, Theft.Worth(prices, ScriptableObject.CreateInstance<ItemDefinition>(), 4f), "not listed");
            Object.DestroyImmediate(prices);
            Object.DestroyImmediate(ingot);
            Object.DestroyImmediate(plank);
        }

        [Test]
        public void TheBook_KeepsEachKeeper_AndSavesOnlyThoseThatMoved()
        {
            var book = new DispositionBook();
            Assert.AreSame(book.Of(3, 1), book.Of(3, 1));
            Assert.AreNotSame(book.Of(3, 1), book.Of(1, 3));
            book.Of(3, 1).Lower(7);
            book.Of(0, 2).Record(250f);
            int earnedValue = book.Of(0, 2).Value;
            float earnedCarried = book.Of(0, 2).Carried;
            book.Of(5, 0); // asked about, never dealt with
            string saved = book.Save();
            Assert.AreEqual(2, saved.Split(';').Length);

            var loaded = new DispositionBook();
            loaded.Of(9, 9).Lower(3);
            loaded.Restore(saved);
            Assert.AreEqual(Disposition.Start - 7, loaded.Of(3, 1).Value);
            Assert.Greater(earnedValue, Disposition.Start + 2, "250c at the start buys many points");
            Assert.AreEqual(earnedValue, loaded.Of(0, 2).Value);
            Assert.AreEqual(earnedCarried, loaded.Of(0, 2).Carried, 1e-3f);
            Assert.AreEqual(Disposition.Start, loaded.Of(9, 9).Value, "a keeper the save does not name is back at the start");
            loaded.Restore("garbage;1,2;x,y,z,w");
            Assert.AreEqual(Disposition.Start, loaded.Of(3, 1).Value);
        }

        [Test]
        public void TheBook_StartsTheQuarrysKeepersAtTheirOwnStart()
        {
            // GL734: settlement 4 is the tutorial's quarry, whose keepers start at 0.
            System.Func<int, int> startOf = settlement => settlement == 4 ? 0 : Disposition.Start;
            var book = new DispositionBook { StartOf = startOf };
            Assert.AreEqual(0, book.Of(4, 0).Value);
            Assert.AreEqual(Disposition.Start, book.Of(2, 0).Value);
            Assert.AreEqual("", book.Save(), "nobody has moved");
            book.Of(4, 0).Record(Disposition.CoinsBetween(0, Disposition.Start));
            Assert.AreEqual(Disposition.Start, book.Of(4, 0).Value);
            string saved = book.Save();
            Assert.That(saved, Does.StartWith("4,0,10,"), "at 10 the quarry's keeper has moved");

            var loaded = new DispositionBook { StartOf = startOf };
            loaded.Of(4, 1).Record(500f);
            loaded.Restore(saved);
            Assert.AreEqual(Disposition.Start, loaded.Of(4, 0).Value);
            Assert.AreEqual(0, loaded.Of(4, 1).Value, "one the save does not name is back at its own start");
        }

        [Test]
        public void TheGoodsAboard_AreBoundForTheEndAhead_ThenTheOtherEndOnceLoaded()
        {
            Assert.AreEqual(7, TradeStop.GoodsBoundFor(new RunPlace(0.4f, true, true, 0), 100f, 2, 7));
            Assert.AreEqual(2, TradeStop.GoodsBoundFor(new RunPlace(0.4f, false, true, 1), 100f, 2, 7));
            Assert.AreEqual(7, TradeStop.GoodsBoundFor(new RunPlace(1f, true, false, 0, 20f), 100f, 2, 7), "unloading at the far end");
            Assert.AreEqual(2, TradeStop.GoodsBoundFor(new RunPlace(1f, true, false, 0, 70f), 100f, 2, 7), "loaded for the way back");
            Assert.AreEqual(-1, TradeStop.GoodsBoundFor(new RunPlace(0.5f, true, true, 0), 100f, 2, -1), "off the map");
        }

        [Test]
        public void TheShopTakingGoods_IsTheMatch_ElseTheMerchant_ElseTheFirst()
        {
            var from = new PlannedSettlement();
            from.Shops.Add(Shop("ore", new[] { "copper-ore" }, new string[0]));
            var at = new PlannedSettlement();
            at.Shops.Add(Shop("lumber", new[] { "plank" }, new[] { "log" }));
            at.Shops.Add(Shop(SupplyChains.Merchant, new string[0], new string[0]));
            at.Shops.Add(Shop("smith", new[] { "ingot" }, new[] { "copper-ore" }));
            Assert.AreEqual(2, TradeStop.ShopTaking(from, at));
            Assert.AreEqual(1, TradeStop.ShopTaking(null, at), "goods from beyond the map go to the merchant");
            var village = new PlannedSettlement();
            village.Shops.Add(Shop("lumber", new[] { "plank" }, new[] { "log" }));
            Assert.AreEqual(0, TradeStop.ShopTaking(null, village));
            Assert.AreEqual(-1, TradeStop.ShopTaking(from, new PlannedSettlement()));
        }

        private static PlannedShop Shop(string chain, string[] sells, string[] buys)
        {
            return new PlannedShop
            {
                Specialty = new EconomySpecialty
                {
                    Id = chain, Chain = chain,
                    Sells = sells.Select(id => new TradeGood { ItemId = id, Name = id }).ToArray(),
                    Buys = buys.Select(id => new TradeGood { ItemId = id, Name = id }).ToArray(),
                },
            };
        }
    }
}
