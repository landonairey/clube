namespace Clube.Core
{
    /// <summary>
    /// What one inventory slot holds (GL6): an item and how many, or nothing, with what the
    /// items are made of when that varies (mixed ore, GL32), and how well they were made (GL118).
    /// Every item in a stack has the same <see cref="Contents"/> and <see cref="Quality"/>: gathered
    /// material of another mix blends in (GL120), but crafted items of another quality make their own
    /// stack (GL131). A stack can hold a part item on top of its whole ones (GL136: 0.48 of a tin bun).
    /// </summary>
    public readonly struct ItemStack
    {
        public static readonly ItemStack Empty = default;

        /// <summary>Parts smaller than this count as nothing.</summary>
        public const float PartEpsilon = 1e-3f;

        /// <summary>The best quality a stack can have (GL151): 1 is ordinary, more is better than ordinary.</summary>
        public const float MaxQuality = 2f;

        // Stored as 1 - quality, so the default (an empty or ordinary stack) is quality 1.
        private readonly float flaw;

        public ItemStack(ItemDefinition item, int count, Composition contents = null, int wear = 0, float quality = 1f, float part = 0f)
        {
            Item = count > 0 || part > PartEpsilon ? item : null;
            Count = Item != null && count > 0 ? count : 0;
            Part = Item != null && part > PartEpsilon ? System.Math.Min(part, 1f - PartEpsilon) : 0f;
            Contents = Item != null ? contents : null;
            Wear = Item != null ? System.Math.Max(0, wear) : 0;
            // Up to MaxQuality: maple sticks make tools better than the ordinary (GL151).
            flaw = Item != null ? 1f - System.Math.Max(0f, System.Math.Min(MaxQuality, quality)) : 0f;
        }

        /// <summary>The item, or null for an empty slot.</summary>
        public ItemDefinition Item { get; }

        public int Count { get; }

        /// <summary>A part item on top of the whole ones, 0-1 (GL136: a smelt's yield); recipes use whole items only.</summary>
        public float Part { get; }

        /// <summary>Whole and part items together: 2.48 tin buns.</summary>
        public float Amount => Count + Part;

        /// <summary>What each item is made of by volume (GL32), or null for items that are just themselves.</summary>
        public Composition Contents { get; }

        /// <summary>Hits a tool has taken (GL37); 0 for new tools and for everything that doesn't wear out.</summary>
        public int Wear { get; }

        /// <summary>
        /// How well it was made (GL118): 1 is ordinary, less for impure material, more (up to <see cref="MaxQuality"/>)
        /// for finer stuff (GL151: maple sticks). What's crafted is as good as what it was made from, and a tool
        /// lasts its quality times its hits.
        /// </summary>
        public float Quality => 1f - flaw;

        public bool IsEmpty => Item == null;

        /// <summary>
        /// How many more whole items fit on top; 0 for an empty stack (any item fits there). A stack never holds more
        /// than its item's <see cref="ItemDefinition.MaxStack"/> in all, part items included (GL188), so a part takes the
        /// place of a whole item here: 1023.7 stone has room for 0.3 more, but no whole one.
        /// </summary>
        public int Space => IsEmpty ? 0 : System.Math.Max(0, Item.MaxStack - Count - (Part > 0f ? 1 : 0));

        /// <summary>The whole stack's volume (GL33): count × the item's unit volume.</summary>
        public float Volume => IsEmpty ? 0f : Amount * Item.UnitVolume;

        /// <summary>The hits a tool of this stack lasts (GL118): the tool's hits at its quality, at least 1; 0 when it never wears out.</summary>
        public int MaxHits => Item is ToolDefinition tool && tool.MaxHits > 0
            ? System.Math.Max(1, (int)System.Math.Round(tool.MaxHits * Quality))
            : 0;

        public ItemStack WithCount(int count)
        {
            return new ItemStack(Item, count, Contents, Wear, Quality, Part);
        }
    }
}
