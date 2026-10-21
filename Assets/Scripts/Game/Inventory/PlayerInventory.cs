using System;
using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The player's <see cref="Inventory"/> (GL6), in one of two layouts. The volume inventory (GL99, the
    /// default): a tool slot (slot 0, tools only), three volume slots (<see cref="Bins"/>: rock, soil, plant, GL114) that mined
    /// samples go into by kind, mixed within it, and a few item slots for everything else, spare tools included.
    /// The classic one, kept switchable: the first <see cref="HotbarSlots"/> slots are the hotbar
    /// (<see cref="Hotbar"/>), the rest the backpack, and mined material comes in as items. Each layout
    /// keeps its own items; switching doesn't move them. Starts with the starting items; what doesn't
    /// fit is reported through <see cref="Rejected"/> and lost.
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        /// <summary>The volume inventory's tool slot (GL99).</summary>
        public const int ToolSlot = 0;

        [Tooltip("The volume inventory (GL99): a tool slot, a volume slot mixing every mined voxel and a few item slots. Off: the classic hotbar and backpack of items.")]
        [SerializeField]
        private bool volumeInventory = true;

        [Tooltip("Volume inventory: item slots beside the tool and volume slots.")]
        [SerializeField, Range(1, 8)]
        private int itemSlots = 3;

        [Tooltip("Volume inventory: the least share of a bin an ingredient must be to be taken from it for a recipe (GL100). 0 (GL110): any mix crafts, at its share's quality.")]
        [SerializeField, Range(0f, 1f)]
        private float minimumShare;

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
        private Inventory volumeSlots;

        /// <summary>Raised for each collected item that went in.</summary>
        public event Action<ItemDefinition> Added;

        /// <summary>Raised for each collected item that didn't fit.</summary>
        public event Action<ItemDefinition> Rejected;

        /// <summary>The slots in use: the volume inventory's tool and item slots, or the classic hotbar and backpack.</summary>
        public Inventory Inventory => volumeInventory ? volumeSlots : classic;

        private readonly VolumeMix[] bins = { new VolumeMix(), new VolumeMix(), new VolumeMix() };

        /// <summary>
        /// The volume slots (GL99, GL114): one bin per <see cref="MaterialKind"/> (rock, soil, plant), each
        /// mixing what's mined of its kind. Kept while the classic layout is on, unused.
        /// </summary>
        public IReadOnlyList<VolumeMix> Bins => bins;

        /// <summary>The bin a kind of ground goes into.</summary>
        public VolumeMix Bin(MaterialKind kind)
        {
            return bins[(int)kind];
        }

        /// <summary>True while the volume inventory is the one in use; the lab panel switches it.</summary>
        public bool UsesVolume
        {
            get => volumeInventory;
            set => volumeInventory = value;
        }

        /// <summary>The least share of the mix a recipe ingredient must be to be taken from it (GL100).</summary>
        public float MinimumShare
        {
            get => minimumShare;
            set => minimumShare = Mathf.Clamp01(value);
        }

        /// <summary>Slots along the bottom of the screen: every slot of the volume inventory, or the classic hotbar.</summary>
        public int HotbarSlots => volumeInventory ? volumeSlots.SlotCount : Mathf.Min(hotbarSlots, classic.SlotCount);

        private void Awake()
        {
            classic = new Inventory(hotbarSlots + backpackSlots);
            volumeSlots = new Inventory(1 + itemSlots)
            {
                Accepts = (slot, item) => slot != ToolSlot || item is ToolDefinition,
            };
            foreach (StartingItem start in startingItems)
            {
                if (start.Item != null)
                {
                    classic.Add(start.Item, Mathf.Max(1, start.Count));
                    volumeSlots.Add(start.Item, Mathf.Max(1, start.Count));
                }
            }
        }

        private void OnEnable()
        {
            if (tools != null)
            {
                tools.Collected += OnCollected;
                tools.Unearthed += OnUnearthed;
            }
        }

        private void OnDisable()
        {
            if (tools != null)
            {
                tools.Collected -= OnCollected;
                tools.Unearthed -= OnUnearthed;
            }
        }

        // Classic: what the tools collect comes in as items. The volume inventory takes mined ground as
        // volume instead (OnUnearthed), so it leaves these.
        private void OnCollected(ItemStack collected)
        {
            if (volumeInventory)
            {
                return;
            }
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

        // Volume inventory: each mined sample goes into the mix as the material it was, with what it held
        // (ore's host rock and grade), whether or not the player can tell what it is yet.
        private void OnUnearthed(VoxelMaterial material, Vector3 position, Composition contents)
        {
            if (volumeInventory)
            {
                if (material != null)
                {
                    Bin(material.Kind).Add(material, 1f, contents);
                }
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
