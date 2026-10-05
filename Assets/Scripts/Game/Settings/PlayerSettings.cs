using System;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The player's own settings (A3), separate from the world definition in
    /// <see cref="WorldConfig"/>: things a player tunes for their machine or taste.
    /// Render distance (M3), look sensitivity (M6) and brush size (M8) so far; the
    /// in-game settings menu (P10) will edit it.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerSettings", menuName = "Clube/Player Settings")]
    public class PlayerSettings : ScriptableObject
    {
        public const float MinLookSensitivity = 0.01f;
        public const float MaxLookSensitivity = 1f;
        public const float MinBrushRadius = 0.5f;
        public const float MaxBrushRadius = 6f;

        [Tooltip("How many chunks around the player stay loaded, as a radius in chunks (M3).")]
        [SerializeField, Range(WorldView.MinRenderDistance, WorldView.MaxRenderDistance)]
        private int renderDistance = 6;

        [Tooltip("Degrees the view turns per pixel of mouse movement (M6).")]
        [SerializeField, Range(MinLookSensitivity, MaxLookSensitivity)]
        private float lookSensitivity = 0.1f;

        [Tooltip("Radius in world units of what the player digs or places (M8).")]
        [SerializeField, Range(MinBrushRadius, MaxBrushRadius)]
        private float brushRadius = 1.5f;

        /// <summary>Raised when a setting changes, from the Inspector or from code.</summary>
        public event Action Changed;

        public int RenderDistance
        {
            get => renderDistance;
            set => Set(ref renderDistance, Mathf.Clamp(value, WorldView.MinRenderDistance, WorldView.MaxRenderDistance));
        }

        public float LookSensitivity
        {
            get => lookSensitivity;
            set => Set(ref lookSensitivity, Mathf.Clamp(value, MinLookSensitivity, MaxLookSensitivity));
        }

        public float BrushRadius
        {
            get => brushRadius;
            set => Set(ref brushRadius, Mathf.Clamp(value, MinBrushRadius, MaxBrushRadius));
        }

        private void Set<T>(ref T field, T value)
        {
            if (Equals(field, value))
            {
                return;
            }
            field = value;
            Changed?.Invoke();
        }

        // Called by Unity whenever an Inspector value changes, including in Play mode.
        private void OnValidate()
        {
            Changed?.Invoke();
        }
    }
}
