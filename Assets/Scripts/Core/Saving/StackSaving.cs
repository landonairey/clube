using System;
using System.Collections.Generic;

namespace Clube.Core
{
    /// <summary>
    /// Turns stacks and inventories into save data and back (S3, GL205): items by id, contents by material
    /// id. Loading looks items up through a function, so the game can resolve items made at run time
    /// (the per-ore "? ore" copies, GL90) as well as the catalog's.
    /// </summary>
    public static class StackSaving
    {
        public static StackSave Capture(ItemStack stack)
        {
            if (stack.IsEmpty)
            {
                return default;
            }
            return new StackSave
            {
                Item = stack.Item.Id,
                Count = stack.Count,
                Part = stack.Part,
                Wear = stack.Wear,
                Quality = stack.Quality,
                Contents = Capture(stack.Contents),
            };
        }

        public static List<ContentSave> Capture(Composition contents)
        {
            if (contents == null)
            {
                return null;
            }
            var saved = new List<ContentSave>(contents.Parts.Count);
            foreach (Content part in contents.Parts)
            {
                if (part.Material != null)
                {
                    saved.Add(new ContentSave { Material = part.Material.Id, Fraction = part.Fraction });
                }
            }
            return saved;
        }

        /// <summary>Every slot of an inventory, empty ones included, so each stack comes back to its slot.</summary>
        public static List<StackSave> Capture(Inventory inventory)
        {
            var slots = new List<StackSave>(inventory.SlotCount);
            for (int slot = 0; slot < inventory.SlotCount; slot++)
            {
                slots.Add(Capture(inventory[slot]));
            }
            return slots;
        }

        /// <summary>
        /// The stack a save holds; empty when its item can't be found (removed since the save was made).
        /// </summary>
        public static ItemStack Restore(StackSave save, Func<string, ItemDefinition> findItem, MaterialRegistry materials)
        {
            if (save.IsEmpty)
            {
                return ItemStack.Empty;
            }
            ItemDefinition item = findItem(save.Item);
            if (item == null)
            {
                return ItemStack.Empty;
            }
            return new ItemStack(item, save.Count, Restore(save.Contents, materials), save.Wear, save.Quality, save.Part);
        }

        public static Composition Restore(List<ContentSave> saved, MaterialRegistry materials)
        {
            if (saved == null || saved.Count == 0 || materials == null)
            {
                return null;
            }
            var parts = new List<Content>(saved.Count);
            foreach (ContentSave part in saved)
            {
                VoxelMaterial material = materials.Get(part.Material);
                if (material != null)
                {
                    parts.Add(new Content(material, part.Fraction));
                }
            }
            return parts.Count > 0 ? Composition.From(parts) : null;
        }

        /// <summary>
        /// Fills an inventory slot by slot from a save, emptying what was there first (starting items).
        /// Slots past either end are left alone. Returns how many stacks came back.
        /// </summary>
        public static int Restore(Inventory inventory, IReadOnlyList<StackSave> slots, Func<string, ItemDefinition> findItem, MaterialRegistry materials)
        {
            int restored = 0;
            for (int slot = 0; slot < inventory.SlotCount; slot++)
            {
                inventory.TakeAll(slot);
                if (slot >= slots.Count)
                {
                    continue;
                }
                ItemStack stack = Restore(slots[slot], findItem, materials);
                if (!stack.IsEmpty)
                {
                    inventory.Replace(slot, stack);
                    restored++;
                }
            }
            return restored;
        }
    }
}
