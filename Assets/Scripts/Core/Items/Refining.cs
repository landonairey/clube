using System;
using System.Collections.Generic;

namespace Clube.Core
{
    /// <summary>
    /// Picking the impurities out of a gathered stack by hand (GL177): clay that came up with dirt and grass,
    /// worked over on a boulder, gets purer a pass at a time. Each pass takes out a set volume of whatever isn't
    /// the stack's own material (GL190: a little at a time, so a big stack shows steady progress), so the stack
    /// shrinks to what's left and its quality rises, and what came out is handed back as amounts of each
    /// material. The stack's own material is never taken (GL186: crushed cassiterite keeps its cassiterite).
    /// Plain data; the player's side is in <c>BoulderBashing</c>.
    /// </summary>
    public static class Refining
    {
        // Impurities below this share are picked out entirely by the pass that gets them there.
        private const float Clean = 0.005f;

        /// <summary>True for a stack with a mix to pick over: gathered material that isn't all its own material.</summary>
        public static bool CanRefine(ItemStack stack, MaterialRegistry materials)
        {
            return !stack.IsEmpty && stack.Contents != null && materials != null && !stack.Item.HidesContents
                && materials.Quality(stack.Contents, stack.Item) < 1f - Clean;
        }

        /// <summary>
        /// One pass over the stack: up to <paramref name="volume"/> items' worth of impurities come out, shared
        /// between them by how much of each there is, or all of them once what would be left is under half a
        /// percent. Returns the refined stack (empty if nothing of its own material was in it) and adds what came
        /// out to <paramref name="removed"/>, in items' worth of each material.
        /// </summary>
        public static ItemStack Pass(ItemStack stack, MaterialRegistry materials, float volume, List<Content> removed)
        {
            if (!CanRefine(stack, materials))
            {
                return stack;
            }
            float amount = stack.Amount;
            float own = materials.Quality(stack.Contents, stack.Item);
            float impurity = 1f - own;
            float impurityAmount = amount * impurity;
            // The share of each impurity this pass takes.
            float share = impurityAmount > 0f ? Math.Max(0f, Math.Min(1f, volume / impurityAmount)) : 1f;
            // The last bit comes out with the pass that gets it this low.
            if (impurity * (1f - share) / (own + impurity * (1f - share)) < Clean)
            {
                share = 1f;
            }

            var left = new List<Content>();
            float leftAmount = 0f;
            foreach (Content part in stack.Contents.Parts)
            {
                float partVolume = amount * part.Fraction;
                if (materials.IsSourceOf(part.Material, stack.Item))
                {
                    left.Add(new Content(part.Material, partVolume));
                    leftAmount += partVolume;
                    continue;
                }
                float taken = partVolume * share;
                removed?.Add(new Content(part.Material, taken));
                if (partVolume - taken > 0f)
                {
                    left.Add(new Content(part.Material, partVolume - taken));
                    leftAmount += partVolume - taken;
                }
            }

            Composition contents = Composition.From(left);
            if (contents == null || leftAmount <= ItemStack.PartEpsilon)
            {
                return ItemStack.Empty;
            }
            int count = (int)(leftAmount + 1e-4f);
            float partItem = Math.Max(0f, leftAmount - count);
            if (count >= stack.Item.MaxStack)
            {
                count = stack.Item.MaxStack;
                partItem = 0f;
            }
            return new ItemStack(stack.Item, count, contents, 0, materials.Quality(contents, stack.Item), partItem);
        }
    }
}
