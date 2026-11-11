using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A chest the player builds and places (GL249): an inventory standing in the world, opened with E into a
    /// <see cref="ChestPanel"/>. Built of better wood it holds more (<see cref="StorageCapacity"/>): as many slots as the
    /// player's inventory at 100%, down to 5. Picked up, what's inside comes along (<see cref="PlacedObject"/>), and a
    /// save keeps it (<see cref="PlacedObjectSaving"/>).
    /// </summary>
    /// <remarks>Needs a collider on it or a child so <see cref="PlayerInteractor"/> can target it.</remarks>
    public class StorageChest : MonoBehaviour, IInteractable
    {
        [SerializeField]
        private string displayName = "Wooden chest";

        [Tooltip("Slots at 100% quality: the player's inventory (9 hotbar and 18 backpack).")]
        [SerializeField, Min(1)]
        private int fullSlots = 27;

        [Tooltip("The fewest slots, however poor the wood.")]
        [SerializeField, Min(1)]
        private int floorSlots = 5;

        public string DisplayName => displayName;

        /// <summary>Gives the chest another name (GL553: the tutorial's crates are company property).</summary>
        public void Rename(string name)
        {
            displayName = name;
        }

        /// <summary>What's inside.</summary>
        public Inventory Storage { get; private set; }

        public string Prompt => $"Open {displayName.ToLowerInvariant()}";

        private void Awake()
        {
            Storage ??= new Inventory(fullSlots);
        }

        /// <summary>
        /// Sets its size from how well it was built (GL249). Anything already inside is kept as far as the new size
        /// allows; it's called on placing, before anything goes in.
        /// </summary>
        public void MadeOf(float quality)
        {
            var sized = new Inventory(StorageCapacity.SlotsFor(quality, fullSlots, floorSlots));
            if (Storage != null)
            {
                for (int slot = 0; slot < Storage.SlotCount; slot++)
                {
                    Storage.MoveTo(slot, sized);
                }
            }
            Storage = sized;
        }

        public void Interact(PlayerInteractor by)
        {
            ChestPanel panel = by.GetComponent<ChestPanel>();
            if (panel != null)
            {
                panel.Open(this);
            }
        }
    }
}
