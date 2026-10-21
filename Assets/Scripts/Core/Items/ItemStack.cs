namespace Clube.Core
{
    /// <summary>
    /// What one inventory slot holds (GL6): an item and how many, or nothing, with what the
    /// items are made of when that varies (mixed ore, GL32), and how well they were made (GL118).
    /// Every item in a stack has the same <see cref="Contents"/> and <see cref="Quality"/>: adding
    /// ore of another grade, or sticks of another quality, blends them.
    /// </summary>
    public readonly struct ItemStack
    {
        public static readonly ItemStack Empty = default;

        // Stored as 1 - quality, so the default (an empty or ordinary stack) is quality 1.
        private readonly float flaw;

        public ItemStack(ItemDefinition item, int count, Composition contents = null, int wear = 0, float quality = 1f)
        {
            Item = count > 0 ? item : null;
            Count = item != null && count > 0 ? count : 0;
            Contents = Item != null ? contents : null;
            Wear = Item != null ? System.Math.Max(0, wear) : 0;
            flaw = Item != null ? 1f - System.Math.Max(0f, System.Math.Min(1f, quality)) : 0f;
        }

        /// <summary>The item, or null for an empty slot.</summary>
        public ItemDefinition Item { get; }

        public int Count { get; }

        /// <summary>What each item is made of by volume (GL32), or null for items that are just themselves.</summary>
        public Composition Contents { get; }

        /// <summary>Hits a tool has taken (GL37); 0 for new tools and for everything that doesn't wear out.</summary>
        public int Wear { get; }

        /// <summary>
        /// How well it was made, 0-1 (GL118): material taken from a bin is as good as it is pure, and what's
        /// crafted is as good as what it was made from. A tool lasts its quality's share of its hits.
        /// </summary>
        public float Quality => 1f - flaw;

        public bool IsEmpty => Item == null;

        /// <summary>How many more of its item fit on top; 0 for an empty stack (any item fits there).</summary>
        public int Space => IsEmpty ? 0 : Item.MaxStack - Count;

        /// <summary>The whole stack's volume (GL33): count × the item's unit volume.</summary>
        public float Volume => IsEmpty ? 0f : Count * Item.UnitVolume;

        /// <summary>The hits a tool of this stack lasts (GL118): the tool's hits at its quality, at least 1; 0 when it never wears out.</summary>
        public int MaxHits => Item is ToolDefinition tool && tool.MaxHits > 0
            ? System.Math.Max(1, (int)System.Math.Round(tool.MaxHits * Quality))
            : 0;

        public ItemStack WithCount(int count)
        {
            return new ItemStack(Item, count, Contents, Wear, Quality);
        }
    }
}
