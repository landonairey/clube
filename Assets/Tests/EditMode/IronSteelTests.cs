using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// Iron, coal and steel as the game's own data holds them (GL621-GL625): the materials, the deposits, the smelting and
    /// forging chain, the tools' tiers and the anvil's recipe. Reads the assets, since the content is the assets.
    /// </summary>
    public class IronSteelTests
    {
        private const string Config = "Assets/Config/";
        private static readonly string[] ToolKinds = { "pick", "hatchet", "shovel", "chisel" };
        private static readonly string[] OreWorlds = { "WorldLabWorldConfig", "ProceduralWorldLabWorldConfig", "MapGenWorldConfig" };

        private ItemCatalog catalog;
        private MaterialRegistry registry;
        private PriceList prices;

        [OneTimeSetUp]
        public void Load()
        {
            catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(Config + "Items/ItemCatalog.asset");
            registry = AssetDatabase.LoadAssetAtPath<MaterialRegistry>(Config + "Materials/MaterialRegistry.asset");
            prices = AssetDatabase.LoadAssetAtPath<PriceList>(Config + "Economy/Merchant prices.asset");
        }

        private static Recipe RecipeNamed(string name)
        {
            var recipe = AssetDatabase.LoadAssetAtPath<Recipe>(Config + "Recipes/" + name + ".asset");
            Assert.IsNotNull(recipe, "the recipe " + name);
            return recipe;
        }

        private ItemDefinition Item(string id)
        {
            ItemDefinition item = catalog.Find(id);
            Assert.IsNotNull(item, id + " is in the catalog");
            return item;
        }

        private int Coins(string id)
        {
            Assert.IsTrue(prices.TryGetLot(Item(id), out int coins, out int _), id + " has a price");
            return coins;
        }

        // ---- GL621, GL622: the materials and their deposits ----

        [Test]
        public void TheOresAreTheRegistrysLastMaterials_WithIdsOfTheirOwn_AndDropTheirItems()
        {
            IReadOnlyList<VoxelMaterial> all = registry.Materials;
            Assert.That(all.Select(m => m.Id).Distinct().Count(), Is.EqualTo(all.Count), "no id is used twice");
            // Appended in their turn (Dead Grass, GL633, came after them), so no earlier material's id or texture layer moved.
            int first = all.ToList().FindIndex(m => m.DisplayName == "Magnetite");
            Assert.That(all.Skip(first).Take(3).Select(m => m.DisplayName), Is.EqualTo(new[] { "Magnetite", "Hematite", "Coal" }));
            Assert.That(all.Skip(first).Take(3).Select(m => (int)m.Id), Is.EqualTo(new[] { 31, 32, 33 }));
            foreach (string name in new[] { "Magnetite", "Hematite", "Coal" })
            {
                VoxelMaterial material = registry.Find(name);
                Assert.AreEqual(VoxelMaterialCategory.Ore, material.Category, name);
                Assert.AreSame(Item(name.ToLowerInvariant()), material.Drop, name + " drops its item");
                Assert.IsTrue(registry.IsMaterialItem(material.Drop));
            }
            // Crushed ore is its ore's (GL186).
            Assert.AreSame(Item("magnetite"), Item("crushed-magnetite").PurityAs);
            Assert.AreSame(Item("hematite"), Item("crushed-hematite").PurityAs);
        }

        [Test]
        public void TheGameWorldsHoldTheOres_AndTheirNodesGenerate()
        {
            foreach (string world in OreWorlds)
            {
                var config = AssetDatabase.LoadAssetAtPath<WorldConfig>(Config + world + ".asset");
                List<OreSpec> specs = config.Terrain.Ores.Ores;
                foreach (string name in new[] { "Magnetite", "Hematite", "Coal" })
                {
                    OreSpec spec = specs.SingleOrDefault(s => s.Ore != null && s.Ore.DisplayName == name);
                    Assert.IsNotNull(spec, $"{world} has {name}");
                    // Below every earlier ore, so the nodes that were there stay as they were.
                    Assert.That(spec.Priority, Is.LessThan(specs.Where(s => s != spec && s.Ore.Id < 31).Min(s => s.Priority)), name);
                }

                var field = new OreField(config.Terrain.Ores, 7, new FlatGenerator(40f));
                var found = new HashSet<byte>();
                for (int x = 0; x < 12; x++)
                {
                    for (int y = 0; y < 3; y++)
                    {
                        foreach (OreNode node in field.NodesInCell(new Vector3Int(x, y, 0)))
                        {
                            found.Add(node.Material);
                        }
                    }
                }
                Assert.That(found, Is.SupersetOf(new byte[] { 31, 32, 33 }), world + " rolls nodes of all three");
            }
        }

        [Test]
        public void TheOreSpecialtiesTradeIronCoalAndSteel()
        {
            List<EconomySpecialty> all = EconomySpecialty.Defaults();
            EconomySpecialty Of(string id) => all.Single(s => s.Id == id);
            Assert.That(Of("mining").SellsIn(catalog).Select(i => i.Id), Is.SupersetOf(new[] { "magnetite", "hematite", "coal", "iron-pick", "steel-pick" }));
            Assert.That(Of("crushing").SellsIn(catalog).Select(i => i.Id), Is.SupersetOf(new[] { "crushed-magnetite", "crushed-hematite" }));
            Assert.That(Of("smelting").SellsIn(catalog).Select(i => i.Id), Is.SupersetOf(new[] { "iron-bun", "iron-ingot", "steel-bun", "steel-ingot" }));
            Assert.That(Of("blacksmith").SellsIn(catalog).Select(i => i.Id), Is.SupersetOf(new[] { "iron-chisel", "steel-hatchet", "steel-shovel" }));
        }

        // ---- GL622: coal as fuel ----

        [Test]
        public void CoalBurnsLongerThanAnyOtherFuel_AndAsCleanAsCharcoal()
        {
            ItemDefinition coal = Item("coal");
            ItemDefinition charcoal = Item("charcoal");
            Assert.That(coal.BurnSeconds, Is.GreaterThan(charcoal.BurnSeconds));
            Assert.That(coal.FuelQuality, Is.GreaterThanOrEqualTo(charcoal.FuelQuality));
            foreach (ItemDefinition item in catalog.Items)
            {
                if (item != null && item != coal)
                {
                    Assert.That(coal.BurnSeconds, Is.GreaterThan(item.BurnSeconds), item.DisplayName);
                }
            }

            // A furnace takes it as fuel and smelts on it.
            Recipe smelt = RecipeNamed("Smelt magnetite");
            var furnace = new CraftingStation(StationKind.Furnace, new[] { smelt }, burnsFuel: true);
            furnace.Input.Add(Item("magnetite"), smelt.Inputs[0].Count);
            var player = new Inventory(1);
            player.Add(coal, 1);
            Assert.AreEqual(1, furnace.Load(player, 0));
            Assert.AreEqual(1, furnace.Fuel.Count(coal), "coal goes to the fuel slot");
            furnace.Tick(1f);
            Assert.IsTrue(furnace.IsBurning);
            Assert.That(furnace.Progress, Is.GreaterThan(0f));
        }

        // ---- GL623: buns and ingots ----

        [Test]
        public void EveryRecipeAssetResolves()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Recipe", new[] { "Assets/Config/Recipes" }))
            {
                var recipe = AssetDatabase.LoadAssetAtPath<Recipe>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.IsNotNull(recipe.Output.Item, recipe.name + " makes something");
                Assert.That(recipe.Output.Count, Is.GreaterThan(0), recipe.name);
                Assert.That(catalog.Items, Does.Contain(recipe.Output.Item), recipe.name + ": its output is in the catalog");
                Assert.That(recipe.Inputs.Count, Is.GreaterThan(0), recipe.name);
                foreach (ItemAmount input in recipe.Inputs)
                {
                    Assert.IsNotNull(input.Item, recipe.name + " has no empty input");
                    Assert.That(catalog.Items, Does.Contain(input.Item), recipe.name + ": " + input.Item.DisplayName + " is in the catalog");
                }
            }
        }

        [Test]
        public void BothOresSmeltIntoIronBuns_WholeOrCrushed_InAFurnace()
        {
            foreach (string ore in new[] { "magnetite", "hematite" })
            {
                foreach (string form in new[] { "", "crushed " })
                {
                    Recipe smelt = RecipeNamed("Smelt " + form + ore);
                    Assert.AreEqual(StationKind.Furnace, smelt.Station);
                    Assert.AreEqual(RecipeWork.Time, smelt.Work);
                    Assert.AreSame(Item((form + ore).Replace(' ', '-')), smelt.Inputs.Single().Item);
                    Assert.AreSame(Item("iron-bun"), smelt.Output.Item);
                    // The ore's own mineral is what the smelt extracts (GL192), so its grade sets the yield and the purity.
                    Assert.AreSame(registry.Find(char.ToUpperInvariant(ore[0]) + ore.Substring(1)), smelt.Extracts);
                    Assert.AreEqual(SkillType.Smelting, smelt.ExperienceSkill);
                }
            }
        }

        [Test]
        public void BunsHammerIntoIngots_OnTheAnvilAndTheFlatRock()
        {
            foreach (string metal in new[] { "iron", "steel" })
            {
                foreach ((string suffix, StationKind station) in new[] { ("", StationKind.Anvil), (" (flat rock)", StationKind.FlatRock) })
                {
                    Recipe hammer = RecipeNamed($"Hammer {metal} ingot{suffix}");
                    Assert.AreEqual(station, hammer.Station);
                    Assert.AreEqual(RecipeWork.Strikes, hammer.Work);
                    Assert.AreSame(Item(metal + "-bun"), hammer.Inputs.Single().Item);
                    Assert.AreSame(Item(metal + "-ingot"), hammer.Output.Item);
                }
            }
        }

        [Test]
        public void SteelIsIronAndCoal_AlloyedInAFurnace()
        {
            Recipe alloy = RecipeNamed("Alloy steel buns");
            Assert.AreEqual(StationKind.Furnace, alloy.Station);
            Assert.AreEqual(4, alloy.Inputs.Single(i => i.Item == Item("iron-bun")).Count);
            Assert.AreEqual(1, alloy.Inputs.Single(i => i.Item == Item("coal")).Count);
            Assert.AreSame(Item("steel-bun"), alloy.Output.Item);
            Assert.AreEqual(4, alloy.Output.Count);
            Assert.IsNull(alloy.Extracts, "an alloy, not a smelt: its quality is its inputs'");

            // Made in a furnace: the buns and the coal in, steel out.
            var furnace = new CraftingStation(StationKind.Furnace, new[] { alloy }, burnsFuel: true);
            furnace.Input.Add(Item("iron-bun"), 4);
            furnace.Input.Add(Item("coal"), 1);
            furnace.Fuel.Add(Item("charcoal"), 2);
            furnace.Tick(alloy.Amount + 1f);
            Assert.AreEqual(4, furnace.Output.Count(Item("steel-bun")));
            Assert.AreEqual(0, furnace.Input.Count(Item("iron-bun")));
        }

        // ---- GL624: tools ----

        [Test]
        public void EveryMetalToolHasAnIronAndASteelVersion_EachTierBetter()
        {
            foreach (string kind in ToolKinds)
            {
                var tiers = new[] { "bronze", "iron", "steel" }.Select(metal => (ToolDefinition)Item($"{metal}-{kind}")).ToArray();
                for (int i = 1; i < tiers.Length; i++)
                {
                    ToolDefinition worse = tiers[i - 1], better = tiers[i];
                    string what = better.DisplayName + " over " + worse.DisplayName;
                    Assert.That(better.Tier, Is.GreaterThan(worse.Tier), what);
                    Assert.That(better.MaxHits, Is.GreaterThan(worse.MaxHits), what);
                    Assert.That(better.HitsPerSecond, Is.GreaterThan(worse.HitsPerSecond), what);
                    Assert.That(better.Power, Is.GreaterThan(worse.Power), what);
                    Assert.That(better.ReachScale, Is.GreaterThanOrEqualTo(worse.ReachScale), what);
                    Assert.That(better.WearLostPerRepair, Is.LessThan(worse.WearLostPerRepair), what);
                    Assert.That(Coins(better.Id), Is.GreaterThan(Coins(worse.Id)), what);
                    // What it's for is what the bronze one is for.
                    Assert.AreEqual(worse.ImpactSize, better.ImpactSize, what);
                    Assert.AreEqual(worse.WideOn, better.WideOn, what);
                    Assert.AreEqual(worse.Chisels, better.Chisels, what);
                }
                foreach (string metal in new[] { "iron", "steel" })
                {
                    var tool = (ToolDefinition)Item($"{metal}-{kind}");
                    Assert.IsNotNull(tool.Icon, tool.DisplayName + " has an icon");
                    Assert.AreSame(Item(metal + "-ingot"), tool.RepairItem, tool.DisplayName + " is mended with its metal");
                    Recipe make = RecipeNamed($"Make {metal} {kind}");
                    Assert.AreEqual(StationKind.Anvil, make.Station);
                    Assert.AreSame(tool, make.Output.Item);
                    Assert.That(make.Inputs.Select(i => i.Item), Does.Contain(Item(metal + "-ingot")));
                    Assert.That(make.Inputs.Select(i => i.Item), Does.Contain(Item("stick")));
                }
            }
            Assert.That(Coins("steel-ingot"), Is.GreaterThan(Coins("iron-ingot")));
            Assert.That(Coins("iron-ingot"), Is.GreaterThan(Coins("bronze-ingot")));
        }

        // ---- GL625: the anvil ----

        [Test]
        public void AHundredIronIngotsMakeTheAnvil_APlaceable()
        {
            Recipe build = RecipeNamed("Build anvil");
            Assert.AreEqual(StationKind.Worktop, build.Station);
            ItemAmount ingots = build.Inputs.Single();
            Assert.AreSame(Item("iron-ingot"), ingots.Item);
            Assert.AreEqual(100, ingots.Count);
            Assert.AreEqual(1, build.Output.Count);
            var anvil = build.Output.Item as PlaceableDefinition;
            Assert.IsNotNull(anvil, "the anvil is a placeable");
            Assert.AreEqual("anvil", anvil.Id);
            Assert.IsNotNull(anvil.Prefab, "with the station it sets down");

            // The hundred fit the worktop, and it makes the anvil from them.
            Assert.That(ingots.Count, Is.LessThanOrEqualTo(ingots.Item.MaxStack * CraftingStation.DefaultInputSlots));
            var worktop = new CraftingStation(StationKind.Worktop, new[] { build });
            var player = new Inventory(4);
            player.Add(ingots.Item, 100);
            worktop.LoadFor(build, player);
            Assert.AreEqual(100, worktop.Input.Count(ingots.Item));
            for (int i = 0; i < Mathf.CeilToInt(build.Amount) + 1 && worktop.Output.Count(anvil) == 0; i++)
            {
                worktop.Strike();
            }
            Assert.AreEqual(1, worktop.Output.Count(anvil));
            Assert.AreEqual(0, worktop.Input.Count(ingots.Item));
        }
    }
}
