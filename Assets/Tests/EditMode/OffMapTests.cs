using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>GL725: trade routes running on past the map's edge, and how present a traveller on one is.</summary>
    public class OffMapTests
    {
        // A map whose free ground ends at x = 100, whose limit closes from there to its border at x = 160.
        private static OffMapPath LayEast(Vector2 end, Vector2 along)
        {
            return OffMap.Lay(end, along, p => p.x < 100f, p => Mathf.Clamp01((p.x - 100f) / 60f), p => Vector2.right);
        }

        [Test]
        public void APath_LeavesTheFreeGround_PassesTheBorder_AndRunsAFadePastIt()
        {
            OffMapPath path = LayEast(new Vector2(50f, 0f), Vector2.right);

            Assert.AreEqual(new Vector2(50f, 0f), path.Points[0]);
            Assert.AreEqual(50f, path.Leaves, OffMap.Step, "leaves at x = 100");
            Assert.AreEqual(110f, path.Border, OffMap.Step, "passes the border at x = 160");
            Assert.GreaterOrEqual(path.Length, path.Border + OffMap.FadeLength);
            Assert.Less(path.Length, path.Border + OffMap.FadeLength + OffMap.Step * 1.01f);
            Assert.Greater(path.Points[path.Points.Count - 1].x, 160f + OffMap.FadeLength - 1f);
        }

        [Test]
        public void APathHeadingAlongTheEdge_TurnsOutOfTheMap()
        {
            OffMapPath path = LayEast(new Vector2(90f, 0f), Vector2.up);

            Vector2 last = path.Points[path.Points.Count - 1] - path.Points[path.Points.Count - 2];
            Assert.Greater(last.normalized.x, 0.95f, "it ends heading straight out");
            Assert.Greater(path.Points[1].y, path.Points[0].y, "it sets off the way the road went");
        }

        [Test]
        public void ATraveller_CanBeDealtWithOnTheFreeGround_AndFadesOutPastTheBorder()
        {
            OffMapPath path = LayEast(new Vector2(50f, 0f), Vector2.right);

            Assert.IsTrue(OffMap.Interactable(path, -20f), "still on its road");
            Assert.IsTrue(OffMap.Interactable(path, path.Leaves - 1f));
            Assert.IsFalse(OffMap.Interactable(path, path.Leaves + 1f));
            Assert.AreEqual(1f, OffMap.Fade(path, path.Border - 1f));
            Assert.AreEqual(0.5f, OffMap.Fade(path, path.Border + OffMap.FadeLength * 0.5f), 1e-4f);
            Assert.AreEqual(0f, OffMap.Fade(path, path.Length));
            Assert.IsTrue(OffMap.Interactable(null, 1e6f) && OffMap.Fade(null, 1e6f) == 1f, "a route on the map is always here");
        }

        [Test]
        public void AnEdgeWithNoBorderInReach_StillEnds()
        {
            OffMapPath path = OffMap.Lay(Vector2.zero, Vector2.right, p => p.x < 10f, p => 0f, p => Vector2.right);

            Assert.AreEqual(OffMap.Longest, path.Border, OffMap.Step);
            Assert.LessOrEqual(path.Leaves, path.Border);
            Assert.AreEqual(path.Border + OffMap.FadeLength, path.Length, OffMap.Step);
        }

        [Test]
        public void ARoadOffTheMap_TimesItsCartsForTheWayOn_AndOtherRoadsAreUnchanged()
        {
            var plan = new WorldPlan();
            plan.Roads.Add(new PlannedRoad { Index = 0, Class = RoadClass.DirtCartPath, From = 0, To = 1, Length = 400f, Points = new List<Vector2> { Vector2.zero, Vector2.right * 400f } });
            plan.Roads.Add(new PlannedRoad
            {
                Index = 1, Class = RoadClass.DirtCartPath, From = 0, Length = 300f, LeavesMap = true, ExitSide = MapSide.East,
                Points = new List<Vector2> { Vector2.zero, Vector2.up * 300f },
            });

            List<CartRun> runs = CartRuns.Plan(plan, CartKind.Defaults(), 5, road => 200f);

            CartRun onMap = runs.Single(r => r.Road == 0), off = runs.Single(r => r.Road == 1);
            Assert.AreEqual(400f, onMap.Length);
            Assert.IsFalse(onMap.LeavesMap);
            Assert.AreEqual(500f, off.Length);
            Assert.IsTrue(off.LeavesMap);
            Assert.AreEqual(0.4f, off.OffMapShare, 1e-4f);
        }

        [Test]
        public void AVesselToTheOpenSea_IsTimedForTheWayOn()
        {
            var plan = new WorldPlan();
            plan.WaterRoutes.Add(new WaterRoute { Kind = WaterRouteKind.Sea, From = 0, To = 1, Length = 500f, Points = new List<Vector2> { Vector2.zero, Vector2.right * 500f } });
            plan.WaterRoutes.Add(new WaterRoute { Kind = WaterRouteKind.Sea, From = 0, To = -1, Length = 300f, Points = new List<Vector2> { Vector2.zero, Vector2.up * 300f } });

            List<BoatRun> runs = BoatRuns.Plan(plan, VesselClass.Defaults(), 5, route => 250f);

            Assert.IsTrue(runs.Where(r => r.Route == 0).All(r => r.Length == 500f));
            Assert.IsTrue(runs.Where(r => r.Route == 1).All(r => r.Length == 550f));
            Assert.IsTrue(runs.Any(r => r.Route == 1));
        }

        [Test]
        public void AtAnEnd_TheTimetableSaysHowLongItHasStood()
        {
            var run = new CartRun { Kind = new CartKind { Id = "test", Speed = 2f, RestSeconds = 50f }, Length = 400f };

            Assert.AreEqual(0f, run.At(100d).Rested);
            Assert.AreEqual(20f, run.At(220d).Rested, 1e-3f);
            Assert.AreEqual(30f, run.At(480d).Rested, 1e-3f);
        }
    }
}
