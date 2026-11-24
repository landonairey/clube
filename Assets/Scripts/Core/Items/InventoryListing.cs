using System;
using System.Collections.Generic;

namespace Clube.Core
{
    /// <summary>What a list inventory's rows are sorted by (list inventory prototype).</summary>
    public enum ListSort
    {
        Name,
        Value,
        Quality,
        Quantity,
    }

    /// <summary>
    /// One line of a list inventory (list inventory prototype): a single stack, or a collection of every stack of one item
    /// (two stone picks worn differently, stone at two grades) with its averages, or one stack shown under an opened collection.
    /// </summary>
    public sealed class ListRow
    {
        internal ListRow(ItemDefinition item, List<int> slots, bool isCollection, bool isChild, float value, float quality, float amount, bool expanded, bool equipped)
        {
            Item = item;
            Slots = slots;
            IsCollection = isCollection;
            IsChild = isChild;
            Value = value;
            Quality = quality;
            Amount = amount;
            Expanded = expanded;
            Equipped = equipped;
        }

        public ItemDefinition Item { get; }

        /// <summary>The inventory slots the row stands for: one, or every slot of a collection.</summary>
        public IReadOnlyList<int> Slots { get; }

        /// <summary>The row's first slot: what a click or a drag on it works on.</summary>
        public int Slot => Slots[0];

        /// <summary>True for a collection's own line (an arrow opens it).</summary>
        public bool IsCollection { get; }

        /// <summary>True for a stack shown under its opened collection.</summary>
        public bool IsChild { get; }

        /// <summary>Coins one item is worth; a collection's is the average over its items, by amount.</summary>
        public float Value { get; }

        /// <summary>Quality as a fraction; a collection's is the average over its items, by amount.</summary>
        public float Quality { get; }

        /// <summary>How many: a stack's amount, or the collection's total.</summary>
        public float Amount { get; }

        /// <summary>A collection shown opened, its stacks listed under it.</summary>
        public bool Expanded { get; }

        /// <summary>The row holds an equipped slot (a collection: any of them).</summary>
        public bool Equipped { get; }
    }

    /// <summary>
    /// Builds the rows of a list inventory from slots (list inventory prototype): stacks of the same item gather into a
    /// collection showing average value and quality and the total amount, opened to list each stack; rows sort by name,
    /// value, quality or amount, either way. Pure, so the screens only draw what this returns.
    /// </summary>
    public static class InventoryListing
    {
        /// <param name="include">Which slots are listed; null for all.</param>
        /// <param name="value">Coins one item of a slot is worth.</param>
        /// <param name="quality">A slot's quality as a fraction.</param>
        /// <param name="expanded">Items whose collection is shown opened.</param>
        /// <param name="equipped">Which slots are equipped; null for none.</param>
        public static List<ListRow> Build(Inventory items, Func<int, bool> include, Func<int, float> value, Func<int, float> quality,
            ICollection<ItemDefinition> expanded, ListSort sort, bool descending, Func<int, bool> equipped = null)
        {
            var groups = new Dictionary<ItemDefinition, List<int>>();
            var order = new List<ItemDefinition>();
            for (int slot = 0; slot < items.SlotCount; slot++)
            {
                ItemStack stack = items[slot];
                if (stack.IsEmpty || (include != null && !include(slot)))
                {
                    continue;
                }
                if (!groups.TryGetValue(stack.Item, out List<int> slots))
                {
                    slots = new List<int>();
                    groups.Add(stack.Item, slots);
                    order.Add(stack.Item);
                }
                slots.Add(slot);
            }

            var tops = new List<ListRow>(order.Count);
            var children = new Dictionary<ListRow, List<ListRow>>();
            foreach (ItemDefinition item in order)
            {
                List<int> slots = groups[item];
                if (slots.Count == 1)
                {
                    tops.Add(Single(items, slots[0], value, quality, equipped, false));
                    continue;
                }
                float total = 0f, coins = 0f, made = 0f;
                bool anyEquipped = false;
                var rows = new List<ListRow>(slots.Count);
                foreach (int slot in slots)
                {
                    float amount = items[slot].Amount;
                    total += amount;
                    coins += value(slot) * amount;
                    made += quality(slot) * amount;
                    anyEquipped |= equipped != null && equipped(slot);
                    rows.Add(Single(items, slot, value, quality, equipped, true));
                }
                bool open = expanded != null && expanded.Contains(item);
                float share = total > 0f ? 1f / total : 0f;
                var collection = new ListRow(item, slots, true, false, coins * share, made * share, total, open, anyEquipped);
                tops.Add(collection);
                if (open)
                {
                    rows.Sort((a, b) => Compare(a, b, sort, descending));
                    children.Add(collection, rows);
                }
            }

            tops.Sort((a, b) => Compare(a, b, sort, descending));
            var result = new List<ListRow>(tops.Count);
            foreach (ListRow top in tops)
            {
                result.Add(top);
                if (children.TryGetValue(top, out List<ListRow> under))
                {
                    result.AddRange(under);
                }
            }
            return result;
        }

        private static ListRow Single(Inventory items, int slot, Func<int, float> value, Func<int, float> quality, Func<int, bool> equipped, bool child)
        {
            return new ListRow(items[slot].Item, new List<int> { slot }, false, child, value(slot), quality(slot), items[slot].Amount, false,
                equipped != null && equipped(slot));
        }

        // Ties fall back to the name, then the slot, so the order never jumps between frames.
        private static int Compare(ListRow a, ListRow b, ListSort sort, bool descending)
        {
            int by = sort switch
            {
                ListSort.Value => a.Value.CompareTo(b.Value),
                ListSort.Quality => a.Quality.CompareTo(b.Quality),
                ListSort.Quantity => a.Amount.CompareTo(b.Amount),
                _ => string.Compare(a.Item.DisplayName, b.Item.DisplayName, StringComparison.OrdinalIgnoreCase),
            };
            if (descending)
            {
                by = -by;
            }
            if (by == 0 && sort != ListSort.Name)
            {
                by = string.Compare(a.Item.DisplayName, b.Item.DisplayName, StringComparison.OrdinalIgnoreCase);
            }
            if (by != 0)
            {
                return by;
            }
            // Equipping moves a stack into a hand slot, so the slot number can't settle ties without the order jumping.
            // Break them on what the row holds instead; only rows identical in every column fall back to equipped, then slot.
            if (a.Item == b.Item)
            {
                by = a.Quality.CompareTo(b.Quality);
                by = by != 0 ? by : a.Value.CompareTo(b.Value);
                by = by != 0 ? by : a.Amount.CompareTo(b.Amount);
                by = by != 0 ? by : (b.Equipped ? 1 : 0).CompareTo(a.Equipped ? 1 : 0);
                if (by != 0)
                {
                    return by;
                }
            }
            return a.Slot.CompareTo(b.Slot);
        }
    }
}
