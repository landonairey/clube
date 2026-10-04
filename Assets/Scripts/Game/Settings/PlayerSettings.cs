using System;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The player's own settings (A3), separate from the world definition in
    /// <see cref="WorldConfig"/>: things a player tunes for their machine or taste.
    /// Starts with the render distance (M3); sensitivity, brush size and the rest join
    /// as the chapters need them, and the in-game settings menu (P10) edits it.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerSettings", menuName = "Clube/Player Settings")]
    public class PlayerSettings : ScriptableObject
    {
        [Tooltip("How many chunks around the player stay loaded, as a radius in chunks (M3).")]
        [SerializeField, Range(WorldView.MinRenderDistance, WorldView.MaxRenderDistance)]
        private int renderDistance = 6;

        /// <summary>Raised when a setting changes, from the Inspector or from code.</summary>
        public event Action Changed;

        public int RenderDistance
        {
            get => renderDistance;
            set
            {
                int clamped = Mathf.Clamp(value, WorldView.MinRenderDistance, WorldView.MaxRenderDistance);
                if (clamped == renderDistance)
                {
                    return;
                }
                renderDistance = clamped;
                Changed?.Invoke();
            }
        }

        // Called by Unity whenever an Inspector value changes, including in Play mode.
        private void OnValidate()
        {
            Changed?.Invoke();
        }
    }
}
