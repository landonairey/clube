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
    /// <para>What stacks (GL131): gathered material, which carries its mix as contents, blends into a stack
    /// of the same item (GL120); anything else stacks only with items of the same quality, so 97% sticks
    /// keep apart from 98% ones.</para>
    /// <para>A stack can hold a part item on top of its whole ones (GL136); parts added to a stack add up,
    /// and a whole one is made when they pass 1. Recipes and counts use whole items.</para>
    /// <para>Capacity is a slot count for now. Tribe progressions and mixed-item bundles
    /// (Chapter 8) will need other capacity rules; they plug in here.</para>
    /// </remarks>
    public sealed class Inventory
    {
        // Qualities closer than this stack together: the same to the percent shown.
        private const float SameQuality = 0.005f;

        private readonly ItemStack[] slots;

        public Inventory(int slotCount)
        {
            slots = new ItemStack[Math.Max(0, slotCount)];
        }

        /// <summary>Raised after any change to the contents.</summary>
        public event Action Changed;

        /// <summary>
        /// Which items a slot takes (GL99: the volume inventory's tool slot takes only tools); null, or
        /// true for every slot and item, for an inventory any item goes anywhere in. Adding and moving keep to it.
        /// </summary>
        public Func<int, ItemDefinition, bool> Accepts { get; set; }

        public int SlotCount => slots.Length;

        public ItemStack this[int slot] => slots[slot];

        /// <summary>
        /// True when items with these contents and quality go onto the stack (GL131): the same item, and
        /// either gathered material (its mix blends in) or the same quality.
        /// </summary>
        public static bool Stacks(ItemStack stack, ItemDefinition item, Composition contents, float quality)
        {
            return !stack.IsEmpty && stack.Item == item
                && (stack.Contents != null || contents != null || Math.Abs(stack.Quality - quality) < SameQuality);
        }

        /// <summary>
        /// Adds items, topping up stacks they stack with first, then filling empty slots.
        /// Returns how many whole items didn't fit (0 when all of them did).
        /// </summary>
        /// <param name="contents">What the items are made of (mixed ore, GL32); a stack topped up with them blends its contents by volume.</param>
        /// <param name="part">A part item on top (GL136), added to a stack it stacks with or put in a slot of its own.</param>
        public int Add(ItemDefinition item, int count = 1, Composition contents = null, int wear = 0, float quality = 1f, float part = 0f)
        {
            count = Math.Max(0, count);
            if (item == null || (count == 0 && part <= ItemStack.PartEpsilon))
            {
                return count;
            }

            int left = count;
            for (int i = 0; i < slots.Length && left > 0; i++)
            {
                if (Stacks(slots[i], item, contents, quality) && slots[i].Space > 0 && Fits(i, item))
                {
                    int moved = Math.Min(left, slots[i].Space);
                    slots[i] = TopUp(slots[i], moved, contents, quality);
                    left -= moved;
                }
            }
            for (int i = 0; i < slots.Length && left > 0; i++)
            {
                if (slots[i].IsEmpty && Fits(i, item))
                {
                    int moved = Math.Min(left, item.MaxStack);
                    slots[i] = new ItemStack(item, moved, contents, wear, quality);
                    left -= moved;
                }
            }
            bool partAdded = part > ItemStack.PartEpsilon && AddPart(item, part, contents, quality);

            if (left != count || partAdded)
            {
                Changed?.Invoke();
            }
            return left;
        }

        /// <summary>
        /// Adds items only onto stacks they stack with, never into an empty slot (GL140: topping up a
        /// station's loaded inputs without taking the slot another input needs). Returns how many didn't fit.
        /// </summary>
        public int AddToStacks(ItemDefinition item, int count, Composition contents = null, float quality = 1f)
        {
            int left = Math.Max(0, count);
            for (int i = 0; i < slots.Length && left > 0 && item != null; i++)
            {
                if (Stacks(slots[i], item, contents, quality) && slots[i].Space > 0 && Fits(i, item))
                {
                    int moved = Math.Min(left, slots[i].Space);
                    slots[i] = TopUp(slots[i], moved, contents, quality);
                    left -= moved;
                }
            }
            if (left != count)
            {
                Changed?.Invoke();
            }
            return left;
        }

        /// <summary>How many whole items of an item the inventory holds across all slots.</summary>
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
        /// as much as fits there, a part item with the whole ones; the rest stays. Returns how many whole items moved.
        /// </summary>
        public int MoveTo(int slot, Inventory target)
        {
            ItemStack stack = slots[slot];
            if (stack.IsEmpty || target == null || target == this)
            {
                return 0;
            }
            int left = target.Add(stack.Item, stack.Count, stack.Contents, stack.Wear, stack.Quality, stack.Part);
            int moved = stack.Count - left;
            slots[slot] = left > 0 ? new ItemStack(stack.Item, left, stack.Contents, stack.Wear, stack.Quality) : ItemStack.Empty;
            Changed?.Invoke();
            return moved;
        }

        /// <summary>
        /// Moves up to <paramref name="count"/> whole items of one slot's stack into another inventory (GL176: half a
        /// stack of wood into a furnace), as many as fit; the rest, and any part item, stay. The whole stack when
        /// <paramref name="count"/> covers it. Returns how many whole items moved.
        /// </summary>
        public int MoveTo(int slot, Inventory target, int count)
        {
            ItemStack stack = slots[slot];
            if (stack.IsEmpty || target == null || target == this || count <= 0)
            {
                return 0;
            }
            if (count >= stack.Count)
            {
                return MoveTo(slot, target);
            }
            int moved = count - target.Add(stack.Item, count, stack.Contents, stack.Wear, stack.Quality);
            if (moved > 0)
            {
                slots[slot] = stack.WithCount(stack.Count - moved);
                Changed?.Invoke();
            }
            return moved;
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

        /// <summary>How well an item held here is made (GL118), blended by count across its stacks; 1 when it isn't here.</summary>
        public float QualityOf(ItemDefinition item)
        {
            float total = 0f;
            int count = 0;
            foreach (ItemStack stack in slots)
            {
                if (stack.Item == item)
                {
                    total += stack.Quality * stack.Count;
                    count += stack.Count;
                }
            }
            return count > 0 ? total / count : 1f;
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
            // A tool lasts its quality's share of its hits (GL118).
            bool broke = wear >= (stack.MaxHits > 0 ? stack.MaxHits : maxHits);
            slots[slot] = broke ? ItemStack.Empty : new ItemStack(stack.Item, stack.Count, stack.Contents, wear, stack.Quality);
            Changed?.Invoke();
            return broke;
        }

        /// <summary>Takes up to <paramref name="count"/> whole items from one slot; returns how many it took. A part item stays.</summary>
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
        /// Puts a stack in a slot in place of what's there (GL177: a refined stack keeps its slot). Nothing changes
        /// when the slot doesn't take the item.
        /// </summary>
        public void Replace(int slot, ItemStack stack)
        {
            if (!Fits(slot, stack.Item))
            {
                return;
            }
            slots[slot] = stack;
            Changed?.Invoke();
        }

        /// <summary>Empties a slot, whole and part items alike (GL136: selling or dropping a whole stack); returns what was there.</summary>
        public ItemStack TakeAll(int slot)
        {
            ItemStack stack = slots[slot];
            if (!stack.IsEmpty)
            {
                slots[slot] = ItemStack.Empty;
                Changed?.Invoke();
            }
            return stack;
        }

        /// <summary>
        /// Removes whole items, from the last stacks first, so the hotbar's are kept longest. All or
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
        /// Moves the stack in one slot onto another: merges into a stack it stacks with as far as it
        /// fits (a part item going with the last whole ones), otherwise the two slots swap.
        /// </summary>
        public void Move(int from, int to)
        {
            if (from == to || slots[from].IsEmpty || !Fits(to, slots[from].Item))
            {
                return;
            }

            ItemStack moving = slots[from];
            if (Stacks(slots[to], moving.Item, moving.Contents, moving.Quality) && (slots[to].Space > 0 || moving.Count == 0))
            {
                int moved = Math.Min(moving.Count, slots[to].Space);
                slots[to] = TopUp(slots[to], moved, moving.Contents, moving.Quality);
                slots[from] = moving.WithCount(moving.Count - moved);
                if (slots[from].Count == 0 && slots[from].Part > 0f)
                {
                    // Everything whole went: the part goes with it.
                    slots[from] = ItemStack.Empty;
                    slots[to] = WithPart(slots[to], moving.Part, out ItemStack spill);
                    slots[from] = spill;
                }
            }
            else if (Fits(from, slots[to].Item))
            {
                (slots[from], slots[to]) = (slots[to], slots[from]);
            }
            else
            {
                return;
            }
            Changed?.Invoke();
        }

        /// <summary>
        /// Moves up to <paramref name="count"/> whole items of an item, from whichever stacks hold it, into
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
                int placed = take - target.Add(item, take, stack.Contents, stack.Wear, stack.Quality);
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
        /// <paramref name="end"/> (GL71: from the backpack to the hotbar, or back): onto stacks it
        /// stacks with that have room first, then into the first empty slot. What doesn't fit stays.
        /// Returns how many whole items moved.
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
                if (slot != from && Stacks(slots[slot], stack.Item, stack.Contents, stack.Quality) && slots[slot].Space > 0 && Fits(slot, stack.Item))
                {
                    int moved = Math.Min(left, slots[slot].Space);
                    slots[slot] = TopUp(slots[slot], moved, stack.Contents, stack.Quality);
                    left -= moved;
                }
            }
            bool partMoved = false;
            for (int slot = start; slot < end && (left > 0 || (!partMoved && stack.Part > 0f)); slot++)
            {
                if (slot != from && slots[slot].IsEmpty && Fits(slot, stack.Item))
                {
                    // The rest, and the part item, into an empty slot.
                    slots[slot] = new ItemStack(stack.Item, left, stack.Contents, stack.Wear, stack.Quality, stack.Part);
                    left = 0;
                    partMoved = true;
                }
            }
            int total = stack.Count - left;
            if (total > 0 || partMoved)
            {
                slots[from] = left > 0 || (!partMoved && stack.Part > 0f) ? new ItemStack(stack.Item, left, stack.Contents, stack.Wear, stack.Quality, partMoved ? 0f : stack.Part) : ItemStack.Empty;
                Changed?.Invoke();
            }
            return total;
        }

        /// <summary>
        /// Tidies a run of slots, <paramref name="start"/> up to but not including <paramref name="end"/> (GL174):
        /// stacks that stack are merged, then the stacks are put in order (tools, then things to build, then
        /// everything else, each by name, better quality and bigger stacks first), empty slots last. A run with
        /// a slot that doesn't take every item (the tool slot) is only merged.
        /// </summary>
        public void Sort(int start, int end)
        {
            start = Math.Max(0, start);
            end = Math.Min(slots.Length, end);
            if (end - start < 2)
            {
                return;
            }

            // Merge: each stack takes what stacks onto it from the slots after it.
            for (int i = start; i < end; i++)
            {
                for (int j = i + 1; j < end && !slots[i].IsEmpty; j++)
                {
                    ItemStack other = slots[j];
                    if (!other.IsEmpty && Stacks(slots[i], other.Item, other.Contents, other.Quality)
                        && (slots[i].Space > 0 || other.Count == 0))
                    {
                        MoveQuietly(j, i);
                    }
                }
            }

            var stacks = new System.Collections.Generic.List<ItemStack>();
            bool everyItemFits = true;
            for (int i = start; i < end; i++)
            {
                if (!slots[i].IsEmpty)
                {
                    stacks.Add(slots[i]);
                }
            }
            for (int i = start; i < end && everyItemFits; i++)
            {
                foreach (ItemStack stack in stacks)
                {
                    everyItemFits &= Fits(i, stack.Item);
                }
            }
            if (everyItemFits)
            {
                stacks.Sort(CompareForSorting);
                for (int i = start; i < end; i++)
                {
                    int index = i - start;
                    slots[i] = index < stacks.Count ? stacks[index] : ItemStack.Empty;
                }
            }
            Changed?.Invoke();
        }

        // Tools first, then things to build, then the rest; by name, better quality first, then bigger stacks first.
        private static int CompareForSorting(ItemStack a, ItemStack b)
        {
            int group = SortGroup(a.Item).CompareTo(SortGroup(b.Item));
            if (group != 0)
            {
                return group;
            }
            int name = string.Compare(a.Item.DisplayName, b.Item.DisplayName, StringComparison.OrdinalIgnoreCase);
            if (name != 0)
            {
                return name;
            }
            int quality = b.Quality.CompareTo(a.Quality);
            return quality != 0 ? quality : b.Amount.CompareTo(a.Amount);
        }

        private static int SortGroup(ItemDefinition item)
        {
            return item is ToolDefinition ? 0 : item is PlaceableDefinition ? 1 : 2;
        }

        // Move, without raising Changed (Sort raises it once).
        private void MoveQuietly(int from, int to)
        {
            ItemStack moving = slots[from];
            int moved = Math.Min(moving.Count, slots[to].Space);
            slots[to] = TopUp(slots[to], moved, moving.Contents, moving.Quality);
            slots[from] = moving.WithCount(moving.Count - moved);
            if (slots[from].Count == 0 && moving.Part > 0f)
            {
                slots[to] = WithPart(BlendPart(slots[to], moving.Part, moving.Contents, moving.Quality), moving.Part, out ItemStack spill);
                slots[from] = spill;
            }
        }

        private bool Fits(int slot, ItemDefinition item)
        {
            return Accepts == null || item == null || Accepts(slot, item);
        }

        // Adds a part item (GL136) to a stack it stacks with, making a whole one when the parts pass 1, or
        // puts it in an empty slot. False when there's nowhere for it.
        private bool AddPart(ItemDefinition item, float part, Composition contents, float quality)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (Stacks(slots[i], item, contents, quality) && Fits(i, item) && (slots[i].Part + part < 1f || slots[i].Space > 0))
                {
                    ItemStack blended = BlendPart(slots[i], part, contents, quality);
                    slots[i] = WithPart(blended, part, out _);
                    return true;
                }
            }
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].IsEmpty && Fits(i, item))
                {
                    slots[i] = new ItemStack(item, 0, contents, 0, quality, part);
                    return true;
                }
            }
            return false;
        }

        // A stack with a part item added: the parts add up, and past 1 make a whole item if there's room.
        // What doesn't fit comes back as a stack of its own.
        private static ItemStack WithPart(ItemStack stack, float part, out ItemStack spill)
        {
            float total = stack.Part + part;
            int whole = stack.Count;
            spill = ItemStack.Empty;
            if (total >= 1f - ItemStack.PartEpsilon)
            {
                if (stack.Space > 0)
                {
                    whole++;
                    total -= 1f;
                }
                else
                {
                    spill = new ItemStack(stack.Item, 0, stack.Contents, 0, stack.Quality, total - stack.Part);
                    total = stack.Part;
                }
            }
            return new ItemStack(stack.Item, whole, stack.Contents, stack.Wear, stack.Quality, Math.Max(0f, total));
        }

        // A stack about to take a part item: its contents and quality blend with the part's by amount (GL172), so
        // every hand hit's mix shows in the stack it joins.
        private static ItemStack BlendPart(ItemStack stack, float part, Composition contents, float quality)
        {
            float amount = stack.Amount;
            if (part <= 0f || amount <= 0f)
            {
                return amount <= 0f ? new ItemStack(stack.Item, stack.Count, contents, stack.Wear, quality, stack.Part) : stack;
            }
            Composition blended = Composition.Blend(stack.Contents, amount, contents, part);
            float mixed = (stack.Quality * amount + quality * part) / (amount + part);
            return new ItemStack(stack.Item, stack.Count, blended, stack.Wear, mixed, stack.Part);
        }

        // A stack with more of its item on top, the contents blended by volume (GL32).
        private static ItemStack TopUp(ItemStack stack, int added, Composition contents, float quality)
        {
            if (added <= 0)
            {
                return stack;
            }
            Composition blended = Composition.Blend(stack.Contents, stack.Count, contents, added);
            // Quality blends by count too (GL118); only gathered mixes differ here (GL131).
            float mixed = (stack.Quality * stack.Count + quality * added) / (stack.Count + added);
            return new ItemStack(stack.Item, stack.Count + added, blended, 0, mixed, stack.Part);
        }
    }
}
