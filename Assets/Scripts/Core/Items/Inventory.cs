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
        public int Add(ItemDefinition item, int count = 1)
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
                    slots[i] = slots[i].WithCount(slots[i].Count + moved);
                    left -= moved;
                }
            }
            for (int i = 0; i < slots.Length && left > 0; i++)
            {
                if (slots[i].IsEmpty)
                {
                    int moved = Math.Min(left, item.MaxStack);
                    slots[i] = new ItemStack(item, moved);
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
                slots[to] = slots[to].WithCount(slots[to].Count + moved);
                slots[from] = slots[from].WithCount(slots[from].Count - moved);
            }
            else
            {
                (slots[from], slots[to]) = (slots[to], slots[from]);
            }
            Changed?.Invoke();
        }
    }
}
