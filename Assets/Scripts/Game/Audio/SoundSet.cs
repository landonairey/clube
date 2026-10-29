using System;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A few takes of one sound: each play picks a random clip at a slightly random pitch, so
    /// repeated steps and hits don't sound identical. With <see cref="playAll"/> every clip plays
    /// at once instead (a layered sound, e.g. a chime over coins).
    /// </summary>
    [Serializable]
    public class SoundSet
    {
        [SerializeField]
        private AudioClip[] clips = Array.Empty<AudioClip>();

        [SerializeField, Range(0f, 1f)]
        private float volume = 0.6f;

        [Tooltip("Each play's pitch is picked between 1 - this and 1 + this.")]
        [SerializeField, Range(0f, 0.5f)]
        private float pitchJitter = 0.08f;

        [Tooltip("Play every clip together instead of one at random.")]
        [SerializeField]
        private bool playAll;

        private int last = -1;

        /// <summary>True when there's at least one clip to play.</summary>
        public bool HasClips => clips != null && clips.Length > 0;

        /// <summary>Plays the sound on a source, scaled by <paramref name="volumeScale"/>.</summary>
        public void Play(AudioSource source, float volumeScale = 1f)
        {
            if (source == null || !HasClips)
            {
                return;
            }
            source.pitch = 1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter);
            if (playAll)
            {
                foreach (AudioClip clip in clips)
                {
                    if (clip != null)
                    {
                        source.PlayOneShot(clip, volume * volumeScale);
                    }
                }
                return;
            }
            AudioClip picked = clips[PickIndex()];
            if (picked != null)
            {
                source.PlayOneShot(picked, volume * volumeScale);
            }
        }

        // A random clip, never the same one twice in a row when there's a choice.
        private int PickIndex()
        {
            if (clips.Length == 1)
            {
                return 0;
            }
            int index = UnityEngine.Random.Range(0, clips.Length - 1);
            if (index >= last)
            {
                index++;
            }
            last = index;
            return index;
        }
    }
}
