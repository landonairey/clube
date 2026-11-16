using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>Tunnels on cart paths where a range lies between two settlements (GL642).</summary>
    public class RoadTunnelTests
    {
        private readonly List<Object> made = new List<Object>();

        [TearDown]
        public void DestroyAll()
        {
            foreach (Object thing in made)
            {
                Object.DestroyImmediate(thing);
            }
            made.Clear();
        }

        // A ridge 70 m high running along z at x = 0, on a plain at 20 m.
        private static float Ridge(float x, float z)
        {
            return 20f + 70f * Mathf.Exp(-(x / 90f) * (x / 90f));
        }

        // Two towns either side of the ridge, and a road straight over it from one to the other.
        private static WorldPlan PlanOverTheRidge(RoadClass roadClass)
        {
            var plan = new WorldPlan { Rules = new WorldPlanRules(), Area = new Rect(-600f, -600f, 1200f, 1200f) };
            SettlementType town = plan.Rules.Types.Find(t => t.Id == "town");
            plan.Settlements.Add(new PlannedSettlement { Index = 0, Type = town, Position = new Vector2(-400f, 0f), Name = "West Town" });
            plan.Settlements.Add(new PlannedSettlement { Index = 1, Type = town, Position = new Vector2(400f, 0f), Name = "East Town" });
            var road = new PlannedRoad { Index = 0, Class = roadClass, From = 0, To = 1, Refined = true, Length = 800f, SteepLength = 300f, SteepestSlope = 0.6f };
            for (float x = -400f; x <= 400f; x += 8f)
            {
                road.Points.Add(new Vector2(x, 0f));
            }
            plan.Roads.Add(road);
            return plan;
        }

        [Test]
        public void ACartPathOverARange_IsBoredUnderIt_AtAGentleGrade()
        {
            WorldPlan plan = PlanOverTheRidge(RoadClass.PavedCartPath);
            PlannedRoad road = plan.Roads[0];
            RoadClassDefinition paved = plan.Rules.Road(RoadClass.PavedCartPath);
            var rules = new RoadTunnelRules();

            int bored = RoadTunnels.Bore(plan, Ridge, null, null, rules);

            Assert.AreEqual(1, bored);
            Assert.AreEqual(1, road.Tunnels.Count);
            RoadTunnel tunnel = road.Tunnels[0];
            Assert.Less(tunnel.From.x, -60f, "it goes in on the west flank");
            Assert.Greater(tunnel.To.x, 60f, "and comes out on the east");
            Assert.That(tunnel.Length, Is.InRange(rules.MinLength, rules.MaxLength));
            Assert.LessOrEqual(Mathf.Abs(tunnel.ToHeight - tunnel.FromHeight) / tunnel.Length, paved.MaxSlope * rules.SlopeShare + 1e-4f);
            Assert.AreEqual(Ridge(tunnel.From.x, 0f), tunnel.FromHeight, 0.01f, "its floor starts at the ground");
            Assert.Greater(Ridge(0f, 0f) - tunnel.FloorAt(tunnel.Length * 0.5f), 40f, "the range is over it");
            // The road now runs through it: shorter than before, in a straight line between its ends, and no longer steep.
            Assert.Less(road.SteepLength, 30f);
            Assert.LessOrEqual(road.SteepestSlope, 0.3f);
            Assert.AreEqual(new Vector2(-400f, 0f), road.Points[0]);
            Assert.AreEqual(new Vector2(400f, 0f), road.Points[road.Points.Count - 1]);
            Assert.IsFalse(road.Points.Any(p => p.x > tunnel.From.x + 0.1f && p.x < tunnel.To.x - 0.1f), "no point of the road is left over the range");
            Assert.IsTrue(plan.Notes.Any(note => note.Contains("tunnel")));
            // The road's line runs on the tunnel's floor inside it, and on the ground outside.
            System.Func<float, float, float> height = RoadTunnels.HeightAt(road, Ridge);
            Vector2 middle = (tunnel.From + tunnel.To) * 0.5f;
            Assert.AreEqual(tunnel.FloorAt(tunnel.Length * 0.5f), height(middle.x, middle.y), 0.01f);
            Assert.AreEqual(Ridge(0f, 30f), height(0f, 30f), 1e-4f, "beside the tunnel the ground is the ground");
            Assert.AreEqual(Ridge(-390f, 0f), height(-390f, 0f), 1e-4f);
            float[] many = RoadTunnels.HeightsAt(road, points => points.Select(p => Ridge(p.x, p.y)).ToArray())(new[] { middle, new Vector2(0f, 30f) });
            Assert.AreEqual(height(middle.x, middle.y), many[0], 1e-4f);
            Assert.AreEqual(Ridge(0f, 30f), many[1], 1e-4f);
        }

        [Test]
        public void AFootpathClimbsOverIt_AndAJunctionIsNeverSwallowed()
        {
            WorldPlan footpath = PlanOverTheRidge(RoadClass.Footpath);
            Assert.AreEqual(0, RoadTunnels.Bore(footpath, Ridge));
            Assert.IsEmpty(footpath.Roads[0].Tunnels);
            Assert.AreEqual(101, footpath.Roads[0].Points.Count, "untouched");

            // Another road joins the cart path on top of the ridge: no tunnel may pass under the junction.
            WorldPlan joined = PlanOverTheRidge(RoadClass.PavedCartPath);
            var side = new PlannedRoad { Index = 1, Class = RoadClass.Footpath, From = 1, Joins = 0, Refined = true };
            side.Points.Add(new Vector2(0f, 300f));
            side.Points.Add(new Vector2(0f, 0f));
            joined.Roads.Add(side);
            RoadTunnels.Bore(joined, Ridge);
            foreach (RoadTunnel tunnel in joined.Roads[0].Tunnels)
            {
                Assert.IsFalse(tunnel.From.x < 0f && tunnel.To.x > 0f, "the junction stays on the road");
            }
            Assert.IsTrue(joined.Roads[0].Points.Any(p => Mathf.Abs(p.x) < 4.1f), "the road still passes the junction");

            // A gentle road wants none.
            WorldPlan gentle = PlanOverTheRidge(RoadClass.PavedCartPath);
            gentle.Roads[0].SteepLength = 0f;
            Assert.AreEqual(0, RoadTunnels.Bore(gentle, Ridge));
        }

        [Test]
        public void TheWriter_BoresAnArchedPassage_AMetreAtATime_AndRemembersIt()
        {
            VoxelMaterial grass = VoxelMaterial.Create(1, "Grass"), stone = VoxelMaterial.Create(2, "Stone"), cobble = VoxelMaterial.Create(3, "Loose stone");
            made.AddRange(new Object[] { grass, stone, cobble });
            MaterialRegistry registry = MaterialRegistry.Create(grass, stone, cobble);
            made.Add(registry);
            var world = new World(new Vector3Int(16, 16, 16), 0.5f, samples => new ByteVoxelStorage(samples));
            var generator = new FlatGenerator(30f);
            for (int z = 0; z <= 2; z++)
            {
                for (int y = 0; y <= 4; y++)
                {
                    for (int x = 0; x <= 5; x++)
                    {
                        world.Load(new Vector3Int(x, y, z), generator);
                    }
                }
            }
            // Under 20 m of ground from end to end, but for its first metres, where the hillside is only starting.
            var tunnel = new RoadTunnel { From = new Vector2(4f, 12f), To = new Vector2(44f, 12f), FromHeight = 10f, ToHeight = 10f };
            var writer = new TunnelWriter(tunnel, 4f, 3.5f, "Loose stone", (x, z) => x < 8f ? 12f : 30f);

            Assert.AreEqual(4f, writer.BoreStart, "the first metres are an open cutting");
            Assert.AreEqual(40f, writer.BoreEnd);
            Assert.AreEqual(36, writer.Pieces);
            Assert.IsTrue(writer.IsBored(new Vector2(20f, 12f)));
            Assert.IsFalse(writer.IsBored(new Vector2(5f, 12f)), "the cutting is the road's to dig");
            Assert.IsFalse(writer.IsBored(new Vector2(20f, 30f)));
            var near = new List<int>();
            writer.Unwritten(new Vector2(14f, 12f), 5f, near);
            Assert.AreEqual(10, near.Count);
            foreach (int piece in near)
            {
                Assert.IsTrue(writer.Loaded(world, 5, piece));
                writer.Write(world, registry, stone, 0.5f, piece);
            }
            WorldGrid grid = world.Grid;
            // The passage is carved voxels (the mesher and the player take them for air); the ground round it is not.
            bool Carved(float x, float y, float z) => world.IsCarved(Vector3Int.FloorToInt(new Vector3(x, y, z) / 0.5f));
            Assert.IsTrue(Carved(14.6f, 11.6f, 12.1f), "air in the passage");
            Assert.IsTrue(Carved(14.6f, 13.1f, 12.1f), "to the top of its arch");
            Assert.IsFalse(Carved(14.6f, 13.1f, 13.9f), "lower at its walls");
            Assert.IsFalse(Carved(14.6f, 9.6f, 12.1f), "a floor under it");
            Assert.IsFalse(Carved(14.6f, 16.1f, 12.1f), "the hill over it");
            Assert.IsFalse(Carved(14.6f, 11.6f, 16.1f), "and beside it");
            Assert.IsFalse(Carved(30.1f, 11.6f, 12.1f), "not bored yet further in");
            Assert.AreEqual(cobble.Id, world.GetMaterial(grid.WorldToNearestSample(new Vector3(14.5f, 10f, 12f))).Value, "the floor is the road's surface");

            var again = new TunnelWriter(tunnel, 4f, 3.5f, "Loose stone", (x, z) => x < 8f ? 12f : 30f);
            again.Restore(writer.Save());
            Assert.AreEqual(10, again.Written);
            Assert.IsTrue(again.IsWritten(near[0]));
        }
    }
}
