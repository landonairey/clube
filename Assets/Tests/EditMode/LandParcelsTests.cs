using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>Land parcels (GL42): which parcel a point is in, what can be bought, and prices.</summary>
    public class LandParcelsTests
    {
        [Test]
        public void ParcelAt_FloorsPositions_SoNegativesWork()
        {
            var parcels = new LandParcels(32f, 100, 100);
            Assert.AreEqual(new Vector2Int(0, 0), parcels.ParcelAt(new Vector3(0f, 50f, 31.9f)));
            Assert.AreEqual(new Vector2Int(1, 0), parcels.ParcelAt(new Vector3(32f, 0f, 0f)), "The line between parcels belongs to the one after it.");
            Assert.AreEqual(new Vector2Int(-1, -2), parcels.ParcelAt(new Vector3(-0.1f, 0f, -33f)));
        }

        [Test]
        public void ParcelsWhoseCentreIsOutsideTheSaleArea_CannotBeBought()
        {
            // GL666: a playable area of x < 50 m; parcels of 32 m have centres at 16, 48, 80...
            var parcels = new LandParcels(32f, 100, 100) { SaleArea = centre => centre.x < 50f };
            parcels.Grant(Vector2Int.zero);
            Assert.AreEqual(new Vector2(48f, 16f), parcels.CentreOf(new Vector2Int(1, 0)));
            Assert.IsTrue(parcels.IsBuyable(new Vector2Int(1, 0)), "Its centre (48 m) is inside, though its far side isn't.");
            parcels.Grant(new Vector2Int(1, 0));
            Assert.IsFalse(parcels.IsInSaleArea(new Vector2Int(2, 0)));
            Assert.IsFalse(parcels.IsBuyable(new Vector2Int(2, 0)), "Its centre (80 m) is beyond the edge.");
        }

        [Test]
        public void OnlyParcelsBesideOwnedLand_CanBeBought()
        {
            var parcels = new LandParcels(32f, 100, 100);
            Assert.IsTrue(parcels.IsBuyable(new Vector2Int(5, 5)), "With nothing owned, any parcel will do.");

            parcels.Grant(Vector2Int.zero);
            Assert.IsTrue(parcels.IsBuyable(new Vector2Int(1, 0)));
            Assert.IsTrue(parcels.IsBuyable(new Vector2Int(0, -1)));
            Assert.IsFalse(parcels.IsBuyable(new Vector2Int(1, 1)), "Corners don't count.");
            Assert.IsFalse(parcels.IsBuyable(new Vector2Int(2, 0)));
            Assert.IsFalse(parcels.IsBuyable(Vector2Int.zero), "Already owned.");
        }

        [Test]
        public void TryBuy_ChargesARisingPrice_AndRefusesWhatCantBePaid()
        {
            var parcels = new LandParcels(32f, 100, 50);
            parcels.Grant(Vector2Int.zero);
            var wallet = new Wallet(260);
            int changes = 0;
            parcels.Changed += () => changes++;

            Assert.AreEqual(100, parcels.NextPrice);
            Assert.IsTrue(parcels.TryBuy(new Vector2Int(1, 0), wallet));
            Assert.AreEqual(160, wallet.Coins);
            Assert.AreEqual(150, parcels.NextPrice);
            Assert.IsTrue(parcels.TryBuy(new Vector2Int(2, 0), wallet), "Next to the parcel just bought.");
            Assert.AreEqual(10, wallet.Coins);

            Assert.IsFalse(parcels.TryBuy(new Vector2Int(3, 0), wallet), "200 coins, only 10.");
            Assert.IsFalse(parcels.IsOwned(new Vector2Int(3, 0)));
            Assert.AreEqual(10, wallet.Coins);
            Assert.AreEqual(2, changes);
            Assert.IsTrue(parcels.IsOwnedAt(new Vector3(70f, 0f, 5f)));
        }
    }
}
