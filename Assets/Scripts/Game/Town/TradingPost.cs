using System;
using System.Collections.Generic;
using System.Globalization;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// What the player has done at a trading post (GL471-GL475), the only part of a settlement that's saved: the town itself
    /// follows from the seed (<see cref="TownSite"/>). Holds each shopkeeper's <see cref="Disposition"/> and the post's
    /// <see cref="PropertySale"/> (GL473: the merchant sells it for 10k, and it pays 500c a game day after), which it offers
    /// through the trade screen as an <see cref="ITraderDeal"/>. Sits on the settlement's object from the scene's start, so a
    /// save restores into it before the town is built; keys start with <see cref="keyPrefix"/>, so a second settlement keeps
    /// its own.
    /// </summary>
    public class TradingPost : MonoBehaviour, ISavedValues, ITraderDeal
    {
        [Tooltip("What this settlement's saved values are called (unique per settlement).")]
        [SerializeField]
        private string keyPrefix = "tradingpost";

        [Tooltip("What the trading post and its parcel cost (GL473).")]
        [SerializeField, Min(0)]
        private int price = 10000;

        [Tooltip("Coins of sale profits it pays its owner each game day (GL473).")]
        [SerializeField, Min(0)]
        private int profitPerDay = 500;

        private readonly Dictionary<ShopKind, Disposition> dispositions = new Dictionary<ShopKind, Disposition>();
        private PropertySale sale;
        private DayNightCycle clock;
        private Wallet owner;

        /// <summary>Raised when the player buys the post: its parcel becomes theirs.</summary>
        public event Action Bought;

        /// <summary>The post's sale and ownership (GL473).</summary>
        public PropertySale Sale => sale ??= new PropertySale(price, profitPerDay);

        /// <summary>True once the player owns the trading post (GL473).</summary>
        public bool Owned => Sale.Owned;

        /// <summary>Coins the post has paid its owner (GL473), this session.</summary>
        public float Paid { get; private set; }

        /// <summary>The clock the days are counted by and the player's wallet the profits go to.</summary>
        public void Setup(DayNightCycle clock, Wallet owner)
        {
            this.clock = clock;
            this.owner = owner;
        }

        /// <summary>A shopkeeper's disposition towards the player (GL471): 10 until they've traded.</summary>
        public Disposition DispositionOf(ShopKind kind)
        {
            if (!dispositions.TryGetValue(kind, out Disposition disposition))
            {
                disposition = new Disposition();
                dispositions[kind] = disposition;
            }
            return disposition;
        }

        private int Today => clock != null ? clock.Day : 1;

        // The day's sale profits (GL473), paid as each game day starts.
        private void Update()
        {
            if (owner == null || !Sale.Owned)
            {
                return;
            }
            int coins = Sale.Collect(Today);
            if (coins > 0)
            {
                owner.Earn(coins);
                Paid += coins;
            }
        }

        // ITraderDeal: the merchant sells the trading post (GL473).

        public string Label => $"Buy the trading post and its parcel ({Sale.Price}c): {Sale.ProfitPerDay}c a day in sale profits";

        public bool IsOffered(Wallet player)
        {
            return player != null && Sale.IsOffered(player.Coins);
        }

        public bool Accept(Wallet player)
        {
            if (!Sale.TryBuy(player, Today))
            {
                return false;
            }
            Bought?.Invoke();
            return true;
        }

        private string Key(string name)
        {
            return $"{keyPrefix}.{name}";
        }

        public void SaveValues(IDictionary<string, string> values)
        {
            foreach (KeyValuePair<ShopKind, Disposition> pair in dispositions)
            {
                values[Key($"disposition.{pair.Key}")] = string.Format(CultureInfo.InvariantCulture, "{0};{1:R}", pair.Value.Value, pair.Value.Carried);
            }
            if (Sale.Owned)
            {
                values[Key("paidday")] = Sale.PaidDay.ToString(CultureInfo.InvariantCulture);
            }
            // What's left in the cave's crates (GL475); a save's, when the town isn't built yet.
            foreach (KeyValuePair<int, string> pair in restoredCrates)
            {
                values[Key($"crate{pair.Key}")] = pair.Value;
            }
            foreach (KeyValuePair<int, StorageChest> pair in crates)
            {
                if (pair.Value != null)
                {
                    values[Key($"crate{pair.Key}")] = Encode(pair.Value.Storage);
                }
            }
        }

        private const int MaxCrates = 8;
        private readonly Dictionary<int, StorageChest> crates = new Dictionary<int, StorageChest>();
        private readonly Dictionary<int, string> restoredCrates = new Dictionary<int, string>();

        /// <summary>
        /// One of the cave's crates (GL475), filled from the seed: its contents are saved from now on, and a save's contents
        /// (what the player left in it) replace the seed's.
        /// </summary>
        public void RegisterCrate(int index, StorageChest crate)
        {
            crates[index] = crate;
            if (restoredCrates.TryGetValue(index, out string text))
            {
                Fill(crate.Storage, text);
            }
        }

        // Slots as "id*count*quality|...".
        private static string Encode(Inventory storage)
        {
            var entries = new List<string>();
            for (int slot = 0; slot < storage.SlotCount; slot++)
            {
                ItemStack stack = storage[slot];
                if (!stack.IsEmpty)
                {
                    entries.Add(string.Format(CultureInfo.InvariantCulture, "{0}*{1}*{2:R}", stack.Item.Id, stack.Count, stack.Quality));
                }
            }
            return string.Join("|", entries);
        }

        private static void Fill(Inventory storage, string text)
        {
            for (int slot = 0; slot < storage.SlotCount; slot++)
            {
                storage.TakeAll(slot);
            }
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            var items = new Dictionary<string, ItemDefinition>();
            foreach (ItemDefinition item in Resources.FindObjectsOfTypeAll<ItemDefinition>())
            {
                if (!string.IsNullOrEmpty(item.Id))
                {
                    items[item.Id] = item;
                }
            }
            foreach (string entry in text.Split('|'))
            {
                string[] parts = entry.Split('*');
                if (parts.Length == 3 && items.TryGetValue(parts[0], out ItemDefinition item)
                    && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)
                    && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float quality))
                {
                    storage.Add(item, count, null, 0, quality);
                }
            }
        }

        public void RestoreValues(IReadOnlyDictionary<string, string> values)
        {
            foreach (ShopKind kind in Enum.GetValues(typeof(ShopKind)))
            {
                if (!values.TryGetValue(Key($"disposition.{kind}"), out string text))
                {
                    continue;
                }
                string[] parts = text.Split(';');
                if (parts.Length == 2 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                    && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float carried))
                {
                    DispositionOf(kind).Restore(value, carried);
                }
            }
            if (values.TryGetValue(Key("paidday"), out string day) && int.TryParse(day, NumberStyles.Integer, CultureInfo.InvariantCulture, out int paid))
            {
                Sale.Restore(true, paid);
                Bought?.Invoke();
            }
            for (int i = 0; i < MaxCrates; i++)
            {
                if (!values.TryGetValue(Key($"crate{i}"), out string crate))
                {
                    continue;
                }
                restoredCrates[i] = crate;
                if (crates.TryGetValue(i, out StorageChest chest) && chest != null)
                {
                    Fill(chest.Storage, crate);
                }
            }
        }
    }
}
