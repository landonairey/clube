using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>One item's price, in coins.</summary>
    [Serializable]
    public struct ItemPrice
    {
        public ItemDefinition Item;

        [Min(0)]
        public int Coins;

        [Tooltip("Units the price is for, e.g. 1000 for 1 coin per 1000 dirt. 0 or 1: the price is per item.")]
        [Min(1)]
        public int Units;

        [Tooltip("Units are volume, not items: the price is for that much of what the item holds (its unit volume each), so the lot is as many items as make that volume.")]
        public bool ByVolume;

        public ItemPrice(ItemDefinition item, int coins, int units = 1, bool byVolume = false)
        {
            Item = item;
            Coins = coins;
            Units = units;
            ByVolume = byVolume;
        }

        /// <summary>Items in the lot the price is for (at least 1).</summary>
        public int Lot
        {
            get
            {
                int units = Units < 1 ? 1 : Units;
                if (!ByVolume || Item == null || Item.UnitVolume <= 0f)
                {
                    return units;
                }
                // A little under, so 4 volume of 0.4 each is 10 items, not 11 from float error.
                return Math.Max(1, (int)Math.Ceiling(units / Item.UnitVolume - 1e-3f));
            }
        }
    }

    /// <summary>
    /// What a merchant pays for each item it buys (GL13, first pass of TR1): a fixed price
    /// per item, in an asset. Items not listed aren't bought. Supply and demand, town prices
    /// and quality (TR1, TR2, TR5) come later and will set these numbers rather than replace
    /// the list.
    /// </summary>
    [CreateAssetMenu(fileName = "PriceList", menuName = "Clube/Price List")]
    public class PriceList : ScriptableObject
    {
        [SerializeField]
        private List<ItemPrice> prices = new List<ItemPrice>();

        /// <summary>Makes a price list in code (tests); the game's are assets.</summary>
        public static PriceList Create(params ItemPrice[] prices)
        {
            var list = CreateInstance<PriceList>();
            list.prices.AddRange(prices);
            return list;
        }

        public IReadOnlyList<ItemPrice> Prices => prices;

        /// <summary>The coins paid for a lot of the item, and how many items make a lot (GL38); false when it isn't listed.</summary>
        public bool TryGetLot(ItemDefinition item, out int coins, out int lot)
        {
            foreach (ItemPrice price in prices)
            {
                if (price.Item != null && price.Item == item)
                {
                    coins = price.Coins;
                    lot = price.Lot;
                    return true;
                }
            }
            coins = 0;
            lot = 1;
            return false;
        }

        /// <summary>The price paid for one of the item; false when it isn't bought.</summary>
        public bool TryGetPrice(ItemDefinition item, out int coins)
        {
            return TryGetLot(item, out coins, out _);
        }

        /// <summary>
        /// What <paramref name="count"/> of a stack's items fetch (GL40): whole lots at the listed price,
        /// and for a tool that wears out, less as it wears: a tool with half its hits left is worth half.
        /// 0 when the item isn't bought.
        /// </summary>
        public int ValueOf(ItemStack stack, int count)
        {
            if (stack.IsEmpty || !TryGetLot(stack.Item, out int coins, out int lot))
            {
                return 0;
            }
            float value = Math.Min(count, stack.Count) / lot * coins;
            if (stack.Item is ToolDefinition tool && tool.MaxHits > 0)
            {
                value *= Math.Max(0f, 1f - (float)stack.Wear / tool.MaxHits);
            }
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// What <paramref name="count"/> of a stack's items are worth in coins, not rounded (GL40): the price
        /// spread over its lot, so one ingot of a lot of ten is a tenth of a coin, less for a worn tool.
        /// 0 when the item isn't bought.
        /// </summary>
        public float PriceOf(ItemStack stack, int count)
        {
            if (stack.IsEmpty || count <= 0 || !TryGetLot(stack.Item, out int coins, out int lot))
            {
                return 0f;
            }
            float value = (float)Math.Min(count, stack.Count) * coins / lot;
            if (stack.Item is ToolDefinition tool && tool.MaxHits > 0)
            {
                value *= Math.Max(0f, 1f - (float)stack.Wear / tool.MaxHits);
            }
            return value;
        }

        /// <summary>
        /// What a bin of mixed material is worth in coins, not rounded (GL111): each material's volume at the
        /// price of what it gives (mined stone at the price of stone), spread over the lot. Material nothing
        /// is paid for is worth nothing.
        /// </summary>
        public float ValueOf(VolumeMix mix)
        {
            float value = 0f;
            if (mix == null)
            {
                return value;
            }
            foreach (VolumeMix.Entry entry in mix.Entries)
            {
                ItemDefinition item = entry.Material != null ? entry.Material.FinalDrop : null;
                if (item != null && TryGetLot(item, out int coins, out int lot))
                {
                    value += entry.Volume * coins / lot;
                }
            }
            return value;
        }

        /// <summary>The most that can be sold of <paramref name="available"/> items: whole lots only (GL38).</summary>
        public int Sellable(ItemDefinition item, int available)
        {
            return TryGetLot(item, out _, out int lot) ? available / lot * lot : 0;
        }

        /// <summary>
        /// Sells up to <paramref name="count"/> of an item from an inventory into a wallet at
        /// this list's price (GL14). Returns how many were sold: 0 when the item isn't bought
        /// or the inventory has none.
        /// </summary>
        public int Sell(ItemDefinition item, int count, Inventory from, Wallet to)
        {
            if (count <= 0 || !TryGetLot(item, out int coins, out int lot))
            {
                return 0;
            }
            // Whole lots only: 999 dirt is worth nothing at 1 coin per 1000.
            int sold = Math.Min(count, from.Count(item)) / lot * lot;
            if (sold == 0 || !from.Remove(item, sold))
            {
                return 0;
            }
            to.Earn(sold / lot * coins);
            return sold;
        }
    }
}
