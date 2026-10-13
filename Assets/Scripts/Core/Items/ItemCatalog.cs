using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Every item in the game, in one asset (GL43): lets code find an item by its id or name
    /// without the asset database, e.g. cheat commands, and later saves (S3). Kept current by
    /// the editor whenever an item asset changes (<c>ItemCatalogBuilder</c>).
    /// </summary>
    [CreateAssetMenu(fileName = "ItemCatalog", menuName = "Clube/Item Catalog")]
    public class ItemCatalog : ScriptableObject
    {
        [SerializeField]
        private List<ItemDefinition> items = new List<ItemDefinition>();

        /// <summary>Makes a catalog in code (tests); the game's is an asset.</summary>
        public static ItemCatalog Create(params ItemDefinition[] items)
        {
            var catalog = CreateInstance<ItemCatalog>();
            catalog.items.AddRange(items);
            return catalog;
        }

        public IReadOnlyList<ItemDefinition> Items => items;

        /// <summary>
        /// The item whose id or display name matches, ignoring case and treating spaces, dashes and
        /// underscores alike ("copper ingot", "copper-ingot", "Copper_Ingot"); null when none does.
        /// </summary>
        public ItemDefinition Find(string idOrName)
        {
            string wanted = Simplify(idOrName);
            if (wanted.Length == 0)
            {
                return null;
            }
            foreach (ItemDefinition item in items)
            {
                if (item != null && (Simplify(item.Id) == wanted || Simplify(item.DisplayName) == wanted))
                {
                    return item;
                }
            }
            return null;
        }

        /// <summary>Replaces the list (the editor's catalog builder).</summary>
        public void SetItems(IEnumerable<ItemDefinition> all)
        {
            items.Clear();
            items.AddRange(all);
        }

        private static string Simplify(string text)
        {
            return string.IsNullOrEmpty(text) ? "" : text.Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");
        }
    }
}
