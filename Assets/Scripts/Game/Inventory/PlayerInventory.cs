using System;
using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The player's <see cref="Inventory"/> (GL6): the first <see cref="HotbarSlots"/> slots
    /// are the hotbar (<see cref="Hotbar"/>), the rest the backpack. Starts with the starting
    /// items and takes in everything the player's tools collect (GL5); what doesn't fit is
    /// reported through <see cref="Rejected"/> and lost.
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        [Tooltip("Slots in the hotbar, picked with the number keys.")]
        [SerializeField, Range(1, 9)]
        private int hotbarSlots = 9;

        [Tooltip("Slots in the backpack, behind the hotbar.")]
        [SerializeField, Min(0)]
        private int backpackSlots = 18;

        [Tooltip("What the player starts with, in order: the first lands in hotbar slot 1.")]
        [SerializeField]
        private List<StartingItem> startingItems = new List<StartingItem>();

        [Tooltip("Collects what the player's tools break loose. Optional.")]
        [SerializeField]
        private PlayerToolUser tools;

        /// <summary>Raised for each collected item that went in.</summary>
        public event Action<ItemDefinition> Added;

        /// <summary>Raised for each collected item that didn't fit.</summary>
        public event Action<ItemDefinition> Rejected;

        public Inventory Inventory { get; private set; }

        public int HotbarSlots => Mathf.Min(hotbarSlots, Inventory.SlotCount);

        private void Awake()
        {
            Inventory = new Inventory(hotbarSlots + backpackSlots);
            foreach (StartingItem start in startingItems)
            {
                if (start.Item != null)
                {
                    Inventory.Add(start.Item, Mathf.Max(1, start.Count));
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

        private void OnCollected(ItemStack collected)
        {
            ItemDefinition item = collected.Item;
            if (Inventory.Add(item, collected.Count, collected.Contents) > 0)
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
