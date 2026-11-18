using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>What building a road costs, and how the road planning chooses by it (GL737): path, grading, bridge, tunnel.</summary>
    public class RoadBuildCostTests
    {
        // Level ground at 20 m in the west and 25 m in the east: between zFrom and zTo a bank (a ledge's edge) across x = 500,
        // elsewhere a gentle rise over 200 m.
        private static System.Func<float, float, float> Bank(float zFrom, float zTo)
        {
            return (x, z) => z < zFrom || z > zTo
                ? 20f + 5f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(400f, 600f, x))
                : 20f + 5f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(498f, 502f, x));
        }

        private static PlannedRoad Straight(RoadClass roadClass, float step)
        {
            var road = new PlannedRoad { Index = 0, Class = roadClass, From = 0, To = 1, Refined = true };
            for (float x = 100f; x <= 900f + 0.01f; x += step)
            {
                road.Points.Add(new Vector2(x, 300f));
            }
            return road;
        }

        private static WorldPlan PlanAcross(RoadClass roadClass)
        {
            var plan = new WorldPlan { Seed = 3, Area = new Rect(0f, 0f, 1000f, 600f), Rules = new WorldPlanRules() };
            SettlementType village = plan.Rules.Types.Find(t => t.Id == "village");
            plan.Settlements.Add(new PlannedSettlement { Index = 0, Type = village, Position = new Vector2(100f, 300f), Name = "West" });
            plan.Settlements.Add(new PlannedSettlement { Index = 1, Type = village, Position = new Vector2(900f, 300f), Name = "East" });
            plan.Roads.Add(Straight(roadClass, 40f));
            plan.Roads[0].Refined = false;
            return plan;
        }

        [Test]
        public void ThePrices_AreByTheVoxelOfRoad_PathGradeBridgeTunnel()
        {
            var costs = new RoadBuildCosts();
            Assert.AreEqual(1f, costs.PriceOf(RoadWork.Path));
            Assert.AreEqual(2f, costs.PriceOf(RoadWork.Grade));
            Assert.AreEqual(3f, costs.PriceOf(RoadWork.Bridge));
            Assert.AreEqual(3f, costs.PriceOf(RoadWork.Tunnel));
            Assert.AreEqual(0.25f, costs.VoxelSize);
            Assert.AreEqual(4f, costs.Cost(RoadWork.Path, 1f), 1e-5f, "a metre is four voxels of road");
            Assert.AreEqual(12f, costs.Cost(RoadWork.Bridge, 1f), 1e-5f);
            Assert.AreEqual(2f, costs.Relative(RoadWork.Grade), 1e-5f, "a metre graded costs two of plain path");

            var works = new RoadWorks { Path = 100f, Grade = 10f, Bridge = 5f, Tunnel = 2f };
            Assert.AreEqual(117f, works.Total, 1e-4f);
            Assert.AreEqual(400f + 80f + 60f + 24f, works.CostAt(costs), 1e-3f);
            works[RoadWork.Grade] += 1f;
            Assert.AreEqual(11f, works.Grade);
            // The prices are data: a dearer tunnel makes the same road dearer.
            costs.Tunnel = 10f;
            Assert.AreEqual(400f + 88f + 60f + 80f, works.CostAt(costs), 1e-3f);
        }

        [Test]
        public void AStretch_IsLaid_Graded_OrSteep_AndGradingNeverUndercutsAGentlePath()
        {
            var costs = new RoadBuildCosts();
            RoadClassDefinition dirt = new WorldPlanRules().Road(RoadClass.DirtCartPath);
            float limit = dirt.MaxSlope;

            Assert.AreEqual(dirt.StretchCost(8f, 0.05f), costs.StretchCost(dirt, 8f, 0.05f, 0.05f), 1e-4f, "within the limit: laid as it lies");
            float graded = costs.StretchCost(dirt, 8f, limit * 1.5f, limit * 0.5f);
            Assert.AreEqual(2f * dirt.StretchCost(8f, limit), graded, 1e-3f, "over it, but gradeable: at the grading price for the limit");
            Assert.Greater(graded, costs.StretchCost(dirt, 8f, limit * 0.99f, 0f), "grading costs more than any path within the limit");
            Assert.AreEqual(dirt.StretchCost(8f, limit * 3f), costs.StretchCost(dirt, 8f, limit * 3f, float.MaxValue), 1e-3f, "not gradeable: steep, and dear");
            Assert.Greater(costs.StretchCost(dirt, 8f, limit * 3f, float.MaxValue), graded);
        }

        [Test]
        public void TheBed_CutsAndFillsALedge_ButNotATallWall()
        {
            var along = new List<float>();
            var ledge = new List<float>();
            var wall = new List<float>();
            for (int i = 0; i <= 60; i++)
            {
                float x = i * 2f;
                along.Add(x);
                ledge.Add(x < 60f ? 20f : 24f);
                wall.Add(x < 60f ? 20f : 50f);
            }
            float limit = 0.2f;
            float[] bed = RoadGrading.Bed(ledge.ToArray(), along, null, limit, 3f);
            for (int i = 1; i < bed.Length; i++)
            {
                Assert.LessOrEqual(Mathf.Abs(bed[i] - bed[i - 1]) / 2f, limit + 1e-4f, $"even at {along[i]}");
                Assert.LessOrEqual(Mathf.Abs(bed[i] - ledge[i]), 3f + 1e-4f);
            }
            Assert.AreEqual(20f, bed[0], 1e-4f, "far from the ledge it is the ground");
            Assert.AreEqual(24f, bed[bed.Length - 1], 1e-4f);
            Assert.Greater(bed[29], 20.5f, "built up below the edge");
            Assert.Less(bed[30], 23.5f, "cut down above it");

            float[] cut = RoadGrading.Bed(wall.ToArray(), along, null, limit, 3f);
            float steepest = 0f;
            for (int i = 1; i < cut.Length; i++)
            {
                steepest = Mathf.Max(steepest, Mathf.Abs(cut[i] - cut[i - 1]) / 2f);
                Assert.LessOrEqual(Mathf.Abs(cut[i] - wall[i]), 3f + 1e-4f, "never deeper than grading goes");
            }
            Assert.Greater(steepest, limit * 5f, "a 30 m wall stays a wall");

            // Over water the bed is left to the bridge.
            var wet = new bool[ledge.Count];
            for (int i = 25; i <= 35; i++)
            {
                wet[i] = true;
            }
            float[] bridged = RoadGrading.Bed(ledge.ToArray(), along, wet, limit, 3f);
            Assert.AreEqual(ledge[30], bridged[30]);
        }

        [Test]
        public void AGradedRoad_CountsItsWorks_MeasuresItsBed_AndIsBuiltOnIt()
        {
            System.Func<float, float, float> ground = Bank(0f, 600f);
            var costs = new RoadBuildCosts();
            RoadClassDefinition dirt = new WorldPlanRules().Road(RoadClass.DirtCartPath);
            PlannedRoad road = Straight(RoadClass.DirtCartPath, 8f);

            RoadGrading.Grade(road, dirt, costs, ground);

            Assert.Greater(road.Works.Grade, 10f, "the bank is graded");
            Assert.Less(road.Works.Grade, 80f, "and only round the bank");
            Assert.AreEqual(800f, road.Works.Total, 0.5f, "every metre is made one way");
            Assert.AreEqual(0f, road.Works.Bridge);
            Assert.AreEqual(0f, road.Works.Tunnel);
            Assert.AreEqual(road.Works.CostAt(costs), road.BuildCost, 1e-2f);
            Assert.Greater(road.BuildCost, costs.Cost(RoadWork.Path, 800f), "grading costs more than a plain road would");
            Assert.AreEqual(0f, road.SteepLength, "on its bed it keeps within its limit");
            Assert.LessOrEqual(road.SteepestSlope, dirt.MaxSlope);
            Assert.AreEqual(1, road.Grades.Count);

            // It is built on the graded bed; beside it the ground is the ground.
            System.Func<float, float, float> built = RoadGrading.HeightAt(road, ground);
            Assert.Greater(built(496f, 300f) - ground(496f, 300f), 1f, "built up below the bank");
            Assert.AreEqual(ground(496f, 320f), built(496f, 320f), 1e-4f);
            Assert.AreEqual(ground(200f, 300f), built(200f, 300f), 1e-4f);
            float[] many = RoadGrading.HeightsAt(road, points => points.Select(p => ground(p.x, p.y)).ToArray())(new[] { new Vector2(496f, 300f) });
            Assert.AreEqual(built(496f, 300f), many[0], 1e-4f);

            // Without the depth to grade it, the bank stays steep.
            PlannedRoad shallow = Straight(RoadClass.DirtCartPath, 8f);
            RoadGrading.Grade(shallow, dirt, new RoadBuildCosts { GradeDepth = 0.5f }, ground);
            Assert.Greater(shallow.SteepLength, 0f);

            // A footpath is not graded: it follows the ground and is laid as path all the way.
            RoadClassDefinition foot = new WorldPlanRules().Road(RoadClass.Footpath);
            foot.MaxCut = 0f;
            PlannedRoad path = Straight(RoadClass.Footpath, 8f);
            RoadGrading.Grade(path, foot, costs, ground);
            Assert.AreEqual(0f, path.Works.Grade);
            Assert.IsEmpty(path.Grades);
        }

        [Test]
        public void TheSearch_GoesRoundABankWhereThatIsCheaper_AndGradesOneWithNoWayRound()
        {
            // A short bank, within the search's corridor: going round its end is a few metres longer, cheaper than grading it.
            WorldPlan around = PlanAcross(RoadClass.DirtCartPath);
            WorldRoads.Finish(around, Bank(260f, 340f), default);
            PlannedRoad roundIt = around.Roads[0];
            Assert.IsTrue(roundIt.Refined);
            Assert.AreEqual(0f, roundIt.Works.Grade, "it went round");
            Assert.IsTrue(roundIt.Points.Any(p => Mathf.Abs(p.x - 500f) < 10f && (p.y < 260f || p.y > 340f)), "past the bank's end");

            // A bank right across the map: graded, and within its limit.
            WorldPlan across = PlanAcross(RoadClass.DirtCartPath);
            WorldRoads.Finish(across, Bank(-100f, 700f), default);
            PlannedRoad over = across.Roads[0];
            Assert.Greater(over.Works.Grade, 0f, "graded where there is no way round");
            Assert.AreEqual(0f, over.SteepLength);
            Assert.Greater(over.BuildCost, roundIt.BuildCost);

            // When grading is dear enough, a long way round is taken instead.
            WorldPlan dear = PlanAcross(RoadClass.DirtCartPath);
            dear.Rules.BuildCosts.Grade = 60f;
            WorldRoads.Finish(dear, Bank(260f, 340f), default);
            Assert.AreEqual(0f, dear.Roads[0].Works.Grade);
        }

        [Test]
        public void ATunnel_IsBoredOnlyWhereItCostsLessThanTheRoadOverTheRange()
        {
            // A ridge 70 m high along z at x = 0, and a paved road straight over it.
            float Ridge(float x, float z) => 20f + 70f * Mathf.Exp(-(x / 90f) * (x / 90f));
            WorldPlan Plan(float tunnelPrice)
            {
                var plan = new WorldPlan { Rules = new WorldPlanRules(), Area = new Rect(-600f, -600f, 1200f, 1200f) };
                plan.Rules.BuildCosts.Tunnel = tunnelPrice;
                SettlementType town = plan.Rules.Types.Find(t => t.Id == "town");
                plan.Settlements.Add(new PlannedSettlement { Index = 0, Type = town, Position = new Vector2(-400f, 0f), Name = "West" });
                plan.Settlements.Add(new PlannedSettlement { Index = 1, Type = town, Position = new Vector2(400f, 0f), Name = "East" });
                var road = new PlannedRoad { Index = 0, Class = RoadClass.PavedCartPath, From = 0, To = 1, Refined = true, SteepLength = 300f };
                for (float x = -400f; x <= 400f; x += 8f)
                {
                    road.Points.Add(new Vector2(x, 0f));
                }
                plan.Roads.Add(road);
                return plan;
            }

            Assert.AreEqual(1, RoadTunnels.Bore(Plan(3f), Ridge), "at the default price the bore is worth it");
            Assert.AreEqual(0, RoadTunnels.Bore(Plan(10000f), Ridge), "at a price past the road's own cost it is not");
        }
    }
}
