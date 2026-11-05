using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>The town's shops (GL400), each a trade with its own keeper and chores.</summary>
    public enum ShopKind
    {
        Blacksmith,
        Mining,
        Wares,
        Lumber,
        Prospector,
    }

    /// <summary>
    /// What one of the town's shops trades in (GL401, first pass of TR1): the items it buys and sells, the skills its keeper
    /// teaches (GL402) and what lies on its counter (GL403). Prices aren't kept here: what a shop pays is the one merchant
    /// <see cref="PriceList"/>'s, so every shop agrees, and it sells at that price times <see cref="SellMarkup"/>. The wares
    /// shop trades in everything the price list has.
    /// </summary>
    [CreateAssetMenu(fileName = "Shop", menuName = "Clube/Shop Specialty")]
    public class ShopSpecialty : ScriptableObject
    {
        [SerializeField]
        private ShopKind kind;

        [Tooltip("The shop's name over its counter, e.g. \"Blacksmith\".")]
        [SerializeField]
        private string shopName = "Shop";

        [Tooltip("What the keeper is called, e.g. \"the blacksmith\".")]
        [SerializeField]
        private string keeperName = "the shopkeeper";

        [Tooltip("Buys everything the price list has (the wares shop), not just the items listed.")]
        [SerializeField]
        private bool buysEverything;

        [Tooltip("What the shop buys, at the price list's price.")]
        [SerializeField]
        private List<ItemDefinition> buys = new List<ItemDefinition>();

        [Tooltip("What the shop sells, at the price list's price times the markup.")]
        [SerializeField]
        private List<ItemDefinition> sells = new List<ItemDefinition>();

        [Tooltip("What the shop asks for an item, as a share of what it pays for one.")]
        [SerializeField, Min(1f)]
        private float sellMarkup = 1.5f;

        [Tooltip("The skills the keeper teaches for coins (GL402).")]
        [SerializeField]
        private List<SkillType> trains = new List<SkillType>();

        [Tooltip("Coins per point of experience a lesson gives: the next level costs the experience it needs times this.")]
        [SerializeField, Min(0f)]
        private float coinsPerExperience = 2f;

        [Tooltip("Items laid out on the counter (GL403); the first few of what it sells when empty.")]
        [SerializeField]
        private List<ItemDefinition> displayed = new List<ItemDefinition>();

        [Tooltip("The colour of the shop's sign and awning.")]
        [SerializeField]
        private Color colour = new Color(0.6f, 0.25f, 0.2f);

        public ShopKind Kind => kind;

        public string ShopName => shopName;

        public string KeeperName => keeperName;

        public bool BuysEverything => buysEverything;

        public IReadOnlyList<ItemDefinition> Buys => buys;

        public IReadOnlyList<ItemDefinition> Sells => sells;

        public float SellMarkup => sellMarkup;

        public IReadOnlyList<SkillType> Trains => trains;

        public float CoinsPerExperience => coinsPerExperience;

        public Color Colour => colour;

        /// <summary>What lies on the counter: the listed display items, or the first four the shop sells.</summary>
        public IReadOnlyList<ItemDefinition> Displayed => displayed.Count > 0 ? displayed : sells.GetRange(0, Math.Min(4, sells.Count));

        /// <summary>Makes a shop in code (tests); the game's are assets.</summary>
        public static ShopSpecialty Create(ShopKind kind, string name, IEnumerable<ItemDefinition> buys, IEnumerable<ItemDefinition> sells,
            float markup = 1.5f, bool buysEverything = false, params SkillType[] trains)
        {
            var shop = CreateInstance<ShopSpecialty>();
            shop.kind = kind;
            shop.shopName = name;
            shop.buys.AddRange(buys);
            shop.sells.AddRange(sells);
            shop.sellMarkup = markup;
            shop.buysEverything = buysEverything;
            shop.trains.AddRange(trains);
            return shop;
        }

        /// <summary>What the shop pays: the price list's entries for what it buys (all of them for the wares shop).</summary>
        public PriceList BuyingPrices(PriceList master)
        {
            if (master == null)
            {
                return PriceList.Create();
            }
            var entries = new List<ItemPrice>();
            foreach (ItemPrice price in master.Prices)
            {
                if (price.Item != null && (buysEverything || buys.Contains(price.Item)))
                {
                    entries.Add(price);
                }
            }
            PriceList list = PriceList.Create(entries.ToArray());
            list.name = $"{shopName} prices";
            return list;
        }

        /// <summary>
        /// What the shop sells and for how much: one lot of each item it sells (as the price list counts lots, e.g. 2 sticks),
        /// at what the list pays times the markup, rounded up to a whole coin. Items the list has no price for aren't sold.
        /// </summary>
        public List<ItemPrice> Offers(PriceList master)
        {
            var offers = new List<ItemPrice>();
            if (master == null)
            {
                return offers;
            }
            foreach (ItemDefinition item in sells)
            {
                if (item == null || !master.TryGetLot(item, out int coins, out int lot))
                {
                    continue;
                }
                int asked = Math.Max(1, (int)Math.Ceiling(coins * sellMarkup - 1e-4f));
                offers.Add(new ItemPrice(item, asked, lot));
            }
            return offers;
        }
    }
}
