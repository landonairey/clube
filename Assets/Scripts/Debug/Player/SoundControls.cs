using Clube.Game;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// The Sound section of the lab panel: one switch for every player sound effect
    /// (<see cref="PlayerSounds.EffectsOn"/>), off by default while the clips are placeholders.
    /// The switch is on the shared <see cref="PlayerSounds"/> asset, so it holds in every scene.
    /// </summary>
    public static class SoundControls
    {
        public static void Draw(LabPanelFrame frame, PlayerSounds sounds)
        {
            if (sounds == null)
            {
                GUILayout.Label("The player has no PlayerSounds asset.", frame.Hint);
                return;
            }
            sounds.EffectsOn = frame.ToggleField("Sound effects", sounds.EffectsOn);
            GUILayout.Label("Footsteps, tool hits, crafting, selling, placing and buying land. Off by default: the clips are placeholders.", frame.Hint);
        }
    }
}
