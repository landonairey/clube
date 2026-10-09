using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Marks an object the player placed from an item (GL23), so it can be picked back up into
    /// that item. A station gives its loaded and finished items back too.
    /// </summary>
    public class PlacedObject : MonoBehaviour
    {
        /// <summary>The item it was placed from, and turns back into.</summary>
        public PlaceableDefinition Item { get; set; }

        /// <summary>
        /// Puts the item, and anything a station holds, into an inventory and removes the object.
        /// False, changing nothing, when they wouldn't all fit.
        /// </summary>
        public bool PickUpInto(Inventory inventory)
        {
            StationObject station = GetComponent<StationObject>();
            if (!Fits(inventory, station))
            {
                return false;
            }
            if (station != null)
            {
                MoveAll(station.Station.Input, inventory);
                MoveAll(station.Station.Output, inventory);
                if (station.Station.Fuel != null)
                {
                    MoveAll(station.Station.Fuel, inventory);
                }
            }
            inventory.Add(Item);
            Destroy(gameObject);
            return true;
        }

        // Checks on a copy, so a refused pickup leaves both inventories as they were.
        private bool Fits(Inventory inventory, StationObject station)
        {
            var trial = new Inventory(inventory.SlotCount);
            for (int slot = 0; slot < inventory.SlotCount; slot++)
            {
                ItemStack stack = inventory[slot];
                if (!stack.IsEmpty && trial.Add(stack.Item, stack.Count) > 0)
                {
                    return false;
                }
            }
            if (station != null && (!AddAll(station.Station.Input, trial) || !AddAll(station.Station.Output, trial)
                || (station.Station.Fuel != null && !AddAll(station.Station.Fuel, trial))))
            {
                return false;
            }
            return Item == null || trial.Add(Item) == 0;
        }

        private static bool AddAll(Inventory from, Inventory to)
        {
            for (int slot = 0; slot < from.SlotCount; slot++)
            {
                ItemStack stack = from[slot];
                if (!stack.IsEmpty && to.Add(stack.Item, stack.Count) > 0)
                {
                    return false;
                }
            }
            return true;
        }

        private static void MoveAll(Inventory from, Inventory to)
        {
            for (int slot = 0; slot < from.SlotCount; slot++)
            {
                from.MoveTo(slot, to);
            }
        }
    }
}
