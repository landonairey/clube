using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// The camera section of the in-game lab panels: the free-fly camera's base speed and
    /// how much Shift multiplies it, for crossing a big world quickly, and, where the lab
    /// has a player, switching to walking as it (M7).
    /// </summary>
    public static class CameraControls
    {
        public static void Draw(LabPanelFrame frame, FreeFlyCamera flyCamera, PlayerCameraToggle playerToggle = null)
        {
            if (playerToggle != null && playerToggle.IsSetUp)
            {
                if (Application.isPlaying)
                {
                    playerToggle.IsPlayer = frame.ToggleField("Walk as the player (P)", playerToggle.IsPlayer);
                }
                else
                {
                    GUILayout.Label("In Play mode, P switches to walking as the player.", frame.Hint);
                }
            }
            flyCamera.MoveSpeed = frame.Slider("Move speed (u/s)", flyCamera.MoveSpeed, FreeFlyCamera.MinMoveSpeed, FreeFlyCamera.MaxMoveSpeed, "0.0");
            flyCamera.FastMultiplier = frame.Slider("Shift boost ×", flyCamera.FastMultiplier, 1f, FreeFlyCamera.MaxFastMultiplier, "0.0");
        }
    }
}
