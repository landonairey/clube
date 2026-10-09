using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Dries what the player drops on the ground (GL34): clay put down with the
    /// <see cref="ItemDropper"/> turns to brick after its drying days on the <see cref="GameClock"/>.
    /// Keeps a Core <see cref="GroundDrying"/> for the world and updates it every frame. Lives on
    /// the world, not the player, so drying goes on while the player is switched off.
    /// </summary>
    public class DroppedGroundDrying : MonoBehaviour
    {
        [SerializeField]
        private WorldView worldView;

        [SerializeField]
        private GameClock clock;

        [Tooltip("The player's dropper, whose drops start drying.")]
        [SerializeField]
        private ItemDropper dropper;

        private readonly GroundDrying drying = new GroundDrying();

        /// <summary>Samples still drying.</summary>
        public int Count => drying.Count;

        /// <summary>In-game days until the next one dries, or null with nothing drying.</summary>
        public double? DaysToNext => drying.NextReadyAt.HasValue && clock != null ? drying.NextReadyAt.Value - clock.Days : (double?)null;

        private void OnEnable()
        {
            if (dropper != null)
            {
                dropper.Piled += OnPiled;
            }
        }

        private void OnDisable()
        {
            if (dropper != null)
            {
                dropper.Piled -= OnPiled;
            }
        }

        private void Update()
        {
            if (clock != null && worldView != null && worldView.IsReady && drying.Count > 0)
            {
                drying.Update(worldView.World, clock.Days, worldView.Config.IsoLevel);
            }
        }

        private void OnPiled(VoxelMaterial material, IReadOnlyList<Vector3Int> samples)
        {
            if (clock != null)
            {
                drying.Add(samples, material, clock.Days);
            }
        }
    }
}
