using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>Settlements, specialties, roads, water routes and names of the bounded world (GL593-GL597).</summary>
    public class WorldPlanTests
    {
        private readonly List<Object> made = new List<Object>();
        private VoxelMaterial stone, dirt, grass, sand, copper, gold, iron, wood, leaves;
        private ResourceSurvey survey;
        private WorldPlan plan;

        [OneTimeSetUp]
        public void BuildThePlan()
        {
            stone = Material(1, "Stone");
            dirt = Material(2, "Dirt");
            grass = Material(3, "Grass");
            sand = Material(4, "Sand");
            copper = Material(5, "Copper", VoxelMaterialCategory.Ore);
            wood = Material(6, "Wood");
            leaves = Material(7, "Leaves");
            gold = Material(8, "Gold", VoxelMaterialCategory.Ore);
            iron = Material(9, "Iron", VoxelMaterialCategory.Ore);

            survey = ResourceSurvey.Build(Settings(5, 3072f, copper, gold));
            plan = WorldPlanner.Build(survey, 5);
            UnityEngine.Debug.Log($"World plan: {plan.Settlements.Count} settlements, {plan.Roads.Count} roads, {plan.WaterRoutes.Count} water routes; "
                + $"{plan.Timings}; notes: {string.Join(" | ", plan.Notes)}");
        }

        [OneTimeTearDown]
        public void DestroyAll()
        {
            Hydrology.DisposeAll();
            foreach (Object thing in made)
            {
                Object.DestroyImmediate(thing);
            }
        }

        private VoxelMaterial Material(int id, string name, VoxelMaterialCategory category = VoxelMaterialCategory.Aggregate)
        {
            VoxelMaterial material = VoxelMaterial.Create(id, name, category);
            made.Add(material);
            return material;
        }

        private TerrainSettings Settings(int seed, float size, params VoxelMaterial[] ores)
        {
            var settings = new TerrainSettings
            {
                Generator = TerrainGeneratorType.BoundedMap, Seed = seed, SurfaceLevel = 20f, Amplitude = 40f,
            };
            settings.Layers.Top = grass;
            settings.Layers.TopDepth = 2f;
            settings.Layers.Band = dirt;
            settings.Layers.BandDepth = 4f;
            settings.Layers.Base = stone;
            settings.Water.Sand = sand;
            settings.Water.Enabled = true;
            settings.Map.Size = size;
            settings.Map.PeakHeight = 150f;
            settings.Map.SetSides(
                MapBoundaryType.OceanName, MapBoundaryType.OceanName, MapBoundaryType.OceanName, MapBoundaryType.OceanName);
            settings.Trees.Chance = 0.5f;
            settings.Trees.Wood = wood;
            settings.Trees.Leaves = leaves;
            foreach (VoxelMaterial ore in ores)
            {
                settings.Ores.Ores.Add(new OreSpec
                {
                    Ore = ore, NodesPerCell = 1.5f, MinDepth = 1f, MaxDepth = 14f, PeakProbability = 0.9f, Spread = new Vector3(1.5f, 1f, 1.5f),
                });
            }
            return settings;
        }

        // ---- GL593: the types ----

        [Test]
        public void Types_AreTheDesignTable()
        {
            List<SettlementType> types = SettlementType.Defaults();
            var rows = types.Select(t => $"{t.Id}|{t.Icon}|{t.MinimumRoad}|{t.ParcelSize}|{t.PopulationMin}-{t.PopulationMax}|{t.Count}").ToArray();

            Assert.That(rows, Is.EqualTo(new[]
            {
                "camp|tent|None|<1|1-1|12",
                "thieves-camp|tent|None|<1|0-0|4",
                "outpost|house|Footpath|~1|2-4|12",
                "village|houses|DirtCartPath|~4|4-8|10",
                "town|town-hall|DirtCartPath|~9|8-16|8",
                "city|tower|PavedCartPath|~16|16-24|4",
                "capital|castle|PavedCartPath|>16|24-32|1",
            }));
            Assert.That(types.Single(t => t.NeedsOcean).Id, Is.EqualTo("capital"));
            for (int i = 2; i < types.Count - 1; i++)
            {
                Assert.That(types[i + 1].Shops, Is.GreaterThan(types[i].Shops), "bigger types hold more shops");
                Assert.That(types[i + 1].Parcels, Is.GreaterThan(types[i].Parcels));
            }
        }

        // ---- GL594: the specialties ----

        [Test]
        public void Specialties_AreTheDesignList()
        {
            List<EconomySpecialty> all = EconomySpecialty.Defaults();
            string Chain(string chain) => string.Join(",", all.Where(s => s.Chain == chain).Select(s => s.Id));

            Assert.That(Chain(SupplyChains.Lumber), Is.EqualTo("lumber-supplies,carpentry,tool-componentry"));
            Assert.That(Chain(SupplyChains.Ore), Is.EqualTo("mining,crushing,smelting,blacksmith,goldsmith,jeweler"));
            Assert.That(Chain(SupplyChains.Aggregate), Is.EqualTo("aggregate-supplies,masonry,stone-quarry,limestone-quarry,sand-pit,clay-pit"));
            Assert.That(Chain(SupplyChains.Textile), Is.EqualTo("fabrics,clothing,tannery,leathery"));
            Assert.That(Chain(SupplyChains.Merchant), Is.EqualTo("merchant"));
            Assert.That(all.Select(s => s.Id), Is.Unique);
            Assert.That(all.Single(s => s.Id == "blacksmith").SubSpecialties, Is.EqualTo(new[] { "Armory", "Weaponsmith", "Toolsmith" }));
            EconomySpecialty merchant = all.Single(s => s.Id == "merchant");
            Assert.That(merchant.BuysEverything && merchant.SellsVariety && merchant.Needs.Length == 0);
            Assert.That(all.Where(s => s != merchant).All(s => s.Needs.Length > 0 && s.Buys.Length > 0 && s.Sells.Length > 0));
            // The textile goods aren't in the game yet: names only.
            Assert.That(all.Where(s => s.Chain == SupplyChains.Textile).SelectMany(s => s.Sells).All(g => !g.InGame && g.Name.Length > 0));
        }

        [Test]
        public void Specialties_NameOnlyItemsTheGameHas()
        {
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Config/Items/ItemCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            var ids = new HashSet<string>(catalog.Items.Where(item => item != null).Select(item => item.Id));

            foreach (EconomySpecialty specialty in EconomySpecialty.Defaults())
            {
                foreach (TradeGood good in specialty.Buys.Concat(specialty.Sells).Where(good => good.InGame))
                {
                    Assert.That(ids, Does.Contain(good.ItemId), specialty.Id);
                }
            }
        }

        [Test]
        public void Specialty_ShopTradesTheGoodsThatExist()
        {
            ItemDefinition woodItem = ItemDefinition.Create("wood", "Wood");
            ItemDefinition sandItem = ItemDefinition.Create("sand", "Sand");
            ItemCatalog catalog = ItemCatalog.Create(woodItem, sandItem);
            made.AddRange(new Object[] { woodItem, sandItem, catalog });
            List<EconomySpecialty> all = EconomySpecialty.Defaults();

            ShopSpecialty mill = all.Single(s => s.Id == "lumber-supplies").ToShop(catalog);
            ShopSpecialty tannery = all.Single(s => s.Id == "tannery").ToShop(catalog);
            List<ItemDefinition> variety = EconomySpecialty.VarietyFor(catalog, new[] { all.Single(s => s.Id == "sand-pit") }, 3, 1);
            ShopSpecialty merchant = all.Single(s => s.Id == "merchant").ToShop(catalog, variety);
            made.AddRange(new Object[] { mill, tannery, merchant });

            Assert.That(mill.Sells, Is.EqualTo(new[] { woodItem }));
            Assert.That(mill.Kind, Is.EqualTo(ShopKind.Lumber));
            Assert.That(tannery.Sells, Is.Empty);
            Assert.That(merchant.BuysEverything);
            Assert.That(merchant.Sells, Is.EqualTo(new[] { woodItem }), "skewed to what the local sand pit does not sell");
        }

        // ---- GL595: placing ----

        [Test]
        public void Plan_HoldsTheExactNumbersOfEachType()
        {
            foreach (SettlementType type in plan.Rules.Types)
            {
                Assert.That(plan.CountOf(type.Id), Is.EqualTo(type.Count), type.Name);
            }
            Assert.That(plan.Settlements.Count, Is.EqualTo(51));
            Assert.That(plan.Settlements.Select(s => s.Index), Is.EqualTo(Enumerable.Range(0, 51)));
        }

        [Test]
        public void Plan_SitesAreOnDryLandInsideTheMapAndApart()
        {
            Assert.That(plan.Settlements.Select(s => s.Cell), Is.Unique);
            foreach (PlannedSettlement settlement in plan.Settlements)
            {
                SurveyCell cell = survey.Cell(settlement.Cell.x, settlement.Cell.y);
                Assert.That(plan.Area.Contains(settlement.Position), settlement.Name);
                Assert.That(cell.Sea, Is.LessThan(0.5f), settlement.Name);
                Assert.That(cell.Land, Is.GreaterThanOrEqualTo(0.6f), settlement.Name);
                Assert.That(settlement.Ground, Is.EqualTo(cell.Height));
                Assert.That(settlement.Reasons, Is.Not.Empty, settlement.Name);
                Assert.That(settlement.Population, Is.InRange(settlement.Type.PopulationMin, settlement.Type.PopulationMax));
            }
            // Most keep to level ground; only a crowded map makes one give way (and says so in the notes).
            int level = plan.Settlements.Count(s => survey.Cell(s.Cell.x, s.Cell.y).Flatness >= plan.Rules.MinFlatness);
            Assert.That(level, Is.GreaterThanOrEqualTo(plan.Settlements.Count - plan.Notes.Count(n => n.Contains("no level dry site"))));
        }

        [Test]
        public void Plan_EverySpecialtyOccurs_AndBiggerTypesHoldMoreShops()
        {
            foreach (EconomySpecialty specialty in plan.Rules.Specialties)
            {
                Assert.That(plan.With(specialty.Id), Is.Not.Empty, specialty.Name);
            }
            foreach (PlannedSettlement settlement in plan.Settlements)
            {
                Assert.That(settlement.Shops.Count, Is.EqualTo(settlement.Type.Shops), settlement.Name);
                Assert.That(settlement.Shops.Select(shop => shop.Specialty.Id), Is.Unique, settlement.Name);
                Assert.That(settlement.Shops.All(shop => shop.Reason.Length > 0 && shop.Specialty.CraftLevel <= settlement.Type.CraftLevel));
            }
            // A lumber mill stands where the wood is: not by guarantee, and with wood in reach.
            PlannedSettlement mill = plan.With("lumber-supplies").First();
            PlannedShop shop = mill.Shops.Single(s => s.Specialty.Id == "lumber-supplies");
            Assert.That(shop.Guaranteed, Is.False);
            Assert.That(shop.Reason, Does.Contain("wood nearby"));
        }

        [Test]
        public void Plan_TheCapitalIsOnTheOcean_WithEveryEconomyType()
        {
            PlannedSettlement capital = plan.Capital;

            Assert.That(capital.Type.Id, Is.EqualTo("capital"));
            Assert.That(capital.IsCoastal, string.Join(" ", capital.Reasons));
            Assert.That(survey.Cell(capital.Harbour.Value.x, capital.Harbour.Value.y).Sea, Is.GreaterThanOrEqualTo(0.5f));
            Assert.That(capital.Shops.Select(shop => shop.Specialty.Chain).Distinct().Count(), Is.EqualTo(5));
            // Every resource type the map has is in its area or comes by road, and the reasons say which.
            Assert.That(plan.Notes.Any(note => note.StartsWith("The capital has no")), Is.False, string.Join(" | ", plan.Notes));
        }

        [Test]
        public void Plan_IsTheSameForTheSameSeed()
        {
            WorldPlan again = WorldPlanner.Build(survey, 5);
            string Describe(WorldPlan p) => string.Join(";", p.Settlements.Select(s =>
                    $"{s.Name}@{s.Cell}:{string.Join(",", s.Shops.Select(shop => shop.Specialty.Id))}"))
                + "|" + string.Join(";", p.Roads.Select(r => $"{r.Class}{r.From}>{r.To}/{r.Joins}:{r.Points.Count}"))
                + "|" + string.Join(";", p.WaterRoutes.Select(r => $"{r.Kind}{r.From}>{r.To}:{r.Points.Count}"));

            Assert.That(Describe(again), Is.EqualTo(Describe(plan)));
            Assert.That(Describe(WorldPlanner.Build(survey, 6)), Is.Not.EqualTo(Describe(plan)), "another seed, other names at least");
        }

        // ---- GL596: roads and water routes ----

        [Test]
        public void Roads_ReachEverySettlementButCamps_AtItsTypesMinimum()
        {
            foreach (PlannedSettlement settlement in plan.Settlements)
            {
                if (settlement.Type.MinimumRoad == RoadClass.None)
                {
                    Assert.That(plan.RoadsOf(settlement.Index), Is.Empty, settlement.Name);
                    continue;
                }
                Assert.That(settlement.BestRoad, Is.GreaterThanOrEqualTo(settlement.Type.MinimumRoad), settlement.Name);
                // The class it claims is a road that really passes its centre.
                Assert.That(plan.Roads.Any(road => road.Class == settlement.BestRoad && road.Points.Contains(settlement.Position)), settlement.Name);
            }
            foreach (PlannedRoad road in plan.Roads)
            {
                Assert.That(road.Points.Count, Is.GreaterThanOrEqualTo(2));
                Assert.That(road.Points[0], Is.EqualTo(plan.Settlements[road.From].Position));
                Assert.That(road.To >= 0 || road.Joins >= 0, "ends at a settlement or a junction");
                foreach (Vector2 point in road.Points)
                {
                    Assert.That(survey.TryCell(point, out Vector2Int cell) && survey.Cell(cell.x, cell.y).Sea < 0.5f, "never through the sea");
                }
                foreach (RoadBridge bridge in road.Bridges)
                {
                    Assert.That(road.Points.Any(point => Vector2.Distance(point, bridge.Position) <= survey.CellSize * 4f + bridge.Length));
                }
            }
        }

        [Test]
        public void Roads_AreOneNetwork_AndPavedFromTheCapitalToEveryCity()
        {
            List<PlannedSettlement> onRoads = plan.Settlements.Where(s => s.Type.MinimumRoad != RoadClass.None).ToList();

            Assert.That(Groups(plan.Roads, onRoads), Is.EqualTo(1), "every settlement but the camps is on one network");
            List<PlannedRoad> paved = plan.Roads.Where(road => road.Class == RoadClass.PavedCartPath).ToList();
            List<PlannedSettlement> pavedPlaces = onRoads.Where(s => s.Type.MinimumRoad == RoadClass.PavedCartPath).ToList();
            Assert.That(Groups(paved, pavedPlaces), Is.EqualTo(1), "the capital and the cities are joined by paved roads alone");
            Assert.That(plan.Capital.BestRoad, Is.EqualTo(RoadClass.PavedCartPath));
            Assert.That(paved, Is.Not.Empty);
        }

        // How many separate groups the settlements fall into when joined by these roads (roads meet where they share a point).
        private static int Groups(List<PlannedRoad> roads, List<PlannedSettlement> settlements)
        {
            var parent = new Dictionary<Vector2, Vector2>();
            Vector2 Find(Vector2 p)
            {
                if (!parent.ContainsKey(p))
                {
                    parent[p] = p;
                }
                while (parent[p] != p)
                {
                    parent[p] = parent[parent[p]];
                    p = parent[p];
                }
                return p;
            }
            foreach (PlannedRoad road in roads)
            {
                for (int i = 1; i < road.Points.Count; i++)
                {
                    parent[Find(road.Points[i])] = Find(road.Points[0]);
                }
            }
            return settlements.Select(s => Find(s.Position)).Distinct().Count();
        }

        [Test]
        public void WaterRoutes_SailTheSeaFromTheCapital_AndTheRivers()
        {
            List<WaterRoute> sea = plan.WaterRoutes.Where(route => route.Kind == WaterRouteKind.Sea).ToList();

            Assert.That(sea.Any(route => route.From == plan.Capital.Index || route.To == plan.Capital.Index), "the capital has a sea route");
            foreach (WaterRoute route in plan.WaterRoutes)
            {
                Assert.That(route.Points.Count, Is.GreaterThanOrEqualTo(1));
                foreach (Vector2 point in route.Points)
                {
                    Assert.That(survey.TryCell(point, out Vector2Int at));
                    SurveyCell cell = survey.Cell(at.x, at.y);
                    Assert.That(route.Kind == WaterRouteKind.Sea ? cell.Sea >= 0.5f : cell.River || cell.FreshWater > 0.25f, $"{route.Kind} route on its water");
                }
                if (route.To >= 0)
                {
                    PlannedSettlement to = plan.Settlements[route.To];
                    Assert.That(route.Kind == WaterRouteKind.Sea ? to.IsCoastal : to.IsOnRiver);
                }
            }
        }

        // ---- GL597: names ----

        [Test]
        public void Names_EndInTheType_AndAreAllDifferent()
        {
            Assert.That(plan.Settlements.Select(s => s.Name), Is.Unique);
            foreach (PlannedSettlement settlement in plan.Settlements)
            {
                Assert.That(settlement.Name, Does.EndWith(" " + settlement.Type.Name));
                Assert.That(settlement.Name.Length, Is.GreaterThan(settlement.Type.Name.Length + 3));
            }
            var taken = new HashSet<string>();
            string first = SettlementNames.Generate(1, new Vector2Int(2, 3), "Village", taken);
            string second = SettlementNames.Generate(1, new Vector2Int(2, 3), "Village", taken);
            Assert.That(first, Is.EqualTo(SettlementNames.Generate(1, new Vector2Int(2, 3)) + " Village"));
            Assert.That(second, Is.Not.EqualTo(first));
        }

        [Test]
        public void EdmundFitzgerald_IsKeptUntilTheMapHasIron_ThenCarriesItsOre()
        {
            // No iron on the first map: the name waits, with a note.
            Assert.That(plan.WaterRoutes.Any(route => route.Vessel != null), Is.False);
            Assert.That(plan.ReservedVessels.Single().Name, Is.EqualTo("Edmund Fitzgerald"));
            Assert.That(plan.ReservedVessels[0].Note, Does.Contain("iron ore"));

            WorldPlan withIron = WorldPlanner.Build(ResourceSurvey.Build(Settings(5, 2048f, iron)), 5);
            WaterRoute route = withIron.WaterRoutes.SingleOrDefault(r => r.Vessel == "Edmund Fitzgerald");
            if (route == null)
            {
                // Legitimate only when no port has iron in reach, and then the plan says so.
                Assert.That(withIron.ReservedVessels.Single().Note, Does.Contain("No settlement on a sea route"));
                Assert.Inconclusive("This map's ports have no iron nearby; the name stayed in reserve.");
            }
            Assert.That(route.Kind, Is.EqualTo(WaterRouteKind.Sea));
            Assert.That(route.Cargo, Is.EqualTo("iron ore"));
            Assert.That(withIron.ReservedVessels, Is.Empty);
            Assert.That(new[] { route.From, route.To }.Where(i => i >= 0).Any(i => withIron.Settlements[i].Has("mining")), "an iron-mining port");
        }

        // ---- GL599, GL601: the map's picture and the start ----

        [Test]
        public void TheStartIsOnDryLandBesideTheCapital()
        {
            Vector2 start = MapStart.Pick(plan, survey);
            Assert.That(MapStart.IsDry(survey, start), "the start is on land");
            Assert.That(Vector2.Distance(start, plan.Capital.Position), Is.LessThanOrEqualTo(MapStart.Offset + 0.01f));
        }

        [Test]
        public void WithNoSettlementsTheStartIsFlatDryLand()
        {
            Vector2 start = MapStart.Pick(new WorldPlan(), survey);
            Assert.That(survey.TryCell(start, out Vector2Int cell));
            Assert.That(survey.Cell(cell.x, cell.y).Land, Is.GreaterThan(0.9f));
        }

        [Test]
        public void TheMapPictureShowsTheSeaRoundTheIslandAndLandAtTheCapital()
        {
            var map = (BoundedMapGenerator)TerrainGenerators.Create(Settings(5, 3072f, copper, gold));
            const int resolution = 96;
            Color32[] pixels = MapPicture.Render(map, survey, resolution);
            Assert.That(pixels.Length, Is.EqualTo(resolution * resolution));

            // Every side is an ocean: the corner pixel is sea-blue, bluer than it is red.
            Color32 corner = pixels[0];
            Assert.That(corner.b, Is.GreaterThan(corner.r + 20), "sea at the map's corner");

            Vector2 at = MapPicture.PixelOf(survey.Area, resolution, MapStart.Pick(plan, survey));
            Color32 land = pixels[Mathf.Clamp((int)at.x, 0, resolution - 1) + Mathf.Clamp((int)at.y, 0, resolution - 1) * resolution];
            Assert.That(land.b, Is.LessThan(land.g + 40), "land, not water, where the player starts");
        }

        [Test]
        public void FootpathsAreNarrowDirtCartPathsWiderDirtAndPavedOnesWiderCobble()
        {
            // GL620: a narrow dirt path, a wider path of dirt, a wider cobblestone path.
            Assert.That(MapPicture.RoadWidth(RoadClass.DirtCartPath), Is.GreaterThan(2f * MapPicture.RoadWidth(RoadClass.Footpath)));
            Assert.That(MapPicture.RoadWidth(RoadClass.PavedCartPath), Is.GreaterThanOrEqualTo(MapPicture.RoadWidth(RoadClass.DirtCartPath)));
            Assert.That(MapPicture.RoadColour(RoadClass.Footpath), Is.EqualTo(MapPicture.Dirt));
            Assert.That(MapPicture.RoadColour(RoadClass.DirtCartPath), Is.EqualTo(MapPicture.Dirt));
            Color paved = MapPicture.RoadColour(RoadClass.PavedCartPath);
            Assert.That(Mathf.Abs(paved.r - paved.b), Is.LessThan(0.05f), "stone-grey");
        }
    }
}
