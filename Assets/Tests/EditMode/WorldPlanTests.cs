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
            // The iron ore by its real name (GL621): the plan knows iron by its ores (hematite, magnetite).
            iron = Material(9, "Magnetite", VoxelMaterialCategory.Ore);

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
            settings.Map.VoxelFloor = -40f;
            settings.Map.SetShape(MapShapeType.IslandName);
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
            // What the textile shops make isn't in the game yet (names only); the hides the tannery passes on are (GL603).
            Assert.That(all.Where(s => s.Chain == SupplyChains.Textile).SelectMany(s => s.Sells)
                .All(g => g.InGame ? g.ItemId.EndsWith("-hide") : g.Name.Length > 0));
            Assert.That(all.Where(s => s.Chain == SupplyChains.Textile).SelectMany(s => s.Sells).Count(g => !g.InGame), Is.GreaterThan(4));
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
            // All keep to ground that is level or can be terraced (GL642); only a crowded map makes one give way (and says so in the notes).
            int level = plan.Settlements.Count(s => survey.Cell(s.Cell.x, s.Cell.y).Flatness >= plan.Rules.TerraceFlatness);
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
                if (settlement.RoadNetwork > 0 && plan.RoadsOf(settlement.Index).Count == 0)
                {
                    // GL643: the first of a cut-off land needs no road, and the plan says how it is reached.
                    Assert.That(plan.Notes.Any(note => note.StartsWith(settlement.Name) && note.Contains("reached by water")), settlement.Name);
                    continue;
                }
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

            // GL643: one network on each land; the capital's is number 0.
            Assert.That(Groups(plan.Roads, onRoads), Is.EqualTo(onRoads.Select(s => s.RoadNetwork).Distinct().Count()), "one network a land");
            Assert.That(Groups(plan.Roads, onRoads.Where(s => s.RoadNetwork == 0).ToList()), Is.EqualTo(1), "the capital's network is one");
            Assert.That(onRoads.Where(s => s.RoadNetwork != 0).All(s => s.Land != plan.Capital.Land), "only other lands are cut off");
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

        // ---- GL643: settlements on every land with resources ----

        [Test]
        public void EveryIslandOfAChainHasASettlement_ReachedBySeaNotByRoad()
        {
            TerrainSettings settings = Settings(6, 3072f, copper, gold);
            settings.Map.SetShape(MapShapeType.IslandChainName);
            ResourceSurvey chain = ResourceSurvey.Build(settings);
            WorldPlan world = WorldPlanner.Build(chain, settings.Seed);
            UnityEngine.Debug.Log($"Island chain: lands with settlements {string.Join(", ", world.Settlements.GroupBy(s => s.Land).Select(g => $"{g.Key}: {g.Count()}"))}; "
                + $"networks {world.Settlements.Select(s => s.RoadNetwork).Distinct().Count()}; notes: {string.Join(" | ", world.Notes)}");

            // The pieces of land, as the planner sees them: cells that are not sea, joined through their eight neighbours.
            int size = chain.Size;
            var land = new int[size * size];
            var counts = new List<int> { 0 };
            for (int i = 0; i < land.Length; i++)
            {
                if (land[i] != 0 || chain.Cell(i % size, i / size).Sea >= 0.5f)
                {
                    continue;
                }
                var open = new Stack<int>();
                open.Push(i);
                land[i] = counts.Count;
                int count = 0;
                while (open.Count > 0)
                {
                    int cell = open.Pop();
                    count++;
                    for (int n = 0; n < 9; n++)
                    {
                        int x = cell % size + n % 3 - 1, z = cell / size + n / 3 - 1;
                        if (x >= 0 && z >= 0 && x < size && z < size && land[x + z * size] == 0 && chain.Cell(x, z).Sea < 0.5f)
                        {
                            land[x + z * size] = counts.Count;
                            open.Push(x + z * size);
                        }
                    }
                }
                counts.Add(count);
            }
            PlannedSettlement capital = world.Capital;
            Assert.That(counts[land[capital.Cell.x + capital.Cell.y * size]], Is.EqualTo(counts.Max()), "the capital stands on the largest land");
            Assert.That(world.Settlements.Select(s => s.Land).Distinct().Count(), Is.GreaterThan(1), "not only the largest land is settled");
            for (int id = 1; id < counts.Count; id++)
            {
                // A land of a tenth of a square kilometre and more has wood or stone at least: it has resources.
                if (counts[id] >= 60)
                {
                    Assert.That(world.Settlements.Any(s => land[s.Cell.x + s.Cell.y * size] == id), $"land {id} of {counts[id]} cells has a settlement");
                }
            }
            foreach (PlannedSettlement settlement in world.Settlements)
            {
                Assert.That(settlement.Land, Is.EqualTo(land[settlement.Cell.x + settlement.Cell.y * size]), settlement.Name);
                if (settlement.Land == capital.Land)
                {
                    continue;
                }
                // Cut off: no road leads off its land, and its own land's roads are a network apart.
                Assert.That(settlement.RoadNetwork, Is.Not.EqualTo(0), settlement.Name);
                foreach (PlannedRoad road in world.RoadsOf(settlement.Index))
                {
                    Assert.That(road.Points.All(p => chain.TryCell(p, out Vector2Int at) && land[at.x + at.y * size] == settlement.Land), "its roads stay on its land");
                }
            }
            List<PlannedSettlement> ports = world.Settlements.Where(s => s.Land != capital.Land && s.IsCoastal && !s.Type.Hostile).ToList();
            Assert.That(ports, Is.Not.Empty, "an island settlement stands by the sea");
            Assert.That(ports.Count(port => world.WaterRoutes.Any(route => route.Kind == WaterRouteKind.Sea && route.To == port.Index)),
                Is.GreaterThanOrEqualTo(ports.Count - 2), "vessels reach the island settlements (a shared harbour serves one route)");
        }

        // ---- GL642: the roads on the natural terrain (GL637-GL641) ----

        [Test]
        public void RoadsOnTheNaturalMap_KeepToFreeGround_OffTheSea_AndMostlyWithinTheirSlopes()
        {
            // A continent with a mountain ridge and a desert at two sides: ranges to climb, and edges with limits.
            TerrainSettings settings = Settings(4, 3072f, copper, gold);
            settings.SurfaceLevel = 36f;
            settings.Amplitude = 110f;
            settings.Map.PeakHeight = 320f;
            settings.Map.SetShape(MapShapeType.ContinentName);
            settings.Map.SetSides(
                MapBoundaryType.OceanName, MapBoundaryType.MountainRidgeName, MapBoundaryType.OceanName, MapBoundaryType.DesertName);
            var map = (BoundedMapGenerator)TerrainGenerators.Create(settings);
            MapLayout layout = map.Layout;
            ResourceSurvey natural = ResourceSurvey.Build(settings, map, null, null, 40f);
            WorldPlan world = WorldPlanner.Build(natural, settings.Seed);
            WorldRoads.Finish(world, map);
            System.Func<float, float, RoadWater> waterAt = WorldRoads.WaterOf(map.Water);
            UnityEngine.Debug.Log($"Natural map: {world.Settlements.Count} settlements, {world.Roads.Count} roads; {world.Timings}; "
                + $"notes: {string.Join(" | ", world.Notes.Where(note => note.StartsWith("Road")))}");

            Assert.That(world.Roads.Count, Is.GreaterThan(8), "a network to judge");
            foreach (PlannedSettlement settlement in world.Settlements)
            {
                Assert.IsTrue(layout.WalksFreely(settlement.Position.x, settlement.Position.y), $"{settlement.Name} stands where the player can walk");
            }
            Assert.IsFalse(world.Notes.Any(note => note.Contains("keeps its planned line")), "every road found its way over the real ground");
            float cartLength = 0f, cartSteep = 0f;
            foreach (PlannedRoad road in world.Roads)
            {
                Assert.IsTrue(road.Refined, $"road {road.Index}");
                bool strait = world.Notes.Any(note => note.StartsWith($"Road {road.Index}:") && note.Contains("strait"));
                foreach (Vector2 p in road.Points)
                {
                    Assert.IsTrue(layout.WalksFreely(p.x, p.y), $"road {road.Index} leaves the free ground at {p}");
                    Assert.IsTrue(strait || waterAt(p.x, p.y) != RoadWater.Sea, $"road {road.Index} is in the sea at {p}");
                }
                foreach (RoadBridge bridge in road.Bridges)
                {
                    Assert.That(bridge.Length, Is.LessThan(45f), $"road {road.Index}: a bridge crosses water, it does not follow it");
                }
                RoadClassDefinition definition = world.Rules.Road(road.Class);
                if (road.Class == RoadClass.Footpath)
                {
                    Assert.That(road.SteepLength, Is.LessThan(road.Length * 0.05f + 10f), $"footpath {road.Index} stays walkable");
                }
                else
                {
                    cartLength += road.Length;
                    cartSteep += road.SteepLength;
                    Assert.That(road.SteepestSlope, Is.LessThan(definition.MaxSlope * 6f), $"cart path {road.Index} goes up no cliff");
                }
            }
            Assert.That(cartSteep, Is.LessThan(cartLength * 0.08f), "the cart paths are over their slope limits for little of their length");
            Assert.That(world.Signposts.Count, Is.GreaterThan(world.Roads.Count), "signposts at the settlements and junctions");
        }
    }
}
