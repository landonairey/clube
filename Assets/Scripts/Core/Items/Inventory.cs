using System;

namespace Clube.Core
{
    /// <summary>
    /// A fixed number of slots holding item stacks (GL6, first pass of N1): adding fills
    /// stacks of the same item first, then empty slots, each up to the item's
    /// <see cref="ItemDefinition.MaxStack"/>. Plain data with no UI, so the player, chests and
    /// stations can each have one, and the rules are unit tested (N4).
    /// </summary>
    /// <remarks>
    /// Capacity is a slot count for now. Tribe progressions and mixed-item bundles
    /// (Chapter 8) will need other capacity rules; they plug in here.
    /// </remarks>
    public sealed class Inventory
    {
        private readonly ItemStack[] slots;

        public Inventory(int slotCount)
        {
            slots = new ItemStack[Math.Max(0, slotCount)];
        }

        /// <summary>Raised after any change to the contents.</summary>
        public event Action Changed;

        public int SlotCount => slots.Length;

        public ItemStack this[int slot] => slots[slot];

        /// <summary>
        /// Adds items, topping up stacks of the same item first, then filling empty slots.
        /// Returns how many didn't fit (0 when all of them did).
        /// </summary>
        /// <param name="contents">What the items are made of (mixed ore, GL32); a stack topped up with them blends its contents by volume.</param>
        public int Add(ItemDefinition item, int count = 1, Composition contents = null, int wear = 0)
        {
            if (item == null || count <= 0)
            {
                return count;
            }

            int left = count;
            for (int i = 0; i < slots.Length && left > 0; i++)
            {
                if (slots[i].Item == item && slots[i].Space > 0)
                {
                    int moved = Math.Min(left, slots[i].Space);
                    slots[i] = TopUp(slots[i], moved, contents);
                    left -= moved;
                }
            }
            for (int i = 0; i < slots.Length && left > 0; i++)
            {
                if (slots[i].IsEmpty)
                {
                    int moved = Math.Min(left, item.MaxStack);
                    slots[i] = new ItemStack(item, moved, contents, wear);
                    left -= moved;
                }
            }

            if (left != count)
            {
                Changed?.Invoke();
            }
            return left;
        }

        /// <summary>How many of an item the inventory holds across all slots.</summary>
        public int Count(ItemDefinition item)
        {
            int total = 0;
            foreach (ItemStack stack in slots)
            {
                if (stack.Item == item)
                {
                    total += stack.Count;
                }
            }
            return total;
        }

        /// <summary>
        /// Moves the stack in one slot into another inventory (a station's slots, a chest),
        /// as much as fits there; the rest stays. Returns how many moved.
        /// </summary>
        public int MoveTo(int slot, Inventory target)
        {
            ItemStack stack = slots[slot];
            if (stack.IsEmpty || target == null || target == this)
            {
                return 0;
            }
            int moved = stack.Count - target.Add(stack.Item, stack.Count, stack.Contents, stack.Wear);
            return RemoveAt(slot, moved);
        }

        /// <summary>
        /// What an item held here is made of, blended by volume across its stacks (GL32); null
        /// when it has no contents or isn't here.
        /// </summary>
        public Composition ContentsOf(ItemDefinition item)
        {
            Composition contents = null;
            int count = 0;
            foreach (ItemStack stack in slots)
            {
                if (stack.Item == item)
                {
                    contents = Composition.Blend(contents, count, stack.Contents, stack.Count);
                    count += stack.Count;
                }
            }
            return contents;
        }

        /// <summary>
        /// Wears the tool in a slot by one hit (GL37). It breaks, leaving the slot empty, once it has
        /// taken <paramref name="maxHits"/>; returns true then. Does nothing for an empty slot.
        /// </summary>
        public bool Wear(int slot, int maxHits)
        {
            ItemStack stack = slots[slot];
            if (stack.IsEmpty)
            {
                return false;
            }
            int wear = stack.Wear + 1;
            bool broke = wear >= maxHits;
            slots[slot] = broke ? ItemStack.Empty : new ItemStack(stack.Item, stack.Count, stack.Contents, wear);
            Changed?.Invoke();
            return broke;
        }

