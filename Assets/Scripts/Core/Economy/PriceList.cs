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

        public ItemPrice(ItemDefinition item, int coins)
        {
            Item = item;
            Coins = coins;
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

        /// <summary>The price paid for one of the item; false when it isn't bought.</summary>
        public bool TryGetPrice(ItemDefinition item, out int coins)
        {
            foreach (ItemPrice price in prices)
            {
                if (price.Item != null && price.Item == item)
                {
                    coins = price.Coins;
                    return true;
                }
            }
            coins = 0;
            return false;
        }

        /// <summary>
        /// Sells up to <paramref name="count"/> of an item from an inventory into a wallet at
        /// this list's price (GL14). Returns how many were sold: 0 when the item isn't bought
        /// or the inventory has none.
        /// </summary>
        public int Sell(ItemDefinition item, int count, Inventory from, Wallet to)
        {
            if (count <= 0 || !TryGetPrice(item, out int coins))
            {
                return 0;
            }
            int sold = Math.Min(count, from.Count(item));
            if (sold == 0 || !from.Remove(item, sold))
            {
                return 0;
            }
            to.Earn(sold * coins);
            return sold;
        }
    }
}
