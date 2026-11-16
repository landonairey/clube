using System;
using System.Collections.Generic;

namespace Clube.Core
{
    /// <summary>
    /// A good a specialty trades (GL594): an item of the game by its <see cref="ItemDefinition.Id"/>, or a good that isn't in
    /// the game yet, kept by name only until its item exists.
    /// </summary>
    [Serializable]
    public sealed class TradeGood
    {
        /// <summary>The item's id in the <see cref="ItemCatalog"/>; null for a good not in the game yet.</summary>
        public string ItemId;
        /// <summary>The good's name, for one that has no item yet.</summary>
        public string Name;

        public bool InGame => !string.IsNullOrEmpty(ItemId);

        public override string ToString()
        {
            return InGame ? ItemId : Name;
        }
    }

    /// <summary>The supply chains of the economy (GL594), by id. A chain is only a string on its specialties: a new one needs no code.</summary>
    public static class SupplyChains
    {
        public const string Lumber = "lumber";
        public const string Ore = "ore";
        public const string Aggregate = "aggregate";
        public const string Textile = "textile";
        public const string Merchant = "merchant";
    }

    /// <summary>
    /// One sub-category of a supply chain (GL594, TR1): what its shop buys and sells and the resources it needs nearby. A row of
    /// <see cref="Defaults"/>; settlements are given specialties from what the survey finds around them (GL595).
    /// <see cref="ToShop"/> turns it into the <see cref="ShopSpecialty"/> the trade screen already runs on.
    /// </summary>
    [Serializable]
    public sealed class EconomySpecialty
    {
        /// <summary>Stable id ("stone-quarry").</summary>
        public string Id;
        public string Name;
        /// <summary>Its supply chain (<see cref="SupplyChains"/>).</summary>
        public string Chain;
        /// <summary>The existing kind of shop it is drawn and staffed as.</summary>
        public ShopKind Kind = ShopKind.Wares;
        /// <summary>0 extraction (fits a camp or outpost), 1 processing (a village up), 2 fine crafts (a town up).</summary>
        public int CraftLevel;
        /// <summary>Ids of the survey resources it needs nearby (<c>SurveyResource.Id</c>); all of them.</summary>
        public string[] Needs = Array.Empty<string>();
        public TradeGood[] Buys = Array.Empty<TradeGood>();
        public TradeGood[] Sells = Array.Empty<TradeGood>();
        /// <summary>The merchant's exception: buys everything.</summary>
        public bool BuysEverything;
        /// <summary>The merchant's exception: sells a random variety, skewed to what its settlement's other shops don't sell.</summary>
        public bool SellsVariety;
        /// <summary>Sub-sub-categories by name (the blacksmith's armory, weaponsmith, toolsmith).</summary>
        public string[] SubSpecialties = Array.Empty<string>();

        /// <summary>The goods it buys that the game has, as items.</summary>
        public List<ItemDefinition> BuysIn(ItemCatalog catalog)
        {
            return InGame(Buys, catalog);
        }

        /// <summary>The goods it sells that the game has, as items.</summary>
        public List<ItemDefinition> SellsIn(ItemCatalog catalog)
        {
            return InGame(Sells, catalog);
        }

        /// <summary>
        /// The shop that trades this specialty's goods that exist (the caller owns the returned asset). A merchant's variety
        /// is chosen by the caller, which knows the settlement's other shops: see <see cref="VarietyFor"/>.
        /// </summary>
        public ShopSpecialty ToShop(ItemCatalog catalog, IEnumerable<ItemDefinition> variety = null)
        {
            IEnumerable<ItemDefinition> sells = SellsVariety && variety != null ? variety : SellsIn(catalog);
            return ShopSpecialty.Create(Kind, Name, BuysIn(catalog), sells, buysEverything: BuysEverything);
        }

