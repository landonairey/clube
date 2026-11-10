using System.Collections.Generic;
using System.Globalization;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The player's lifetime volume of ground taken out of the terrain and put back into it (GL442), in units (one mined
    /// sample is one unit). Counts what the tools gather and what Drop places, and is kept in saves. Shown as "Volume" on
    /// the lab HUD.
    /// </summary>
    public class PlayerVolumeLedger : MonoBehaviour, ISavedValues
    {
        private const string TakenKey = "volume.taken";
        private const string PlacedKey = "volume.placed";

        private PlayerToolUser tools;
        private ItemDropper dropper;

        /// <summary>Units gathered from the terrain, all time.</summary>
        public double Taken { get; private set; }

        /// <summary>Units placed into the terrain, all time.</summary>
        public double Placed { get; private set; }

        private void OnEnable()
        {
            tools = GetComponent<PlayerToolUser>();
            dropper = GetComponent<ItemDropper>();
            if (tools != null)
            {
                tools.Collected += OnCollected;
            }
            if (dropper != null)
            {
                dropper.Dropped += OnDropped;
            }
        }

        private void OnDisable()
        {
            if (tools != null)
            {
                tools.Collected -= OnCollected;
            }
            if (dropper != null)
            {
                dropper.Dropped -= OnDropped;
            }
        }

        private void OnCollected(ItemStack stack)
        {
            if (!stack.IsEmpty)
            {
                Taken += stack.Amount * stack.Item.UnitVolume;
            }
        }

        private void OnDropped(ItemDefinition item, int count)
        {
            if (item != null)
            {
                Placed += count * item.UnitVolume;
            }
        }

        public void SaveValues(IDictionary<string, string> values)
        {
            values[TakenKey] = Taken.ToString("R", CultureInfo.InvariantCulture);
            values[PlacedKey] = Placed.ToString("R", CultureInfo.InvariantCulture);
        }

        public void RestoreValues(IReadOnlyDictionary<string, string> values)
        {
            if (values.TryGetValue(TakenKey, out string taken) && double.TryParse(taken, NumberStyles.Float, CultureInfo.InvariantCulture, out double t))
            {
                Taken = t;
            }
            if (values.TryGetValue(PlacedKey, out string placed) && double.TryParse(placed, NumberStyles.Float, CultureInfo.InvariantCulture, out double p))
            {
                Placed = p;
            }
        }
    }
}
