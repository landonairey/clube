using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>Carts on the cart paths (GL616): which roads get which carts, and where a cart is by the clock.</summary>
    public class CartRunsTests
    {
        private static WorldPlan Plan()
        {
            var plan = new WorldPlan();
            void Road(RoadClass roadClass, float length, int from, int to)
            {
                plan.Roads.Add(new PlannedRoad
                {
                    Index = plan.Roads.Count, Class = roadClass, From = from, To = to, Length = length,
                    Points = new List<Vector2> { Vector2.zero, new Vector2(length, 0f) },
                });
            }
            Road(RoadClass.PavedCartPath, 900f, 0, 1);
            Road(RoadClass.DirtCartPath, 500f, 2, -1);
            Road(RoadClass.DirtCartPath, 400f, 3, 0);
            Road(RoadClass.DirtCartPath, 30f, 4, 0);
            Road(RoadClass.Footpath, 700f, 5, 0);
            return plan;
        }

        [Test]
        public void APavedRoad_HasAHorseCartAndAHandCart_ADirtOneASingleCart_AFootpathNone()
        {
            List<CartRun> runs = CartRuns.Plan(Plan(), CartKind.Defaults(), 3);

            Assert.AreEqual(2, runs.Count(r => r.Road == 0));
            Assert.IsTrue(runs.Any(r => r.Road == 0 && r.Kind.HorseDrawn));
            Assert.IsTrue(runs.Any(r => r.Road == 0 && !r.Kind.HorseDrawn));
            Assert.AreEqual(1, runs.Count(r => r.Road == 1));
            Assert.AreEqual(1, runs.Count(r => r.Road == 2));
            Assert.AreEqual(0, runs.Count(r => r.Road == 3), "too short to be worth a trip");
            Assert.AreEqual(0, runs.Count(r => r.Road == 4), "no carts on a footpath");
            CollectionAssert.AreEqual(Enumerable.Range(0, runs.Count), runs.Select(r => r.Index));
        }

        [Test]
        public void TheSamePlanAndSeed_GiveTheSameCarts_AndBothKindsTurnUpOnDirtRoads()
        {
            List<CartRun> a = CartRuns.Plan(Plan(), CartKind.Defaults(), 3), b = CartRuns.Plan(Plan(), CartKind.Defaults(), 3);
            CollectionAssert.AreEqual(a.Select(r => (r.Road, r.Kind.Id, r.Offset)), b.Select(r => (r.Road, r.Kind.Id, r.Offset)));

            var drawn = new HashSet<string>();
            for (int seed = 0; seed < 40; seed++)
            {
                drawn.Add(CartRuns.Plan(Plan(), CartKind.Defaults(), seed).First(r => r.Road == 1).Kind.Id);
            }
            Assert.AreEqual(2, drawn.Count);
        }

        [Test]
        public void ACart_GoesOut_Stands_ComesBack_Stands_ByTheClockAlone()
        {
            var kind = new CartKind { Id = "test", Speed = 2f, RestSeconds = 50f };
            var run = new CartRun { Kind = kind, Length = 400f, Offset = 0f };
            Assert.AreEqual(200f, run.OneWay);
            Assert.AreEqual(500f, run.Round);

            CartPlace leaving = run.At(0d);
            Assert.AreEqual(0f, leaving.Fraction);
            Assert.IsTrue(leaving.Outbound && leaving.Moving);
            CartPlace halfway = run.At(100d);
            Assert.AreEqual(0.5f, halfway.Fraction, 1e-4f);
            Assert.AreEqual(0, halfway.Trip);
            CartPlace there = run.At(220d);
            Assert.AreEqual(1f, there.Fraction);
            Assert.IsTrue(there.Outbound);
            Assert.IsFalse(there.Moving);
            CartPlace back = run.At(300d);
            Assert.AreEqual(0.75f, back.Fraction, 1e-4f);
            Assert.IsFalse(back.Outbound);
            Assert.IsTrue(back.Moving);
            Assert.AreEqual(1, back.Trip, "another trip, another load");
            CartPlace home = run.At(470d);
            Assert.AreEqual(0f, home.Fraction);
            Assert.IsFalse(home.Moving);
            CartPlace again = run.At(500d + 100d);
            Assert.AreEqual(0.5f, again.Fraction, 1e-4f);
            Assert.AreEqual(2, again.Trip);
            Assert.AreEqual(run.At(100d + 40d * 500d).Fraction, halfway.Fraction, 1e-3f, "days later it is on the same timetable");
        }

        [Test]
        public void AnOffset_PutsACartElsewhereOnItsRound()
        {
            var kind = new CartKind { Id = "test", Speed = 2f, RestSeconds = 50f };
            var run = new CartRun { Kind = kind, Length = 400f, Offset = 100f };

            Assert.AreEqual(0.5f, run.At(0d).Fraction, 1e-4f);
        }
    }
}
