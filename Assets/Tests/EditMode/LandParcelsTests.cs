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
        public void PointsAround_AreOffTheParcel_NearestFirst()
        {
            // GL744, GL766: where the cart can set the player down, to walk onto the family's land.
            var parcels = new LandParcels(32f, 100, 100);
            var parcel = new Vector2Int(2, 1);
            var from = new Vector2(70f, 60f);
            Vector2[] points = parcels.PointsAround(parcel, from, 6f, 4f);

            Assert.AreEqual(44, points.Length, "11 steps along each of the four 44 m sides.");
            for (int i = 0; i < points.Length; i++)
            {
                Assert.AreNotEqual(parcel, parcels.ParcelAt(new Vector3(points[i].x, 0f, points[i].y)));
                Assert.AreEqual(6f, SideDistance(parcels.AreaOf(parcel), points[i]), 1e-3f);
                Assert.IsTrue(i == 0 || (points[i] - from).sqrMagnitude >= (points[i - 1] - from).sqrMagnitude);
            }
        }

        [Test]
        public void Home_IsTheFirstParcelGiven_AndStaysSo()
        {
            // GL764: the map's Home icon.
            var parcels = new LandParcels(32f, 100, 100);
            Assert.IsNull(parcels.Home);

            parcels.Grant(new Vector2Int(3, 4));
            parcels.Grant(new Vector2Int(3, 5));
            Assert.AreEqual(new Vector2Int(3, 4), parcels.Home);
        }

        private static float SideDistance(Rect area, Vector2 point)
        {
            return Mathf.Max(area.xMin - point.x, point.x - area.xMax, area.yMin - point.y, point.y - area.yMax);
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
