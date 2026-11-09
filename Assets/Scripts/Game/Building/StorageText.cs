using System.Collections.Generic;
using System.Globalization;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A storage's contents as a short line of text for the save's named values (GL475's cave crates, GL477's cart crates):
    /// slots as "id*count*quality|...".
    /// </summary>
    public static class StorageText
    {
        /// <summary>The storage's items as text.</summary>
        public static string Encode(Inventory storage)
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

        /// <summary>Empties the storage and fills it from text made by <see cref="Encode"/>; unknown items are skipped.</summary>
        public static void Fill(Inventory storage, string text)
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
    }
}
