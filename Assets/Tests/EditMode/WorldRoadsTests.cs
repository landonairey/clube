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
                Assert.Less(Mathf.Abs(paved.Points[i].y - 300f), RoadRefiner.Corridor * RoadRefiner.HardReach + 1f, "inside its corridor");
            }
        }


        // A hillside 144 m high across the whole area, rising 90% (steeper than anyone walks) between z = 200 and z = 360.
        private static float Hillside(float x, float z)
        {
            return 20f + 0.9f * Mathf.Clamp(z - 200f, 0f, 160f);
        }

        [Test]
        public void Refining_ClimbsASteepHillsideInSwitchbacks_EachClassWithinItsSlope()
        {
            PlannedRoad Up(RoadClass roadClass, out RoadClassDefinition definition)
            {
                WorldPlan plan = Plan();
                definition = plan.Rules.Road(roadClass);
                PlannedRoad road = Road(plan, roadClass, -1, -1, -1, new Vector2(500f, 100f), new Vector2(500f, 460f));
                Assert.IsTrue(new RoadRefiner(plan.Area, Hillside, null).Refine(plan, road));
                return road;
            }
            PlannedRoad paved = Up(RoadClass.PavedCartPath, out RoadClassDefinition pavedClass);
            PlannedRoad dirt = Up(RoadClass.DirtCartPath, out RoadClassDefinition dirtClass);
            PlannedRoad foot = Up(RoadClass.Footpath, out RoadClassDefinition footClass);
            // 144 m of climb at the class's limit is the least road there can be; straight up is 360 m.
            Assert.Greater(paved.Length, 144f / pavedClass.MaxSlope, "the paved road doubles back up the hillside");
            Assert.Greater(dirt.Length, 144f / dirtClass.MaxSlope);
            Assert.Less(foot.Length, dirt.Length, "a walker climbs more directly than a cart");
            Assert.LessOrEqual(dirt.Length, paved.Length + 100f, "and a dirt path no less directly than a paved one");
            Assert.Less(paved.SteepestSlope, pavedClass.MaxSlope * 1.6f, "nowhere much over its limit, hairpins included");
            Assert.Less(paved.SteepLength, paved.Length * 0.1f, "and over it for little of its length");
            Assert.Less(dirt.SteepestSlope, dirtClass.MaxSlope * 1.6f);
            Assert.Less(dirt.SteepLength, dirt.Length * 0.1f);
            Assert.AreEqual(0f, foot.SteepLength, "the footpath stays walkable");
            Assert.Less(foot.SteepestSlope, footClass.MaxSlope);
            float reach = RoadRefiner.Corridor * RoadRefiner.HardReach + 1f;
            foreach (Vector2 p in paved.Points)
            {
                Assert.Less(Mathf.Abs(p.x - 500f), reach, "inside the wide corridor a steep stretch is given");
            }
        }

        [Test]
        public void Refining_DoesNotClimbAHillsideUpItsStream()
        {
            // A stream runs straight down the hillside under the planned line: it is water to bridge, not a way up.
            RoadWater Water(float x, float z)
            {
                return Mathf.Abs(x - 500f) <= 3f && z >= 150f && z <= 420f ? RoadWater.River : RoadWater.Dry;
            }
            WorldPlan plan = Plan();
            PlannedRoad road = Road(plan, RoadClass.DirtCartPath, -1, -1, -1, new Vector2(500f, 100f), new Vector2(500f, 460f));
            Assert.IsTrue(new RoadRefiner(plan.Area, Hillside, Water).Refine(plan, road));
            Assert.Greater(road.Length, 144f / plan.Rules.Road(RoadClass.DirtCartPath).MaxSlope, "still in switchbacks");
            Assert.Less(road.SteepLength, road.Length * 0.1f);
            foreach (RoadBridge bridge in road.Bridges)
            {
                Assert.Less(bridge.Length, 30f, "each crossing is short: across the stream, never along it");
            }
        }

        [Test]
        public void Refining_KeepsOffClosedGround()
        {
            // Closed ground (past the map's playable edge) lies across the straight line; the way round is to the south.
            bool Closed(float x, float z) => x >= 380f && x <= 460f && z >= 180f;
            WorldPlan plan = Plan(("town", new Vector2(180f, 300f)), ("town", new Vector2(700f, 300f)));
            PlannedRoad road = Road(plan, RoadClass.DirtCartPath, 0, 1, -1, plan.Settlements[0].Position, new Vector2(420f, 140f), plan.Settlements[1].Position);
            Assert.IsTrue(new RoadRefiner(plan.Area, (x, z) => 20f, null, Closed).Refine(plan, road));
            for (int i = 0; i < road.Points.Count - 1; i++)
            {
                for (int k = 0; k <= 4; k++)
                {
                    Vector2 p = Vector2.Lerp(road.Points[i], road.Points[i + 1], k / 4f);
                    // Rounding the route may shave a corner of the closed ground, by less than half a grid step.
                    Assert.IsFalse(Closed(p.x - 4f, p.y - 4f) && Closed(p.x + 4f, p.y - 4f), $"on closed ground at {p}");
                }
            }
        }

        [Test]
        public void Refining_BridgesAStraitOfTheSea_OnlyWhereThereIsNoWayRound()
        {
            // A strait 16 m wide cuts the whole area in two: too narrow for the plan's 40 m cells to have seen.
            RoadWater Water(float x, float z) => x >= 392f && x <= 408f ? RoadWater.Sea : RoadWater.Dry;
            WorldPlan plan = Plan(("town", new Vector2(180f, 300f)), ("town", new Vector2(620f, 300f)));
            PlannedRoad road = Road(plan, RoadClass.DirtCartPath, 0, 1, -1, plan.Settlements[0].Position, plan.Settlements[1].Position);
            Assert.IsTrue(new RoadRefiner(plan.Area, (x, z) => 20f, Water).Refine(plan, road), "the road gets across");
            Assert.AreEqual(1, road.Bridges.Count);
            Assert.That(road.Bridges[0].Length, Is.InRange(16f, 40f), "straight across the strait");
            Assert.IsTrue(plan.Notes.Exists(note => note.Contains("strait")), "and the plan says so");
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
            Assert.IsFalse(foot.FollowsGround, "GL646: a footpath has a bed too, level from side to side");
            Assert.IsFalse(paved.FollowsGround);
            float roughGround = 0f, roughBed = 0f;
            for (float d = 30f; d < 90f; d += 1f)
            {
                roughGround += Mathf.Abs(line.HeightAlong(d + 1f) + line.HeightAlong(d - 1f) - 2f * line.HeightAlong(d));
                roughBed += Mathf.Abs(paved.HeightAlong(d + 1f) + paved.HeightAlong(d - 1f) - 2f * paved.HeightAlong(d));
                Assert.LessOrEqual(Mathf.Abs(paved.HeightAlong(d) - line.HeightAlong(d)), rules.Road(RoadClass.PavedCartPath).MaxCut + 1e-3f);
                Assert.LessOrEqual(Mathf.Abs(foot.HeightAlong(d) - line.HeightAlong(d)), rules.Road(RoadClass.Footpath).MaxCut + 1e-3f, "it stays close to the ground it is walked on");
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
        public void Bed_IsLevelAcrossAFootpath_LeavesWaterAlone_AndLevelsAHairpinsTurn()
        {
            var rules = new WorldPlanRules();
            // GL646: a footpath along a hillside (the ground climbs across it): one height from edge to edge.
            RoadClassDefinition footClass = rules.Road(RoadClass.Footpath);
            var along = new RoadLine(new[] { new Vector2(0f, 50f), new Vector2(120f, 50f) }, (x, z) => 20f + 0.5f * z);
            var foot = new RoadBed(along, footClass, null, 1);
            List<Vector2Int> stretches = foot.StretchesNear(new Rect(59f, 49f, 2f, 2f), foot.Reach);
            float uphill = foot.Surface(new Vector2(60f, 50.4f), 20f + 0.5f * 50.4f, stretches), downhill = foot.Surface(new Vector2(60f, 49.6f), 20f + 0.5f * 49.6f, stretches);
            Assert.AreEqual(uphill, downhill, 1e-3f, "cut into the slope on one side, built up on the other");
            Assert.AreEqual(foot.HeightAlong(60f), uphill, 1e-3f);

            // GL645: nothing of a road is written where water stands.
            foot.WetAt = (x, z) => x > 70f;
            Assert.IsTrue(foot.IsBed(new Vector2(60f, 50f), stretches));
            List<Vector2Int> wetStretches = foot.StretchesNear(new Rect(79f, 49f, 2f, 2f), foot.Reach);
            Assert.IsFalse(foot.IsBed(new Vector2(80f, 50f), wetStretches));
            Assert.IsFalse(foot.IsRoad(new Vector2(80f, 50f), wetStretches));

            // GL648: a hairpin up a hillside. Its legs lie side by side at different heights; the bed is one level round the turn.
            RoadClassDefinition paved = rules.Road(RoadClass.PavedCartPath);
            var hairpin = new RoadLine(new[] { new Vector2(0f, 0f), new Vector2(60f, 0f), new Vector2(63f, 3f), new Vector2(60f, 6f), new Vector2(0f, 6f) }, (x, z) => 10f + 0.3f * z);
            var bend = new RoadBed(hairpin, paved, null, 1);
            float apex = 60f + Mathf.Sqrt(18f);
            Assert.Greater(Mathf.Abs(hairpin.HeightAlong(apex + 12f) - hairpin.HeightAlong(apex - 12f)), 1.5f, "the ground under the two legs differs");
            Assert.Less(Mathf.Abs(bend.HeightAlong(apex + 12f) - bend.HeightAlong(apex - 12f)), 0.3f, "the bed does not: no step down the middle of the road");
        }

        [Test]
        public void OnRock_AFootpathHasLooseStones_AndACartPathAMixOfItsTwoMaterials()
        {
            var rules = new WorldPlanRules();
            var line = new RoadLine(new[] { new Vector2(0f, 0f), new Vector2(120f, 0f) }, (x, z) => 5f);
            var foot = new RoadBed(line, rules.Road(RoadClass.Footpath), null, 7);
            var dirt = new RoadBed(line, rules.Road(RoadClass.DirtCartPath), null, 7);
            List<Vector2Int> all = foot.StretchesNear(new Rect(0f, -1f, 120f, 2f), 3f);
            List<Vector2Int> allDirt = dirt.StretchesNear(new Rect(0f, -1f, 120f, 2f), 3f);
            float highest = 0f;
            int stones = 0, plain = 0, mixed = 0, looked = 0;
            for (float x = 2f; x < 118f; x += 0.25f)
            {
                for (float z = -0.5f; z <= 0.5f; z += 0.25f)
                {
                    foot.OnRock(new Vector2(x, z), all, out bool stone, out float raise);
                    highest = Mathf.Max(highest, raise);
                    stones += stone ? 1 : 0;
                    plain += stone ? 0 : 1;
                    Assert.IsTrue(!stone || raise > 0f, "a footpath has its second material only as loose stones");
                    dirt.OnRock(new Vector2(x, z), allDirt, out bool mix, out float bump);
                    Assert.AreEqual(0f, bump, "a cart path has no stones lying on it");
                    mixed += mix ? 1 : 0;
                    looked++;
                }
            }
            Assert.Greater(highest, 0.1f, "stones stand over the path");
            Assert.LessOrEqual(highest, rules.Road(RoadClass.Footpath).RockBumpHeight + 1e-3f);
            Assert.Greater(plain, stones * 3, "but most of it is the bare stone");
            Assert.That(mixed / (float)looked, Is.InRange(0.1f, 0.7f), "a cart path over rock is part crushed stone, part stone");
            Assert.IsNull(rules.Road(RoadClass.PavedCartPath).RockSurface, "a paved road keeps its cobbles");
        }

        [Test]
        public void ATreeCutByARoad_LeavesNoBranchHanging_AndItsNeighbourStands()
        {
            VoxelMaterial grass = VoxelMaterial.Create(1, "Grass"), wood = VoxelMaterial.Create(2, "Wood", VoxelMaterialCategory.Plant);
            made.Add(grass);
            made.Add(wood);
            MaterialRegistry registry = MaterialRegistry.Create(grass, wood);
            made.Add(registry);
            var world = new World(new Vector3Int(16, 16, 16), 0.5f, samples => new ByteVoxelStorage(samples));
            var generator = new FlatGenerator(4.2f);
            for (int z = 0; z <= 2; z++)
            {
                for (int x = 0; x <= 3; x++)
                {
                    world.Load(new Vector3Int(x, 0, z), generator);
                    world.Load(new Vector3Int(x, 1, z), generator);
                }
            }
            WorldGrid grid = world.Grid;
            void Wood(float x, float y, float z)
            {
                Vector3Int sample = grid.WorldToNearestSample(new Vector3(x, y, z));
                world.SetDensity(sample, 1f);
                world.SetMaterial(sample, wood.Id);
            }
            bool IsWood(float x, float y, float z)
            {
                Vector3Int sample = grid.WorldToNearestSample(new Vector3(x, y, z));
                return (world.GetDensity(sample) ?? 0f) >= 0.5f && world.GetMaterial(sample) == wood.Id;
            }
            // A tree on the road at x = 10 with a long branch out to x = 17, and a tree of its own at x = 22.
            for (float y = 4.5f; y <= 8f; y += 0.5f)
            {
                Wood(10f, y, 10f);
                Wood(22f, y, 10f);
            }
            for (float x = 10.5f; x <= 17f; x += 0.5f)
            {
                Wood(x, 8f, 10f);
            }
            GroundLeveling.Level(world, registry, new GroundLeveling.Area
            {
                Centre = new Vector2(10f, 10f), Half = new Vector2(1f, 1f), FollowGround = true, Natural = (x, z) => 4.2f, Headroom = 3f, TreeReach = 2f,
            }, 0.5f);
            Assert.IsFalse(IsWood(10f, 6f, 10f), "the trunk on the road is cut");
            Assert.IsFalse(IsWood(16f, 8f, 10f), "GL652: and the branch past the cut's reach goes with it, not left in the air");
            Assert.IsTrue(IsWood(22f, 6f, 10f), "a tree off the road stands");
        }

        [Test]
        public void LevellingInSmallSteps_EndsAsOneCallDoes()
        {
            VoxelMaterial grass = VoxelMaterial.Create(1, "Grass"), wood = VoxelMaterial.Create(2, "Wood", VoxelMaterialCategory.Plant);
            made.Add(grass);
            made.Add(wood);
            MaterialRegistry registry = MaterialRegistry.Create(grass, wood);
            made.Add(registry);
            World Wooded()
            {
                var world = new World(new Vector3Int(16, 16, 16), 0.5f, samples => new ByteVoxelStorage(samples));
                var generator = new FlatGenerator(4.2f);
                for (int z = 0; z <= 2; z++)
                {
                    for (int x = 0; x <= 3; x++)
                    {
                        world.Load(new Vector3Int(x, 0, z), generator);
                        world.Load(new Vector3Int(x, 1, z), generator);
                    }
                }
                // A trunk on the road with a long branch out past the cut's reach, and a tree of its own further off.
                void Wood(float x, float y, float z)
                {
                    Vector3Int sample = world.Grid.WorldToNearestSample(new Vector3(x, y, z));
                    world.SetDensity(sample, 1f);
                    world.SetMaterial(sample, wood.Id);
                }
                for (float y = 4.5f; y <= 8f; y += 0.5f)
                {
                    Wood(10f, y, 10f);
                    Wood(22f, y, 10f);
                }
                for (float x = 10.5f; x <= 17f; x += 0.5f)
                {
                    Wood(x, 8f, 10f);
                }
                return world;
            }
            GroundLeveling.Area Road() => new GroundLeveling.Area
            {
                Centre = new Vector2(10f, 10f), Half = new Vector2(1f, 1f), FollowGround = true, Natural = (x, z) => 4.2f, Headroom = 3f, TreeReach = 2f,
            };

            World atOnce = Wooded();
            GroundLeveling.Level(atOnce, registry, Road(), 0.5f);
            World stepped = Wooded();
            int steps = 0;
            using (var work = new SlicedWork(GroundLeveling.LevelSteps(stepped, registry, Road(), 0.5f)))
            {
                while (!work.Run(0f))
                {
                    steps++;
                }
            }

            Assert.Greater(steps, 5, "the cut and the levelling are spread over many steps");
            for (int z = 0; z < 48; z++)
            {
                for (int y = 0; y < 32; y++)
                {
                    for (int x = 0; x < 64; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        Assert.AreEqual(atOnce.GetDensity(sample), stepped.GetDensity(sample), $"density at {sample}");
                        Assert.AreEqual(atOnce.GetMaterial(sample), stepped.GetMaterial(sample), $"material at {sample}");
                    }
                }
            }
        }

        [Test]
        public void APieceWrittenInSteps_CountsAsWrittenOnlyOnceItIsDone()
        {
            VoxelMaterial grass = VoxelMaterial.Create(1, "Grass");
            made.Add(grass);
            MaterialRegistry registry = MaterialRegistry.Create(grass);
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
            var line = new RoadLine(new[] { new Vector2(4f, 8f), new Vector2(40f, 8f) }, (x, z) => 4.2f);
            var writer = new RoadWriter(new RoadBed(line, new WorldPlanRules().Road(RoadClass.Footpath), null, 3));

            using var work = new SlicedWork(writer.WriteSteps(world, registry, (x, z) => 4.2f, 0.5f, 4));
            work.Run(0f);
            Assert.IsFalse(writer.IsWritten(4), "still being written");
            while (!work.Run(0f))
            {
            }
            Assert.IsTrue(writer.IsWritten(4));
            Assert.AreEqual(1, writer.Written);
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
        public void DeadGrass_IsMaterial34_AndBurnsWorseThanGrass()
        {
            var registry = UnityEditor.AssetDatabase.LoadAssetAtPath<MaterialRegistry>("Assets/Config/Materials/MaterialRegistry.asset");
            VoxelMaterial dead = registry.Get(34), grass = registry.Find("Grass");
            Assert.AreEqual("Dead Grass", dead.DisplayName, "appended after the earlier ones, so none of their ids or texture layers moved");
            Assert.AreEqual(34, dead.Id);
            Assert.AreEqual(MaterialKind.Soil, dead.Kind);
            Assert.AreNotSame(grass.Albedo, dead.Albedo, "its own, yellower texture");
            Assert.AreEqual("dead-grass", dead.Drop.Id);
            Assert.IsTrue(registry.IsMaterialItem(dead.Drop));
            Assert.Less(dead.Drop.BurnSeconds, grass.Drop.BurnSeconds, "burns even worse as a fuel source");
            Assert.Less(dead.Drop.FuelQuality, grass.Drop.FuelQuality);
            foreach (RoadClassDefinition road in RoadClassDefinition.Defaults())
            {
                Assert.IsNotNull(registry.Find(road.Surface), road.Name + " is surfaced with a material that exists: " + road.Surface);
            }
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
