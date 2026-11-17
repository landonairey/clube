using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>GL728: a horse-hire stand by the name board outside the capital and each city.</summary>
    public class HorseHireTests
    {
        private static SettlementType TypeOf(string id)
        {
            return SettlementType.Defaults().First(t => t.Id == id);
        }

        [Test]
        public void OnlyTheCapitalAndCities_KeepHorses()
        {
            CollectionAssert.AreEquivalent(new[] { "city", "capital" }, SettlementType.Defaults().Where(t => t.HorseHire).Select(t => t.Id));
        }

        [Test]
        public void EachHorseTown_GetsOneStand_ByItsFirstEdgePost_AwayFromTheRoad()
        {
            var plan = new WorldPlan();
            plan.Settlements.Add(new PlannedSettlement { Name = "Kingsport", Type = TypeOf("capital") });
            plan.Settlements.Add(new PlannedSettlement { Name = "Hamlet", Type = TypeOf("village") });
            plan.Settlements.Add(new PlannedSettlement { Name = "Towerby", Type = TypeOf("city") });
            plan.Signposts.Add(new PlannedSignpost { AtSettlement = false, Position = new Vector2(5f, 5f) });
            plan.Signposts.Add(new PlannedSignpost { AtSettlement = true, Settlement = 2, Position = new Vector2(100f, 0f), Outward = Vector2.right, Roadside = Vector2.up });
            plan.Signposts.Add(new PlannedSignpost { AtSettlement = true, Settlement = 1, Position = new Vector2(50f, 0f), Outward = Vector2.right, Roadside = Vector2.up });
            plan.Signposts.Add(new PlannedSignpost { AtSettlement = true, Settlement = 0, Position = new Vector2(0f, 0f), Outward = Vector2.up, Roadside = Vector2.right });
            plan.Signposts.Add(new PlannedSignpost { AtSettlement = true, Settlement = 2, Position = new Vector2(300f, 0f), Outward = Vector2.left, Roadside = Vector2.up });

            List<HorseStand> stands = HorseHire.Plan(plan);

            CollectionAssert.AreEqual(new[] { 0, 2 }, stands.Select(s => s.Settlement), "the village keeps none, in settlement order");
            HorseStand city = stands[1];
            Assert.AreEqual(1, city.Signpost, "its first edge post");
            Assert.AreEqual(100f - HorseHire.Back, city.Post.x, 1e-4f, "back along the road, into the city");
            Assert.AreEqual(HorseHire.FromPost, city.Post.y, 1e-4f, "further from the road than the name board");
            Assert.Less(Vector2.Dot(city.Arrival - city.Post, city.Roadside), 0f, "riders arrive on the road's side");
        }

        [Test]
        public void ARide_CostsTheFare()
        {
            Assert.AreEqual(250, HorseHire.Fare);
            Assert.IsTrue(HorseHire.CanPay(250f));
            Assert.IsFalse(HorseHire.CanPay(249.5f));
        }
    }
}
