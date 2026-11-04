using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Stepping into the campfire throws the player back the way they came (GL356), with a short sizzle. On the campfire
    /// prefab: within <see cref="radius"/> of its ember bed (and not far above or below it) the player is pushed back along
    /// the way they were moving, or straight away from the fire when standing still, with a small hop.
    /// </summary>
    public class CampfireBounce : MonoBehaviour
    {
        [Tooltip("How near the ember bed, in metres across, counts as touching the fire.")]
        [SerializeField, Min(0.1f)]
        private float radius = 0.75f;

        [Tooltip("How fast the player is thrown back, in metres a second.")]
        [SerializeField, Min(0f)]
        private float pushSpeed = 5f;

        [Tooltip("The hop up, in metres a second.")]
        [SerializeField, Min(0f)]
        private float lift = 3f;

        [Tooltip("How long the push carries the player, in seconds.")]
        [SerializeField, Min(0.05f)]
        private float pushSeconds = 0.35f;

        [SerializeField, Range(0f, 1f)]
        private float volume = 0.6f;

        private static AudioClip sizzle;
        private PlayerController player;
        private float readyAt;

        private void Update()
        {
            if (player == null)
            {
                player = FindFirstObjectByType<PlayerController>();
                if (player == null)
                {
                    return;
                }
            }
            if (!player.isActiveAndEnabled || Time.time < readyAt)
            {
                return;
            }
            Vector3 offset = player.transform.position - transform.position;
            var across = new Vector3(offset.x, 0f, offset.z);
            if (across.sqrMagnitude > radius * radius || offset.y < -0.5f || offset.y > 1.5f)
            {
                return;
            }
            // Back the way they came; standing still in it, straight out from the fire.
            Vector3 moving = player.HorizontalVelocity;
            Vector3 back = moving.sqrMagnitude > 0.04f ? -moving.normalized : across.sqrMagnitude > 1e-4f ? across.normalized : -player.transform.forward;
            player.Knockback(back * pushSpeed, lift, pushSeconds);
            readyAt = Time.time + 0.5f;
            AudioSource.PlayClipAtPoint(Sizzle(), transform.position, volume);
        }

        private static AudioClip Sizzle()
        {
            if (sizzle == null)
            {
                float[] samples = SoundSynth.Sizzle(7);
                sizzle = AudioClip.Create("Sizzle", samples.Length, 1, SoundSynth.SampleRate, false);
                sizzle.SetData(samples, 0);
            }
            return sizzle;
        }
    }
}
