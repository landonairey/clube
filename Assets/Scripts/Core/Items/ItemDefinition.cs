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

        [Tooltip("Volume of one, in mined samples (GL33): ore is 1, the sample it came from. Stations turn volumes into volumes, so a bun is the volume of metal a smelt leaves.")]
        [SerializeField, Min(0.001f)]
        private float unitVolume = 1f;

        [Tooltip("Seconds one burns for as fuel in a furnace (GL35). 0: not a fuel.")]
        [SerializeField, Min(0f)]
        private float burnSeconds;

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

        /// <summary>Volume of one, in mined samples (GL33).</summary>
        public float UnitVolume => unitVolume;

        /// <summary>Seconds one burns for as fuel (GL35); 0 when it isn't a fuel.</summary>
        public float BurnSeconds => burnSeconds;

        public bool IsFuel => burnSeconds > 0f;

        /// <summary>Sets its volume and burn time, for items made in code (tests, tools).</summary>
        public ItemDefinition With(float unitVolume = 1f, float burnSeconds = 0f)
        {
            this.unitVolume = Mathf.Max(0.001f, unitVolume);
            this.burnSeconds = Mathf.Max(0f, burnSeconds);
            return this;
        }
    }
}
