using System;
using System.Collections.Generic;
using Clube.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace Clube.Game
{
    /// <summary>
    /// The player's <see cref="Inventory"/> (GL6), in one of two layouts. The classic one, the default (again
    /// since GL120): the first <see cref="HotbarSlots"/> slots are the hotbar (<see cref="Hotbar"/>), the rest
    /// the backpack, and anything goes in any slot. The tool-slot inventory (GL99), kept switchable: a tool
    /// slot (slot 0, tools only) and item slots. Either way what the tools gather comes in as stacks sorted by
    /// what was aimed at (<see cref="Gathering"/>). Each layout keeps its own items; switching doesn't move
    /// them. Starts with the starting items; what doesn't fit is reported through <see cref="Rejected"/> and lost.
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        /// <summary>The tool-slot inventory's tool slot (GL99).</summary>
        public const int ToolSlot = 0;

        [Tooltip("The tool-slot inventory (GL99, GL120): a tool slot and item slots along the bottom. Off: the classic hotbar and backpack.")]
        [SerializeField, FormerlySerializedAs("volumeInventory")]
        private bool toolSlotLayout;

        [Tooltip("Tool-slot inventory: item slots beside the tool slot.")]
        [SerializeField, Range(1, 8)]
        private int itemSlots = 8;

        [Tooltip("Classic inventory: slots in the hotbar, picked with the number keys.")]
        [SerializeField, Range(1, 9)]
        private int hotbarSlots = 9;

        [Tooltip("Classic inventory: slots in the backpack, behind the hotbar.")]
        [SerializeField, Min(0)]
        private int backpackSlots = 18;

        [Tooltip("The list inventory (prototype): no hotbar; two hands of 5 slots each, scrolled through with the wheel (Ctrl: left hand), and a bag shown as a list. Off: the classic grid.")]
        [SerializeField]
        private bool listLayout = true;

        [Tooltip("List inventory: slots in the bag, behind the hands. The list shows them all, so the number only caps how many different stacks fit.")]
        [SerializeField, Min(10)]
        private int bagSlots = 120;

        [Tooltip("What the player starts with, in order: the first lands in hotbar slot 1.")]
        [SerializeField]
        private List<StartingItem> startingItems = new List<StartingItem>();

        [Tooltip("Collects what the player's tools break loose. Optional.")]
        [SerializeField]
        private PlayerToolUser tools;

        private Inventory classic;
        private Inventory toolSlotItems;
        private Inventory listItems;

        /// <summary>List inventory: slots each hand holds, picked with the wheel.</summary>
        public const int HandSlots = 5;

        /// <summary>Raised for what a collected stack put in, with the amount that went in (parts included).</summary>
        public event Action<ItemDefinition, float> Added;

        /// <summary>Raised for each collected item that didn't fit.</summary>
        public event Action<ItemDefinition> Rejected;

        /// <summary>The slots in use: the tool slot and item slots, or the classic hotbar and backpack.</summary>
        public Inventory Inventory => toolSlotLayout ? toolSlotItems : listLayout ? listItems : classic;

        /// <summary>True while the list inventory (prototype) is the one in use: hands and a bag, no hotbar.</summary>
        public bool UsesList
        {
            get => listLayout && !toolSlotLayout;
            set => listLayout = value;
        }

        /// <summary>List inventory: slots in the bag; the hands' slots come after them, so what's gathered fills the bag first.</summary>
        public int BagSlots => bagSlots;

        /// <summary>List inventory: the inventory slot of the right hand's <paramref name="index"/>th slot (0-4).</summary>
        public int RightHandSlot(int index) => bagSlots + index;

        /// <summary>List inventory: the inventory slot of the left hand's <paramref name="index"/>th slot (0-4).</summary>
        public int LeftHandSlot(int index) => bagSlots + HandSlots + index;

        /// <summary>List inventory: true for a hand's slot, which the list shows highlighted as equipped.</summary>
        public bool IsHandSlot(int slot) => UsesList && slot >= bagSlots && slot < bagSlots + HandSlots * 2;

        /// <summary>True while the tool-slot inventory is the one in use; the lab panel switches it.</summary>
        public bool UsesToolSlot
        {
            get => toolSlotLayout;
            set => toolSlotLayout = value;
        }

        /// <summary>Slots along the bottom of the screen: every slot of the tool-slot inventory, or the classic hotbar.</summary>
        public int HotbarSlots => toolSlotLayout ? toolSlotItems.SlotCount : listLayout ? HandSlots : Mathf.Min(hotbarSlots, classic.SlotCount);

        private void Awake()
        {
            classic = new Inventory(hotbarSlots + backpackSlots);
            toolSlotItems = new Inventory(1 + itemSlots)
            {
                Accepts = (slot, item) => slot != ToolSlot || item is ToolDefinition,
            };
            listItems = new Inventory(bagSlots + HandSlots * 2);
            int hand = 0;
            foreach (StartingItem start in startingItems)
            {
                if (start.Item != null)
                {
                    classic.Add(start.Item, Mathf.Max(1, start.Count));
                    toolSlotItems.Add(start.Item, Mathf.Max(1, start.Count));
                    // The list inventory starts its tools in the right hand, as the hotbar had them in its first slots.
                    if (start.Item is ToolDefinition && hand < HandSlots)
                    {
                        listItems.Set(RightHandSlot(hand++), new ItemStack(start.Item, 1));
                        listItems.Add(start.Item, Mathf.Max(1, start.Count) - 1);
                    }
                    else
                    {
                        listItems.Add(start.Item, Mathf.Max(1, start.Count));
                    }
                }
            }
        }

        private void OnEnable()
        {
            if (tools != null)
            {
                tools.Collected += OnCollected;
            }
        }

        private void OnDisable()
        {
            if (tools != null)
            {
                tools.Collected -= OnCollected;
            }
        }

        /// <summary>Shows what came in some other way than the tools (GL387: crushing on a boulder) in the pickup notices.</summary>
        public void Notify(ItemDefinition item, float amount)
        {
            if (item != null && amount > ItemStack.PartEpsilon)
            {
                Added?.Invoke(item, amount);
            }
        }

        /// <summary>Shows "Inventory full" for something that could not be taken (GL610: a dropped item picked up).</summary>
        public void NotifyFull(ItemDefinition item)
        {
            if (item != null)
            {
                Rejected?.Invoke(item);
            }
        }

        /// <summary>
        /// Adds a made or returned stack (GL704): what fits goes in, and what doesn't is announced ("Inventory full") and
        /// dropped as a sack in front of the player, never lost. Returns how many whole items went to the ground.
        /// </summary>
        public int GiveOrDrop(ItemDefinition item, int count, Composition contents = null, int wear = 0, float quality = 1f, float part = 0f)
        {
            int left = Inventory.Add(item, count, contents, wear, quality, part);
            if (left <= 0)
            {
                return 0;
            }
            Rejected?.Invoke(item);
            return DropAhead(new ItemStack(item, left, contents, wear, quality, part)) ? left : 0;
        }

        /// <summary>Puts a stack on the ground in front of the player as a sack (GL704); false when there's nothing to drop it with.</summary>
        public bool DropAhead(ItemStack stack)
        {
            dropped ??= GetComponent<DroppedItems>();
            if (dropped == null || stack.IsEmpty)
            {
                return false;
            }
            var controller = GetComponent<PlayerController>();
            Transform head = controller != null && controller.Head != null ? controller.Head : transform;
            Vector3 at = head.position + head.forward * 0.7f - head.up * 0.3f;
            Vector3 toss = head.forward * 2f + Vector3.up * 1f;
            return dropped.Spawn(stack, true, at, Quaternion.Euler(0f, head.eulerAngles.y + 180f, 0f), toss) != null;
        }

        /// <summary>Drags out of the screen drop this much of a slot on the ground as a sack (GL768): the whole stack (a part included) when <paramref name="count"/> covers it. Returns what was dropped; nothing when there is no way to drop it.</summary>
        public bool DropFromSlot(int slot, int count)
        {
            return DropFrom(Inventory, slot, count);
        }

        /// <summary>
        /// The same from any inventory the player has open (GL792: a station's or a chest's slots dragged off its screen):
        /// that much of the slot goes on the ground in front of the player as a sack. What can't be dropped stays.
        /// </summary>
        public bool DropFrom(Inventory source, int slot, int count)
        {
            ItemStack held = source[slot];
            if (held.IsEmpty || count < 1)
            {
                return false;
            }
            ItemStack taken = count >= held.Count ? source.TakeAll(slot) : new ItemStack(held.Item, source.RemoveAt(slot, count), held.Contents, held.Wear, held.Quality);
            if (DropAhead(taken))
            {
                return true;
            }
            source.Add(taken.Item, taken.Count, taken.Contents, taken.Wear, taken.Quality, taken.Part);
            return false;
        }

        private DroppedItems dropped;

        // What the tools gather comes in as stacks, with their contents and quality.
        private void OnCollected(ItemStack collected)
        {
            ItemDefinition item = collected.Item;
            int left = Inventory.Add(item, collected.Count, collected.Contents, 0, collected.Quality, collected.Part);
            // The notice shows what went into the stack, parts included (GL279).
            float added = left > 0 ? collected.Count - left : collected.Amount;
            if (added > ItemStack.PartEpsilon)
            {
                Added?.Invoke(item, added);
            }
            if (left > 0)
            {
                Rejected?.Invoke(item);
            }
        }

        [Serializable]
        private struct StartingItem
        {
            public ItemDefinition Item;

            [Min(1)]
            public int Count;
        }
    }
}
