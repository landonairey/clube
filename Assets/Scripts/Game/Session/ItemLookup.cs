using System.Collections.Generic;
using Clube.Core;

namespace Clube.Game
{
    /// <summary>
    /// Finds the item a saved id names (GL205): the catalog's items by exact id, then the "? ore" copies the
    /// player's <see cref="OreKnowledge"/> makes at run time (GL90).
    /// </summary>
    public sealed class ItemLookup
    {
        private readonly Dictionary<string, ItemDefinition> byId = new Dictionary<string, ItemDefinition>();
        private readonly OreKnowledge ores;

        public ItemLookup(ItemCatalog catalog, OreKnowledge ores)
        {
            this.ores = ores;
            if (catalog == null)
            {
                return;
            }
            foreach (ItemDefinition item in catalog.Items)
            {
                if (item != null && !byId.ContainsKey(item.Id))
                {
                    byId.Add(item.Id, item);
                }
            }
        }

        /// <summary>The item with an id; null when there's none (an item removed since the save was made).</summary>
        public ItemDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }
            if (byId.TryGetValue(id, out ItemDefinition item))
            {
                return item;
            }
            return ores != null ? ores.DisguiseById(id, real => byId.TryGetValue(real, out ItemDefinition found) ? found : null) : null;
        }
    }
}
