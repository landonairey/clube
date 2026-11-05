using System.Collections.Generic;
using Clube.Core;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>The town center (GL399-GL407): its site, layout, shops, hours, lessons and levelled ground.</summary>
    public class TownTests
    {
        private static ItemDefinition Item(string name)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.name = name;
            return item;
        }

        [Test]
        public void ShopHours_CounterNineToFive_ChoresThenSleepSixToSix()
        {
            ShopHours hours = ShopHours.Default;
            Assert.AreEqual(KeeperActivity.Chores, hours.At(6f));
            Assert.AreEqual(KeeperActivity.Chores, hours.At(8.9f));
            Assert.AreEqual(KeeperActivity.Counter, hours.At(9f));
            Assert.AreEqual(KeeperActivity.Counter, hours.At(16.9f));
            Assert.AreEqual(KeeperActivity.Chores, hours.At(17f));
            Assert.AreEqual(KeeperActivity.Sleep, hours.At(18f));
            Assert.AreEqual(KeeperActivity.Sleep, hours.At(23.5f));
            Assert.AreEqual(KeeperActivity.Sleep, hours.At(3f));
            Assert.AreEqual(KeeperActivity.Counter, hours.At(24f + 10f), "hours wrap round the day");
        }

        [Test]
        public void ShopHours_FormatsLikeAClock()
        {
            Assert.AreEqual("9 am", ShopHours.Format(9f));
            Assert.AreEqual("5:30 pm", ShopHours.Format(17.5f));
            Assert.AreEqual("12 am", ShopHours.Format(0f));
            Assert.AreEqual("12 pm", ShopHours.Format(12f));
        }

        [Test]
        public void Lesson_CostsTheExperienceItNeeds_AndRaisesTheLevel()
        {
            var skills = new SkillSet();
            var wallet = new Wallet(1000f);
            int first = SkillTraining.Cost(skills, SkillType.Mining, 2f);
            Assert.AreEqual(Mathf.CeilToInt(SkillSet.ExperienceFor(1) * 2f), first);

            Assert.IsTrue(SkillTraining.Train(skills, wallet, SkillType.Mining, 2f));
            Assert.AreEqual(1, skills.Level(SkillType.Mining));
            Assert.AreEqual(1000f - first, wallet.Coins, 0.01f);
            Assert.Greater(SkillTraining.Cost(skills, SkillType.Mining, 2f), first, "each level costs more than the last");
        }

        [Test]
        public void Lesson_EarnedExperienceIsADiscount()
        {
            var skills = new SkillSet();
            skills.Award(SkillType.Smelting, SkillSet.ExperienceFor(1) * 0.5f);
            Assert.AreEqual(Mathf.CeilToInt(SkillSet.ExperienceFor(1) * 0.5f * 3f), SkillTraining.Cost(skills, SkillType.Smelting, 3f));
        }

        [Test]
        public void Lesson_TooDear_ChangesNothing()
        {
            var skills = new SkillSet();
            var wallet = new Wallet(5f);
            Assert.IsFalse(SkillTraining.Train(skills, wallet, SkillType.Mining, 2f));
            Assert.AreEqual(0, skills.Level(SkillType.Mining));
            Assert.AreEqual(5f, wallet.Coins, 0.001f);
        }

        [Test]
        public void Shop_BuysOnlyItsTrade_AndSellsAtTheMarkup()
        {
            ItemDefinition ingot = Item("Copper ingot"), stick = Item("Stick"), dirt = Item("Dirt");
            PriceList master = PriceList.Create(new ItemPrice(ingot, 30), new ItemPrice(stick, 1, 2), new ItemPrice(dirt, 1, 10));
            ShopSpecialty smith = ShopSpecialty.Create(ShopKind.Blacksmith, "Blacksmith", new[] { ingot }, new[] { ingot, stick });

            PriceList buying = smith.BuyingPrices(master);
            Assert.IsTrue(buying.TryGetPrice(ingot, out int paid));
            Assert.AreEqual(30, paid, "the shop pays the merchant list's price");
            Assert.IsFalse(buying.TryGetPrice(dirt, out _), "a blacksmith doesn't buy dirt");

            List<ItemPrice> offers = smith.Offers(master);
            Assert.AreEqual(2, offers.Count);
            Assert.AreEqual(45, offers[0].Coins, "30 times 1.5");
            Assert.AreEqual(2, offers[1].Coins, "1 times 1.5, rounded up");
            Assert.AreEqual(2, offers[1].Lot, "sold by the price list's lot: 2 sticks");
        }

        [Test]
        public void WaresShop_BuysEverything()
        {
            ItemDefinition ingot = Item("Copper ingot"), dirt = Item("Dirt");
            PriceList master = PriceList.Create(new ItemPrice(ingot, 30), new ItemPrice(dirt, 1, 10));
            ShopSpecialty wares = ShopSpecialty.Create(ShopKind.Wares, "Wares", new ItemDefinition[0], new[] { ingot }, 2f, buysEverything: true);
            PriceList buying = wares.BuyingPrices(master);
            Assert.IsTrue(buying.TryGetPrice(ingot, out _));
            Assert.IsTrue(buying.TryGetPrice(dirt, out _));
            Assert.AreEqual(60, wares.Offers(master)[0].Coins);
        }

        [Test]
        public void Gate_FacesTheSpawn()
        {
            Vector2 centre = new Vector2(100f, 50f);
            foreach (Vector2 spawn in new[] { new Vector2(100f, -200f), new Vector2(400f, 60f), new Vector2(90f, 300f), new Vector2(-50f, 40f) })
            {
                float yaw = TownLayout.GateYawTowards(centre, spawn);
                Assert.AreEqual(0f, Mathf.Repeat(yaw, 90f), 1e-3f, "a quarter turn");
                Vector2 gateOut = TownLayout.Turn(new Vector2(0f, -1f), yaw);
                Assert.Greater(Vector2.Dot(gateOut, (spawn - centre).normalized), 0.7f, $"gate faces {spawn}");
            }
        }

        [Test]
        public void Shops_StandInsideTheWall_WithoutOverlapping()
        {
            var footprints = new List<Rect>();
            foreach (TownLayout.ShopPlot plot in TownLayout.Shops)
            {
                Vector2 a = plot.ToTown(new Vector2(-TownLayout.ShopWidth * 0.5f, -TownLayout.ShopDepth * 0.5f));
                Vector2 b = plot.ToTown(new Vector2(TownLayout.ShopWidth * 0.5f, TownLayout.ShopDepth * 0.5f));
                Rect r = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                Assert.LessOrEqual(Mathf.Max(Mathf.Abs(r.xMin), Mathf.Abs(r.xMax), Mathf.Abs(r.yMin), Mathf.Abs(r.yMax)), TownLayout.InnerHalf, $"{plot.Kind} inside");
                foreach (Rect other in footprints)
                {
                    Assert.IsFalse(r.Overlaps(other), $"{plot.Kind} overlaps another shop");
                }
                footprints.Add(r);
                // Its front faces the middle of the square.
                Vector2 front = TownLayout.Turn(Vector2.up, plot.Yaw);
                Assert.Greater(Vector2.Dot(front, -plot.Centre.normalized), 0.7f, $"{plot.Kind} faces in");
            }
        }

        [Test]
        public void Paths_RunFromTheGate_AndToEveryShop()
        {
            Assert.IsTrue(TownLayout.IsPath(new Vector2(0f, -TownLayout.InnerHalf)), "through the gate");
            Assert.IsTrue(TownLayout.IsPath(Vector2.zero), "the middle of the square");
            foreach (TownLayout.ShopPlot plot in TownLayout.Shops)
            {
                Assert.IsTrue(TownLayout.IsPath(plot.ToTown(new Vector2(0f, TownLayout.CounterZ + 1.6f))), $"to the {plot.Kind}");
            }
            Assert.IsFalse(TownLayout.IsPath(new Vector2(-12f, -14f)), "not all of the square is paved");
        }

        [Test]
        public void SiteSearch_OnFlatDryLand_PicksTheNearestSiteClearOfTheSpawn()
        {
            var request = new TownSiteRequest
            {
                Area = new Rect(-200f, -200f, 400f, 400f),
                Spawn = Vector2.zero,
                KeepFromSpawn = 60f,
                Footprint = TownLayout.Footprint,
                Step = 8f,
                Top = 64f,
            };
            TownPlacement site = TownSiteFinder.Find(request, new FlatGenerator(20f), null, WaterMap.None);
            Assert.IsTrue(site.Found);
            Assert.GreaterOrEqual(site.Centre.magnitude, 60f);
            Assert.Less(site.Centre.magnitude, 70f, "the nearest clear site wins when nothing else differs");
            Assert.AreEqual(20f, site.Ground, 0.01f);
            Assert.AreEqual(0f, site.Rise, 0.01f);
        }

        [Test]
        public void SiteSearch_IsTheSameEveryTime()
        {
            var request = new TownSiteRequest { Area = new Rect(-120f, -120f, 240f, 240f), Footprint = 40f, Top = 64f };
            TownPlacement a = TownSiteFinder.Find(request, new FlatGenerator(10f), null, WaterMap.None);
            TownPlacement b = TownSiteFinder.Find(request, new FlatGenerator(10f), null, WaterMap.None);
            Assert.AreEqual(a.Centre, b.Centre);
        }

        [Test]
        public void Leveling_RaisesTheGroundToTheTargetHeight()
        {
            var world = new World(new Vector3Int(16, 16, 16), 1f, samples => new ByteVoxelStorage(samples));
            var generator = new FlatGenerator(4.5f);
            for (int z = -1; z <= 1; z++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    world.Load(new Vector3Int(x, 0, z), generator);
                }
            }
            GroundLeveling.Level(world, null, new GroundLeveling.Area
            {
                Centre = new Vector2(8f, 8f),
                Half = new Vector2(4f, 4f),
                Height = 7.5f,
                Ramp = 4f,
                Natural = (x, z) => 4.5f,
                Headroom = 3f,
                Fill = 2f,
            }, 0.5f);
            Assert.AreEqual(7.5f, GroundLeveling.GroundHeight(world, null, new Vector3(8f, 0f, 8f), 0.5f, 1), 0.1f, "flat in the middle");
            Assert.AreEqual(4.5f, GroundLeveling.GroundHeight(world, null, new Vector3(8f, 0f, 20f), 0.5f, 1), 0.1f, "untouched past the ramp");
            float halfway = GroundLeveling.GroundHeight(world, null, new Vector3(8f, 0f, 14f), 0.5f, 1);
            Assert.That(halfway, Is.InRange(4.6f, 7.4f), "ramped between");
        }
    }
}
