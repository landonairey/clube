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
            float w = building.Width * 0.5f, d = building.Depth * 0.5f;
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
                        Assert.IsTrue(end == town.Position || streets.Any(s => PolylineDistance(end, s.Points) < 0.05f), "a climb joins streets");
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
                Assert.IsTrue(site.Lanes.Any(lane => PolylineDistance(town.Position, lane.Points) < 0.05f));
            }
        }

        private static float PolylineDistance(Vector2 point, List<Vector2> points)
        {
            float best = points.Count == 1 ? Vector2.Distance(point, points[0]) : float.MaxValue;
            for (int i = 1; i < points.Count; i++)
            {
                best = Mathf.Min(best, SettlementSiteDistance(point, points[i - 1], points[i]));
            }
            return best;
        }

        [Test]
        public void TheCatalog_GivesAShopItsWorkshop_AndAHouseOneOfItsSettlementType()
        {
            // GL710, GL713, GL714.
            List<BuildingDesign> catalog = BuildingDesign.Defaults();
            var town = new SettlementType { Id = "town" };
            var camp = new SettlementType { Id = "camp" };
            Assert.AreEqual("smithy", BuildingDesign.Choose(catalog, town, "blacksmith", true, 0.5f).Id);
            Assert.AreEqual("fabric-workshop", BuildingDesign.Choose(catalog, town, "fabrics", true, 0.5f).Id);
            BuildingDesign merchant = BuildingDesign.Choose(catalog, town, "merchant", true, 0.5f);
            Assert.IsTrue(merchant.ShopFront && merchant.Specialties.Length == 0, "a shop with no workshop gets a plain one");
            for (float pick = 0f; pick <= 1f; pick += 0.05f)
            {
                BuildingDesign house = BuildingDesign.Choose(catalog, camp, null, false, pick);
                Assert.IsFalse(house.ShopFront);
                Assert.IsTrue(house.BuiltIn(camp), house.Id);
            }
            var stories = new HashSet<int>();
            for (float pick = 0f; pick <= 1f; pick += 0.02f)
            {
                stories.Add(BuildingDesign.Choose(catalog, town, null, false, pick).Stories);
            }
            Assert.IsTrue(stories.SetEquals(new[] { 1, 2, 3 }), "a town has one, two and three story houses");

            BuildingDesign smithy = catalog.Find(design => design.Id == "smithy");
            Assert.AreEqual("anvil", smithy.StationFor("hammer", 0).Kind);
            Assert.AreEqual("anvil", smithy.StationFor("hammer", 1).Kind);
            Assert.AreNotEqual(smithy.StationFor("hammer", 0).Local, smithy.StationFor("hammer", 1).Local, "two workers, two anvils");
            Assert.AreEqual("spinning-wheel", catalog.Find(design => design.Id == "fabric-workshop").StationFor("spin", 0).Kind);
        }

        [Test]
        public void ATwoStoryHouse_HasAnUpperFloorAndWindows_AndItsRoofSkinIsAtItsPitch()
        {
            // GL710, GL711.
            BuildingDesign design = BuildingDesign.Defaults().Find(row => row.Id == "two-story-house");
            List<(Vector3 Centre, TownStructures.ShopPart Part)> cells = SettlementStructures.Voxels(design, 0.8f, 0.25f);
            bool Solid(float x, float y, float z) => cells.Any(c => Vector3.Distance(c.Centre, new Vector3(x, y, z)) < 0.01f);
            float upper = TownLayout.FloorHeight + design.StoryHeight;
            Assert.IsTrue(Solid(0.125f, upper + 0.125f, 0.125f), "an upper floor");
            Assert.IsFalse(Solid(0.125f, upper + 1.2f, 0.125f), "room over it");
            Assert.IsTrue(cells.Any(c => c.Centre.y > upper + 1f && c.Centre.y < upper + 1.75f && Mathf.Abs(c.Centre.z - (design.Depth * 0.5f - 0.125f)) < 0.01f),
                "an upper front wall");

            var building = new SettlementBuilding { Design = design, Pitch = 0.8f, Centre = Vector2.zero, Pad = 10f };
            Dictionary<Vector3Int, float> skin = SettlementStructures.RoofSkin(building, 0.25f);
            Assert.IsNotEmpty(skin);
            // Where each column's density crosses a half is the roof's plane: one slant at the pitch.
            float top = TownLayout.FloorHeight + 2f * design.StoryHeight;
            foreach (float z in new[] { 0.5f, 1f, 1.5f, 2f })
            {
                float plane = 10f + SettlementStructures.RoofAt(design, 0.8f, 0.25f, z);
                int zi = Mathf.RoundToInt(z / 0.25f);
                float below = skin.TryGetValue(new Vector3Int(0, Mathf.FloorToInt(plane / 0.25f), zi), out float b) ? b : 1f;
                float above = skin.TryGetValue(new Vector3Int(0, Mathf.FloorToInt(plane / 0.25f) + 1, zi), out float a) ? a : 0f;
                Assert.GreaterOrEqual(below, 0.5f, $"under the plane at {z}");
                Assert.Less(above, 0.5f, $"over the plane at {z}");
            }
            Assert.AreEqual(10f + top + (design.Depth * 0.5f + 0.5f) * 0.8f, 10f + SettlementStructures.RoofAt(design, 0.8f, 0.25f, 0f), 0.001f);
        }

        [Test]
        public void SettlementLanes_Curve_RatherThanTurnSquare()
        {
            // GL712: a street wanders between the houses; a climb bends from one street to the next.
            PlannedSettlement town = Settlement("town", 18, 6, new Vector2(1000f, 1000f));
            SettlementSite site = SettlementLayout.Plan(town, 32f, new SettlementSiteRules(), (x, z) => 80f + 0.2f * (z - 1000f));
            SettlementLane street = site.Lanes.First(lane => lane.Kind == SettlementLaneKind.Street && lane.Points.Count > 4);
            float off = street.Points.Max(p => SettlementSiteDistance(p, street.Points[0], street.Points[street.Points.Count - 1]));
            Assert.Greater(off, 0.1f, "it isn't ruled straight");
            foreach (SettlementLane climb in site.Lanes.Where(lane => lane.Kind == SettlementLaneKind.Climb && lane.Points.Count > 2))
            {
                for (int i = 2; i < climb.Points.Count; i++)
                {
                    Vector2 one = climb.Points[i - 1] - climb.Points[i - 2], two = climb.Points[i] - climb.Points[i - 1];
                    Assert.Less(Vector2.Angle(one, two), 60f, "no sharp corner along a climb");
                }
            }
            Assert.IsTrue(site.Lanes.Any(lane => lane.Kind == SettlementLaneKind.Climb && lane.Points.Count > 2));
            Assert.IsTrue(site.Buildings.Any(b => b.Design != null && b.Design.Stories > 1), "buildings of more than one story");
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

        [Test]
        public void TheWriter_WritesItsPiecesInAnyOrderTheyAreReady_ToTheSameGround()
        {
            // GL653: a settlement larger than the loaded area is built round the viewer, so its pieces are written in
            // whatever order their ground loads; a piece only waits for the pieces it stands on.
            VoxelMaterial grass = VoxelMaterial.Create(1, "Grass"), stone = VoxelMaterial.Create(2, "Stone"), oak = VoxelMaterial.Create(3, "Oak boards"),
                maple = VoxelMaterial.Create(4, "Maple boards"), dead = VoxelMaterial.Create(5, "Dead Grass");
            made.AddRange(new Object[] { grass, stone, oak, maple, dead });
            MaterialRegistry registry = MaterialRegistry.Create(grass, stone, oak, maple, dead);
            made.Add(registry);
            var generator = new FlatGenerator(4.2f);
            World NewWorld()
            {
                var world = new World(new Vector3Int(16, 16, 16), 0.5f, samples => new ByteVoxelStorage(samples));
                for (int z = 0; z <= 4; z++)
                {
                    for (int x = 0; x <= 4; x++)
                    {
                        world.Load(new Vector3Int(x, 0, z), generator);
                        world.Load(new Vector3Int(x, 1, z), generator);
                    }
                }
                return world;
            }
            PlannedSettlement camp = Settlement("camp", 1, 1, new Vector2(20f, 20f));
            SettlementSite site = SettlementLayout.Plan(camp, 32f, null, (x, z) => 4.2f, null, null, null, 0.5f);
            var street = new RoadClassDefinition { Class = RoadClass.Footpath, Width = 2.5f, Surface = "Dead Grass", GradeLength = 8f, MaxCut = 8f, Shoulder = 0.75f };
            var materials = new SettlementWriter.Materials { Bank = stone, Stone = stone, Boards = oak, Trim = maple };

            World inOrder = NewWorld(), lastFirst = NewWorld();
            var first = new SettlementWriter(site, street, street);
            while (!first.Done)
            {
                first.WriteNext(inOrder, registry, materials, (x, z) => 4.2f, 0.5f);
            }

            var writer = new SettlementWriter(site, street, street);
            var ready = new List<int>();
            bool sawWaiting = false;
            string half = null;
            int guard = 0;
            while (!writer.Done && guard++ < 10000)
            {
                ready.Clear();
                writer.Ready(new Vector2(20f, 20f), 1000f, ready);
                Assert.Greater(ready.Count, 0, "something can always be written");
                int piece = ready[ready.Count - 1];
                Assert.IsTrue(writer.IsReady(piece));
                Assert.IsTrue(writer.Loaded(lastFirst, 2, piece));
                Assert.Greater(writer.Footprint(piece).width, 0f);
                // The last piece of all is a slice of the building: it waits for the ground under it.
                sawWaiting |= !writer.IsReady(writer.Pieces - 1) && !writer.IsWritten(writer.Pieces - 1);
                writer.Write(lastFirst, registry, materials, (x, z) => 4.2f, 0.5f, piece);
                Assert.IsTrue(writer.IsWritten(piece));
                if (writer.Written == writer.Pieces / 2)
                {
                    half = writer.Save();
                }
            }
            Assert.IsTrue(writer.Done);
            Assert.IsTrue(sawWaiting, "the building waited for its terrace");
            Assert.IsTrue(writer.IsStanding(0));

            int differing = 0;
            for (int z = 0; z < 5 * 16; z++)
            {
                for (int y = 0; y < 2 * 16; y++)
                {
                    for (int x = 0; x < 5 * 16; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        if (inOrder.GetDensity(sample) != lastFirst.GetDensity(sample) || inOrder.GetMaterial(sample) != lastFirst.GetMaterial(sample))
                        {
                            differing++;
                        }
                    }
                }
            }
            Assert.AreEqual(0, differing, "the same ground whatever the order");

            // A save keeps which pieces, not how many.
            var again = new SettlementWriter(site, street, street);
            again.Restore(half);
            Assert.AreEqual(writer.Pieces / 2, again.Written);
            again.Restore(writer.Save());
            Assert.IsTrue(again.Done);
            again.Restore("7:ff");
            Assert.IsTrue(again.Done, "a save of another settlement is ignored");
        }

        [Test]
        public void TheQuarrySettlement_HasAStoneQuarryPit_SunkBesideIt_InsideItsWall()
        {
            var rules = new SettlementSiteRules();
            PlannedSettlement village = Settlement("village", 6, 1, new Vector2(1000f, 1000f));
            village.IsQuarry = true;
            // A gentle hillside rising to the north.
            SettlementSite site = SettlementLayout.Plan(village, 32f, rules, (x, z) => 40f + (z - 1000f) * 0.05f);

            QuarryYard yard = site.Quarry;
            Assert.IsNotNull(yard, "GL734: the quarry pit");
            Assert.AreEqual(CompanyWork.Yard.size, yard.Area.size, "the tutorial's yard fits its floor");
            SettlementTerrace floor = site.Terraces[yard.Terrace];
            Assert.AreEqual(yard.Area, floor.Area);
            Assert.AreEqual(yard.Floor, floor.Height);
            Assert.That(yard.Floor, Is.LessThanOrEqualTo(floor.NaturalLow - rules.QuarryDepth + 0.13f), "sunk under the land round it");
            Assert.AreEqual(yard.Floor, site.HeightAt(yard.Area.center.x, yard.Area.center.y), 1e-3f);
            // Beside the buildings, not under them.
            foreach (SettlementBuilding building in site.Buildings)
            {
                foreach (Vector2 corner in Corners(building))
                {
                    Assert.Greater(SettlementSite.Beyond(yard.Area, corner), rules.Ramp, "clear of the buildings");
                }
            }
            // The wall rings the settlement and the pit.
            Assert.AreEqual(5, site.Wall.Count, "a closed ring");
            Rect ring = Rect.MinMaxRect(site.Wall.Min(p => p.x), site.Wall.Min(p => p.y), site.Wall.Max(p => p.x), site.Wall.Max(p => p.y));
            Assert.That(ring.Contains(yard.Area.min) && ring.Contains(yard.Area.max), "the pit is inside the wall");
            // The tutorial's map space lands on the floor.
            Vector3 spawn = yard.ToWorld(new Vector3(CompanyWork.Yard.xMin, CompanyWork.Ground, CompanyWork.Yard.yMin));
            Assert.AreEqual(new Vector3(yard.Area.xMin, yard.Floor, yard.Area.yMin), spawn);

            PlannedSettlement plain = Settlement("village", 6, 2, new Vector2(1000f, 1000f));
            SettlementSite plainSite = SettlementLayout.Plan(plain, 32f, rules, (x, z) => 40f);
            Assert.IsNull(plainSite.Quarry);
            Assert.IsEmpty(plainSite.Wall, "only the quarry village is walled");
        }

        [Test]
        public void TheQuarryPit_KeepsClearOfARoadRunningBetweenItsSamples()
        {
            // GL739: a narrow road between the survey's rows left a ridge of uncut ground across the floor.
            var rules = new SettlementSiteRules();
            PlannedSettlement village = Settlement("village", 6, 1, new Vector2(1000f, 1000f));
            village.IsQuarry = true;
            SettlementSite open = SettlementLayout.Plan(village, 32f, rules, (x, z) => 40f);
            float between = open.Quarry.Area.center.y + 2f - Mathf.Repeat(open.Quarry.Area.height * 0.5f + rules.Ramp, 4f);
            var roads = new List<SettlementRoad>
            {
                new SettlementRoad { Points = new[] { new Vector2(700f, between), new Vector2(1300f, between) }, Reach = 1.5f, HalfWidth = 1f },
            };

            PlannedSettlement again = Settlement("village", 6, 1, new Vector2(1000f, 1000f));
            again.IsQuarry = true;
            SettlementSite site = SettlementLayout.Plan(again, 32f, rules, (x, z) => 40f, roads: roads);

            Assert.IsNotNull(site.Quarry);
            for (float x = site.Quarry.Area.xMin; x <= site.Quarry.Area.xMax; x += 1f)
            {
                for (float z = site.Quarry.Area.yMin; z <= site.Quarry.Area.yMax; z += 0.5f)
                {
                    Assert.Greater(site.RoadBeyond(x, z), rules.RoadBlend, "no road shapes the floor");
                    Assert.AreEqual(site.Quarry.Floor, site.HeightAt(x, z), 1e-3f, "the floor is level everywhere");
                }
            }
        }

        [Test]
        public void TheCapitalHasACastleAndAGuardTower_InsideATownWall_AndACityAGuardTower()
        {
            var rules = new SettlementSiteRules();
            PlannedSettlement capital = Settlement("capital", 28, 8, new Vector2(1000f, 1000f));
            SettlementSite site = SettlementLayout.Plan(capital, 32f, rules, (x, z) => 40f);

            Assert.AreEqual("castle", site.Buildings[8].Design.Id, "the first place after the shops");
            Assert.AreEqual("guard-tower", site.Buildings[9].Design.Id);
            Assert.AreEqual(1, site.Buildings.Count(b => b.Design.Landmark && b.Design.Id == "castle"));
            Assert.AreEqual(5, site.Wall.Count, "a closed ring of four corners");
            Rect bounds = site.Bounds;
            foreach (Vector2 corner in site.Wall)
            {
                Assert.IsFalse(bounds.Contains(corner), "the wall stands outside everything the settlement changes");
            }

            PlannedSettlement city = Settlement("city", 20, 5, new Vector2(1000f, 1000f));
            SettlementSite citySite = SettlementLayout.Plan(city, 32f, rules, (x, z) => 40f);
            Assert.AreEqual("guard-tower", citySite.Buildings[5].Design.Id);
            Assert.IsEmpty(citySite.Wall, "only the capital is walled");

            // The catalog never picks a landmark for an ordinary house.
            for (int i = 0; i < 50; i++)
            {
                Assert.IsFalse(BuildingDesign.Choose(rules.Buildings, capital.Type, null, false, i / 50f).Landmark);
            }
        }

        [Test]
        public void ACastleIsStoneWithBattlementsAndTurrets_AndHasNoSlantedRoofSkin()
        {
            BuildingDesign castle = BuildingDesign.Find(BuildingDesign.Defaults(), "castle");
            var cells = SettlementStructures.Voxels(castle, 0.5f, 0.25f);
            float top = TownLayout.FloorHeight + castle.Stories * castle.StoryHeight;
            Assert.IsTrue(cells.Where(c => Mathf.Abs(c.Centre.x) > castle.Width * 0.5f - 0.25f && c.Centre.y > 1f && c.Centre.y < top)
                .All(c => c.Part == TownStructures.ShopPart.Stone), "the walls are stone through");
            float highest = cells.Max(c => c.Centre.y);
            Assert.Greater(highest, top + BuildingDesign.ParapetHeight + 1f, "turrets rise over the parapet");
            // The parapet has gaps between its merlons.
            int merlonTops = cells.Count(c => Mathf.Abs(c.Centre.y - (top + BuildingDesign.ParapetHeight - 0.125f)) < 0.01f && Mathf.Abs(c.Centre.x) < 1f);
            Assert.Greater(merlonTops, 0);
            Assert.IsEmpty(SettlementStructures.RoofSkin(new SettlementBuilding { Design = castle }, 0.25f));
        }

        [Test]
        public void TheTownWall_FollowsTheGround_AndLeavesAGateWhereARoadComesIn()
        {
            var road = new SettlementRoad { Points = new List<Vector2> { new Vector2(1000f, 700f), new Vector2(1000f, 1300f) }, Reach = 3f, HalfWidth = 2.5f };
            PlannedSettlement capital = Settlement("capital", 28, 8, new Vector2(1000f, 1000f));
            SettlementSite site = SettlementLayout.Plan(capital, 32f, new SettlementSiteRules(), (x, z) => 40f + x * 0.01f, roads: new[] { road });
            Assert.AreEqual(5, site.Wall.Count);
            Assert.IsTrue(SettlementWalls.IsGate(site, new Vector2(1000f, site.Wall[0].y)), "a gateway where the road crosses");
            Assert.IsFalse(SettlementWalls.IsGate(site, new Vector2(site.Wall[0].x + 2f, site.Wall[0].y)));

            List<SettlementWalls.Stretch> stretches = SettlementWalls.Stretches(site);
            Assert.AreEqual(4, stretches.Count(s => s.Corner));
            Dictionary<Vector3Int, byte> blocks = SettlementWalls.Blocks(site, stretches[0], 0.25f, 7);
            Assert.IsNotEmpty(blocks);
            Assert.IsTrue(blocks.Values.All(m => m == 7));
            // Its top stands the wall's height over the ground under it.
            Vector3Int highest = blocks.Keys.OrderByDescending(k => k.y).First();
            float ground = site.HeightAt(highest.x * 0.25f, highest.z * 0.25f);
            Assert.AreEqual(ground + site.Rules.WallHeight + site.Rules.WallTowerRise, (highest.y + 1) * 0.25f, 0.3f, "the corner tower's top");
        }

        [Test]
        public void SettlementLights_TorchesForTheSmall_BonfiresForTheMiddling_LanternsForTheLarge()
        {
            var rules = new SettlementSiteRules();
            SettlementSite camp = SettlementLayout.Plan(Settlement("camp", 1, 1, new Vector2(1000f, 1000f)), 32f, rules, (x, z) => 40f);
            Assert.AreEqual(2, camp.Lights.Count, "a couple of torches");
            Assert.IsTrue(camp.Lights.All(l => l.Kind.Id == "torch"));

            SettlementSite town = SettlementLayout.Plan(Settlement("town", 12, 3, new Vector2(1000f, 1000f)), 32f, rules, (x, z) => 40f);
            Assert.IsTrue(town.Lights.Any(l => l.Kind.Id == "torch"));
            Assert.IsTrue(town.Lights.Any(l => l.Kind.Id == "bonfire"), "a 2 x 2 m bonfire");
            Assert.AreEqual(2f, town.Lights.First(l => l.Kind.Id == "bonfire").Kind.Size);

            SettlementSite city = SettlementLayout.Plan(Settlement("city", 20, 5, new Vector2(1000f, 1000f)), 32f, rules, (x, z) => 40f);
            Assert.Greater(city.Lights.Count, city.Buildings.Count / 2);
            Assert.IsTrue(city.Lights.All(l => l.Kind.Id == "lantern"));

            // Each stands clear of every building and of every other light.
            foreach (SettlementSite site in new[] { camp, town, city })
            {
                foreach (SettlementLight light in site.Lights)
                {
                    foreach (SettlementBuilding building in site.Buildings)
                    {
                        Vector2 local = Quaternion.Euler(0f, 0f, building.Yaw) * (light.Position - building.Centre);
                        Assert.IsFalse(Mathf.Abs(local.x) < building.Width * 0.5f && Mathf.Abs(local.y) < building.Depth * 0.5f, "not inside a building");
                    }
                    Assert.IsTrue(site.Lights.All(o => o == light || Vector2.Distance(o.Position, light.Position) > 2f));
                }
            }
        }

        [Test]
        public void WallInsideFollowsTheRing()
        {
            var ring = new List<Vector2> { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 6), new Vector2(0, 6), new Vector2(0, 0) };
            Assert.IsTrue(SettlementWalls.Inside(ring, new Vector2(5, 3)));
            Assert.IsTrue(SettlementWalls.Inside(ring, new Vector2(9.9f, 0.1f)));
            Assert.IsFalse(SettlementWalls.Inside(ring, new Vector2(10.5f, 3)));
            Assert.IsFalse(SettlementWalls.Inside(ring, new Vector2(5, -1)));
            Assert.IsFalse(SettlementWalls.Inside(new List<Vector2>(), new Vector2(5, 3)));
        }
    }
}
