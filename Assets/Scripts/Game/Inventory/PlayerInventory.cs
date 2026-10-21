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

        [Tooltip("What the player starts with, in order: the first lands in hotbar slot 1.")]
        [SerializeField]
        private List<StartingItem> startingItems = new List<StartingItem>();

        [Tooltip("Collects what the player's tools break loose. Optional.")]
        [SerializeField]
        private PlayerToolUser tools;

        private Inventory classic;
        private Inventory toolSlotItems;

        /// <summary>Raised for each collected item that went in.</summary>
        public event Action<ItemDefinition> Added;

        /// <summary>Raised for each collected item that didn't fit.</summary>
        public event Action<ItemDefinition> Rejected;

        /// <summary>The slots in use: the tool slot and item slots, or the classic hotbar and backpack.</summary>
        public Inventory Inventory => toolSlotLayout ? toolSlotItems : classic;

        /// <summary>True while the tool-slot inventory is the one in use; the lab panel switches it.</summary>
        public bool UsesToolSlot
        {
            get => toolSlotLayout;
            set => toolSlotLayout = value;
        }

        /// <summary>Slots along the bottom of the screen: every slot of the tool-slot inventory, or the classic hotbar.</summary>
        public int HotbarSlots => toolSlotLayout ? toolSlotItems.SlotCount : Mathf.Min(hotbarSlots, classic.SlotCount);

        private void Awake()
        {
            classic = new Inventory(hotbarSlots + backpackSlots);
            toolSlotItems = new Inventory(1 + itemSlots)
            {
                Accepts = (slot, item) => slot != ToolSlot || item is ToolDefinition,
            };
            foreach (StartingItem start in startingItems)
            {
                if (start.Item != null)
                {
                    classic.Add(start.Item, Mathf.Max(1, start.Count));
                    toolSlotItems.Add(start.Item, Mathf.Max(1, start.Count));
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

        // What the tools gather comes in as stacks, with their contents and quality.
        private void OnCollected(ItemStack collected)
        {
            ItemDefinition item = collected.Item;
            if (Inventory.Add(item, collected.Count, collected.Contents, 0, collected.Quality) > 0)
            {
                Rejected?.Invoke(item);
            }
            else
            {
                Added?.Invoke(item);
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
