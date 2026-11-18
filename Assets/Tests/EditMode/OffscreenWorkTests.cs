using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// Settlements at work while nobody watches (GL730): the hours their people spend on each work, where they dig, how
    /// much is done by a time on the clock; and the land kept round the player's start (GL731).
    /// </summary>
    public class OffscreenWorkTests
    {
        private static readonly List<Duty> Duties = Duty.Defaults();
        private static readonly List<Occupation> Occupations = Occupation.Defaults();
        private static readonly WorldGrid Grid = new WorldGrid(new Vector3Int(16, 16, 16), 0.25f);

        private static SettlementSite Site()
        {
            List<EconomySpecialty> specialties = EconomySpecialty.Defaults();
            var settlement = new PlannedSettlement
            {
                Index = 3, Type = SettlementType.Defaults().Find(t => t.Id == "town"), Position = new Vector2(1000f, 1000f), Name = "Test town",
                Population = 12,
            };
            foreach (string shop in new[] { "lumber-supplies", "mining" })
            {
                settlement.Shops.Add(new PlannedShop { Specialty = specialties.Find(s => s.Id == shop), Reason = "test" });
            }
            return SettlementLayout.Plan(settlement, 32f, new SettlementSiteRules(), (x, z) => 40f);
        }

        private static SettlementWorks Works(SettlementSite site, WorkLand land = null)
        {
            return new SettlementWorks(site, SettlementRoster.Build(site, Occupations, 7), Duties, land ?? new WorkLand(null, null, 32f), null, Grid,
                (x, z) => 40f, spot => true);
        }

        [Test]
        public void DutyHours_CountALumberjacksFellingShifts()
        {
            var hours = new DutyHours(Duties);
            Occupation lumberjack = Occupation.Find(Occupations, "lumberjack");

            // Monday to Saturday 7-12, and 13-17 on the days the afternoon is not spent sawing (Monday, Wednesday, Friday).
            Assert.That(hours.Weekly(lumberjack, WorkRate.Fell), Is.EqualTo(42d).Within(1e-6));
            Assert.That(hours.Before(lumberjack, WorkRate.Fell, 12d), Is.EqualTo(5d).Within(1e-6), "Monday morning.");
            Assert.That(hours.Between(lumberjack, WorkRate.Fell, 12d, 13d), Is.Zero, "The midday walk.");
            Assert.That(hours.Before(lumberjack, WorkRate.Fell, 24d * 7 * 3 + 12d), Is.EqualTo(42d * 3 + 5d).Within(1e-6), "Whole weeks, then a part.");
            Assert.That(hours.Weekly(lumberjack, WorkRate.DigPit), Is.Zero);
        }

        [Test]
        public void WorkLand_KeepsTheStartingParcelAndTheEightRoundIt()
        {
            var land = new WorkLand(null, new Vector2(100f, 100f), 32f);
            var town = new Rect(500f, 500f, 40f, 40f);

            Assert.That(land.StartLand, Is.EqualTo(new Rect(64f, 64f, 96f, 96f)));
            Assert.That(land.MayWork(town, new Vector2(70f, 150f), 0f), Is.False, "A corner parcel of the nine.");
            Assert.That(land.MayWork(town, new Vector2(165f, 100f), 0f), Is.True, "The next parcel out.");
            Assert.That(land.MayWork(town, new Vector2(165f, 100f), 6f), Is.False, "Within the margin.");
            Assert.That(land.MayWork(town, new Vector2(520f, 520f), 0f), Is.False, "In the town.");
            Assert.That(new WorkLand(null, null, 32f).MayWork(town, new Vector2(100f, 100f), 0f), Is.True, "No start, nothing kept.");
        }

        [Test]
        public void DigSite_LaysPitsOutsideTheTown_AndNoTunnelOnFlatGround()
        {
            SettlementSite site = Site();
            var land = new WorkLand(null, null, 32f);
            var pits = new DigSite(DigSite.SeedOf(3, 1), false, site.Bounds, 40f, Grid, (x, z) => 40f, spot => true, (p, m) => land.MayWork(site.Bounds, p, m));
            var tunnels = new DigSite(DigSite.SeedOf(3, 1), true, site.Bounds, 60f, Grid, (x, z) => 40f, spot => true, (p, m) => land.MayWork(site.Bounds, p, m));

            Assert.That(pits.Count, Is.EqualTo(DigSite.MostDigs));
            Assert.That(tunnels.Count, Is.Zero);
            Assert.That(pits.TryGet(0, out Dig first), Is.True);
            Assert.That(first.GroundVoxels, Is.GreaterThan(100));
            Assert.That(first.GroundVoxels, Is.EqualTo(first.Ground.Count(g => g)));
            Vector3 middle = first.Middle;
            Assert.That(land.MayWork(site.Bounds, new Vector2(middle.x, middle.z), 6f), Is.True);
            Assert.That(pits.TryGet(1, out Dig second) && second.Top != first.Top, Is.True, "The next pit is somewhere else.");
        }

        [Test]
        public void DigSite_DrivesATunnelIntoASlope()
        {
            SettlementSite site = Site();
            Rect town = site.Bounds;
            // The ground rises steeply east of the town.
            System.Func<float, float, float> ground = (x, z) => x > town.xMax + 20f ? 40f + (x - town.xMax - 20f) : 40f;
            var tunnels = new DigSite(DigSite.SeedOf(3, 1), true, town, 60f, Grid, ground, spot => true, (p, m) => true);

            Assert.That(tunnels.TryGet(0, out Dig dig), Is.True);
            Assert.That(dig.Bore, Is.True);
            Assert.That(dig.Along.x, Is.GreaterThan(0.5f), "Into the rise.");
        }

        [Test]
        public void SettlementWorks_DigsAtTheRate_ForTheHoursWorked()
        {
            SettlementSite site = Site();
            SettlementWorks works = Works(site);
            var hours = new DutyHours(Duties);
            List<WorkRate> rates = WorkRate.Defaults();
            int miners = works.Residents.Count(r => r.Occupation.Id == "miner");
            Assume.That(miners, Is.GreaterThan(0));

            IReadOnlyList<DigSite> sites = works.Sites;
            Assert.That(sites.Count, Is.EqualTo(1), "Flat ground: the tunnel's shifts go to the pits.");
            int shop = works.Residents.First(r => r.Occupation.Id == "miner").Shop;
            Assert.That(works.SiteOf(shop, true), Is.SameAs(works.SiteOf(shop, false)));

            long week = works.VoxelsBy(sites[0], 24d * 7, hours, rates);
            Assert.That(week, Is.EqualTo((long)(miners * 42 * WorkRate.Of(rates, WorkRate.DigPit))));
            Assert.That(works.VoxelsBy(sites[0], 6d, hours, rates), Is.Zero, "Nothing before the first shift.");
            Assert.That(works.TreesBy(24d * 7, hours, rates), Is.Zero, "No trees in this world.");
        }

        [Test]
        public void SettlementWorks_ClaimsNoTreesOnTheStartLand()
        {
            SettlementSite site = Site();
            var land = new WorkLand(null, site.Bounds.center + new Vector2(120f, 0f), 32f);
            SettlementWorks works = Works(site, land);

            Assert.That(works.MayWork(site.Bounds.center + new Vector2(120f, 0f), 0f), Is.False);
            Assert.That(works.MayWork(site.Bounds.center + new Vector2(-120f, 0f), 0f), Is.True);
        }
    }
}
