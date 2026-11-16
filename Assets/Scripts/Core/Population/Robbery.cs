using System;
using System.Collections.Generic;

namespace Clube.Core
{
    /// <summary>
    /// What a thief takes (GL617): whole stacks out of an inventory (a cart's crate, a vessel's, the player's bag) and a
    /// share of a purse, moved into the thief's own, never made or lost: whatever is taken can be taken back, from the
    /// thief or from the camp's stash.
    /// </summary>
    public static class Robbery
    {
        /// <summary>
        /// Moves up to <paramref name="stacks"/> stacks, drawn at random from <paramref name="from"/> slot
        /// <paramref name="firstSlot"/> on, into <paramref name="to"/>, as much of each as fits there. Returns how many
        /// whole items moved, and lists them in <paramref name="taken"/> when given.
        /// </summary>
        public static int Take(Inventory from, Inventory to, int stacks, Random random, List<ItemStack> taken = null, int firstSlot = 0)
        {
            if (from == null || to == null || from == to)
            {
                return 0;
            }
            var full = new List<int>();
            for (int slot = Math.Max(0, firstSlot); slot < from.SlotCount; slot++)
            {
                if (!from[slot].IsEmpty && from[slot].Count > 0)
                {
                    full.Add(slot);
                }
            }
            int moved = 0;
            for (int n = 0; n < stacks && full.Count > 0; n++)
            {
                int pick = random.Next(full.Count);
                int slot = full[pick];
                full.RemoveAt(pick);
                ItemDefinition item = from[slot].Item;
                int count = from.MoveTo(slot, to);
                if (count > 0)
                {
                    moved += count;
                    taken?.Add(new ItemStack(item, count));
                }
            }
            return moved;
        }

        /// <summary>Everything in <paramref name="from"/> that fits into <paramref name="to"/>; returns how many whole items moved.</summary>
        public static int TakeAll(Inventory from, Inventory to)
        {
            int moved = 0;
            for (int slot = 0; from != null && to != null && from != to && slot < from.SlotCount; slot++)
            {
                moved += from.MoveTo(slot, to);
            }
            return moved;
        }

        /// <summary>True when an inventory holds nothing.</summary>
        public static bool IsEmpty(Inventory inventory)
        {
            for (int slot = 0; inventory != null && slot < inventory.SlotCount; slot++)
            {
                if (!inventory[slot].IsEmpty)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Moves a share of a purse's coins (whole coins, at most <paramref name="most"/>) into another; returns how many.
        /// </summary>
        public static float TakeCoins(Wallet from, Wallet to, float share, float most)
        {
            if (from == null || to == null || from == to)
            {
                return 0f;
            }
            float coins = (float)Math.Floor(Math.Min(most, from.Coins * Math.Max(0f, Math.Min(1f, share))));
            if (coins <= 0f || !from.Spend(coins))
            {
                return 0f;
            }
            to.Earn(coins);
            return coins;
        }
    }
}
