using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>Settlements laid out on their ground (GL613) and terraced up a hillside (GL642).</summary>
    public class SettlementLayoutTests
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

        private static PlannedSettlement Settlement(string type, int population, int shops, Vector2 at)
        {
            List<EconomySpecialty> specialties = EconomySpecialty.Defaults();
            var settlement = new PlannedSettlement
            {
                Index = 3, Type = SettlementType.Defaults().Find(t => t.Id == type), Position = at, Name = $"Test {type}", Population = population,
            };
            for (int i = 0; i < shops; i++)
            {
                settlement.Shops.Add(new PlannedShop { Specialty = specialties[i], Reason = "test" });
            }
            return settlement;
        }

        // The ground a building stands on (its walls' corners), in the world.
        private static IEnumerable<Vector2> Corners(SettlementBuilding building)
        {
            float w = TownLayout.ShopWidth * 0.5f, d = TownLayout.ShopDepth * 0.5f;
            yield return building.ToWorld(new Vector2(-w, -d));
            yield return building.ToWorld(new Vector2(w, -d));
            yield return building.ToWorld(new Vector2(-w, d));
            yield return building.ToWorld(new Vector2(w, d));
            yield return building.Centre;
        }

        [Test]
        public void OnLevelGround_ASettlementIsOneLevel_WithShopsNearestTheCentre()
        {
            PlannedSettlement town = Settlement("town", 12, 3, new Vector2(1000f, 1000f));
            var rules = new SettlementSiteRules();

            SettlementSite site = SettlementLayout.Plan(town, 32f, rules, (x, z) => 40f);

            Assert.AreEqual(SettlementLayout.BuildingsWanted(town, rules), site.Buildings.Count);
            Assert.AreEqual(8, site.Buildings.Count, "3 shops, and houses for the other 9 two to a house");
            Assert.AreEqual(1, site.Levels);
            Assert.IsEmpty(site.Notes);
            Assert.IsTrue(site.Buildings.Take(3).All(b => b.Kind == SettlementBuildingKind.Shop && b.Shop != null));
            Assert.IsTrue(site.Buildings.Skip(3).All(b => b.Kind == SettlementBuildingKind.House && b.Shop == null));
            float farthestShop = site.Buildings.Take(3).Max(b => Vector2.Distance(b.Centre, town.Position));
            float nearestHouse = site.Buildings.Skip(3).Min(b => Vector2.Distance(b.Centre, town.Position));
            Assert.LessOrEqual(farthestShop, nearestHouse + 0.5f, "the shops take the places nearest the centre");
            foreach (SettlementBuilding building in site.Buildings)
            {
                Assert.AreEqual(40f, building.Pad, 1e-3f);
                Assert.IsTrue(site.Footprint.Contains(building.Centre), "inside its parcels");
                Assert.AreEqual(0f, Mathf.Repeat(building.Yaw, 90f), 1e-3f, "a quarter turn");
            }
            // No two buildings share ground.
            for (int i = 0; i < site.Buildings.Count; i++)
            {
                for (int j = i + 1; j < site.Buildings.Count; j++)
                {
                    Vector2 apart = site.Buildings[i].Centre - site.Buildings[j].Centre;
                    Assert.IsTrue(Mathf.Abs(apart.x) >= TownLayout.ShopDepth || Mathf.Abs(apart.y) >= TownLayout.ShopDepth, "apart");
                }
            }
            Assert.IsFalse(site.Lanes.Any(lane => lane.Switchback), "nothing to climb");
            // The same again: a settlement is always laid out the same.
            SettlementSite again = SettlementLayout.Plan(town, 32f, rules, (x, z) => 40f);
            CollectionAssert.AreEqual(site.Buildings.Select(b => b.Centre), again.Buildings.Select(b => b.Centre));
        }

        [Test]
        public void OnAHillside_RowsStepUpItOnTerraces_CutAndFilled()
        {
            // The land climbs 30% towards -x: rows run along z, and step up towards -x.
            PlannedSettlement town = Settlement("town", 16, 3, new Vector2(1000f, 1000f));
            var rules = new SettlementSiteRules();
            System.Func<float, float, float> hillside = (x, z) => 80f - 0.3f * (x - 1000f);

            SettlementSite site = SettlementLayout.Plan(town, 32f, rules, hillside);

            Assert.AreEqual(new Vector2(-1f, 0f), site.Up);
            Assert.AreEqual(0.3f, site.Slope, 0.01f);
            Assert.AreEqual(SettlementLayout.BuildingsWanted(town, rules), site.Buildings.Count, "the slope refuses none");
            Assert.GreaterOrEqual(site.Levels, 2, "tiers");
            foreach (SettlementTerrace terrace in site.Terraces)
            {
                Assert.Greater(terrace.Cut, 0.5f, "cut into the hillside at its back");
                Assert.Greater(terrace.Fill, 0.5f, "built up at its front");
                Assert.LessOrEqual(terrace.Cut, rules.MaxRelief);
                Assert.AreEqual(0f, Mathf.Repeat(terrace.Height + 1e-4f, 0.25f), 1e-3f, "on the voxel grid");
            }
            // Higher rows stand higher, by about the hillside's rise from row to row.
            List<IGrouping<int, SettlementTerrace>> rows = site.Terraces.GroupBy(t => t.Row).OrderBy(g => g.Key).ToList();
            for (int i = 1; i < rows.Count; i++)
            {
                float step = rows[i].Average(t => t.Height) - rows[i - 1].Average(t => t.Height);
                Assert.AreEqual(0.3f * rules.RowDepth * (rows[i].Key - rows[i - 1].Key), step, 0.5f);
            }
            foreach (SettlementBuilding building in site.Buildings)
            {
                foreach (Vector2 corner in Corners(building))
                {
                    Assert.AreEqual(building.Pad, site.HeightAt(corner.x, corner.y), 1e-3f, "level under the building");
                }
                Assert.AreEqual(new Vector2(1f, 0f), (building.ToWorld(new Vector2(0f, 1f)) - building.Centre).normalized, "fronts face downhill");
            }
            // Past a terrace's bank the land is as it was.
            SettlementTerrace first = site.Terraces[0];
            Vector2 beyond = new Vector2(first.Area.xMax + rules.Ramp + 0.1f, first.Area.center.y);
            if (site.Terraces.All(t => SettlementSite.Beyond(t.Area, beyond) >= rules.Ramp))
            {
                Assert.AreEqual(hillside(beyond.x, beyond.y), site.HeightAt(beyond.x, beyond.y), 1e-3f);
            }
        }

        [Test]
        public void TheWayUpClimbsStraightOnAnEasySlope_AndDoublesBackOnASteepOne()
        {
            PlannedSettlement town = Settlement("town", 16, 3, new Vector2(1000f, 1000f));
            var rules = new SettlementSiteRules();

            SettlementSite easy = SettlementLayout.Plan(town, 32f, rules, (x, z) => 80f + 0.2f * (z - 1000f));
            SettlementSite steep = SettlementLayout.Plan(town, 32f, rules, (x, z) => 80f + 0.7f * (z - 1000f));

            List<SettlementLane> easyClimbs = easy.Lanes.Where(lane => lane.Kind == SettlementLaneKind.Climb).ToList();
            List<SettlementLane> steepClimbs = steep.Lanes.Where(lane => lane.Kind == SettlementLaneKind.Climb).ToList();
            Assert.IsNotEmpty(easyClimbs);
            Assert.IsFalse(easyClimbs.Any(lane => lane.Switchback), "20% is climbed straight");
            Assert.IsNotEmpty(steepClimbs);
            Assert.IsTrue(steepClimbs.All(lane => lane.Switchback), "70% is not");
            foreach (SettlementSite site in new[] { easy, steep })
            {
                List<SettlementLane> streets = site.Lanes.Where(lane => lane.Kind == SettlementLaneKind.Street).ToList();
                Assert.AreEqual(site.Terraces.Select(t => t.Row).Distinct().Count(), streets.Count, "a street a row");
                foreach (SettlementLane climb in site.Lanes.Where(lane => lane.Kind == SettlementLaneKind.Climb))
                {
                    // Each end of a climb is on a street (or at the centre, where the roads meet).
                    foreach (Vector2 end in new[] { climb.Points[0], climb.Points[climb.Points.Count - 1] })
                    {
                        Assert.IsTrue(end == town.Position || streets.Any(s => SettlementSiteDistance(end, s.Points[0], s.Points[1]) < 0.01f), "a climb joins streets");
                    }
                    float length = 0f;
                    for (int i = 1; i < climb.Points.Count; i++)
                    {
                        length += Vector2.Distance(climb.Points[i - 1], climb.Points[i]);
                    }
                    Vector2 a = climb.Points[0], b = climb.Points[climb.Points.Count - 1];
                    float rise = Mathf.Abs(site.HeightAt(b.x, b.y) - site.HeightAt(a.x, a.y));
                    // Within the climbing slope, unless the swing is as wide as it may be.
                    float widest = climb.Points.Max(p => SettlementSiteDistance(p, a, b));
                    if (widest < rules.MaxSwing - 0.01f)
                    {
                        Assert.LessOrEqual(rise / length, rules.ClimbSlope + 0.01f);
                    }
                }
                // The centre, where the planned roads meet, is on a lane.
                Assert.IsTrue(site.Lanes.Any(lane => SettlementSiteDistance(town.Position, lane.Points[0], lane.Points[lane.Points.Count - 1]) < 0.01f));
            }
        }

        private static float SettlementSiteDistance(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(point, a + ab * t);
        }

        [Test]
        public void BuildingsKeepOffRoadsWaterAndCliffs_AndTheRoadsGroundIsLeftToTheRoad()
        {
            PlannedSettlement village = Settlement("village", 8, 2, new Vector2(1000f, 1000f));
            var rules = new SettlementSiteRules();
            // A paved road through the centre; water east of x = 1020; a cliff west of x = 985.
            var road = new SettlementRoad { Points = new[] { new Vector2(1000f, 900f), new Vector2(1000f, 1100f) }, Reach = 4f };
            System.Func<float, float, float> land = (x, z) => 30f + (x < 985f ? (985f - x) * 10f : 0f) + 0.1f * (z - 1000f);

            SettlementSite site = SettlementLayout.Plan(village, 32f, rules, land, null, (x, z) => x > 1020f, new[] { road });

            Assert.IsNotEmpty(site.Buildings);
            foreach (SettlementBuilding building in site.Buildings)
            {
                foreach (Vector2 corner in Corners(building))
                {
                    Assert.GreaterOrEqual(site.RoadBeyond(corner.x, corner.y), rules.RoadBlend - 0.01f, "clear of the road");
                    Assert.LessOrEqual(corner.x, 1020f, "off the water");
                    Assert.GreaterOrEqual(corner.x, 985f, "off the cliff");
                    Assert.AreEqual(building.Pad, site.HeightAt(corner.x, corner.y), 1e-3f);
                }
            }
            // On the road and its shoulder the land is untouched, whatever terrace lies beside it.
            for (float z = 960f; z <= 1040f; z += 4f)
            {
                Assert.AreEqual(land(1003f, z), site.HeightAt(1003f, z), 1e-3f);
            }
            if (site.Buildings.Count < SettlementLayout.BuildingsWanted(village, rules))
            {
                Assert.IsNotEmpty(site.Notes, "it says what it could not place");
            }
        }

        [Test]
        public void ACampIsOneShop_AndAThievesCampIsHousesForItsBand()
        {
            SettlementSite camp = SettlementLayout.Plan(Settlement("camp", 1, 1, new Vector2(500f, 500f)), 32f, null, (x, z) => 12f);
            SettlementSite thieves = SettlementLayout.Plan(Settlement("thieves-camp", 0, 0, new Vector2(500f, 500f)), 32f, null, (x, z) => 12f);

            Assert.AreEqual(1, camp.Buildings.Count);
            Assert.AreEqual(SettlementBuildingKind.Shop, camp.Buildings[0].Kind);
            // GL617: no shops, and a roof over the band (four, two to a house).
            Assert.AreEqual(2, thieves.Buildings.Count);
            Assert.IsTrue(thieves.Buildings.TrueForAll(building => building.Kind == SettlementBuildingKind.House));
        }

        [Test]
        public void AHouseHasADoorAndAWindow_AndAShopAnOpenFront()
        {
            List<(Vector3 Centre, TownStructures.ShopPart Part)> house = SettlementStructures.HouseVoxels(0.25f);
            float d = TownLayout.ShopDepth * 0.5f;
            bool Solid(float x, float y, float z) => house.Any(c => Vector3.Distance(c.Centre, new Vector3(x, y, z)) < 0.01f);

            Assert.IsFalse(Solid(SettlementStructures.DoorX + 0.125f, 1.125f, d - 0.125f), "the door is open");
            Assert.IsFalse(Solid(-SettlementStructures.DoorX + 0.125f, TownLayout.FloorHeight + 1.375f, d - 0.125f), "the window is open");
            Assert.IsTrue(Solid(0.125f, 1.125f, d - 0.125f), "the front wall between them is not");
            Assert.IsTrue(Solid(0.125f, 1.125f, -d + 0.125f), "nor the back wall");
            Assert.IsTrue(Solid(0.125f, 0.125f, 0.125f), "a floor");
            Assert.IsFalse(Solid(0.125f, 1.125f, 0.125f), "room inside");
        }

        [Test]
        public void TheWriter_WritesGroundLanesAndBuildings_InOrder_AndRemembersHowFar()
        {
            VoxelMaterial grass = VoxelMaterial.Create(1, "Grass"), stone = VoxelMaterial.Create(2, "Stone"), oak = VoxelMaterial.Create(3, "Oak boards"),
                maple = VoxelMaterial.Create(4, "Maple boards"), dead = VoxelMaterial.Create(5, "Dead Grass");
            made.AddRange(new Object[] { grass, stone, oak, maple, dead });
            MaterialRegistry registry = MaterialRegistry.Create(grass, stone, oak, maple, dead);
            made.Add(registry);
            var world = new World(new Vector3Int(16, 16, 16), 0.5f, samples => new ByteVoxelStorage(samples));
            var generator = new FlatGenerator(4.2f);
            for (int z = 0; z <= 4; z++)
            {
                for (int x = 0; x <= 4; x++)
                {
                    world.Load(new Vector3Int(x, 0, z), generator);
                    world.Load(new Vector3Int(x, 1, z), generator);
                }
            }
            PlannedSettlement camp = Settlement("camp", 1, 1, new Vector2(20f, 20f));
            SettlementSite site = SettlementLayout.Plan(camp, 32f, null, (x, z) => 4.2f, null, null, null, 0.5f);
            var street = new RoadClassDefinition { Class = RoadClass.Footpath, Width = 2.5f, Surface = "Dead Grass", GradeLength = 8f, MaxCut = 8f, Shoulder = 0.75f };
            var writer = new SettlementWriter(site, street, street);
            var materials = new SettlementWriter.Materials { Bank = stone, Stone = stone, Boards = oak, Trim = maple };

            Assert.AreEqual(1, site.Buildings.Count);
            Assert.Greater(writer.Pieces, 3, "tiles of ground, metres of street, a building");
            Assert.IsFalse(writer.IsStanding(0));
            int guard = 0;
            while (!writer.Done && guard++ < 1000)
            {
                Assert.IsTrue(writer.NextLoaded(world, 2), "its chunks are loaded");
                writer.WriteNext(world, registry, materials, (x, z) => 4.2f, 0.5f);
            }
            Assert.IsTrue(writer.Done);
            Assert.IsTrue(writer.IsStanding(0));
            SettlementBuilding shop = site.Buildings[0];
            WorldGrid grid = world.Grid;
            // Its back wall stands on its terrace, in boards.
            Vector2 back = shop.ToWorld(new Vector2(0.25f, -TownLayout.ShopDepth * 0.5f + 0.25f));
            Assert.GreaterOrEqual(world.GetDensity(grid.WorldToNearestSample(new Vector3(back.x, shop.Pad + 2f, back.y))) ?? 0f, 0.5f, "a wall");
            Assert.AreEqual(oak.Id, world.GetMaterial(grid.WorldToNearestSample(new Vector3(back.x, shop.Pad + 2f, back.y))).Value);
            // The street in front of it is surfaced.
            Vector2 before = site.Lanes[0].Points[0] * 0.5f + site.Lanes[0].Points[1] * 0.5f;
            Assert.AreEqual(dead.Id, world.GetMaterial(grid.WorldToNearestSample(new Vector3(before.x, 4f, before.y))).Value);

            var again = new SettlementWriter(site, street, street);
            again.Restore(writer.Written);
            Assert.IsTrue(again.Done, "a loaded game writes nothing again");
            again.Restore(writer.Pieces + 5);
            Assert.AreEqual(writer.Pieces, again.Written, "a count that doesn't fit is ignored");
        }
    }
}
