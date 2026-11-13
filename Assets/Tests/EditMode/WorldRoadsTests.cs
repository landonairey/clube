using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>Roads in the world (GL613, GL620, GL631-GL634): class data, routes on the real ground, beds, the writer, signposts.</summary>
    public class WorldRoadsTests
    {
        private readonly List<UnityEngine.Object> made = new List<UnityEngine.Object>();

        [TearDown]
        public void DestroyAll()
        {
            foreach (UnityEngine.Object thing in made)
            {
                UnityEngine.Object.DestroyImmediate(thing);
            }
            made.Clear();
        }

        // A plan by hand: settlements at points, roads through 40 m steps between them.
        private static WorldPlan Plan(params (string Type, Vector2 At)[] settlements)
        {
            var plan = new WorldPlan { Seed = 7, Area = new Rect(0f, 0f, 1000f, 600f), Rules = new WorldPlanRules() };
            foreach ((string type, Vector2 at) in settlements)
            {
                plan.Settlements.Add(new PlannedSettlement
                {
                    Index = plan.Settlements.Count, Type = plan.Rules.Types.Find(t => t.Id == type), Position = at,
                    Name = $"Place{plan.Settlements.Count} {type}",
                });
            }
            return plan;
        }

        private static PlannedRoad Road(WorldPlan plan, RoadClass roadClass, int from, int to, int joins, params Vector2[] through)
        {
            var road = new PlannedRoad { Index = plan.Roads.Count, Class = roadClass, From = from, To = to, Joins = joins };
            for (int i = 0; i < through.Length - 1; i++)
            {
                float length = Vector2.Distance(through[i], through[i + 1]);
                int steps = Mathf.Max(1, Mathf.RoundToInt(length / 40f));
                for (int k = i == 0 ? 0 : 1; k <= steps; k++)
                {
                    road.Points.Add(Vector2.Lerp(through[i], through[i + 1], k / (float)steps));
                }
            }
            plan.Roads.Add(road);
            return road;
        }

        // A cone 60 m high and 100 m across its foot's radius, at (400, 300), on level ground at 20 m.
        private static float Hill(float x, float z)
        {
            float d = Vector2.Distance(new Vector2(x, z), new Vector2(400f, 300f));
            return 20f + Mathf.Max(0f, 60f * (1f - d / 100f));
        }

        private static float DistanceTo(List<Vector2> line, Vector2 point)
        {
            float best = float.MaxValue;
            for (int i = 0; i < line.Count - 1; i++)
            {
                Vector2 a = line[i], ab = line[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                best = Mathf.Min(best, (a + ab * t - point).magnitude);
            }
            return best;
        }

        [Test]
        public void Classes_TakeTheirSlopesFromTheWalkableAngle_AndTheirSurfacesFromTheRule()
        {
            var rules = new WorldPlanRules();
            RoadClassDefinition foot = rules.Road(RoadClass.Footpath), dirt = rules.Road(RoadClass.DirtCartPath), paved = rules.Road(RoadClass.PavedCartPath);
            Assert.AreEqual(Mathf.Tan(50f * Mathf.Deg2Rad), foot.MaxSlope, 1e-4f, "a walking path may be as steep as the player can walk");
            Assert.Less(paved.MaxSlope, dirt.MaxSlope, "paved has the least tolerance");
            Assert.Less(dirt.MaxSlope, foot.MaxSlope * 0.5f, "dirt only slightly more than paved");
            Assert.Less(dirt.MaxSlope, paved.MaxSlope * 2f);
            rules.SetWalkableSlope(40f);
            Assert.AreEqual(Mathf.Tan(40f * Mathf.Deg2Rad), foot.MaxSlope, 1e-4f, "the limit follows the controller's");
            Assert.AreEqual(Mathf.Tan(40f * paved.SlopeShare * Mathf.Deg2Rad), paved.MaxSlope, 1e-4f);
            Assert.AreEqual("Dead Grass", foot.Surface);
            Assert.AreEqual("Dirt", dirt.Surface);
            Assert.AreEqual("Loose stone", paved.Surface, "cobbles: what the towns' paths are laid with");
            Assert.Less(foot.Width, dirt.Width);
            Assert.Less(dirt.Width, paved.Width);
            Assert.Greater(paved.StretchCost(10f, paved.MaxSlope * 1.2f), paved.StretchCost(10f, paved.MaxSlope * 0.9f) * 3f, "past the limit only as a last resort");
        }

        [Test]
        public void Refining_TakesACartPathRoundAHill_AndAFootpathMoreDirectly()
        {
            float Highest(RoadClass roadClass, out PlannedRoad road)
            {
                WorldPlan plan = Plan(("town", new Vector2(180f, 300f)), ("town", new Vector2(620f, 300f)));
                road = Road(plan, roadClass, 0, 1, -1, plan.Settlements[0].Position, plan.Settlements[1].Position);
                Assert.IsTrue(new RoadRefiner(plan.Area, Hill, null).Refine(plan, road));
                float highest = 0f;
                foreach (Vector2 p in road.Points)
                {
                    highest = Mathf.Max(highest, Hill(p.x, p.y));
                }
                return highest;
            }
            float pavedTop = Highest(RoadClass.PavedCartPath, out PlannedRoad paved);
            float footTop = Highest(RoadClass.Footpath, out PlannedRoad foot);
            Assert.Less(pavedTop, 20f + 60f * 0.5f, "the paved road keeps to the hill's lower half");
            Assert.LessOrEqual(foot.Length, paved.Length + 0.5f, "the footpath is no longer than the cart path");
            Assert.IsTrue(paved.Refined);
            Assert.AreEqual(new Vector2(180f, 300f), paved.Points[0]);
            Assert.AreEqual(new Vector2(620f, 300f), paved.Points[paved.Points.Count - 1]);
            for (int i = 1; i < paved.Points.Count - 1; i++)
            {
                Vector2 a = paved.Points[i] - paved.Points[i - 1], b = paved.Points[i + 1] - paved.Points[i];
                Assert.Less(a.magnitude, 13f, "points close enough to read as a curve");
                Assert.Less(Vector2.Angle(a, b), 50f, $"no sharp corner at point {i}");
                Assert.Less(Mathf.Abs(paved.Points[i].y - 300f), RoadRefiner.Corridor * 2.5f + 1f, "inside its corridor");
            }
        }

        [Test]
        public void Refining_BridgesARiverOnce_AndNeverCrossesTheSea()
        {
            RoadWater Water(float x, float z)
            {
                if (x >= 396f && x <= 404f)
                {
                    return RoadWater.River;
                }
                return x >= 560f && x <= 600f && z >= 270f && z <= 420f ? RoadWater.Sea : RoadWater.Dry;
            }
            WorldPlan plan = Plan(("town", new Vector2(180f, 300f)), ("town", new Vector2(780f, 300f)));
            PlannedRoad road = Road(plan, RoadClass.DirtCartPath, 0, 1, -1, plan.Settlements[0].Position, plan.Settlements[1].Position);
            Assert.IsTrue(new RoadRefiner(plan.Area, (x, z) => 20f, Water).Refine(plan, road));
            Assert.AreEqual(1, road.Bridges.Count, "one crossing of the river");
            Assert.That(road.Bridges[0].Position.x, Is.InRange(390f, 410f));
            Assert.That(road.Bridges[0].Length, Is.InRange(8f, 30f));
            for (int i = 0; i < road.Points.Count - 1; i++)
            {
                for (int k = 0; k <= 4; k++)
                {
                    Vector2 p = Vector2.Lerp(road.Points[i], road.Points[i + 1], k / 4f);
                    Assert.AreNotEqual(RoadWater.Sea, Water(p.x, p.y), $"in the sea at {p}");
                }
            }
        }

        [Test]
        public void Refining_KeepsTheNetworkJoined_AndRoadsThroughTheirSettlements()
        {
            // Road 0 runs from 0 through 2 to 1; road 1 comes down from 3 and joins road 0 between 2 and 1.
            WorldPlan plan = Plan(("town", new Vector2(100f, 300f)), ("town", new Vector2(900f, 300f)), ("village", new Vector2(420f, 340f)), ("outpost", new Vector2(660f, 60f)));
            PlannedRoad main = Road(plan, RoadClass.DirtCartPath, 0, 1, -1, plan.Settlements[0].Position, plan.Settlements[2].Position, plan.Settlements[1].Position);
            PlannedRoad side = Road(plan, RoadClass.Footpath, 3, -1, 0, plan.Settlements[3].Position, new Vector2(660f, 320f));
            float Ground(float x, float z) => 20f + 6f * Mathf.Sin(x * 0.02f) * Mathf.Cos(z * 0.03f);
            new RoadRefiner(plan.Area, Ground, null).Refine(plan);
            Assert.Less(DistanceTo(main.Points, plan.Settlements[2].Position), 0.01f, "still through the village");
            Assert.Less(DistanceTo(main.Points, side.Points[side.Points.Count - 1]), 0.01f, "the side road ends on the main road's new line");

            List<PlannedSignpost> posts = SignpostPlanner.Plan(plan);
            PlannedSignpost junction = posts.Find(post => !post.AtSettlement);
            Assert.IsNotNull(junction, "a signpost at the junction");
            Assert.AreEqual(3, junction.Arms.Count, "an arm each way");
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, junction.Arms.ConvertAll(arm => arm.Settlement), "each naming the nearest place that way");
            foreach (SignArm arm in junction.Arms)
            {
                Vector2 towards = plan.Settlements[arm.Settlement].Position - junction.Position;
                Assert.Greater(Vector2.Dot(arm.Direction, towards.normalized), 0.3f, $"the arm for {arm.Name} points its way");
                Assert.Greater(arm.Metres, towards.magnitude * 0.8f, "distance by road");
                Assert.AreEqual(plan.Settlements[arm.Settlement].Name, arm.Name);
            }
            Assert.Greater(DistanceTo(main.Points, junction.Position), 1.5f, "beside the road, not on it");
            Assert.Greater(DistanceTo(side.Points, junction.Position), 0.75f);

            // Where each road leaves a settlement: an arm out and one back naming the place itself.
            List<PlannedSignpost> edges = posts.FindAll(post => post.AtSettlement);
            Assert.AreEqual(5, edges.Count, "one at each end town and the outpost, two at the village the road runs through");
            foreach (PlannedSignpost post in edges)
            {
                Assert.GreaterOrEqual(post.Arms.Count, 2);
                PlannedSettlement here = plan.Nearest(post.Position);
                Assert.IsTrue(post.Arms.Exists(arm => arm.Settlement == here.Index), "names the settlement it stands at");
                Assert.IsTrue(post.Arms.Exists(arm => arm.Settlement != here.Index), "and somewhere to go");
            }
        }

        [Test]
        public void Bed_EvensOutACartPath_WithinItsCut_AndLeavesAFootpathOnTheGround()
        {
            float Bumpy(float x, float z) => 20f + 0.6f * Mathf.Sin(x * 0.9f) + 0.05f * x;
            var rules = new WorldPlanRules();
            var line = new RoadLine(new[] { new Vector2(0f, 50f), new Vector2(120f, 50f) }, Bumpy);
            var paved = new RoadBed(line, rules.Road(RoadClass.PavedCartPath), null, 1);
            var foot = new RoadBed(line, rules.Road(RoadClass.Footpath), null, 1);
            Assert.IsTrue(foot.FollowsGround);
            Assert.IsFalse(paved.FollowsGround);
            float roughGround = 0f, roughBed = 0f;
            for (float d = 30f; d < 90f; d += 1f)
            {
                roughGround += Mathf.Abs(line.HeightAlong(d + 1f) + line.HeightAlong(d - 1f) - 2f * line.HeightAlong(d));
                roughBed += Mathf.Abs(paved.HeightAlong(d + 1f) + paved.HeightAlong(d - 1f) - 2f * paved.HeightAlong(d));
                Assert.LessOrEqual(Mathf.Abs(paved.HeightAlong(d) - line.HeightAlong(d)), rules.Road(RoadClass.PavedCartPath).MaxCut + 1e-3f);
                Assert.AreEqual(line.HeightAlong(d), foot.HeightAlong(d), 1e-4f);
            }
            Assert.Less(roughBed, roughGround * 0.2f, "the bed is far smoother than the ground under it");
            Assert.AreEqual(line.HeightAlong(0f), paved.HeightAlong(0f), 1e-3f, "and meets the ground at its ends");

            List<Vector2Int> stretches = paved.StretchesNear(new Rect(59f, 49f, 2f, 2f), paved.Reach);
            Assert.AreEqual(1, stretches.Count);
            Assert.AreEqual(paved.HeightAlong(60f), paved.Surface(new Vector2(60f, 51f), 99f, stretches), 1e-3f, "the bed across its width");
            Assert.AreEqual(99f, paved.Surface(new Vector2(60f, 50f + 2.5f + 1.5f + 0.1f), 99f, stretches), 1e-3f, "the ground past its shoulder");
            Assert.IsTrue(paved.IsRoad(new Vector2(60f, 52.4f), stretches));
            Assert.IsFalse(paved.IsRoad(new Vector2(60f, 52.6f), stretches));
            Assert.IsTrue(paved.IsBed(new Vector2(60f, 53.9f), stretches));
            Assert.IsFalse(paved.IsBed(new Vector2(60f, 54.1f), stretches));

            // A hairpin: both legs are found near a box between them.
            var hairpin = new RoadLine(new[] { new Vector2(0f, 0f), new Vector2(60f, 0f), new Vector2(64f, 4f), new Vector2(60f, 8f), new Vector2(0f, 8f) }, (x, z) => 0f);
            var bend = new RoadBed(hairpin, rules.Road(RoadClass.Footpath), null, 1);
            Assert.AreEqual(2, bend.StretchesNear(new Rect(20f, 0f, 2f, 1f), 7.5f).Count);
        }

        [Test]
        public void Writer_SurfacesTheRoad_PieceByPiece_AndRemembersWhatItWrote()
        {
            VoxelMaterial grass = VoxelMaterial.Create(1, "Grass"), dead = VoxelMaterial.Create(2, "Dead Grass");
            made.Add(grass);
            made.Add(dead);
            MaterialRegistry registry = MaterialRegistry.Create(grass, dead);
            made.Add(registry);
            var world = new World(new Vector3Int(16, 16, 16), 0.5f, samples => new ByteVoxelStorage(samples));
            var generator = new FlatGenerator(4.2f);
            for (int z = 0; z <= 1; z++)
            {
                for (int x = 0; x <= 2; x++)
                {
                    world.Load(new Vector3Int(x, 0, z), generator);
                }
            }
            var rules = new WorldPlanRules();
            var line = new RoadLine(new[] { new Vector2(4f, 8f), new Vector2(40f, 8f) }, (x, z) => 4.2f);
            var writer = new RoadWriter(new RoadBed(line, rules.Road(RoadClass.Footpath), null, 3));
            Assert.AreEqual(36, writer.Pieces);
            Assert.IsTrue(writer.Loaded(world, 1, 4));
            Assert.IsFalse(writer.Loaded(world, 1, 28), "its chunks (x = 3) are not loaded");

            var near = new List<int>();
            writer.Unwritten(new Vector2(9f, 8f), 5f, near);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, near);
            foreach (int piece in near)
            {
                writer.Write(world, registry, (x, z) => 4.2f, 0.5f, piece);
            }
            Assert.AreEqual(10, writer.Written);
            WorldGrid grid = world.Grid;
            Assert.AreEqual(dead.Id, world.GetMaterial(grid.WorldToNearestSample(new Vector3(8f, 4f, 8f))).Value, "dead grass on the path");
            Assert.AreNotEqual(dead.Id, world.GetMaterial(grid.WorldToNearestSample(new Vector3(8f, 4f, 11f))).Value, "not beside it");
            Assert.AreNotEqual(dead.Id, world.GetMaterial(grid.WorldToNearestSample(new Vector3(8f, 2.5f, 8f))).Value, "nor deep under it");
            Assert.AreEqual(4.2f, GroundLeveling.GroundHeight(world, registry, new Vector3(8f, 0f, 8f), 0.5f, 1), 0.3f, "a footpath keeps the ground's shape");

            var again = new RoadWriter(new RoadBed(line, rules.Road(RoadClass.Footpath), null, 3));
            again.Restore(writer.Save());
            Assert.AreEqual(10, again.Written);
            Assert.IsTrue(again.IsWritten(9));
            Assert.IsFalse(again.IsWritten(10));
            again.Skip(20);
            Assert.AreEqual(1, again.Skipped);
            Assert.IsTrue(again.IsWritten(20), "a skipped piece is never written");
            again.Restore("7:ff");
            Assert.AreEqual(11, again.Written, "a save for a road of another length is ignored");
        }

        [Test]
        public void RoadLine_AnswersLikeTheRouteItCameFrom()
        {
            var line = new RoadLine(new[] { new Vector2(0f, 0f), new Vector2(10f, 0f), new Vector2(10f, 10f) }, (x, z) => x + z);
            Assert.AreEqual(20f, line.Length, 1e-4f);
            Assert.AreEqual(new Vector2(10f, 5f), line.Flat(15f));
            Assert.AreEqual(15f, line.HeightAlong(15f), 1e-3f);
            Assert.AreEqual(3f, line.DistanceTo(new Vector2(13f, 5f), out float along), 1e-4f);
            Assert.AreEqual(15f, along, 1e-4f);
            Assert.AreEqual(1, line.CornerBefore(12f));
            Assert.Throws<ArgumentException>(() => new RoadLine(new[] { Vector2.zero }, (x, z) => 0f));
        }
    }
}
