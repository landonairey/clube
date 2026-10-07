using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// One kind of item (O6, I1): what extracting a material gives, what an
    /// <see cref="Inventory"/> holds, and later what recipes use. An asset, so items are added
    /// without code changes. Each <see cref="VoxelMaterial"/> links the item it drops.
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

        [Tooltip("Most of it one inventory slot holds (GL6). 1 for tools.")]
        [SerializeField, Min(1)]
        private int maxStack = 64;

        /// <summary>Makes an item in code (tests, tools); the game's items are assets.</summary>
        public static ItemDefinition Create(string id, string displayName, int maxStack = 64)
        {
            return Create<ItemDefinition>(id, displayName, maxStack);
        }

        /// <summary>Makes an item of a derived kind in code (tests, tools).</summary>
        protected static T Create<T>(string id, string displayName, int maxStack) where T : ItemDefinition
        {
            var item = CreateInstance<T>();
            item.id = id;
            item.displayName = displayName;
            item.maxStack = Mathf.Max(1, maxStack);
            item.name = displayName;
            return item;
        }

        public string Id => id;

        public string DisplayName => displayName;

        public Texture2D Icon => icon;

        /// <summary>Most of it one inventory slot holds (GL6).</summary>
        public int MaxStack => maxStack;
    }
}
