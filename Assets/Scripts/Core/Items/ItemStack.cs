namespace Clube.Core
{
    /// <summary>What one inventory slot holds (GL6): an item and how many, or nothing.</summary>
    public readonly struct ItemStack
    {
        public static readonly ItemStack Empty = default;

        public ItemStack(ItemDefinition item, int count)
        {
            Item = count > 0 ? item : null;
            Count = item != null && count > 0 ? count : 0;
        }

        /// <summary>The item, or null for an empty slot.</summary>
        public ItemDefinition Item { get; }

        public int Count { get; }

        public bool IsEmpty => Item == null;

        /// <summary>How many more of its item fit on top; 0 for an empty stack (any item fits there).</summary>
        public int Space => IsEmpty ? 0 : Item.MaxStack - Count;

        public ItemStack WithCount(int count)
        {
            return new ItemStack(Item, count);
        }
    }
}
