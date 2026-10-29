using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The sound of each tool hit: picked by the tool in hand and the material under the dot
    /// (<see cref="PlayerSounds.HitWith"/>), quieter when the hit took nothing.
    /// </summary>
    [RequireComponent(typeof(PlayerToolUser))]
    public class PlayerToolSounds : MonoBehaviour
    {
        [SerializeField]
        private PlayerSounds sounds;

        [SerializeField]
        private AudioSource source;

        [Tooltip("Volume of a hit that broke nothing, against one that did.")]
        [SerializeField, Range(0f, 1f)]
        private float missVolume = 0.5f;

        private PlayerToolUser tools;

        private void Awake()
        {
            tools = GetComponent<PlayerToolUser>();
        }

        private void OnEnable()
        {
            tools.Struck += OnStruck;
        }

        private void OnDisable()
        {
            tools.Struck -= OnStruck;
        }

        private void OnStruck(StrikeResult result)
        {
            if (sounds != null)
            {
                sounds.Play(sounds.HitWith(tools.Current, tools.HitMaterial), source, result.Broken > 0 ? 1f : missVolume);
            }
        }
    }
}
