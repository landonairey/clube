using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>GL726: a cart's or vessel's stop at a settlement (drive in, unload, load, drive out), the shop it goes to, and docks.</summary>
    public class TradeStopTests
    {
        [Test]
        public void AStop_DrivesIn_Unloads_Loads_DrivesOut_ByTheClock()
        {
            // A rest of 100 s with 20 s drives: 30 s unloading and 30 s loading between them.
            Assert.AreEqual(StopPhase.Arriving, TradeStop.At(10f, 100f, 20f).Phase);
            Assert.AreEqual(0.5f, TradeStop.At(10f, 100f, 20f).Progress, 1e-4f);
            Assert.AreEqual(StopPhase.Unloading, TradeStop.At(35f, 100f, 20f).Phase);
            Assert.AreEqual(0.5f, TradeStop.At(35f, 100f, 20f).Progress, 1e-4f);
            Assert.AreEqual(StopPhase.Loading, TradeStop.At(65f, 100f, 20f).Phase);
            Assert.AreEqual(StopPhase.Leaving, TradeStop.At(90f, 100f, 20f).Phase);
            Assert.AreEqual(1f, TradeStop.At(100f, 100f, 20f).Progress, 1e-4f);
        }

        [Test]
        public void AVesselAtItsDock_OnlyUnloadsAndLoads()
        {
            Assert.AreEqual(StopPhase.Unloading, TradeStop.At(0f, 100f, 0f).Phase);
            Assert.AreEqual(StopPhase.Loading, TradeStop.At(60f, 100f, 0f).Phase);
            Assert.AreEqual(StopPhase.Loading, TradeStop.At(100f, 100f, 0f).Phase, "it stays loaded until it sails");
        }

        [Test]
        public void ALongWayInTown_IsDrivenFaster_SoTheStopStillHasTimeToWork()
        {
            Assert.AreEqual(10f, TradeStop.LegSeconds(20f, 2f, 150f), 1e-4f);
            Assert.AreEqual(150f * TradeStop.LegShare, TradeStop.LegSeconds(400f, 2f, 150f), 1e-4f);
        }

        [Test]
        public void Unloading_EmptiesCratesFromTheFront_Loading_FillsThem()
        {
            TradeStop.CratesHeld(new StopPlace(StopPhase.Arriving, 0.5f), 4, out int first, out int last, out bool arrived);
            Assert.AreEqual((0, 4, true), (first, last, arrived));
            TradeStop.CratesHeld(new StopPlace(StopPhase.Unloading, 0.5f), 4, out first, out last, out arrived);
            Assert.AreEqual((2, 4, true), (first, last, arrived));
            TradeStop.CratesHeld(new StopPlace(StopPhase.Unloading, 1f), 4, out first, out last, out arrived);
            Assert.AreEqual(first, last, "all unloaded");
            TradeStop.CratesHeld(new StopPlace(StopPhase.Loading, 0.25f), 4, out first, out last, out arrived);
            Assert.AreEqual((0, 1, false), (first, last, arrived));
            TradeStop.CratesHeld(new StopPlace(StopPhase.Leaving, 0f), 4, out first, out last, out arrived);
            Assert.AreEqual((0, 4, false), (first, last, arrived));
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

        [Test]
        public void ACart_GoesToTheShopThatBuysWhatItBrings()
        {
            var from = new PlannedSettlement();
            from.Shops.Add(Shop("ore", new[] { "copper-ore", "tin-ore" }, new string[0]));
            var at = new PlannedSettlement();
            at.Shops.Add(Shop("lumber", new[] { "plank" }, new[] { "log" }));
            at.Shops.Add(Shop("smith", new[] { "ingot" }, new[] { "copper-ore", "tin-ore" }));
            at.Shops.Add(Shop("ore", new[] { "copper-ore" }, new string[0]));

            Assert.AreEqual(1, TradeStop.ShopFor(from, at), "the smelter buys both ores");
            var nothing = new PlannedSettlement();
            nothing.Shops.Add(Shop("textile", new[] { "cloth" }, new string[0]));
            Assert.AreEqual(-1, TradeStop.ShopFor(nothing, at));
            Assert.AreEqual(-1, TradeStop.ShopFor(null, at), "goods from beyond the map match nothing");
        }

        // Sea east of x = 30, deepening by 0.1 m a metre.
        private static float Depth(Vector2 p)
        {
            return (p.x - 30f) * 0.1f;
        }

        [Test]
        public void ADock_StandsOnTheShoreNearestTheTown_AndRunsOutToDeepEnoughWater()
        {
            DockDesign pier = DockDesign.Defaults().First(d => d.Id == "pier");
            var fronts = new List<Vector2> { new Vector2(0f, 0f), new Vector2(20f, 40f) };

            DockLayout dock = Docks.Lay(pier, fronts, new Vector2(80f, 40f), Depth, 1.6f);

            Assert.IsNotNull(dock);
            Assert.AreEqual(new Vector2(20f, 40f), dock.PathFrom, "from the building nearest the water");
            Assert.AreEqual(30f, dock.Shore.x, 1.5f);
            Assert.Less(dock.Root.x, 30f, "its root is on land");
            Assert.GreaterOrEqual(Depth(dock.Tip), 1.6f - 0.15f);
            Assert.Greater(dock.Out.x, 0.9f, "straight out into the sea");
            Assert.AreEqual(pier.Berths, dock.Berths.Count);
            foreach (DockBerth berth in dock.Berths)
            {
                Assert.Greater(Depth(berth.Mooring(3.8f)), 0f, "a vessel moored there floats");
            }
        }

        [Test]
        public void ADock_IsGivenByTownSize_TheCapitalsStoneAndGrandest()
        {
            List<DockDesign> designs = DockDesign.Defaults();
            List<SettlementType> types = SettlementType.Defaults();
            DockDesign Of(string id) => DockDesign.For(designs, types.First(t => t.Id == id));

            Assert.IsNull(Of("camp"));
            Assert.IsNull(Of("thieves-camp"));
            Assert.AreEqual("foot-dock", Of("village").Id);
            Assert.AreEqual("foot-dock", Of("outpost").Id);
            Assert.IsTrue(Of("capital").Stone);
            Assert.IsFalse(Of("town").Stone);
            Assert.Greater(Of("capital").Berths, Of("city").Berths);
            Assert.Greater(Of("city").Width, Of("town").Width);
            Assert.Greater(Of("town").Width, Of("village").Width);
        }

        [Test]
        public void NoWaterInReach_NoDock()
        {
            DockDesign pier = DockDesign.Defaults().First(d => d.Id == "pier");
            Assert.IsNull(Docks.Lay(pier, new List<Vector2> { Vector2.zero }, new Vector2(500f, 0f), p => -1f, 1.6f));
        }

        [Test]
        public void TheGrandDocks_Berths_DoNotOverlap()
        {
            DockDesign grand = DockDesign.Defaults().First(d => d.Id == "grand-docks");
            DockLayout dock = Docks.Lay(grand, new List<Vector2> { Vector2.zero }, new Vector2(100f, 0f), Depth, 1.6f);

            List<Vector2> moorings = dock.Berths.Select(b => b.Mooring(3.8f)).ToList();
            for (int a = 0; a < moorings.Count; a++)
            {
                for (int b = a + 1; b < moorings.Count; b++)
                {
                    Assert.Greater(Vector2.Distance(moorings[a], moorings[b]), 4f, $"berths {a} and {b}");
                }
            }
        }
    }
}
