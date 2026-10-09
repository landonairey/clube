using System;
using System.Collections.Generic;
using System.Text;

namespace Clube.Core
{
    /// <summary>One part of a <see cref="Composition"/>: a material and its share of the volume.</summary>
    public readonly struct Content
    {
        public Content(VoxelMaterial material, float fraction)
        {
            Material = material;
            Fraction = fraction;
        }

        public VoxelMaterial Material { get; }

        /// <summary>Share of the item's volume, 0-1.</summary>
        public float Fraction { get; }
    }

    /// <summary>
    /// What an item is made of, by volume (GL32, first pass of PK1): mined copper ore might be
    /// 80% stone and 20% copper. Fractions always add up to 1. Immutable, so stacks can share one.
    /// Items without contents (tools, buns, ingots) carry none (null).
    /// </summary>
    public sealed class Composition
    {
        private readonly Content[] parts;

        private Composition(Content[] parts)
        {
            this.parts = parts;
        }

        public IReadOnlyList<Content> Parts => parts;

        /// <summary>
        /// Contents from shares in any units: merges repeats of a material, drops empty shares
        /// and scales the rest to add up to 1. Null when nothing is left.
        /// </summary>
        public static Composition From(IEnumerable<Content> shares)
        {
            var merged = new List<Content>();
            float total = 0f;
            foreach (Content share in shares)
            {
                if (share.Material == null || share.Fraction <= 0f)
                {
                    continue;
                }
                total += share.Fraction;
                int at = merged.FindIndex(c => c.Material == share.Material);
                if (at >= 0)
                {
                    merged[at] = new Content(share.Material, merged[at].Fraction + share.Fraction);
                }
                else
                {
                    merged.Add(share);
                }
            }
            if (total <= 0f)
            {
                return null;
            }
            // Largest share first, so it reads as "80% stone, 20% copper".
            merged.Sort((a, b) => b.Fraction.CompareTo(a.Fraction));
            var parts = new Content[merged.Count];
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = new Content(merged[i].Material, merged[i].Fraction / total);
            }
            return new Composition(parts);
        }

        public static Composition From(params Content[] shares)
        {
            return From((IEnumerable<Content>)shares);
        }

        /// <summary>
        /// Two lots mixed together, weighted by their volumes (a stack topped up with ore of
        /// another grade). A lot without contents counts as all of nothing in particular and is
        /// left out, so mixing with null keeps the other's contents.
        /// </summary>
        public static Composition Blend(Composition a, float volumeA, Composition b, float volumeB)
        {
            if (a == null || volumeA <= 0f)
            {
                return b;
            }
            if (b == null || volumeB <= 0f || Same(a, b))
            {
                return a;
            }
            var shares = new List<Content>();
            foreach (Content part in a.parts)
            {
                shares.Add(new Content(part.Material, part.Fraction * volumeA));
            }
            foreach (Content part in b.parts)
            {
                shares.Add(new Content(part.Material, part.Fraction * volumeB));
            }
            return From(shares);
        }

        /// <summary>The share of a material, 0 when it has none.</summary>
        public float FractionOf(VoxelMaterial material)
        {
            foreach (Content part in parts)
            {
                if (part.Material == material)
                {
                    return part.Fraction;
                }
            }
            return 0f;
        }

        /// <summary>True when both hold the same shares (within a rounding error), or are both null.</summary>
        public static bool Same(Composition a, Composition b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }
            if (a == null || b == null || a.parts.Length != b.parts.Length)
            {
                return false;
            }
            foreach (Content part in a.parts)
            {
                if (Math.Abs(b.FractionOf(part.Material) - part.Fraction) > 1e-4f)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>"80% Stone, 20% Copper".</summary>
        public override string ToString()
        {
            var text = new StringBuilder();
            foreach (Content part in parts)
            {
                if (text.Length > 0)
                {
                    text.Append(", ");
                }
                float percent = part.Fraction * 100f;
                text.Append(percent >= 1f ? $"{percent:0}%" : $"{percent:0.#}%");
                text.Append(' ').Append(part.Material.DisplayName);
            }
            return text.ToString();
        }
    }
}
