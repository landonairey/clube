using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// What a list inventory's Val, Qual and Qty columns read for a stack (list inventory prototype): the value of one item at
    /// the merchant's prices, quality the way a slot's corner shows it (a tool's durability, an ore's purity), and the amount.
    /// </summary>
    public static class ListColumns
    {
        /// <summary>Coins one item of the stack fetches at <see cref="ItemSlotGui.Prices"/>; 0 when nobody buys it.</summary>
        public static float ValueOf(ItemStack stack)
        {
            PriceList prices = ItemSlotGui.Prices;
            if (stack.IsEmpty || prices == null || !prices.TryGetLot(stack.Item, out _, out _))
            {
                return 0f;
            }
            float one = Mathf.Min(1f, stack.Amount);
            return one > 0f ? prices.PriceOf(stack, one) / one : 0f;
        }

        /// <summary>Quality as a fraction: a tool's durability left, a mined stack's purity, else the stack's quality.</summary>
        public static float QualityOf(ItemStack stack)
        {
            if (stack.IsEmpty)
            {
                return 0f;
            }
            if (stack.Item is ToolDefinition tool && tool.MaxHits > 0)
            {
                return Mathf.Clamp01((float)Mathf.Max(0, stack.MaxHits - stack.Wear) / tool.MaxHits);
            }
            MaterialRegistry materials = ItemSlotGui.Materials;
            if (stack.Quality >= 0.995f && stack.Quality <= 1.005f && materials != null && stack.Contents != null && !stack.Item.HidesContents
                && materials.IsMaterialItem(stack.Item))
            {
                return materials.Quality(stack.Contents, stack.Item);
            }
            return stack.Quality;
        }

        /// <summary>
        /// What Qual sorts by (item 14): a stack shown as a word grade stands in as the midpoint of its range (Pure 100%, High
        /// 94.5%, Good 82%, Medium 62%, Poor 24.5%), among stacks with a % quality.
        /// </summary>
        public static float SortQualityOf(ItemStack stack)
        {
            float quality = QualityOf(stack);
            if (stack.IsEmpty || stack.Item is ToolDefinition || !(ItemSlotGui.ReadsAsGrades(stack) || stack.Item.QualityAsGrade))
            {
                return quality;
            }
            switch (ProspectReading.GradeOf(quality))
            {
                case Grade.Pure: return 1f;
                case Grade.HighGrade: return 0.945f;
                case Grade.Good: return 0.82f;
                case Grade.Medium: return 0.62f;
                default: return 0.245f;
            }
        }

        /// <summary>A row's Qual text and its colour: a stack's corner label (grades below Prospecting 10), a collection's average.</summary>
        public static string QualityLabel(ListRow row, Inventory items, out Color colour)
        {
            ItemStack first = items[row.Slot];
            if (!row.IsCollection && !first.IsEmpty && !(first.Item is ToolDefinition))
            {
                return ItemSlotGui.PurityLabel(first, out colour);
            }
            if (row.Item.QualityAsGrade || (!first.IsEmpty && ItemSlotGui.ReadsAsGrades(first)))
            {
                Grade grade = ProspectReading.GradeOf(row.Quality);
                colour = ProspectReading.ColourOf(grade);
                return ProspectReading.ShortName(grade);
            }
            colour = ItemSlotGui.PurityColour;
            return Clube.Core.QualityText.Percent(row.Quality);
        }

        /// <summary>Coins to two places at most, "-" for nothing: "50c", "0.1c", "0.001c".</summary>
        public static string Coins(float coins)
        {
            return coins <= 0f ? "-" : coins >= 0.01f ? $"{coins:0.##}c" : $"{coins:0.###}c";
        }

        /// <summary>An amount: whole items plain, parts to the hundredth.</summary>
        public static string Amount(float amount)
        {
            return Mathf.Approximately(amount, Mathf.Round(amount)) ? $"{Mathf.Round(amount):0}" : $"{amount:0.##}";
        }
    }
}
