using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A meteor's sounds (GL870): the flyby as it comes in, and the impact, 3D with a long falloff so it is loud near the
    /// crash and still heard a couple of settlements away, arriving late with the speed of sound. The impact can be a
    /// window of a longer clip (<see cref="impactStart"/>, <see cref="impactLength"/>: "the middle part" of the source).
    /// One asset, <c>Resources/Meteor/MeteorSounds</c>, loaded by <see cref="MeteorEvent"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Clube/Meteor Sounds", fileName = "MeteorSounds")]
    public class MeteorSounds : ScriptableObject
    {
        public const string ResourcePath = "Meteor/MeteorSounds";

        private const float SpeedOfSound = 343f;

        [SerializeField]
        private AudioClip flyby;

        [SerializeField, Range(0f, 1f)]
        private float flybyVolume = 0.8f;

        [Tooltip("The flyby is heard in full within this many metres of the meteor, fading to nothing at the far distance.")]
        [SerializeField]
        private float flybyNear = 300f, flybyFar = 4000f;

        [SerializeField]
        private AudioClip impact;

        [SerializeField, Range(0f, 1f)]
        private float impactVolume = 1f;

        [Tooltip("Where in the impact clip to start, seconds.")]
        [SerializeField, Min(0f)]
        private float impactStart;

        [Tooltip("How much of the impact clip to play from its start, seconds; 0 plays to the end.")]
        [SerializeField, Min(0f)]
        private float impactLength;

        [Tooltip("Loud within this many metres of the crash; heard faintly out to the far distance (a couple of settlements).")]
        [SerializeField]
        private float impactNear = 80f, impactFar = 2500f;

        private static MeteorSounds loaded;
        private static bool looked;

        /// <summary>The project's meteor sounds, or null when the asset is missing.</summary>
        public static MeteorSounds Load()
        {
            if (!looked)
            {
                looked = true;
                loaded = Resources.Load<MeteorSounds>(ResourcePath);
            }
            return loaded;
        }

        /// <summary>Seconds of flyby clip (0 without one).</summary>
        public float FlybySeconds => flyby != null ? flyby.length : 0f;

        /// <summary>Starts the flyby on a source that rides with the meteor.</summary>
        public void PlayFlyby(GameObject meteor)
        {
            if (flyby == null)
            {
                return;
            }
            AudioSource source = Source(meteor, flybyNear, flybyFar);
            source.clip = flyby;
            source.volume = flybyVolume * SoundSettings.Gain(AudioChannel.Effects);
            source.Play();
        }

        /// <summary>
        /// Plays the impact at the crash, delayed by how long sound takes to reach the listener; the source removes itself.
        /// </summary>
        public void PlayImpact(Transform parent, Vector3 at)
        {
            if (impact == null)
            {
                return;
            }
            var go = new GameObject("Meteor impact sound");
            go.transform.SetParent(parent, true);
            go.transform.position = at;
            AudioSource source = Source(go, impactNear, impactFar);
            source.clip = impact;
            source.volume = impactVolume * SoundSettings.Gain(AudioChannel.Effects);
            float start = Mathf.Clamp(impactStart, 0f, Mathf.Max(0f, impact.length - 0.05f));
            float length = impactLength > 0f ? Mathf.Min(impactLength, impact.length - start) : impact.length - start;
            source.time = start;
            AudioListener listener = Object.FindAnyObjectByType<AudioListener>();
            float delay = listener != null ? Vector3.Distance(listener.transform.position, at) / SpeedOfSound : 0f;
            source.PlayDelayed(delay);
            if (impactLength > 0f)
            {
                source.SetScheduledEndTime(AudioSettings.dspTime + delay + length);
            }
            Destroy(go, delay + length + 0.5f);
        }

        // A 3D source: full within near, a long tail to nothing at far.
        private static AudioSource Source(GameObject on, float near, float far)
        {
            AudioSource source = on.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Custom;
            source.maxDistance = far;
            float n = Mathf.Clamp01(near / far);
            var curve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(n, 1f),
                new Keyframe(Mathf.Lerp(n, 1f, 0.25f), 0.35f),
                new Keyframe(Mathf.Lerp(n, 1f, 0.6f), 0.12f),
                new Keyframe(1f, 0f));
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, curve);
            return source;
        }
    }
}
