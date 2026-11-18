using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>GL753: the way down to a dock is a road on the ground, ending on the shore where the pier starts.</summary>
    public class DockRoadTests
    {
        // Land west of x = 100, the sea east of it.
        private static RoadWater Sea(float x, float z)
        {
            return x >= 100f ? RoadWater.Sea : RoadWater.Dry;
        }

        private static float Low(float x, float z)
        {
            return Mathf.Max(0f, (100f - x) * 0.05f);
        }

        private static WorldPlan PlanWith(params Vector2[] points)
        {
            var plan = new WorldPlan { Rules = new WorldPlanRules() };
            plan.Settlements.Add(new PlannedSettlement { Index = 0, Name = "Port" });
            plan.Roads.Add(new PlannedRoad { Index = 0, Class = RoadClass.DirtCartPath, Length = 300f, Points = new List<Vector2> { new Vector2(-200f, 0f), new Vector2(0f, 0f) } });
            plan.Roads.Add(new PlannedRoad { Index = 1, Class = RoadClass.Footpath, ToDock = true, Points = points.ToList(), Length = 40f });
            return plan;
        }

        [Test]
        public void ARoadEndingInland_GoesOnToAFewMetresShortOfTheSea()
        {
            WorldPlan plan = PlanWith(new Vector2(0f, 0f), new Vector2(60f, 0f));

            DockLandings.Land(plan, Low, Sea);

            Vector2 end = plan.Roads[1].Points.Last();
            Assert.AreEqual(3, plan.Roads[1].Points.Count);
            Assert.AreEqual(100f - DockLandings.Inland, end.x, 1.5f);
            Assert.AreEqual(RoadWater.Dry, Sea(end.x, end.y));
        }

        [Test]
        public void ARoadEndingInTheShallows_ComesBackOntoTheLand()
        {
            WorldPlan plan = PlanWith(new Vector2(0f, 0f), new Vector2(112f, 0f));

            DockLandings.Land(plan, Low, Sea);

            Assert.AreEqual(2, plan.Roads[1].Points.Count, "the wet end is replaced");
            Assert.AreEqual(100f - DockLandings.Inland, plan.Roads[1].Points.Last().x, 1.5f);
        }

        [Test]
        public void ALanding_PrefersALowShoreToANearerCliff()
        {
            // A cliff along the nearest stretch of shore (z under 30); the land comes down to the sea beyond it.
            float Cliff(float x, float z) => z < 30f && x < 100f ? 20f : 0f;

            Assert.IsTrue(DockLandings.TryLanding(new Vector2(80f, 0f), Cliff, Sea, null, out Vector2 landing));

            Assert.GreaterOrEqual(landing.y, 30f, "down at the low shore");
        }

        [Test]
        public void ARoadWithNoShoreInReach_IsTakenOffThePlan()
        {
            WorldPlan plan = PlanWith(new Vector2(-900f, 0f), new Vector2(-800f, 0f));

            DockLandings.Land(plan, Low, Sea);

            Assert.AreEqual(1, plan.Roads.Count);
            Assert.IsTrue(plan.Notes.Any(note => note.Contains("no dock")));
        }

        [Test]
        public void ARiverInTheWay_IsNoShore()
        {
            // A river all round the road's end, the sea beyond it.
            RoadWater Moat(float x, float z)
            {
                float distance = new Vector2(x, z).magnitude;
                return distance < 20f ? RoadWater.Dry : distance < 30f ? RoadWater.River : RoadWater.Sea;
            }

            Assert.IsFalse(DockLandings.TryLanding(Vector2.zero, Low, Moat, null, out _));
        }

        private static WorldPlan Graded(float steep, float length)
        {
            WorldPlan plan = PlanWith(new Vector2(0f, 0f), new Vector2(96f, 0f));
            plan.Settlements[0].Type = SettlementType.Defaults().First(type => type.Id == "village");
            plan.Roads[1].SteepLength = steep;
            plan.Roads[1].Length = length;
            return plan;
        }

        [Test]
        public void ARoadEndingLowAndGentle_KeepsItsDock()
        {
            WorldPlan plan = Graded(0f, 200f);

            DockLandings.Prune(plan, Low, 0f);

            Assert.AreEqual(2, plan.Roads.Count);
        }

        [Test]
        public void ARoadEndingOnACliff_LosesItsDock_ThePiersRampCouldNotReach()
        {
            WorldPlan plan = Graded(0f, 200f);

            DockLandings.Prune(plan, (x, z) => 12f, 0f);

            Assert.AreEqual(1, plan.Roads.Count);
            Assert.IsTrue(plan.Notes.Any(note => note.Contains("cliff")));
        }

        [Test]
        public void ARoadSteeperThanItsClassForLong_LosesItsDock_ButAShortPitchIsAllowed()
        {
            WorldPlan steep = Graded(40f, 200f), pitch = Graded(10f, 200f);

            DockLandings.Prune(steep, Low, 0f);
            DockLandings.Prune(pitch, Low, 0f);

            Assert.AreEqual(1, steep.Roads.Count);
            Assert.AreEqual(2, pitch.Roads.Count);
        }

        [Test]
        public void OnADryMap_TheRoadsStayAsPlanned()
        {
            WorldPlan plan = PlanWith(new Vector2(0f, 0f), new Vector2(60f, 0f));

            DockLandings.Land(plan, Low, null);

            Assert.AreEqual(new Vector2(60f, 0f), plan.Roads[1].Points.Last());
        }

        [Test]
        public void NoCartRunsDownToADock()
        {
            WorldPlan plan = PlanWith(new Vector2(0f, 0f), new Vector2(400f, 0f));
            plan.Roads[1].Class = RoadClass.PavedCartPath;
            plan.Roads[1].Length = 400f;

            Assert.IsFalse(CartRuns.Plan(plan, CartKind.Defaults(), 3).Any(run => run.Road == 1));
        }

        [Test]
        public void EachDockRow_NamesTheRoadThatFitsTheTown()
        {
            List<DockDesign> rows = DockDesign.Defaults();

            Assert.AreEqual(RoadClass.Footpath, rows.First(row => row.Id == "foot-dock").Road);
            Assert.AreEqual(RoadClass.DirtCartPath, rows.First(row => row.Id == "pier").Road);
            Assert.AreEqual(RoadClass.PavedCartPath, rows.First(row => row.Id == "grand-docks").Road);
        }
    }
}
