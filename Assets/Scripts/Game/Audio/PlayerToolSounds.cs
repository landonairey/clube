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
            tools.Collected += OnCollected;
            tools.BushPicked += OnBushPicked;
        }

        private void OnDisable()
        {
            tools.Struck -= OnStruck;
            tools.Collected -= OnCollected;
            tools.BushPicked -= OnBushPicked;
        }

        private void OnStruck(StrikeResult result)
        {
            if (sounds == null)
            {
                return;
            }
            sounds.Play(sounds.HitWith(tools.Current, tools.HitMaterial), source, result.Broken > 0 ? 1f : missVolume);
            // Ground that came away crumbles, thuds or rustles by what it was (GL271).
            if (result.Removed > 0)
            {
                sounds.Play(sounds.BreakOf(tools.HitMaterial), source, Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(result.Removed / 8f)));
            }
        }

        // One soft pick-up a frame, however many stacks a hit filled.
        private void OnCollected(ItemStack stack)
        {
            if (sounds != null && collectedFrame != Time.frameCount)
            {
                collectedFrame = Time.frameCount;
                sounds.Play(sounds.Gathered, source, 0.7f);
            }
        }

        private void OnBushPicked(Bush bush)
        {
            if (sounds != null)
            {
                sounds.Play(sounds.Plants, source);
            }
        }

        private int collectedFrame = -1;
    }
}
