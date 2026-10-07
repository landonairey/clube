using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// One kind of item (O6): what extracting a material gives, and later what inventories
    /// hold and recipes use (Chapter 5). An asset, so items are added without code changes.
    /// Each <see cref="VoxelMaterial"/> links the item it drops.
    /// </summary>
    [CreateAssetMenu(fileName = "Item", menuName = "Clube/Item")]
    public class ItemDefinition : ScriptableObject
    {
        [Tooltip("Stable id used in saves and recipes, e.g. \"copper-ore\". Never change it once used.")]
        [SerializeField]
        private string id = "item";

        [SerializeField]
        private string displayName = "Item";

        [Tooltip("Shown in inventories (Chapter 5).")]
        [SerializeField]
        private Texture2D icon;

        /// <summary>Makes an item in code (tests, tools); the game's items are assets.</summary>
        public static ItemDefinition Create(string id, string displayName)
        {
            return Create<ItemDefinition>(id, displayName);
        }

        /// <summary>Makes an item of a derived kind in code (tests, tools).</summary>
        protected static T Create<T>(string id, string displayName) where T : ItemDefinition
        {
            var item = CreateInstance<T>();
            item.id = id;
            item.displayName = displayName;
            item.name = displayName;
            return item;
        }

        public string Id => id;

        public string DisplayName => displayName;

        public Texture2D Icon => icon;
    }
}
