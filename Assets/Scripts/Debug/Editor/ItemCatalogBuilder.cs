using System.Collections.Generic;
using System.Linq;
using Clube.Core;
using UnityEditor;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Keeps every <see cref="ItemCatalog"/> listing all item assets (GL43), sorted by name:
    /// rebuilt whenever an asset is imported, deleted or moved, and from
    /// <i>Clube → Items → Rebuild item catalog</i>.
    /// </summary>
    public class ItemCatalogBuilder : AssetPostprocessor
    {
        [MenuItem("Clube/Items/Rebuild item catalog")]
        public static void RebuildAll()
        {
            List<ItemDefinition> items = AssetDatabase.FindAssets("t:" + nameof(ItemDefinition))
                .Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(item => item != null)
                .OrderBy(item => item.DisplayName)
                .ToList();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(ItemCatalog)))
            {
                var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                if (catalog == null || catalog.Items.SequenceEqual(items))
                {
                    continue;
                }
                catalog.SetItems(items);
                EditorUtility.SetDirty(catalog);
            }
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(path => path.EndsWith(".asset")))
            {
                // After the import finishes, so the new assets load.
                EditorApplication.delayCall -= RebuildAll;
                EditorApplication.delayCall += RebuildAll;
            }
        }
    }
}
