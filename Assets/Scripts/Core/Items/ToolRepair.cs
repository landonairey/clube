using System;

namespace Clube.Core
{
    /// <summary>
    /// Mending a worn tool with its repair item (GL286: a copper tool with a copper ingot, bronze with bronze). Each item
    /// gives back <see cref="ToolDefinition.RepairShare"/> of the tool's hits, but a share of every hit given back
    /// (<see cref="ToolDefinition.WearLostPerRepair"/>) is lost for good: the tool's quality, and so the hits it can ever
    /// have, drops by it. That loss shows grey on the tool's bar, and builds up, so a tool mended often never comes back to
    /// full. Stone and wood tools have no repair item and just wear out.
    /// </summary>
    public static class ToolRepair
    {
        /// <summary>True when <paramref name="item"/> mends this tool stack and it has wear to mend.</summary>
        public static bool CanRepair(ItemStack tool, ItemDefinition item)
        {
            return tool.Item is ToolDefinition definition && definition.RepairItem != null && item == definition.RepairItem
                   && tool.Wear > 0 && tool.MaxHits > 0;
        }

        /// <summary>The tool after one repair item: less wear, and a lower quality for what was lost for good.</summary>
        public static ItemStack Repaired(ItemStack tool)
        {
            var definition = (ToolDefinition)tool.Item;
            int mended = Math.Min(tool.Wear, Math.Max(1, (int)Math.Round(definition.MaxHits * definition.RepairShare)));
            float lost = mended * definition.WearLostPerRepair / definition.MaxHits;
            float quality = Math.Max(0.05f, tool.Quality - lost);
            // The hits lost for good are in the lower quality; the wear left can't be more than the hits the tool now has.
            int maxHits = Math.Max(1, (int)Math.Round(definition.MaxHits * quality));
            int wear = Math.Min(tool.Wear - mended, maxHits - 1);
            return new ItemStack(tool.Item, tool.Count, tool.Contents, Math.Max(0, wear), quality, tool.Part);
        }

        /// <summary>
        /// Mends the tool in <paramref name="toolSlot"/> with the repair items in <paramref name="itemSlot"/>, one at a time
        /// until it's whole, the items run out or <paramref name="most"/> are used. Returns how many were used.
        /// </summary>
        public static int Repair(Inventory tools, int toolSlot, Inventory items, int itemSlot, int most = int.MaxValue)
        {
            int used = 0;
            while (used < most && CanRepair(tools[toolSlot], items[itemSlot].Item) && items[itemSlot].Count > 0)
            {
                tools.Set(toolSlot, Repaired(tools[toolSlot]));
                items.RemoveAt(itemSlot, 1);
                used++;
            }
            return used;
        }
    }
}
