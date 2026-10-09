namespace Clube.Core
{
    /// <summary>
    /// What one inventory slot holds (GL6): an item and how many, or nothing, with what the
    /// items are made of when that varies (mixed ore, GL32). Every item in a stack has the same
    /// <see cref="Contents"/>: adding ore of another grade blends them.
    /// </summary>
    public readonly struct ItemStack
    {
        public static readonly ItemStack Empty = default;

        public ItemStack(ItemDefinition item, int count, Composition contents = null)
        {
            Item = count > 0 ? item : null;
            Count = item != null && count > 0 ? count : 0;
            Contents = Item != null ? contents : null;
        }

        /// <summary>The item, or null for an empty slot.</summary>
        public ItemDefinition Item { get; }

        public int Count { get; }

        /// <summary>What each item is made of by volume (GL32), or null for items that are just themselves.</summary>
        public Composition Contents { get; }

        public bool IsEmpty => Item == null;

        /// <summary>How many more of its item fit on top; 0 for an empty stack (any item fits there).</summary>
        public int Space => IsEmpty ? 0 : Item.MaxStack - Count;

        /// <summary>The whole stack's volume (GL33): count × the item's unit volume.</summary>
        public float Volume => IsEmpty ? 0f : Count * Item.UnitVolume;

        public ItemStack WithCount(int count)
        {
            return new ItemStack(Item, count, Contents);
        }
    }
}
