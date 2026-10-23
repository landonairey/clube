using System.Collections.Generic;

namespace Clube.Core
{
    /// <summary>
    /// Crushing ore on a boulder (GL148): the ore minerals stay, and half of everything else (the stone
    /// and soil it was mined with) is knocked away as crushed stone. One item of raw ore makes one item of
    /// crushed ore, richer in its mineral, which smelts at a better yield (GL128) and sells for more (GL130).
    /// Plain data.
    /// </summary>
    public static class Crushing
    {
        /// <summary>The share of the rock that a crush knocks away.</summary>
        public const float RockRemoved = 0.5f;

        /// <summary>
        /// What an item of ore made of <paramref name="contents"/> holds once crushed: its ore minerals
        /// as they were, and half its other contents. <paramref name="crushedStone"/> is how much of an
        /// item was knocked away, the crushed stone that comes with it (0.41 of an item from ore that was
        /// 82% stone). Null contents (pure ore) stay null, with nothing knocked away.
        /// </summary>
        public static Composition Crush(Composition contents, out float crushedStone)
        {
            crushedStone = 0f;
            if (contents == null)
            {
                return null;
            }
            var kept = new List<Content>();
            foreach (Content part in contents.Parts)
            {
                bool ore = part.Material != null && part.Material.Category == VoxelMaterialCategory.Ore;
                float share = ore ? part.Fraction : part.Fraction * (1f - RockRemoved);
                crushedStone += part.Fraction - share;
                kept.Add(new Content(part.Material, share));
            }
            return Composition.From(kept);
        }
    }
}
