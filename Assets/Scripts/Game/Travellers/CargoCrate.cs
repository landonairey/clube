using System;
using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A crate of goods on a cart or a vessel that belongs to someone (GL729: "when you take from a cart or boat, the
    /// shops that the cart or boat are going to will reduce their disposition with you"). While the player has it open
    /// (<see cref="ChestPanel"/>) it counts what leaves it; when the panel closes it tells <see cref="Taken"/> what was
    /// taken. The vehicle's own loading and unloading (<see cref="CrateCargo"/>) is not taking.
    /// </summary>
    [RequireComponent(typeof(StorageChest))]
    public class CargoCrate : MonoBehaviour
    {
        private readonly Dictionary<ItemDefinition, float> before = new Dictionary<ItemDefinition, float>();
        private readonly Dictionary<ItemDefinition, float> taken = new Dictionary<ItemDefinition, float>();
        private readonly List<(ItemDefinition Item, float Amount)> report = new List<(ItemDefinition, float)>();
        private StorageChest chest;
        private Inventory watched;

        /// <summary>Raised when the player closes the crate having taken goods: each item and how much of it.</summary>
        public event Action<IReadOnlyList<(ItemDefinition Item, float Amount)>> Taken;

        private void Awake()
        {
            chest = GetComponent<StorageChest>();
        }

        /// <summary>The player opened it: what is in it now is what counts.</summary>
        public void Opened()
        {
            taken.Clear();
            Look();
        }

        /// <summary>The player closed it: reports what was taken since it opened.</summary>
        public void Closed()
        {
            if (watched == null)
            {
                return;
            }
            Count();
            watched = null;
            report.Clear();
            foreach (KeyValuePair<ItemDefinition, float> pair in taken)
            {
                if (pair.Value > 1e-4f)
                {
                    report.Add((pair.Key, pair.Value));
                }
            }
            taken.Clear();
            if (report.Count > 0)
            {
                Taken?.Invoke(report);
            }
        }

        /// <summary>
        /// Around the vehicle's own change of what the crate holds (<paramref name="starting"/> true before, false after):
        /// what the player took until then is counted, and the new contents are what counts from then on.
        /// </summary>
        public void Restocking(bool starting)
        {
            if (watched == null)
            {
                return;
            }
            if (starting)
            {
                Count();
            }
            else
            {
                Look();
            }
        }

        // Adds what has left the crate since the last look to what was taken, and looks again.
        private void Count()
        {
            if (chest == null || chest.Storage != watched)
            {
                // Its contents were swapped (a pirates' haul): nothing the player did.
                Look();
                return;
            }
            foreach (KeyValuePair<ItemDefinition, float> pair in before)
            {
                float lost = pair.Value - watched.AmountOf(pair.Key);
                if (lost > 1e-4f)
                {
                    taken[pair.Key] = (taken.TryGetValue(pair.Key, out float sum) ? sum : 0f) + lost;
                }
            }
            Look();
        }

        private void Look()
        {
            watched = chest != null ? chest.Storage : null;
            before.Clear();
            if (watched == null)
            {
                return;
            }
            for (int slot = 0; slot < watched.SlotCount; slot++)
            {
                ItemStack stack = watched[slot];
                if (!stack.IsEmpty && !before.ContainsKey(stack.Item))
                {
                    before[stack.Item] = watched.AmountOf(stack.Item);
                }
            }
        }
    }
}
