using Clube.Game;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// The player section of the lab panel (M16): a gravity multiplier and the jump height,
    /// for tuning how jumps and falls feel, with the gravity and airtime they give. The
    /// multiplier is for trying values in Play mode; bake a good result into the player's
    /// own gravity.
    /// </summary>
    public static class PlayerControls
    {
        public static void Draw(LabPanelFrame frame, PlayerController player)
        {
            if (Application.isPlaying)
            {
                player.GravityMultiplier = frame.Slider(
                    "Gravity ×", player.GravityMultiplier,
                    PlayerController.MinGravityMultiplier, PlayerController.MaxGravityMultiplier);
            }
            else
            {
                GUILayout.Label("In Play mode, a gravity multiplier tunes jumps and falls.", frame.Hint);
            }
            player.JumpHeight = frame.Slider(
                "Jump height (m)", player.JumpHeight, PlayerController.MinJumpHeight, PlayerController.MaxJumpHeight, "0.0");
            frame.Line($"Gravity {player.Gravity:0.0} m/s² · jump airtime {player.JumpAirtime:0.00} s");
        }
    }
}