        /// <summary>
        /// What a merchant offers beside these shops (GL594): <paramref name="count"/> items picked by the seed from the
        /// catalog, those no local shop sells before those one does.
        /// </summary>
        public static List<ItemDefinition> VarietyFor(ItemCatalog catalog, IEnumerable<EconomySpecialty> localShops, int seed, int count)
        {
            var local = new HashSet<string>();
            foreach (EconomySpecialty shop in localShops)
            {
                foreach (TradeGood good in shop.Sells)
                {
                    if (good.InGame)
                    {
                        local.Add(good.ItemId);
                    }
                }
            }
            var items = new List<ItemDefinition>(catalog.Items);
            items.RemoveAll(item => item == null);
            var random = new System.Random(seed);
            var keys = new Dictionary<ItemDefinition, double>();
            foreach (ItemDefinition item in items)
            {
                keys[item] = random.NextDouble() + (local.Contains(item.Id) ? 1.0 : 0.0);
            }
            items.Sort((a, b) => keys[a].CompareTo(keys[b]));
            return items.GetRange(0, Math.Min(Math.Max(0, count), items.Count));
        }

        private static List<ItemDefinition> InGame(TradeGood[] goods, ItemCatalog catalog)
        {
            var found = new List<ItemDefinition>();
            foreach (TradeGood good in goods)
            {
                ItemDefinition item = good.InGame && catalog != null ? catalog.Find(good.ItemId) : null;
                if (item != null && !found.Contains(item))
                {
                    found.Add(item);
                }
            }
            return found;
        }

        // ---- The table ----

        private static readonly string[] RawOres = { "cassiterite", "chalcopyrite", "malachite", "gold-ore", "silver-ore", "magnetite", "hematite", "coal" };
        private static readonly string[] CrushedOres =
        {
            "crushed-cassiterite", "crushed-chalcopyrite", "crushed-malachite", "crushed-gold-ore", "crushed-silver-ore",
            "crushed-magnetite", "crushed-hematite",
        };
        private static readonly string[] Buns = { "copper-bun", "tin-bun", "bronze-bun", "gold-bun", "silver-bun", "iron-bun", "steel-bun" };
        private static readonly string[] Ingots = { "copper-ingot", "tin-ingot", "bronze-ingot", "gold-ingot", "silver-ingot", "iron-ingot", "steel-ingot" };
        // What the animals leave (GL603), by AnimalSpeciesTable's drops.
        private static readonly string[] FursAndWools = { "rabbit-fur", "goat-wool", "sheep-wool", "yak-wool", "alpaca-wool" };
        private static readonly string[] Hides = { "cow-hide", "deer-hide", "elk-hide", "bison-hide" };
        private static readonly string[] Furnaces = { "furnace", "clay-furnace", "brick-furnace", "refractory-furnace" };
        private static readonly string[] Picks = { "stone-pick", "copper-pick", "bronze-pick", "iron-pick", "steel-pick" };
        private static readonly string[] Hatchets = { "stone-hatchet", "copper-hatchet", "bronze-hatchet", "iron-hatchet", "steel-hatchet" };
        private static readonly string[] Shovels = { "shovel", "bronze-shovel", "iron-shovel", "steel-shovel" };
        private static readonly string[] Chisels = { "bronze-chisel", "iron-chisel", "steel-chisel" };
        private static readonly string[] Boards = { "oak-boards", "maple-boards" };
        private static readonly string[] Woods = { "wood", "maple-wood" };

