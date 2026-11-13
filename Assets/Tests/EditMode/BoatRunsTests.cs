using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// Boats on the water routes (GL618): which routes get which vessels, where one is by the clock, the lane found on the
    /// real water, and the hulls built from the class rows.
    /// </summary>
    public class BoatRunsTests
    {
        private static WorldPlan Plan()
        {
            var plan = new WorldPlan { Rules = new WorldPlanRules() };
            void Route(WaterRouteKind kind, float length, int from, int to, string vessel = null)
            {
                plan.WaterRoutes.Add(new WaterRoute
                {
                    Kind = kind, From = from, To = to, Length = length, Vessel = vessel, Cargo = vessel != null ? "iron ore" : null,
                    Points = new List<Vector2> { Vector2.zero, new Vector2(length, 0f) },
                });
            }
            Route(WaterRouteKind.Sea, 1500f, 0, 1);
            Route(WaterRouteKind.Sea, 2200f, 0, 2, "Edmund Fitzgerald");
            Route(WaterRouteKind.River, 600f, 3, 4);
            Route(WaterRouteKind.River, 40f, 4, 5);
            return plan;
        }

        [Test]
        public void SmallBoatsOnRivers_LargerVesselsAtSea_AndTheEdmundFitzgeraldOnItsRoute()
        {
            List<BoatRun> runs = BoatRuns.Plan(Plan(), VesselClass.Defaults(), 5);

            Assert.AreEqual(VesselClass.SeaTrader, runs.Single(r => r.Route == 0).Class.Id);
            BoatRun fitzgerald = runs.Single(r => r.Name == "Edmund Fitzgerald");
            Assert.AreEqual(1, fitzgerald.Route);
            Assert.AreEqual(VesselClass.OreFreighter, fitzgerald.Class.Id);
            Assert.AreEqual("iron ore", fitzgerald.Cargo);
            CollectionAssert.Contains(fitzgerald.CargoWords, "iron");
            Assert.AreEqual(2, runs.Count(r => r.Route == 1), "a trader sails the named vessel's route too");
            Assert.AreEqual(VesselClass.RiverBoat, runs.Single(r => r.Route == 2).Class.Id);
            Assert.AreEqual(0, runs.Count(r => r.Route == 3), "too short to be worth a boat");
            Assert.IsFalse(runs.Any(r => r.Class.Raider), "thieves' boats sail from their camps, not on the routes");
            Assert.Greater(fitzgerald.Class.Length, runs.Single(r => r.Route == 0).Class.Length);
            Assert.Greater(runs.Single(r => r.Route == 0).Class.Length, runs.Single(r => r.Route == 2).Class.Length);
            CollectionAssert.AreEqual(Enumerable.Range(0, runs.Count), runs.Select(r => r.Index));
        }

        [Test]
        public void TheSamePlanAndSeed_GiveTheSameVessels()
        {
            List<BoatRun> a = BoatRuns.Plan(Plan(), VesselClass.Defaults(), 5), b = BoatRuns.Plan(Plan(), VesselClass.Defaults(), 5);
            CollectionAssert.AreEqual(a.Select(r => (r.Route, r.Class.Id, r.Name, r.Offset)), b.Select(r => (r.Route, r.Class.Id, r.Name, r.Offset)));
        }

        [Test]
        public void AVessel_SailsOut_Lies_SailsBack_ByTheClockAlone()
        {
            BoatRun run = BoatRuns.Plan(Plan(), VesselClass.Defaults(), 5).First(r => r.Route == 0);
            run.Offset = 0f;

            RunPlace half = run.At(run.OneWay * 0.5f);
            Assert.AreEqual(0.5f, half.Fraction, 1e-3f);
            Assert.IsTrue(half.Outbound && half.Moving);
            RunPlace lying = run.At(run.OneWay + run.Class.RestSeconds * 0.5f);
            Assert.AreEqual(1f, lying.Fraction, 1e-4f);
            Assert.IsFalse(lying.Moving);
            RunPlace back = run.At(run.OneWay * 1.25f + run.Class.RestSeconds);
            Assert.AreEqual(0.75f, back.Fraction, 1e-3f);
            Assert.IsFalse(back.Outbound);
            Assert.AreEqual(half.Trip + 1, back.Trip);
            Assert.AreEqual(run.At(100.0).Fraction, run.At(100.0 + run.Round * 3).Fraction, 1e-3f);
        }

        // A river 8 m wide winding either side of the straight line the plan drew.
        private static bool Winding(float x, float z)
        {
            return Mathf.Abs(z - 30f * Mathf.Sin(x / 50f)) < 4f && x > -10f && x < 610f;
        }

        [Test]
        public void ALane_FollowsTheWindingWater_WhereThePlannedLineCutsAcrossTheLand()
        {
            var planned = new List<Vector2>();
            for (int i = 0; i <= 15; i++)
            {
                planned.Add(new Vector2(i * 40f, 0f));
            }

            List<Vector2> lane = WaterLanes.Find(planned, Winding, 2f, 70f, out bool whole);

            Assert.IsTrue(whole);
            Assert.Less(Vector2.Distance(lane[0], planned[0]), 6f);
            Assert.Less(Vector2.Distance(lane[lane.Count - 1], planned[15]), 20f, "the water nearest the planned end");
            Assert.Greater(WaterLanes.Length(lane), 640f, "longer than the straight line: it winds");
            float length = WaterLanes.Length(lane);
            for (float d = 0f; d <= length; d += 1f)
            {
                Vector2 at = WaterLanes.Along(lane, d);
                Assert.IsTrue(Winding(at.x, at.y), $"dry at {at}, {d} m along");
                Assert.Less(Mathf.Abs(at.y - 30f * Mathf.Sin(at.x / 50f)), 3f, "keeps off the banks");
            }
        }

        [Test]
        public void ALane_StopsWhereTheWaterDoes_AndThereIsNoneWithoutWaterAtTheStart()
        {
            var planned = new List<Vector2> { new Vector2(0f, 0f), new Vector2(600f, 0f) };
            // A dam across the river at x = 300.
            List<Vector2> lane = WaterLanes.Find(planned, (x, z) => Winding(x, z) && Mathf.Abs(x - 300f) > 6f, 2f, 70f, out bool whole);

            Assert.IsFalse(whole);
            Assert.Less(lane[lane.Count - 1].x, 300f);
            Assert.Greater(lane[lane.Count - 1].x, 270f, "it goes as far as it can");
            Assert.IsNull(WaterLanes.Find(planned, (x, z) => false, 2f, 70f, out _));
        }

        [Test]
        public void ASeaLane_GoesRoundAnIsland()
        {
            bool Sea(float x, float z) => new Vector2(x - 400f, z).magnitude > 60f;
            var planned = new List<Vector2> { new Vector2(0f, 0f), new Vector2(400f, 40f), new Vector2(800f, 0f) };

            List<Vector2> lane = WaterLanes.Find(planned, Sea, 8f, 100f, out bool whole);

            Assert.IsTrue(whole);
            float length = WaterLanes.Length(lane);
            for (float d = 0f; d <= length; d += 2f)
            {
                Vector2 at = WaterLanes.Along(lane, d);
                Assert.IsTrue(Sea(at.x, at.y), $"aground at {at}");
            }
        }

        [Test]
        public void EveryClass_BuildsAHullOfItsOwnMeasures_WithRoomForItsHandsAndCrates()
        {
            foreach (VesselClass vessel in VesselClass.Defaults())
            {
                Mesh mesh = BoatMeshes.Build(vessel);
                try
                {
                    Assert.Greater(mesh.vertexCount, 100, vessel.Id);
                    Bounds bounds = mesh.bounds;
                    Assert.AreEqual(vessel.Length, bounds.size.z, vessel.Length * 0.25f, vessel.Id + " length");
                    Assert.AreEqual(-vessel.Draught, bounds.min.y, 0.05f + vessel.Draught * 0.6f, vessel.Id + " draught");
                    if (!vessel.Oars)
                    {
                        Assert.AreEqual(vessel.Beam, bounds.size.x, vessel.Beam * 0.6f, vessel.Id + " beam");
                    }
                    Assert.AreEqual(vessel.Crew, BoatMeshes.CrewSpots(vessel).Count);
                    List<Vector3> crates = BoatMeshes.CrateSpots(vessel, 0.8f);
                    Assert.AreEqual(vessel.Crates, crates.Count);
                    foreach (Vector3 spot in crates.Concat(BoatMeshes.CrewSpots(vessel)))
                    {
                        float along = spot.z / vessel.Length + 0.5f;
                        Assert.That(along, Is.InRange(0.05f, 0.95f), vessel.Id);
                        Assert.LessOrEqual(Mathf.Abs(spot.x), BoatMeshes.HalfBeam(vessel, along), vessel.Id + " is overboard");
                    }
                    Assert.Less(BoatMeshes.DeckHeight(vessel), vessel.Freeboard, vessel.Id + ": the bulwark stands over the deck");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
            }
        }

        [Test]
        public void ASeaVessel_NeedsDeepSeaAndRoom_ARiverBoatAnyWater()
        {
            Assert.AreEqual(WaterRouteKind.Sea, VesselClass.Find(VesselClass.Defaults(), VesselClass.OreFreighter).Kind);
            Func<float, float, bool> nowhere = WaterLanes.Floats(WaterMap.None, VesselClass.Defaults()[0]);
            Assert.IsFalse(nowhere(0f, 0f));
        }
    }
}
