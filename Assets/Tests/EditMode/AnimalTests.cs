using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// The animals (GL602-GL607): the species table against the biomes and the item catalog, where herds are sited, what an
    /// animal's brain decides by day, by night and when the player comes near, what a carcass holds and how hits kill.
    /// </summary>
    public class AnimalTests
    {
        private static readonly string[] Wanted = { "rabbit", "goat", "sheep", "yak", "alpaca", "cow", "deer", "elk", "bison" };

        private ItemCatalog catalog;
        private PriceList prices;
        private List<BiomeDefinition> biomes;

        [OneTimeSetUp]
        public void Load()
        {
            catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Config/Items/ItemCatalog.asset");
            prices = AssetDatabase.LoadAssetAtPath<PriceList>("Assets/Config/Economy/Merchant prices.asset");
            biomes = BiomeDefinition.Defaults(null, null, null);
        }

        private BiomeDefinition Biome(string name)
        {
            return biomes.Single(b => b.Name == name);
        }

        // ---- GL602: the species ----

        [Test]
        public void Species_AreTheNineAsked_EachWithValidData()
        {
            Assert.That(AnimalSpeciesTable.All.Select(s => s.Id), Is.EquivalentTo(Wanted));
            foreach (AnimalSpecies species in AnimalSpeciesTable.All)
            {
                string what = species.Id;
                Assert.AreSame(species, AnimalSpeciesTable.Find(species.Id));
                Assert.That(species.Biomes, Is.Not.Empty, what);
                foreach (string biome in species.Biomes)
                {
                    Assert.That(biomes.Select(b => b.Name), Does.Contain(biome), what + " lives in a biome that exists");
                }
                Assert.That(species.Herd.x, Is.GreaterThanOrEqualTo(1), what);
                Assert.That(species.Herd.y, Is.GreaterThanOrEqualTo(species.Herd.x), what);
                Assert.That(species.Health, Is.GreaterThan(0f), what);
                Assert.That(species.Length, Is.GreaterThan(0f), what);
                Assert.That(species.ShoulderHeight, Is.GreaterThan(0f), what);
                Assert.That(species.RunSpeed, Is.GreaterThan(species.WalkSpeed), what);
                Assert.That(species.WalkSpeed, Is.GreaterThan(0f), what);
                Assert.That(species.Skittishness, Is.GreaterThan(0f), what);
                Assert.That(species.Drops, Is.Not.Empty, what);
            }
            Assert.That(AnimalSpeciesTable.All.Where(s => s.Feeds == AnimalSpeciesTable.Textile).Select(s => s.Id),
                Is.EquivalentTo(new[] { "rabbit", "goat", "sheep", "yak", "alpaca" }));
            Assert.That(AnimalSpeciesTable.All.Where(s => s.Feeds == AnimalSpeciesTable.Tannery).Select(s => s.Id),
                Is.EquivalentTo(new[] { "cow", "deer", "elk", "bison" }));
        }

        // ---- GL603: the drops ----

        [Test]
        public void Drops_AreItemsInTheCatalog_WithIconsAndPrices_EachSpeciesItsOwn()
        {
            var seen = new HashSet<string>();
            foreach (AnimalSpecies species in AnimalSpeciesTable.All)
            {
                foreach (AnimalDrop drop in species.Drops)
                {
                    ItemDefinition item = catalog.Find(drop.ItemId);
                    Assert.IsNotNull(item, drop.ItemId + " is in the catalog");
                    Assert.AreEqual(drop.ItemId, item.Id);
                    Assert.IsNotNull(item.Icon, drop.ItemId + " has an icon");
                    Assert.IsTrue(prices.TryGetLot(item, out int coins, out int _), drop.ItemId + " has a price");
                    Assert.That(coins, Is.GreaterThan(0));
                    Assert.IsTrue(seen.Add(drop.ItemId), drop.ItemId + " comes from one species only");
                    Assert.That(drop.Max, Is.GreaterThanOrEqualTo(1));
                }
            }
        }

        [Test]
        public void Specialties_BuyTheFursWoolsAndHidesByItemId()
        {
            List<EconomySpecialty> all = EconomySpecialty.Defaults();
            IEnumerable<string> fabrics = all.Single(s => s.Id == "fabrics").Buys.Where(g => g.InGame).Select(g => g.ItemId);
            IEnumerable<string> tannery = all.Single(s => s.Id == "tannery").Buys.Where(g => g.InGame).Select(g => g.ItemId);
            Assert.That(fabrics, Is.EquivalentTo(new[] { "rabbit-fur", "goat-wool", "sheep-wool", "yak-wool", "alpaca-wool" }));
            Assert.That(tannery, Is.EquivalentTo(new[] { "cow-hide", "deer-hide", "elk-hide", "bison-hide" }));
            foreach (string id in fabrics.Concat(tannery))
            {
                Assert.IsNotNull(catalog.Find(id), id);
            }
        }

        [Test]
        public void RollDrops_StaysWithinEachRange_AndIsTheSameForASeed()
        {
            foreach (AnimalSpecies species in AnimalSpeciesTable.All)
            {
                for (int seed = 0; seed < 40; seed++)
                {
                    var loot = new List<AnimalLoot>();
                    species.RollDrops(new System.Random(seed), loot);
                    var again = new List<AnimalLoot>();
                    species.RollDrops(new System.Random(seed), again);
                    Assert.That(again.Select(l => l.Count), Is.EqualTo(loot.Select(l => l.Count)));
                    foreach (AnimalLoot each in loot)
                    {
                        AnimalDrop drop = species.Drops.Single(d => d.ItemId == each.ItemId);
                        Assert.That(each.Count, Is.InRange(Mathf.Max(1, drop.Min), drop.Max), species.Id);
                    }
                }
            }
        }

        // ---- GL605: where the herds are ----

        [Test]
        public void Pick_GivesOnlySpeciesOfTheBiome_AndNoneWhereNoneLives()
        {
            foreach (BiomeDefinition biome in biomes)
            {
                for (int i = 0; i < 20; i++)
                {
                    AnimalSpecies picked = HerdSites.Pick(biome.Name, AnimalSpeciesTable.All, i / 20.0);
                    if (picked != null)
                    {
                        Assert.IsTrue(picked.LivesIn(biome.Name), picked.Id + " in " + biome.Name);
                    }
                }
            }
            Assert.IsNull(HerdSites.Pick(BiomeDefinition.OceanName, AnimalSpeciesTable.All, 0.5));
            Assert.IsNull(HerdSites.Pick(BiomeDefinition.BeachName, AnimalSpeciesTable.All, 0.5));
            Assert.IsNull(HerdSites.Pick(BiomeDefinition.DesertName, AnimalSpeciesTable.All, 0.5));
            var grass = Enumerable.Range(0, 50).Select(i => HerdSites.Pick(BiomeDefinition.GrasslandName, AnimalSpeciesTable.All, i / 50.0).Id).Distinct();
            Assert.That(grass, Is.EquivalentTo(new[] { "rabbit", "sheep", "cow", "deer", "bison" }));
            // Every species lives somewhere it can be picked.
            foreach (AnimalSpecies species in AnimalSpeciesTable.All)
            {
                Assert.IsTrue(species.Biomes.Any(b => Enumerable.Range(0, 50).Any(
                    i => HerdSites.Pick(b, AnimalSpeciesTable.All, i / 50.0) == species)), species.Id);
            }
        }

        [Test]
        public void Sites_AreTheSameForASeed_InTheirCells_SizedAsTheSpeciesSays()
        {
            BiomeDefinition forest = Biome(BiomeDefinition.ForestName), ocean = Biome(BiomeDefinition.OceanName);
            // Forest east of x = 0, sea west of it.
            BiomeDefinition At(float x, float z) => x >= 0f ? forest : ocean;

            var first = new List<HerdSite>();
            HerdSites.Near(42, new Vector2(0f, 0f), 1500f, At, AnimalSpeciesTable.All, first);
            var second = new List<HerdSite>();
            HerdSites.Near(42, new Vector2(0f, 0f), 1500f, At, AnimalSpeciesTable.All, second);
            var other = new List<HerdSite>();
            HerdSites.Near(43, new Vector2(0f, 0f), 1500f, At, AnimalSpeciesTable.All, other);

            Assert.That(first, Is.Not.Empty);
            Assert.That(second.Select(s => (s.Cell, s.Centre, s.Species.Id, s.Count, s.Seed)),
                Is.EqualTo(first.Select(s => (s.Cell, s.Centre, s.Species.Id, s.Count, s.Seed))));
            Assert.That(other.Select(s => s.Centre), Is.Not.EqualTo(first.Select(s => s.Centre)));
            foreach (HerdSite site in first)
            {
                Assert.That(site.Centre.x, Is.GreaterThanOrEqualTo(0f), "none in the sea");
                Assert.IsTrue(site.Species.LivesIn(BiomeDefinition.ForestName));
                Assert.AreEqual(site.Cell, HerdSites.CellOf(site.Centre.x, site.Centre.y));
                Assert.That(site.Count, Is.InRange(site.Species.Herd.x, site.Species.Herd.y));
                Assert.That((site.Centre - Vector2.zero).magnitude, Is.LessThanOrEqualTo(1500f));
            }
            // Forest is rich in game: most of its cells have a herd, but not all.
            int cells = first.Count;
            float land = Mathf.PI * 1500f * 1500f * 0.5f / (HerdSites.CellSize * HerdSites.CellSize);
            Assert.That(cells / land, Is.InRange(0.55f, 0.95f));
            Assert.That(first.Select(s => s.AnimalKey(0)).Distinct().Count(), Is.EqualTo(first.Count));
        }

        [Test]
        public void StandIn_SandHasNoAnimals_RockAndSoilDo()
        {
            var cell = new Vector2Int(1, -2);
            Assert.IsNull(HerdSites.Pick(HerdSites.StandInBiome(true, false, cell), AnimalSpeciesTable.All, 0.3));
            Assert.IsNotNull(HerdSites.Pick(HerdSites.StandInBiome(false, true, cell), AnimalSpeciesTable.All, 0.3));
            var soils = Enumerable.Range(0, 30).Select(i => HerdSites.StandInBiome(false, false, new Vector2Int(i, i * 3))).Distinct();
            Assert.That(soils, Is.EquivalentTo(new[] { BiomeDefinition.GrasslandName, BiomeDefinition.ForestName }));
        }

        // ---- GL606: the brain ----

        private static AnimalSenses Senses(float hour, Vector2 position, Vector2? threat = null)
        {
            return new AnimalSenses
            {
                Hour = hour, Position = position, Home = Vector2.zero, HerdCentre = Vector2.zero,
                HasThreat = threat.HasValue, Threat = threat ?? default,
            };
        }

        // Runs a brain for some seconds, moving its body as it asks; returns where it ends.
        private static Vector2 Run(AnimalBrain brain, float hour, Vector2 position, float seconds, Vector2? threat = null, HashSet<AnimalState> seen = null)
        {
            const float Step = 0.1f;
            for (float t = 0f; t < seconds; t += Step)
            {
                brain.Tick(Step, Senses(hour, position, threat));
                position += brain.Heading * (brain.Speed * Step);
                seen?.Add(brain.State);
            }
            return position;
        }

        [Test]
        public void Brain_ByDay_GrazesAndWanders_WithinTheHerdsRange()
        {
            AnimalSpecies sheep = AnimalSpeciesTable.Find("sheep");
            var brain = new AnimalBrain(sheep, 5);
            var seen = new HashSet<AnimalState>();
            Vector2 position = Vector2.zero;
            float farthest = 0f;
            for (int i = 0; i < 60; i++)
            {
                position = Run(brain, 12f, position, 10f, null, seen);
                farthest = Mathf.Max(farthest, position.magnitude);
            }
            Assert.That(seen, Is.EquivalentTo(new[] { AnimalState.Graze, AnimalState.Wander }));
            Assert.That(farthest, Is.GreaterThan(2f), "it does move about");
            Assert.That(farthest, Is.LessThanOrEqualTo(sheep.Range + 1f));
        }

        [Test]
        public void Brain_AStray_WalksBackToTheHerd()
        {
            AnimalSpecies cow = AnimalSpeciesTable.Find("cow");
            var brain = new AnimalBrain(cow, 3);
            Vector2 end = Run(brain, 12f, new Vector2(cow.Range * 3f, 0f), 200f);
            Assert.That(end.magnitude, Is.LessThanOrEqualTo(cow.Range + 1f));
        }

        [Test]
        public void Brain_AtNight_GathersAndRests_AndIsUpAgainByDay()
        {
            AnimalSpecies goat = AnimalSpeciesTable.Find("goat");
            var brain = new AnimalBrain(goat, 9);
            var seen = new HashSet<AnimalState>();
            Vector2 position = Run(brain, 23f, new Vector2(12f, 0f), 60f, null, seen);
            Assert.AreEqual(AnimalState.Rest, brain.State);
            Assert.IsTrue(brain.IsLying);
            Assert.That(seen, Does.Contain(AnimalState.Gather));
            Assert.That(position.magnitude, Is.LessThan(6f), "it lies with the herd");
            Assert.AreEqual(0f, brain.Speed);

            Run(brain, 9f, position, 1f);
            Assert.That(brain.State, Is.EqualTo(AnimalState.Graze).Or.EqualTo(AnimalState.Wander));
            Assert.IsFalse(brain.IsLying);
        }

        [Test]
        public void Routine_DeerAndRabbitsAreOutAtDawnAndDusk_TheRestByDay()
        {
            AnimalSpecies deer = AnimalSpeciesTable.Find("deer"), cow = AnimalSpeciesTable.Find("cow");
            Assert.AreEqual(AnimalRoutine.Twilight, deer.Routine);
            Assert.AreEqual(AnimalRoutine.Twilight, AnimalSpeciesTable.Find("rabbit").Routine);
            Assert.IsTrue(deer.IsActiveAt(SkyPositions.SunriseHour));
            Assert.IsTrue(deer.IsActiveAt(SkyPositions.SunsetHour));
            Assert.IsFalse(deer.IsActiveAt(12f));
            Assert.IsFalse(deer.IsActiveAt(0f));
            Assert.IsTrue(cow.IsActiveAt(12f));
            Assert.IsFalse(cow.IsActiveAt(0f));
            Assert.IsFalse(cow.IsActiveAt(SkyPositions.SunsetHour + 1f));
        }

        [Test]
        public void Brain_FleesThePlayerWhenClose_NotWhenFar_AndCalmsDownAfter()
        {
            AnimalSpecies deer = AnimalSpeciesTable.Find("deer");
            var brain = new AnimalBrain(deer, 1);
            var far = new Vector2(deer.Skittishness + 5f, 0f);
            brain.Tick(0.1f, Senses(6f, Vector2.zero, far));
            Assert.AreNotEqual(AnimalState.Flee, brain.State);

            var near = new Vector2(deer.Skittishness - 1f, 0f);
            brain.Tick(0.1f, Senses(6f, Vector2.zero, near));
            Assert.AreEqual(AnimalState.Flee, brain.State);
            Assert.AreEqual(deer.RunSpeed, brain.Speed);
            Assert.That(Vector2.Dot(brain.Heading, Vector2.left), Is.GreaterThan(0.99f), "straight away from the player");

            // The player stays put: it runs out of reach, then stops running.
            Vector2 end = Run(brain, 6f, Vector2.zero, AnimalBrain.FleeSeconds + 20f, near);
            Assert.AreNotEqual(AnimalState.Flee, brain.State);
            Assert.That((end - near).magnitude, Is.GreaterThan(deer.Skittishness));
        }

        [Test]
        public void Brain_AsleepNoticesLater_AndBlockedFleeingSwerves()
        {
            AnimalSpecies sheep = AnimalSpeciesTable.Find("sheep");
            var brain = new AnimalBrain(sheep, 2);
            Run(brain, 0f, Vector2.zero, 5f);
            Assert.AreEqual(AnimalState.Rest, brain.State);
            brain.Tick(0.1f, Senses(0f, Vector2.zero, new Vector2(sheep.Skittishness * 0.8f, 0f)));
            Assert.AreEqual(AnimalState.Rest, brain.State, "asleep, it lets the player nearer");
            brain.Tick(0.1f, Senses(0f, Vector2.zero, new Vector2(sheep.Skittishness * 0.5f, 0f)));
            Assert.AreEqual(AnimalState.Flee, brain.State);

            Vector2 straight = brain.Heading;
            brain.Blocked();
            brain.Tick(0.1f, Senses(0f, Vector2.zero, new Vector2(sheep.Skittishness * 0.5f, 0f)));
            Assert.That(Vector2.Angle(straight, brain.Heading), Is.InRange(60f, 100f));
            Assert.AreEqual(AnimalState.Flee, brain.State);
        }

        // ---- GL607: hits ----

        [Test]
        public void Hit_MakesItFlee_AndEnoughOfThemKillIt()
        {
            AnimalSpecies bison = AnimalSpeciesTable.Find("bison");
            var brain = new AnimalBrain(bison, 4);
            float damage = 1.2f * AnimalSpeciesTable.DamagePerPower;
            Assert.IsFalse(brain.Hit(damage, new Vector2(1f, 0f)));
            Assert.AreEqual(AnimalState.Flee, brain.State);
            Assert.AreEqual(bison.Health - damage, brain.Health, 1e-4f);
            brain.Tick(0.1f, Senses(12f, Vector2.zero));
            Assert.That(Vector2.Dot(brain.Heading, Vector2.left), Is.GreaterThan(0.99f), "away from where the hit came");

            int hits = 1;
            while (!brain.Hit(damage, new Vector2(1f, 0f)))
            {
                hits++;
                Assert.That(hits, Is.LessThan(100));
            }
            Assert.AreEqual(Mathf.CeilToInt(bison.Health / damage), hits + 1);
            Assert.IsTrue(brain.IsDead);
            Assert.IsTrue(brain.IsLying);
            Assert.AreEqual(0f, brain.Health);

            // Dead stays dead: no more hits count, and it goes nowhere.
            Assert.IsFalse(brain.Hit(damage, Vector2.zero));
            brain.Tick(1f, Senses(12f, Vector2.zero, new Vector2(0.5f, 0f)));
            Assert.AreEqual(AnimalState.Dead, brain.State);
            Assert.AreEqual(0f, brain.Speed);
        }

        [Test]
        public void Hit_ARabbitDiesToOneToolBlow_ButNotToABareHand()
        {
            AnimalSpecies rabbit = AnimalSpeciesTable.Find("rabbit");
            Assert.IsFalse(new AnimalBrain(rabbit, 1).Hit(0.5f * AnimalSpeciesTable.DamagePerPower, Vector2.zero));
            Assert.IsTrue(new AnimalBrain(rabbit, 1).Hit(1.2f * AnimalSpeciesTable.DamagePerPower, Vector2.zero));
        }
    }
}
