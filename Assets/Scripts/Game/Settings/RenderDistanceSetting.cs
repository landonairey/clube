using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Applies the player's render distance (<see cref="PlayerSettings"/>, A3) to the
    /// world's chunk manager (M3): when the scene starts, and whenever the setting changes.
    /// The core stays unaware of player settings; this is the only link.
    /// </summary>
    [RequireComponent(typeof(WorldView))]
    public class RenderDistanceSetting : MonoBehaviour
    {
        [SerializeField]
        private PlayerSettings settings;

        private WorldView worldView;

        private void Awake()
        {
            worldView = GetComponent<WorldView>();
        }

        private void OnEnable()
        {
            if (settings == null)
            {
                UnityEngine.Debug.LogWarning($"{nameof(RenderDistanceSetting)} on '{name}' has no {nameof(PlayerSettings)} assigned.", this);
                return;
            }
            settings.Changed += Apply;
            Apply();
        }

        private void OnDisable()
        {
            if (settings != null)
            {
                settings.Changed -= Apply;
            }
        }

        private void Apply()
        {
            worldView.RenderDistance = settings.RenderDistance;
        }
    }
}
