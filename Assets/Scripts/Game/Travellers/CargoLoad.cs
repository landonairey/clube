using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// What rides in the crates of a cart or a boat (GL616, GL618): the goods of the settlement it left, and the clock
    /// its timetable runs on. Shared so carts and vessels load and keep time the same way.
    /// </summary>
    public static class CargoLoad
    {
        /// <summary>Real seconds since the clock started, at its normal rate: what the timetables run on.</summary>
        public static double Seconds(DayNightCycle clock)
        {
            return clock != null && clock.Clock != null ? clock.Days * clock.Clock.DayLengthSeconds : Time.timeAsDouble;
        }

        /// <summary>The scene's clock: the world's own, else any.</summary>
        public static DayNightCycle ClockOf(WorldView view)
        {
            DayNightCycle clock = view != null ? view.GetComponent<DayNightCycle>() : null;
            return clock != null ? clock : Object.FindAnyObjectByType<DayNightCycle>();
        }

        /// <summary>What a settlement's shops sell, of the items that exist, into <paramref name="goods"/>.</summary>
        public static void GoodsOf(WorldPlan plan, ItemCatalog catalog, int settlement, List<ItemDefinition> goods)
        {
            goods.Clear();
            if (catalog == null || plan == null || settlement < 0 || settlement >= plan.Settlements.Count)
            {
                return;
            }
            foreach (PlannedShop shop in plan.Settlements[settlement].Shops)
            {
                foreach (ItemDefinition item in shop.Specialty.SellsIn(catalog))
                {
                    if (!goods.Contains(item))
                    {
                        goods.Add(item);
                    }
                }
            }
        }

        /// <summary>
        /// Keeps of <paramref name="goods"/> only what a vessel with one cargo carries (any of <paramref name="words"/> in
        /// the item's name); where the settlement sells none of it, the catalog's raw items of that name ("ore").
        /// </summary>
        public static void Only(List<ItemDefinition> goods, ItemCatalog catalog, IReadOnlyList<string> words)
        {
            if (words == null || words.Count == 0)
            {
                return;
            }
            goods.RemoveAll(item => !Named(item, words));
            if (goods.Count > 0 || catalog == null)
            {
                return;
            }
            foreach (ItemDefinition item in catalog.Items)
            {
                if (item != null && Named(item, words) && item.DisplayName.ToLowerInvariant().Contains("ore"))
                {
                    goods.Add(item);
                }
            }
        }

        /// <summary>Empties the crates and fills each with a few kinds of the goods, the draw picking which.</summary>
        public static void Fill(IReadOnlyList<StorageChest> crates, IReadOnlyList<ItemDefinition> goods, System.Random random)
        {
            foreach (StorageChest crate in crates)
            {
                if (crate == null || crate.Storage == null)
                {
                    continue;
                }
                StorageText.Fill(crate.Storage, "");
                for (int k = 0; k < 3 && goods.Count > 0; k++)
                {
                    crate.Storage.Add(goods[random.Next(goods.Count)], 2 + random.Next(7));
                }
            }
        }

        private static bool Named(ItemDefinition item, IReadOnlyList<string> words)
        {
            string name = item.DisplayName.ToLowerInvariant();
            foreach (string word in words)
            {
                if (name.Contains(word))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
