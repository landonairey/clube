using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// Switches the game's input actions off while the pause menu is open (GL584), so no hotkey (journal, inventory, help,
    /// hotbar, tools) does anything behind it, however its handler reads the key. One place, not a check in every handler.
    /// Only the actions that were enabled are put back, so one the game had switched off stays off.
    /// </summary>
    public sealed class PausedInput
    {
        private readonly List<InputAction> suspended = new List<InputAction>();

        /// <summary>Turns off every enabled action in the asset; does nothing when already suspended.</summary>
        public void Suspend(InputActionAsset asset)
        {
            if (asset == null || suspended.Count > 0)
            {
                return;
            }
            foreach (InputAction action in asset)
            {
                if (action.enabled)
                {
                    suspended.Add(action);
                    action.Disable();
                }
            }
        }

        /// <summary>Turns back on what <see cref="Suspend"/> turned off.</summary>
        public void Resume()
        {
            foreach (InputAction action in suspended)
            {
                action.Enable();
            }
            suspended.Clear();
        }
    }
}
