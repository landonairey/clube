using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>Buying land from a settlement's Merchant (GL864, GL865): ring prices, the disposition, settlement prices.</summary>
    public class ParcelPricesTests
    {
        // A 2 x 2 settlement at parcels (0, 0)-(1, 1).
        private static readonly List<Vector2Int> Town = new List<Vector2Int>
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1),
        };

        [Test]
        public void AdjacentParcel_AtDisposition50_Costs150()
        {
            // The user's worked example: 100 * (1 + 50/100) = 150c.
            Assert.AreEqual(150, ParcelPrices.PriceOf(new Vector2Int(2, 0), Town, 50));
        }

        [Test]
        public void BasePrice_Is100Touching_Plus200EachRingOut()
        {
            Assert.AreEqual(0, ParcelPrices.BasePrice(0), "The settlement's own parcels aren't sold one by one.");
            Assert.AreEqual(100, ParcelPrices.BasePrice(1));
            Assert.AreEqual(300, ParcelPrices.BasePrice(2));
            Assert.AreEqual(500, ParcelPrices.BasePrice(3));
            Assert.AreEqual(1900, ParcelPrices.BasePrice(10));
        }

        [Test]
        public void Rings_AreChebyshev_SoCornersTouch()
        {
            Assert.AreEqual(0, ParcelPrices.RingsFrom(new Vector2Int(1, 1), Town));
            Assert.AreEqual(1, ParcelPrices.RingsFrom(new Vector2Int(-1, -1), Town), "A corner touching the settlement is ring 1.");
            Assert.AreEqual(1, ParcelPrices.RingsFrom(new Vector2Int(2, 1), Town));
            Assert.AreEqual(2, ParcelPrices.RingsFrom(new Vector2Int(3, -1), Town));
            Assert.AreEqual(3, ParcelPrices.RingsFrom(new Vector2Int(0, 4), Town));
            Assert.AreEqual(int.MaxValue, ParcelPrices.RingsFrom(Vector2Int.zero, new List<Vector2Int>()));
        }

        [Test]
        public void Disposition_ScalesPrice_AsWritten_HigherCostsMore()
        {
            Assert.AreEqual(100, ParcelPrices.WithDisposition(100, 0));
            Assert.AreEqual(110, ParcelPrices.WithDisposition(100, 10));
            Assert.AreEqual(200, ParcelPrices.WithDisposition(100, 100));
            Assert.AreEqual(411, ParcelPrices.WithDisposition(300, 37));
            Assert.AreEqual(685, ParcelPrices.PriceOf(new Vector2Int(-3, 3), Town, 37), "Ring 3 (500) at 37: 685.");
            Assert.AreEqual(1500000, ParcelPrices.WithDisposition(1000000, 50));
        }

        [Test]
        public void SettlementTypes_CarryTheUsersSalePrices()
        {
            var expected = new Dictionary<string, int>
            {
                { "camp", 1000 }, { "outpost", 10000 }, { "village", 20000 }, { "town", 50000 }, { "city", 100000 }, { "capital", 1000000 },
                { "thieves-camp", 0 },
            };
            List<SettlementType> types = SettlementType.Defaults();
            Assert.AreEqual(expected.Count, types.Count, "Every settlement type has a sale price.");
            foreach (SettlementType type in types)
            {
                Assert.AreEqual(expected[type.Id], type.SalePrice, type.Id);
            }
        }

        [Test]
        public void ParcelsCovering_IncludesFarEdges()
        {
            var parcels = new LandParcels(32f, 100, 100);
            List<Vector2Int> covered = parcels.ParcelsCovering(new Rect(10f, -5f, 40f, 30f));
            CollectionAssert.AreEquivalent(new[]
            {
                new Vector2Int(0, -1), new Vector2Int(1, -1), new Vector2Int(0, 0), new Vector2Int(1, 0),
            }, covered);
        }

        [Test]
        public void TryBuyAt_BuysAnyFreeParcel_AtTheGivenPrice()
        {
            var parcels = new LandParcels(32f, 100, 100);
            parcels.Grant(new Vector2Int(10, 10));
            var wallet = new Wallet(500f);
            Assert.IsTrue(parcels.TryBuyAt(new Vector2Int(2, 0), 150, wallet), "Need not touch the player's land.");
            Assert.AreEqual(350f, wallet.Coins, 1e-4f);
            Assert.IsTrue(parcels.IsOwned(new Vector2Int(2, 0)));
            Assert.IsFalse(parcels.TryBuyAt(new Vector2Int(2, 0), 150, wallet), "Owned already.");
            parcels.Lock(new Vector2Int(5, 5));
            Assert.IsFalse(parcels.TryBuyAt(new Vector2Int(5, 5), 10, wallet), "Locked land isn't sold one by one.");
            Assert.IsFalse(parcels.TryBuyAt(new Vector2Int(3, 0), 400, wallet), "Too dear.");
            Assert.AreEqual(350f, wallet.Coins, 1e-4f);
        }

        [Test]
        public void TryBuyAll_FreesTheSettlementsLockedLand()
        {
            var parcels = new LandParcels(32f, 100, 100);
            foreach (Vector2Int parcel in Town)
            {
                parcels.Lock(parcel);
            }
            var wallet = new Wallet(1000f);
            Assert.IsFalse(parcels.TryBuyAll(Town, 1500, wallet), "Short of coins.");
            Assert.IsTrue(parcels.IsLocked(Town[0]));
            Assert.IsTrue(parcels.TryBuyAll(Town, 1000, wallet));
            Assert.AreEqual(0f, wallet.Coins, 1e-4f);
            foreach (Vector2Int parcel in Town)
            {
                Assert.IsTrue(parcels.IsOwned(parcel));
                Assert.IsFalse(parcels.IsLocked(parcel));
            }
            Assert.IsFalse(parcels.TryBuyAll(Town, 0, wallet), "All owned already.");
        }
    }
}
