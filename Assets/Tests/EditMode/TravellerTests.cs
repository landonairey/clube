using Clube.Core;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>The travellers (GL476-GL477): the adventurer's peak and arrival, the trade cart's schedule and route.</summary>
    public class TravellerTests
    {
        // A hill at (40, -20), 30 m high, on flat ground at 10 m.
        private static float Hill(float x, float z)
        {
            float d = new Vector2(x - 40f, z + 20f).magnitude;
            return 10f + 30f * Mathf.Exp(-d * d / 400f);
        }

        [Test]
        public void PeakSearch_FindsTheHilltop()
        {
            Vector2 peak = PeakSearch.Highest(Hill, Vector2.zero, 100f, 8f, null, out float height);
            Assert.AreEqual(40f, peak.x, 1f);
            Assert.AreEqual(-20f, peak.y, 1f);
            Assert.AreEqual(40f, height, 0.2f);
        }

        [Test]
        public void PeakSearch_StaysWithinTheRadius()
        {
            // The hilltop is 45 m away; with 20 m the best is on the circle's edge towards it.
            Vector2 peak = PeakSearch.Highest(Hill, Vector2.zero, 20f, 4f, null, out _);
            Assert.LessOrEqual(peak.magnitude, 20.01f);
            Assert.Greater(peak.x, 10f);
        }

        [Test]
        public void PeakSearch_SummitClimbsPastTheRadiusToTheTop()
        {
            Vector2 edge = PeakSearch.Highest(Hill, Vector2.zero, 20f, 4f, null, out float edgeHeight);
            Vector2 top = PeakSearch.Summit(Hill, edge, 4f, null, out float height);
            Assert.AreEqual(40f, top.x, 0.6f);
            Assert.AreEqual(-20f, top.y, 0.6f);
            Assert.Greater(height, edgeHeight);
        }

        [Test]
        public void PeakSearch_StaysInsideTheArea()
        {
            var area = Rect.MinMaxRect(-50f, -10f, 50f, 50f);
            Vector2 peak = PeakSearch.Highest(Hill, Vector2.zero, 100f, 8f, area, out _);
            Assert.IsTrue(area.Contains(peak), peak.ToString());
        }

        [Test]
        public void CartSchedule_TripsAreTwoOrThreeDaysApart()
        {
            for (int seed = 0; seed < 5; seed++)
            {
                var gaps = new System.Collections.Generic.HashSet<int>();
                for (int trip = 0; trip < 30; trip++)
                {
                    double gap = CartSchedule.TripStart(seed, trip + 1) - CartSchedule.TripStart(seed, trip);
                    Assert.GreaterOrEqual(gap, 2d - 1e-9);
                    Assert.LessOrEqual(gap, 3d + 1e-9);
                    gaps.Add((int)System.Math.Round(gap));
                }
                Assert.AreEqual(2, gaps.Count, "both two- and three-day gaps happen");
            }
        }

        [Test]
        public void CartSchedule_GoesToThePostStopsAndComesBack()
        {
            const int Seed = 42;
            const double Leg = 0.1, Wait = 0.1;
            Assert.AreEqual(-1, CartSchedule.At(Seed, 0.05, Leg, Wait).Trip);
            double start = CartSchedule.TripStart(Seed, 3);
            CartState going = CartSchedule.At(Seed, start + Leg * 0.5, Leg, Wait);
            Assert.AreEqual(3, going.Trip);
            Assert.AreEqual(CartPhase.ToPost, going.Phase);
            Assert.AreEqual(0.5f, going.Progress, 1e-4f);
            Assert.AreEqual(CartPhase.AtPost, CartSchedule.At(Seed, start + Leg + Wait * 0.5, Leg, Wait).Phase);
            CartState back = CartSchedule.At(Seed, start + Leg + Wait + Leg * 0.25, Leg, Wait);
            Assert.AreEqual(CartPhase.ToTown, back.Phase);
            Assert.AreEqual(0.75f, back.Progress, 1e-4f);
            Assert.AreEqual(CartPhase.Away, CartSchedule.At(Seed, start + 1.5, Leg, Wait).Phase);
            // The same moment always gives the same answer: nothing to save.
            Assert.AreEqual(going.Progress, CartSchedule.At(Seed, start + Leg * 0.5, Leg, Wait).Progress);
        }

        [Test]
        public void CartRoute_DistantTownIsAtLeastTenParcelsOutOfTheGate()
        {
            const float Parcel = 32f;
            for (int seed = 0; seed < 20; seed++)
            {
                var post = new Vector2(16f, 48f);
                Vector2 town = CartRoute.DistantTown(post, Parcel, seed, null, Vector2.up, 60f);
                Assert.GreaterOrEqual((town - post).magnitude, CartRoute.MinParcels * Parcel - 0.01f);
                // A parcel's centre.
                Assert.AreEqual(Parcel * 0.5f, Mathf.Repeat(town.x, Parcel), 1e-3f);
                // Within the cone round the gate's direction (a little more after snapping to a parcel).
                Assert.Less(Vector2.Angle(Vector2.up, town - post), 66f);
            }
        }

        [Test]
        public void CartRoute_RunsEndToEndAndSkirtsTheHill()
        {
            var from = new Vector2(-100f, -20f);
            var to = new Vector2(180f, -20f);
            var route = new CartRoute(from, to, Hill, 7);
            Assert.AreEqual(from, route.Points[0]);
            Assert.AreEqual(to, route.Points[route.Points.Count - 1]);
            Assert.GreaterOrEqual(route.Length, (to - from).magnitude - 0.01f);
            Assert.Less(route.Length, (to - from).magnitude * 1.3f);
            Assert.AreEqual(from.x, route.At(0f).x, 1e-3f);
            Assert.AreEqual(Hill(from.x, from.y), route.At(0f).y, 0.05f);
            // The straight line crosses the hilltop (40 m); the road goes round lower down.
            float highest = 0f;
            for (float d = 0f; d <= route.Length; d += 2f)
            {
                highest = Mathf.Max(highest, route.HeightAlong(d));
            }
            Assert.Less(highest, 36f);
        }

        [Test]
        public void CartRoute_GoesRoundASteepFaceThroughTheGap()
        {
            // A 30 m wide-based ridge across the straight line, with a low gap 20 m to one side (GL510).
            float Wall(float x, float z) => Mathf.Abs(x) < 40f && Mathf.Abs(z - 20f) > 6f ? 30f : 0f;
            var route = new CartRoute(new Vector2(-100f, 0f), new Vector2(100f, 0f), Wall, 3);
            float steepest = 0f;
            for (float d = 1f; d <= route.Length; d += 1f)
            {
                steepest = Mathf.Max(steepest, Mathf.Abs(route.HeightAlong(d) - route.HeightAlong(d - 1f)));
            }
            Assert.Less(steepest, 5f);
        }

        [Test]
        public void AdventurerVisit_ArrivesAtSunsetOnTheFifthDay()
        {
            const float Sunset = 20f;
            Assert.IsFalse(AdventurerVisit.HasArrived(1, 22f, Sunset));
            Assert.IsFalse(AdventurerVisit.HasArrived(5, 7f, Sunset));
            Assert.IsFalse(AdventurerVisit.HasArrived(5, 19.9f, Sunset));
            Assert.IsTrue(AdventurerVisit.HasArrived(5, 20f, Sunset));
            Assert.IsTrue(AdventurerVisit.HasArrived(6, 1f, Sunset));
        }

        [Test]
        public void UpgradeSale_IsBoughtOnceForItsPriceAndPaysTheSeller()
        {
            var sale = new UpgradeSale(1000);
            var player = new Wallet(2500f);
            var seller = new Wallet(0f);
            Assert.IsTrue(sale.TryBuy(player, seller));
            Assert.IsTrue(sale.Owned);
            Assert.AreEqual(1500f, player.Coins, 1e-3f);
            Assert.AreEqual(1000f, seller.Coins, 1e-3f);
            Assert.IsFalse(sale.TryBuy(player, seller), "once only");
            Assert.AreEqual(1500f, player.Coins, 1e-3f);
        }

        [Test]
        public void UpgradeSale_IsRefusedWithoutEnoughCoins()
        {
            var sale = new UpgradeSale(1000);
            var player = new Wallet(999f);
            var seller = new Wallet(0f);
            Assert.IsFalse(sale.TryBuy(player, seller));
            Assert.IsFalse(sale.Owned);
            Assert.AreEqual(999f, player.Coins, 1e-3f);
            Assert.AreEqual(0f, seller.Coins, 1e-3f);
            Assert.IsFalse(sale.TryBuy(null, seller));
        }
    }
}
