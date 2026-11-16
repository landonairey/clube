using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>The people of a settlement (GL615): occupations and their week, who lives where, the ways through the streets, and what a digger takes out.</summary>
    public class PopulationTests
    {
        private static PlannedSettlement Settlement(string type, int population, params string[] shops)
        {
            List<EconomySpecialty> specialties = EconomySpecialty.Defaults();
            var settlement = new PlannedSettlement
            {
                Index = 3, Type = SettlementType.Defaults().Find(t => t.Id == type), Position = new Vector2(1000f, 1000f), Name = $"Test {type}",
                Population = population,
            };
            foreach (string shop in shops)
            {
                settlement.Shops.Add(new PlannedShop { Specialty = specialties.Find(s => s.Id == shop), Reason = "test" });
            }
            return settlement;
        }

        private static SettlementSite Site(string type, int population, params string[] shops)
        {
            return SettlementLayout.Plan(Settlement(type, population, shops), 32f, new SettlementSiteRules(), (x, z) => 40f);
        }

        [Test]
        public void TheWeek_StartsOnAMonday_AndComesRound()
        {
            Assert.AreEqual(0, Occupation.DayOfWeek(1));
            Assert.AreEqual(6, Occupation.DayOfWeek(7));
            Assert.AreEqual(0, Occupation.DayOfWeek(8));
            Assert.AreEqual(7, Occupation.DayNames.Length);
        }

        [Test]
        public void ALumberjack_CyclesDutiesThroughTheHoursOfTheWeek()
        {
            Occupation lumberjack = Occupation.Defaults().Find(o => o.Id == "lumberjack");

            Assert.AreEqual("fell", lumberjack.DutyAt(0, 8f), "Monday morning: out felling");
            Assert.AreEqual(Duty.Stroll, lumberjack.DutyAt(0, 12.5f), "midday");
            Assert.AreEqual("fell", lumberjack.DutyAt(0, 14f), "Monday afternoon");
            Assert.AreEqual("saw", lumberjack.DutyAt(1, 14f), "Tuesday afternoon: boards in the yard");
            Assert.AreEqual("fell", lumberjack.DutyAt(2, 14f), "Wednesday afternoon");
            Assert.AreEqual("sweep", lumberjack.DutyAt(6, 10f), "Sunday: no felling");
            Assert.AreEqual(Duty.AtHome, lumberjack.DutyAt(0, 20f), "evening");
            Assert.AreEqual(Duty.Sleep, lumberjack.DutyAt(0, 23f));
            Assert.AreEqual(Duty.Sleep, lumberjack.DutyAt(3, 3f));
        }

        [Test]
        public void AShiftThroughMidnight_HoldsOnBothSidesOfIt()
        {
            var night = new DutyShift(0, 6, 22f, 5f, "watch");

            Assert.IsTrue(night.Holds(2, 23f));
            Assert.IsTrue(night.Holds(2, 4f));
            Assert.IsFalse(night.Holds(2, 12f));
        }

        [Test]
        public void EveryDutyAnOccupationNames_IsARow_AndEverySpecialtyHasAnOccupation()
        {
            List<Duty> duties = Duty.Defaults();
            List<Occupation> occupations = Occupation.Defaults();

            foreach (Occupation occupation in occupations)
            {
                foreach (DutyShift shift in occupation.Week)
                {
                    Assert.IsNotNull(Duty.Find(duties, shift.Duty), $"{occupation.Id} names the duty {shift.Duty}");
                }
            }
            Assert.AreEqual(duties.Count, duties.Select(d => d.Id).Distinct().Count(), "duty ids are their own");
            foreach (EconomySpecialty specialty in EconomySpecialty.Defaults())
            {
                Assert.IsNotNull(Occupation.For(occupations, specialty.Id), specialty.Id);
            }
            Assert.AreEqual("lumberjack", Occupation.For(occupations, "lumber-supplies").Id);
            Assert.AreEqual("miner", Occupation.For(occupations, "mining").Id);
            Assert.AreEqual("smith", Occupation.For(occupations, "blacksmith").Id);
            Assert.AreEqual("shepherd", Occupation.For(occupations, "fabrics").Id);
            Assert.AreEqual("hunter", Occupation.For(occupations, "tannery").Id);
            Assert.AreEqual("resident", Occupation.For(occupations, "merchant").Id, "a shop with no occupation of its own");
        }

        [Test]
        public void ARoster_IsThePopulationLessTheKeepers_SharedAmongTheShops_TwoToAHouse()
        {
            SettlementSite site = Site("town", 12, "lumber-supplies", "mining", "blacksmith");

            List<Resident> residents = SettlementRoster.Build(site, Occupation.Defaults(), 7);

            Assert.AreEqual(9, residents.Count, "12 people, 3 of them keepers");
            Assert.AreEqual(3, residents.Count(r => r.Occupation.Id == "lumberjack"));
            Assert.AreEqual(3, residents.Count(r => r.Occupation.Id == "miner"));
            Assert.AreEqual(3, residents.Count(r => r.Occupation.Id == "smith"));
            foreach (Resident resident in residents)
            {
                Assert.AreEqual(SettlementBuildingKind.House, site.Buildings[resident.Home].Kind);
                Assert.AreEqual(SettlementBuildingKind.Shop, site.Buildings[resident.Shop].Kind);
            }
            Assert.LessOrEqual(residents.GroupBy(r => r.Home).Max(g => g.Count()), site.Rules.PeoplePerHouse);
            List<Resident> again = SettlementRoster.Build(site, Occupation.Defaults(), 7);
            CollectionAssert.AreEqual(residents.Select(r => r.Seed), again.Select(r => r.Seed));
        }

        [Test]
        public void ACampOfOne_HasOnlyItsKeeper()
        {
            SettlementSite site = Site("camp", 1, "lumber-supplies");

            Assert.IsEmpty(SettlementRoster.Build(site, Occupation.Defaults(), 7));
        }

        [Test]
        public void AWalkBetweenTwoDoors_KeepsOutOfTheBuildings()
        {
            SettlementSite site = Site("city", 20, "lumber-supplies", "mining", "blacksmith", "fabrics", "tannery");
            var walks = new SettlementWalks(site);
            var path = new List<Vector2>();
            Assert.Greater(walks.Nodes.Count, 0);

            int walked = 0;
            for (int a = 0; a < site.Buildings.Count; a++)
            {
                int b = (a * 7 + 3) % site.Buildings.Count;
                Vector2 from = walks.Front(a), to = walks.Front(b);
                Assert.IsFalse(walks.IsBlocked(from), "a building's front is clear");
                walks.Route(from, to, path);
                Assert.AreEqual(to, path[path.Count - 1]);
                Vector2 at = from;
                foreach (Vector2 corner in path)
                {
                    int steps = Mathf.CeilToInt(Vector2.Distance(at, corner) / 0.5f) + 1;
                    for (int s = 1; s <= steps; s++)
                    {
                        Assert.IsFalse(walks.IsBlocked(Vector2.Lerp(at, corner, s / (float)steps), 0.1f), $"from building {a} to {b}");
                    }
                    at = corner;
                    walked++;
                }
            }
            Assert.Greater(walked, site.Buildings.Count, "some ways go round by the lanes");
        }

        [Test]
        public void AWalk_ToASpotInTheOpen_IsStraight()
        {
            SettlementSite site = Site("outpost", 3, "lumber-supplies");
            var walks = new SettlementWalks(site);
            var path = new List<Vector2>();
            Vector2 from = site.Bounds.center + new Vector2(200f, 0f);

            walks.Route(from, from + new Vector2(3f, 0f), path);

            Assert.AreEqual(1, path.Count);
        }

        [Test]
        public void APit_IsDugFromTheTopDown_AndNarrowsToABowl()
        {
            var top = new Vector3Int(100, 40, 200);

            DigPlan pit = DigPlan.Pit(top, 6, 6, 1, above: 2);

            IReadOnlyList<Vector3Int> voxels = pit.Voxels;
            Assert.AreEqual(top.y + 2, voxels[0].y, "what stands over the rim goes first");
            for (int i = 1; i < voxels.Count; i++)
            {
                Assert.LessOrEqual(voxels[i].y, voxels[i - 1].y, "never back up");
            }
            Assert.AreEqual(top.y - 5, voxels.Min(v => v.y));
            int Width(int y) => voxels.Where(v => v.y == y).Max(v => v.x) - top.x;
            Assert.AreEqual(6, Width(top.y));
            Assert.AreEqual(3, Width(top.y - 3));
            Assert.AreEqual(1, Width(top.y - 5));
            Assert.AreEqual(voxels.Count, voxels.Distinct().Count());
        }

        [Test]
        public void ATunnel_IsALevelBoreFromItsMouthInwards()
        {
            var mouth = new Vector3Int(10, 20, 30);

            DigPlan bore = DigPlan.Tunnel(mouth, new Vector2(1f, 0f), 6, 9, 20);

            IReadOnlyList<Vector3Int> voxels = bore.Voxels;
            Assert.AreEqual(voxels.Count, voxels.Distinct().Count());
            Assert.AreEqual(mouth.y, voxels.Min(v => v.y), "the floor is level");
            Assert.AreEqual(mouth.y + 8, voxels.Max(v => v.y));
            Assert.AreEqual(mouth.x, voxels[0].x, "it starts at the mouth");
            Assert.AreEqual(mouth.y + 8, voxels[0].y, "each slice from the top down");
            Assert.GreaterOrEqual(voxels.Max(v => v.x), mouth.x + 19);
            Assert.GreaterOrEqual(voxels.Max(v => v.z) - voxels.Min(v => v.z), 5);
            Assert.LessOrEqual(voxels.Max(v => v.z) - voxels.Min(v => v.z), 6);
            DigPlan slanted = DigPlan.Tunnel(mouth, new Vector2(1f, 1f), 6, 9, 20);
            Assert.Greater(slanted.Voxels.Max(v => v.z), mouth.z + 10, "any direction");
        }

        [Test]
        public void TheNextVoxelToDig_IsTheFirstStillSolid_SoWorkGoesOnFromWhereItIs()
        {
            DigPlan pit = DigPlan.Pit(new Vector3Int(0, 10, 0), 3, 3, 1, above: 0);
            var dug = new HashSet<Vector3Int>(pit.Voxels.Take(10));

            Assert.IsTrue(pit.Next(v => !dug.Contains(v), out Vector3Int next));
            Assert.AreEqual(pit.Voxels[10], next);
            Assert.IsTrue(pit.Next(v => !dug.Contains(v), out Vector3Int same));
            Assert.AreEqual(next, same, "until it is dug it stays the next");
            foreach (Vector3Int voxel in pit.Voxels)
            {
                dug.Add(voxel);
            }
            Assert.IsFalse(pit.Next(v => !dug.Contains(v), out _), "a finished pit");
            dug.Remove(pit.Voxels[2]);
            Assert.IsFalse(pit.Next(v => !dug.Contains(v), out _));
            pit.Rewind();
            Assert.IsTrue(pit.Next(v => !dug.Contains(v), out Vector3Int filled));
            Assert.AreEqual(pit.Voxels[2], filled, "looked over again from the top");
        }
    }
}
