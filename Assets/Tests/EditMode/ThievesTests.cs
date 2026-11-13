using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// Thieves' camps (GL617): the band a camp houses and its week, where it lies in wait, whom it steals from, where its
    /// boat waits, and what a robbery moves.
    /// </summary>
    public class ThievesTests
    {
        private static PlannedSettlement Place(WorldPlan plan, string type, Vector2 at, int shops = 0)
        {
            var settlement = new PlannedSettlement
            {
                Index = plan.Settlements.Count, Type = plan.Rules.Types.Find(t => t.Id == type), Position = at, Name = $"Test {type} {plan.Settlements.Count}",
            };
            for (int i = 0; i < shops; i++)
            {
                settlement.Shops.Add(new PlannedShop { Specialty = plan.Rules.Specialties[i], Reason = "test" });
            }
            plan.Settlements.Add(settlement);
            return settlement;
        }

        private static void Road(WorldPlan plan, RoadClass roadClass, params Vector2[] points)
        {
            plan.Roads.Add(new PlannedRoad { Index = plan.Roads.Count, Class = roadClass, From = 0, To = 1, Points = points.ToList(), Length = WaterLanes.Length(points) });
        }

        // A town at the origin, a village 1 km east of it on a paved road, a footpath going north, and a camp north of the road's middle.
        private static WorldPlan Plan(out PlannedSettlement camp)
        {
            var plan = new WorldPlan { Rules = new WorldPlanRules(), Area = new Rect(-2000f, -2000f, 4000f, 4000f) };
            Place(plan, "town", Vector2.zero, 3);
            Place(plan, "village", new Vector2(1000f, 0f), 2);
            camp = Place(plan, "thieves-camp", new Vector2(450f, 300f));
            Road(plan, RoadClass.PavedCartPath, Vector2.zero, new Vector2(1000f, 0f));
            Road(plan, RoadClass.Footpath, new Vector2(450f, 0f), new Vector2(450f, 250f));
            return plan;
        }

        [Test]
        public void ACampHasABandOfThieves_HousedTwoToAHut_OutlawsNoShopEmploys()
        {
            SettlementType type = SettlementType.Defaults().Find(t => t.Id == "thieves-camp");
            Assert.Greater(type.Band, 0);
            Assert.AreEqual(0, type.PopulationMax, "GL593's table counts honest folk: the band is beside it");
            var settlement = new PlannedSettlement { Index = 5, Type = type, Position = new Vector2(500f, 500f), Name = "Test camp" };
            SettlementSite site = SettlementLayout.Plan(settlement, 32f, new SettlementSiteRules(), (x, z) => 40f);

            List<Resident> band = SettlementRoster.Build(site, Occupation.Defaults(), 7);

            Assert.AreEqual(type.Band, band.Count);
            Assert.IsTrue(band.All(thief => thief.Occupation.Id == type.Residents && thief.Occupation.Outlaw));
            Assert.IsTrue(band.All(thief => site.Buildings[thief.Home].Kind == SettlementBuildingKind.House));
            Assert.LessOrEqual(band.GroupBy(thief => thief.Home).Max(g => g.Count()), site.Rules.PeoplePerHouse);
            foreach (EconomySpecialty specialty in EconomySpecialty.Defaults())
            {
                Assert.IsFalse(Occupation.For(Occupation.Defaults(), specialty.Id).Outlaw, specialty.Id);
            }
            Assert.IsFalse(Occupation.For(Occupation.Defaults(), "no such shop").Outlaw);
        }

        [Test]
        public void TheBand_IsOnTheRoadsByDay_InTheTownsByNight_AndItsLootGoesToTheStash()
        {
            List<Duty> duties = Duty.Defaults();
            Occupation bandit = Occupation.Find(Occupation.Defaults(), "bandit");

            for (int day = 0; day < Occupation.DaysInWeek; day++)
            {
                Assert.AreEqual("waylay", bandit.DutyAt(day, 8f));
                Assert.AreEqual("waylay", bandit.DutyAt(day, 15f));
                Assert.AreEqual("lurk", bandit.DutyAt(day, 12f));
                Assert.AreEqual("burgle", bandit.DutyAt(day, 23.5f));
                Assert.AreEqual("burgle", bandit.DutyAt(day, 1f));
                Assert.AreEqual(Duty.Sleep, bandit.DutyAt(day, 4f));
            }
            foreach (string id in new[] { "waylay", "burgle", "lurk" })
            {
                Duty duty = Duty.Find(duties, id);
                Assert.AreEqual(DutyPlace.Field, duty.Place, id);
                Assert.IsNotEmpty(duty.Work, id);
                Assert.AreEqual(Duty.Stash, duty.LootTo, id);
                Assert.Greater(duty.Notice, 0f, id + ": they go for the player who comes near");
                Assert.Greater(duty.Stacks, 0, id);
            }
            Assert.AreEqual(string.Empty, Duty.Find(duties, "fell").LootTo, "honest work takes nobody's goods");
        }

        [Test]
        public void TheAmbush_IsOnTheNearestCartPath_OutOfSightOfTheSettlements_NeverOnAFootpath()
        {
            WorldPlan plan = Plan(out PlannedSettlement camp);

            Assert.IsTrue(ThiefHaunts.TryAmbush(plan, camp, 900f, out Vector2 spot, out int road));

            Assert.AreEqual(0, road, "the paved road, not the footpath that passes nearer");
            Assert.AreEqual(0f, spot.y, 0.01f);
            Assert.AreEqual(450f, spot.x, 6f);
            Assert.IsFalse(ThiefHaunts.TryAmbush(plan, camp, 200f, out _, out _), "no cart path within 200 m");

            // A camp close by the town: the nearest road is in the town's sight, so the ambush moves along it.
            PlannedSettlement near = Place(plan, "thieves-camp", new Vector2(40f, 120f));
            Assert.IsTrue(ThiefHaunts.TryAmbush(plan, near, 900f, out Vector2 hidden, out _));
            Assert.GreaterOrEqual(hidden.magnitude, ThiefHaunts.OutOfSight - 0.01f);
            Assert.Less(hidden.magnitude, ThiefHaunts.OutOfSight + 15f);
        }

        [Test]
        public void TheMark_IsTheNearestHonestSettlementWithShops()
        {
            WorldPlan plan = Plan(out PlannedSettlement camp);
            Place(plan, "thieves-camp", new Vector2(460f, 320f));

            Assert.AreEqual(0, ThiefHaunts.Mark(plan, camp, 1400f).Index, "the town, 541 m off; the village is 626 m, the other camp no mark");
            Assert.IsNull(ThiefHaunts.Mark(plan, camp, 300f));
        }

        [Test]
        public void ACampByTheWater_WaitsOnTheNearestRoute_OfItsOwnWater()
        {
            WorldPlan plan = Plan(out PlannedSettlement camp);
            plan.WaterRoutes.Add(new WaterRoute
            {
                Kind = WaterRouteKind.Sea, From = 0, To = 1,
                Points = new List<Vector2> { new Vector2(0f, 800f), new Vector2(400f, 700f), new Vector2(800f, 800f) },
            });
            plan.WaterRoutes.Add(new WaterRoute
            {
                Kind = WaterRouteKind.River, From = 0, To = 1,
                Points = new List<Vector2> { new Vector2(400f, 310f), new Vector2(440f, 330f), new Vector2(480f, 350f) },
            });

            Assert.IsFalse(ThiefHaunts.TryIntercept(plan, camp, 40f, 600f, out _, out _, out _, out _), "no water of its own");

            // A harbour: the survey cell whose middle is (420, 500).
            camp.Harbour = new Vector2Int(60, 62);
            Assert.IsTrue(ThiefHaunts.TryIntercept(plan, camp, 40f, 600f, out WaterRouteKind kind, out Vector2 cove, out Vector2 spot, out int route));
            Assert.AreEqual(WaterRouteKind.Sea, kind);
            Assert.AreEqual(0, route, "the river route passes nearer, but its boat is not a sea boat's prey");
            Assert.AreEqual(new Vector2(420f, 500f), cove);
            Assert.AreEqual(new Vector2(400f, 700f), spot, "a place along the route, never its ends");
        }

        [Test]
        public void ACampByTheSea_HasAPirateCutter_OutOnItsSorties_AndHomeWithItsHaul()
        {
            WorldPlan plan = Plan(out PlannedSettlement camp);
            plan.WaterRoutes.Add(new WaterRoute
            {
                Kind = WaterRouteKind.Sea, From = 0, To = 1,
                Points = new List<Vector2> { new Vector2(0f, 800f), new Vector2(400f, 700f), new Vector2(800f, 800f) },
            });
            Assert.IsEmpty(RaiderRuns.Plan(plan, VesselClass.Defaults(), 40f, 700f), "a camp with no water has no boat");
            camp.Harbour = new Vector2Int(60, 62);

            RaiderRun run = RaiderRuns.Plan(plan, VesselClass.Defaults(), 40f, 700f).Single();

            Assert.AreEqual(VesselClass.SeaRaider, run.Class.Id);
            Assert.AreEqual(camp.Index, run.Camp);
            Assert.AreEqual(0, run.Route);
            const float length = 200f;
            Assert.IsFalse(run.Step(false, length, 30f));
            Assert.AreEqual(RaiderPhase.Moored, run.Phase);
            run.Step(true, length, 10f);
            Assert.AreEqual(RaiderPhase.Out, run.Phase);
            Assert.AreEqual(run.Class.Speed * 10f, run.Along, 1e-3f);
            run.Step(true, length, 1000f);
            Assert.AreEqual(RaiderPhase.Waiting, run.Phase);
            Assert.AreEqual(length, run.Along, 1e-3f);
            // With a haul aboard it goes home though the sortie is not over, and says when it is in.
            run.Loaded = true;
            Assert.IsFalse(run.Step(true, length, 5f));
            Assert.AreEqual(RaiderPhase.Home, run.Phase);
            Assert.IsTrue(run.Step(true, length, 1000f));
            Assert.AreEqual(RaiderPhase.Moored, run.Phase);

            Occupation pirate = Occupation.Find(Occupation.Defaults(), Occupation.Pirate);
            Duty prowl = Duty.Find(Duty.Defaults(), pirate.DutyAt(2, 9f));
            Assert.AreEqual(Duty.Board, prowl.Work);
            Assert.AreEqual(Duty.Stash, prowl.LootTo);
            Assert.AreNotEqual(Duty.Board, Duty.Find(Duty.Defaults(), pirate.DutyAt(2, 12.5f)).Work, "in between the two sorties");
        }

        [Test]
        public void ARobbery_MovesStacksAndAShareOfThePurse_AndNothingIsMadeOrLost()
        {
            ItemDefinition ore = ItemDefinition.Create("ore", "Ore"), wood = ItemDefinition.Create("wood", "Wood"), pick = ItemDefinition.Create("pick", "Pick");
            try
            {
                var bag = new Inventory(6);
                bag.Set(0, new ItemStack(pick, 1));
                bag.Set(1, new ItemStack(ore, 9));
                bag.Set(3, new ItemStack(wood, 4));
                var sack = new Inventory(8);
                var taken = new List<ItemStack>();

                int moved = Robbery.Take(bag, sack, 5, new System.Random(1), taken, firstSlot: 1);

                Assert.AreEqual(13, moved);
                Assert.AreEqual(2, taken.Count);
                Assert.AreEqual(1, bag.Count(pick), "the slot before the first is left alone");
                Assert.AreEqual(0, bag.Count(ore) + bag.Count(wood));
                Assert.AreEqual(9, sack.Count(ore));
                Assert.AreEqual(4, sack.Count(wood));
                Assert.IsFalse(Robbery.IsEmpty(sack));

                var stash = new Inventory(30);
                Assert.AreEqual(13, Robbery.TakeAll(sack, stash));
                Assert.IsTrue(Robbery.IsEmpty(sack));
                Assert.AreEqual(9, stash.Count(ore));

                Wallet purse = new Wallet(101.5f), thief = new Wallet();
                Assert.AreEqual(30f, Robbery.TakeCoins(purse, thief, 0.3f, 500f), "whole coins");
                Assert.AreEqual(71.5f, purse.Coins, 1e-3f);
                Assert.AreEqual(30f, thief.Coins, 1e-3f);
                Assert.AreEqual(10f, Robbery.TakeCoins(purse, thief, 0.9f, 10f), "never more than the most");
                Assert.AreEqual(0f, Robbery.TakeCoins(new Wallet(0.5f), thief, 1f, 10f));
            }
            finally
            {
                Object.DestroyImmediate(ore);
                Object.DestroyImmediate(wood);
                Object.DestroyImmediate(pick);
            }
        }
    }
}
