using System;
using System.Collections.Generic;

namespace Clube.Core
{
    /// <summary>
    /// Picking the impurities out of a gathered stack by hand (GL177): clay that came up with dirt and grass,
    /// worked over on a boulder, gets purer a pass at a time. Each pass takes a set volume of one of the materials that
    /// aren't the stack's own (GL386: one material at a time), so the stack shrinks to what's left and its quality rises,
    /// and what came out is handed back. The purer the stack, the more often a pass fails to find anything to pick out
    /// (<see cref="FailChance"/>): a poor stack cleans up fast, the last few percent are slow work. The stack's own
    /// material is never taken (GL186: crushed cassiterite keeps its cassiterite). Plain data; the player's side is in
    /// <c>BoulderBashing</c>.
    /// </summary>
    public static class Refining
    {
        // Impurities below this share are picked out entirely by the pass that gets them there.
        private const float Clean = 0.005f;

        // Fail chance by the stack's quality (GL386): below each quality, the chance a pass picks nothing out.
        private static readonly (float Below, float Fail)[] FailBands =
        {
            (0.50f, 0f),
            (0.60f, 0.10f),
            (0.70f, 0.20f),
            (0.80f, 0.35f),
            (0.85f, 0.40f),
            (0.90f, 0.47f),
            (0.95f, 0.63f),
            (0.97f, 0.75f),
            (0.98f, 0.85f),
            (0.99f, 0.95f),
        };

        /// <summary>The highest fail chance, from 99% quality up.</summary>
        public const float TopFailChance = 0.99f;

        /// <summary>
        /// The chance a pass over a stack of this quality (0-1) fails to pick anything out (GL386): none under 50%, 10% at
        /// 50-60%, rising to 95% at 98-99% and 99% above.
        /// </summary>
        public static float FailChance(float quality)
        {
            foreach ((float below, float fail) in FailBands)
            {
                if (quality < below)
                {
                    return fail;
                }
            }
            return TopFailChance;
        }

        /// <summary>True for a stack with a mix to pick over: gathered material that isn't all its own material.</summary>
        public static bool CanRefine(ItemStack stack, MaterialRegistry materials)
        {
            return !stack.IsEmpty && stack.Contents != null && materials != null && !stack.Item.HidesContents
                && materials.Quality(stack.Contents, stack.Item) < 1f - Clean;
        }

        /// <summary>A pass that never fails, on the first impurity by the pick (tests, and anything that needs no luck).</summary>
        public static ItemStack Pass(ItemStack stack, MaterialRegistry materials, float volume, List<Content> removed)
        {
            return Pass(stack, materials, volume, removed, 1f, 0f, out _);
        }

        /// <summary>
        /// One pass over the stack. Unless it fails (<paramref name="roll"/> under <see cref="FailChance"/> of the stack's
        /// quality), up to <paramref name="volume"/> items' worth of one impurity comes out, picked by
        /// <paramref name="pick"/> (0-1) weighted by how much of each there is; all the impurities come out once what would
        /// be left is under half a percent. Returns the refined stack (empty if nothing of its own material was in it) and
        /// adds what came out to <paramref name="removed"/>, in items' worth of each material.
        /// </summary>
        public static ItemStack Pass(ItemStack stack, MaterialRegistry materials, float volume, List<Content> removed, float roll, float pick, out bool failed)
        {
            failed = false;
            if (!CanRefine(stack, materials))
            {
                return stack;
            }
            float amount = stack.Amount;
            float own = materials.Quality(stack.Contents, stack.Item);
            if (roll < FailChance(own))
            {
                failed = true;
                return stack;
            }

            // The impurity this pass works on, by the pick weighted by share.
            float impurity = 1f - own;
            VoxelMaterial target = null;
            float through = pick * impurity;
            foreach (Content part in stack.Contents.Parts)
            {
                if (materials.IsSourceOf(part.Material, stack.Item))
                {
                    continue;
                }
                target = part.Material;
                through -= part.Fraction;
                if (through < 0f)
                {
                    break;
                }
            }

            float targetVolume = 0f;
            foreach (Content part in stack.Contents.Parts)
            {
                if (part.Material == target)
                {
                    targetVolume += amount * part.Fraction;
                }
            }
            float taken = Math.Min(volume, targetVolume);
            // The last bit comes out with the pass that gets it this low.
            float impurityLeft = amount * impurity - taken;
            bool all = impurityLeft / (amount * own + impurityLeft) < Clean;

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
                float takenHere = all ? partVolume : part.Material == target ? partVolume * (targetVolume > 0f ? taken / targetVolume : 0f) : 0f;
                if (takenHere > 0f)
                {
                    removed?.Add(new Content(part.Material, takenHere));
                }
                if (partVolume - takenHere > 0f)
                {
                    left.Add(new Content(part.Material, partVolume - takenHere));
                    leftAmount += partVolume - takenHere;
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