        /// <summary>Takes up to <paramref name="count"/> from one slot; returns how many it took.</summary>
        public int RemoveAt(int slot, int count = 1)
        {
            int taken = Math.Min(Math.Max(0, count), slots[slot].Count);
            if (taken == 0)
            {
                return 0;
            }
            slots[slot] = slots[slot].WithCount(slots[slot].Count - taken);
            Changed?.Invoke();
            return taken;
        }

        /// <summary>
        /// Removes items, from the last stacks first, so the hotbar's are kept longest. All or
        /// nothing: returns false and changes nothing when there aren't enough.
        /// </summary>
        public bool Remove(ItemDefinition item, int count = 1)
        {
            if (item == null || count <= 0)
            {
                return count <= 0;
            }
            if (Count(item) < count)
            {
                return false;
            }

            int left = count;
            for (int i = slots.Length - 1; i >= 0 && left > 0; i--)
            {
                if (slots[i].Item == item)
                {
                    int taken = Math.Min(left, slots[i].Count);
                    slots[i] = slots[i].WithCount(slots[i].Count - taken);
                    left -= taken;
                }
            }
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Moves the stack in one slot onto another: merges into a stack of the same item as
        /// far as it fits, otherwise the two slots swap.
        /// </summary>
        public void Move(int from, int to)
        {
            if (from == to || slots[from].IsEmpty)
            {
                return;
            }

            if (slots[to].Item == slots[from].Item && slots[to].Space > 0)
            {
                int moved = Math.Min(slots[from].Count, slots[to].Space);
                slots[to] = TopUp(slots[to], moved, slots[from].Contents);
                slots[from] = slots[from].WithCount(slots[from].Count - moved);
            }
            else
            {
                (slots[from], slots[to]) = (slots[to], slots[from]);
            }
            Changed?.Invoke();
        }

        /// <summary>
        /// Moves up to <paramref name="count"/> of an item, from whichever stacks hold it, into
        /// another inventory (GL80: a recipe's ingredients into a station), keeping each stack's
        /// contents and wear. Returns how many moved.
        /// </summary>
        public int MoveItemTo(ItemDefinition item, int count, Inventory target)
        {
            if (item == null || target == null || target == this)
            {
                return 0;
            }
            int moved = 0;
            for (int slot = 0; slot < slots.Length && moved < count; slot++)
            {
                ItemStack stack = slots[slot];
                if (stack.Item != item)
                {
                    continue;
                }
                int take = Math.Min(count - moved, stack.Count);
                int placed = take - target.Add(item, take, stack.Contents, stack.Wear);
                if (placed > 0)
                {
                    RemoveAt(slot, placed);
                    moved += placed;
                }
                if (placed < take)
                {
                    break;
                }
            }
            return moved;
        }

        /// <summary>
        /// Moves a stack into a run of slots, <paramref name="start"/> up to but not including
        /// <paramref name="end"/> (GL71: from the backpack to the hotbar, or back): onto stacks of
        /// the same item with room first, then into the first empty slot. What doesn't fit stays.
        /// Returns how many moved.
        /// </summary>
        public int MoveInto(int from, int start, int end)
        {
            ItemStack stack = slots[from];
            if (stack.IsEmpty)
            {
                return 0;
            }
            int left = stack.Count;
            for (int slot = start; slot < end && left > 0; slot++)
            {
                if (slot != from && slots[slot].Item == stack.Item && slots[slot].Space > 0)
                {
                    int moved = Math.Min(left, slots[slot].Space);
                    slots[slot] = TopUp(slots[slot], moved, stack.Contents);
                    left -= moved;
                }
            }
            for (int slot = start; slot < end && left > 0; slot++)
            {
                if (slot != from && slots[slot].IsEmpty)
                {
                    slots[slot] = new ItemStack(stack.Item, left, stack.Contents, stack.Wear);
                    left = 0;
                }
            }
            int total = stack.Count - left;
            if (total > 0)
            {
                slots[from] = left > 0 ? stack.WithCount(left) : ItemStack.Empty;
                Changed?.Invoke();
            }
            return total;
        }

        // A stack with more of its item on top, the contents blended by volume (GL32).
        private static ItemStack TopUp(ItemStack stack, int added, Composition contents)
        {
            Composition blended = Composition.Blend(stack.Contents, stack.Count, contents, added);
            return new ItemStack(stack.Item, stack.Count + added, blended);
        }
    }
}
