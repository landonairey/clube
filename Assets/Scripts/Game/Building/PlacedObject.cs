using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Marks an object the player placed from an item (GL23), so it can be picked back up into
    /// that item. A station gives its loaded and finished items back too, a chest what it holds (GL249).
    /// </summary>
    public class PlacedObject : MonoBehaviour
    {
        /// <summary>The item it was placed from, and turns back into.</summary>
        public PlaceableDefinition Item { get; set; }

        /// <summary>What it was made of (GL100), given back with it when it's picked up; null when made of pure items.</summary>
        public Composition Contents { get; set; }

        /// <summary>How well it was made (GL118), given back with it when it's picked up.</summary>
        public float Quality { get; set; } = 1f;

        /// <summary>What it has worn or burned (a torch's seconds alight, GL287), given back with it when it's picked up.</summary>
        public int Wear { get; set; }

        /// <summary>
        /// Puts the item, and anything a station holds, into an inventory and removes the object.
        /// False, changing nothing, when they wouldn't all fit.
        /// </summary>
        public bool PickUpInto(Inventory inventory)
        {
            StationObject station = GetComponent<StationObject>();
            StorageChest chest = GetComponent<StorageChest>();
            if (!Fits(inventory, station, chest))
            {
                return false;
            }
            if (chest != null)
            {
                // What's inside comes along (GL249).
                MoveAll(chest.Storage, inventory, false);
            }
            if (station != null)
            {
                MoveAll(station.Station.Input, inventory, true);
                MoveAll(station.Station.Output, inventory, true);
                if (station.Station.Fuel != null)
                {
                    MoveAll(station.Station.Fuel, inventory, true);
                }
            }
            // A structure built from the ground takes its samples back out with it (GL85).
            var structure = GetComponent<TerrainStructure>();
            if (structure != null)
            {
                structure.Clear();
            }
            inventory.Add(Item, 1, Contents, Wear, Quality);
            Destroy(gameObject);
            return true;
        }

        // Checks on a copy, so a refused pickup leaves both inventories as they were.
        private bool Fits(Inventory inventory, StationObject station, StorageChest chest)
        {
            var trial = new Inventory(inventory.SlotCount);
            for (int slot = 0; slot < inventory.SlotCount; slot++)
            {
                ItemStack stack = inventory[slot];
                if (!stack.IsEmpty && trial.Add(stack.Item, stack.Count, stack.Contents, stack.Wear, stack.Quality, stack.Part) > 0)
                {
                    return false;
                }
            }
            if (station != null && (!AddAll(station.Station.Input, trial, true) || !AddAll(station.Station.Output, trial, true)
                || (station.Station.Fuel != null && !AddAll(station.Station.Fuel, trial, true))))
            {
                return false;
            }
            if (chest != null && !AddAll(chest.Storage, trial, false))
            {
                return false;
            }
            return Item == null || trial.Add(Item, 1, Contents, Wear, Quality) == 0;
        }

        // A station's contents keep apart from other qualities on the way out (GL256); a chest's stack as usual.
        private static bool AddAll(Inventory from, Inventory to, bool keepApart)
        {
            for (int slot = 0; slot < from.SlotCount; slot++)
            {
                ItemStack stack = from[slot];
                if (!stack.IsEmpty && to.Add(stack.Item, stack.Count, stack.Contents, stack.Wear, stack.Quality, stack.Part, keepApart) > 0)
                {
                    return false;
                }
            }
            return true;
        }

        private static void MoveAll(Inventory from, Inventory to, bool keepApart)
        {
            for (int slot = 0; slot < from.SlotCount; slot++)
            {
                from.MoveTo(slot, to, keepApart);
            }
        }
    }
}