        /// <summary>
        /// Every specialty of GL594's list. Goods the game has are item ids; the rest are names (<see cref="Later"/>) that
        /// start trading once an item takes the name's place here.
        /// </summary>
        public static List<EconomySpecialty> Defaults()
        {
            return new List<EconomySpecialty>
            {
                // Lumber.
                new EconomySpecialty
                {
                    Id = "lumber-supplies", Name = "Lumber Mill", Chain = SupplyChains.Lumber, Kind = ShopKind.Lumber, CraftLevel = 0,
                    Needs = new[] { "wood" }, Buys = Goods(Woods, Boards), Sells = Goods(Woods, Boards, Hatchets),
                },
                new EconomySpecialty
                {
                    Id = "carpentry", Name = "Carpenter", Chain = SupplyChains.Lumber, Kind = ShopKind.Lumber, CraftLevel = 1,
                    Needs = new[] { "wood" }, Buys = Goods(Woods, Boards),
                    Sells = Goods(Boards, Later("Wooden crate", "Hand-carved flute")),
                },
                new EconomySpecialty
                {
                    Id = "tool-componentry", Name = "Tool Componentry", Chain = SupplyChains.Lumber, Kind = ShopKind.Lumber, CraftLevel = 1,
                    Needs = new[] { "wood" }, Buys = Goods(Woods, new[] { "stick" }),
                    Sells = Goods(new[] { "stick", "hand-axe" }, Chisels, Hatchets, Shovels),
                },

                // Ore.
                new EconomySpecialty
                {
                    Id = "mining", Name = "Mining Post", Chain = SupplyChains.Ore, Kind = ShopKind.Mining, CraftLevel = 0,
                    Needs = new[] { "ore" }, Buys = Goods(RawOres, Picks), Sells = Goods(RawOres, Picks),
                },
                new EconomySpecialty
                {
                    Id = "crushing", Name = "Crushing Mill", Chain = SupplyChains.Ore, Kind = ShopKind.Mining, CraftLevel = 1,
                    Needs = new[] { "ore" }, Buys = Goods(RawOres, CrushedOres), Sells = Goods(CrushedOres),
                },
                new EconomySpecialty
                {
                    Id = "smelting", Name = "Smelter", Chain = SupplyChains.Ore, Kind = ShopKind.Blacksmith, CraftLevel = 1,
                    Needs = new[] { "ore" }, Buys = Goods(RawOres, CrushedOres, Furnaces), Sells = Goods(Buns, Ingots, Furnaces),
                },
                new EconomySpecialty
                {
                    Id = "blacksmith", Name = "Blacksmith", Chain = SupplyChains.Ore, Kind = ShopKind.Blacksmith, CraftLevel = 1,
                    Needs = new[] { "ore" }, Buys = Goods(Ingots, new[] { "anvil" }),
                    Sells = Goods(
                        new[] { "anvil", "copper-pick", "bronze-pick", "copper-hatchet", "bronze-hatchet", "bronze-shovel", "bronze-chisel", "spear" },
                        new[] { "iron-pick", "iron-hatchet", "iron-shovel", "iron-chisel", "steel-pick", "steel-hatchet", "steel-shovel", "steel-chisel" },
                        Later("Armor", "Weapons")),
                    SubSpecialties = new[] { "Armory", "Weaponsmith", "Toolsmith" },
                },
                new EconomySpecialty
                {
                    Id = "goldsmith", Name = "Goldsmith", Chain = SupplyChains.Ore, Kind = ShopKind.Blacksmith, CraftLevel = 2,
                    Needs = new[] { "precious-ore" }, Buys = Goods(new[] { "gold-ingot", "silver-ingot" }, Later("Gems", "Pearls")),
                    Sells = Goods(Later("Gold jewelry", "Silver jewelry", "Jewelry of other precious metals")),
                },
                new EconomySpecialty
                {
                    Id = "jeweler", Name = "Jeweler", Chain = SupplyChains.Ore, Kind = ShopKind.Prospector, CraftLevel = 2,
                    Needs = new[] { "precious-ore" }, Buys = Goods(Later("Gems", "Pearls")), Sells = Goods(Later("Cut gems", "Pearls")),
                },

                // Aggregate.
                new EconomySpecialty
                {
                    Id = "aggregate-supplies", Name = "Building Supplies", Chain = SupplyChains.Aggregate, Kind = ShopKind.Mining, CraftLevel = 0,
                    Needs = new[] { "stone" }, Buys = Goods(new[] { "dirt", "stone", "crushed-stone" }, Later("Limestone")),
                    Sells = Goods(new[] { "dirt", "stone", "crushed-stone" }, Later("Limestone")),
                },
                new EconomySpecialty
                {
                    Id = "masonry", Name = "Mason", Chain = SupplyChains.Aggregate, Kind = ShopKind.Mining, CraftLevel = 1,
                    Needs = new[] { "stone", "clay" }, Buys = Goods(new[] { "brick", "refractory-brick" }, Later("Cobblestone")),
                    Sells = Goods(new[] { "brick", "refractory-brick" }, Later("Cobblestone")),
                },
                new EconomySpecialty
                {
                    Id = "stone-quarry", Name = "Stone Quarry", Chain = SupplyChains.Aggregate, Kind = ShopKind.Mining, CraftLevel = 0,
                    Needs = new[] { "stone" }, Buys = Goods(new[] { "stone", "crushed-stone" }), Sells = Goods(new[] { "stone", "crushed-stone" }),
                },
                new EconomySpecialty
                {
                    Id = "limestone-quarry", Name = "Limestone Quarry", Chain = SupplyChains.Aggregate, Kind = ShopKind.Mining, CraftLevel = 0,
                    Needs = new[] { "limestone" }, Buys = Goods(Later("Limestone")), Sells = Goods(Later("Limestone")),
                },
                new EconomySpecialty
                {
                    Id = "sand-pit", Name = "Sand Pit", Chain = SupplyChains.Aggregate, Kind = ShopKind.Mining, CraftLevel = 0,
                    Needs = new[] { "sand" }, Buys = Goods(new[] { "sand" }), Sells = Goods(new[] { "sand" }),
                },
                new EconomySpecialty
                {
                    Id = "clay-pit", Name = "Clay Pit", Chain = SupplyChains.Aggregate, Kind = ShopKind.Mining, CraftLevel = 0,
                    Needs = new[] { "clay" }, Buys = Goods(new[] { "clay" }), Sells = Goods(new[] { "clay" }),
                },

                // Textile (6I): the animals' furs, wools and hides are in the game (GL603); what is made of them is not yet.
                new EconomySpecialty
                {
                    Id = "fabrics", Name = "Fabric Maker", Chain = SupplyChains.Textile, CraftLevel = 0, Needs = new[] { "grazing" },
                    Buys = Goods(FursAndWools), Sells = Goods(Later("Cloth", "String", "Twine", "Silk")),
                },
                new EconomySpecialty
                {
                    Id = "clothing", Name = "Clothier", Chain = SupplyChains.Textile, CraftLevel = 1, Needs = new[] { "grazing" },
                    Buys = Goods(Later("Cloth", "String", "Twine", "Silk")), Sells = Goods(Later("Clothing")),
                },
                new EconomySpecialty
                {
                    Id = "tannery", Name = "Tannery", Chain = SupplyChains.Textile, CraftLevel = 0, Needs = new[] { "game" },
                    Buys = Goods(Hides), Sells = Goods(Hides, Later("Tanned hides")),
                },
                new EconomySpecialty
                {
                    Id = "leathery", Name = "Leathery", Chain = SupplyChains.Textile, CraftLevel = 1, Needs = new[] { "game" },
                    Buys = Goods(Later("Tanned hides")), Sells = Goods(Later("Leather-worked goods")),
                },

                // Merchant, the exception.
                new EconomySpecialty
                {
                    Id = "merchant", Name = "Merchant", Chain = SupplyChains.Merchant, Kind = ShopKind.Wares, CraftLevel = 1,
                    BuysEverything = true, SellsVariety = true,
                },
            };
        }

        /// <summary>Goods that aren't in the game yet, by name.</summary>
        private static string[] Later(params string[] names)
        {
            var marked = new string[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                marked[i] = "?" + names[i];
            }
            return marked;
        }

        private static TradeGood[] Goods(params string[][] groups)
        {
            var goods = new List<TradeGood>();
            foreach (string[] group in groups)
            {
                foreach (string entry in group)
                {
                    goods.Add(entry.StartsWith("?") ? new TradeGood { Name = entry.Substring(1) } : new TradeGood { ItemId = entry });
                }
            }
            return goods.ToArray();
        }
    }
}
