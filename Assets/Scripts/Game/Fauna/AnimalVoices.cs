using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The sounds an animal makes when struck and when it dies (GL692): a meaty thud for each blow and a harder one for the
    /// killing blow, then a dying groan whose pitch and length follow the animal's size (<see cref="SoundSynth.Groan"/>).
    /// Clips are made from <see cref="SoundSynth"/> the first time a species needs them and kept; played at the animal at the
    /// player's effects volume.
    /// </summary>
    public static class AnimalVoices
    {
        private const int Takes = 3;
        private const float GroanDelay = 0.12f;

        private static readonly Dictionary<string, AudioClip[]> groans = new Dictionary<string, AudioClip[]>();
        private static AudioClip[] thuds;

        /// <summary>The thud of a blow landing; <paramref name="killing"/> for the one that kills.</summary>
        public static void PlayHit(Vector3 at, bool killing)
        {
            thuds ??= MakeClips(i => SoundSynth.Thud(9100 + i), "Animal thud");
            PlayAt(thuds[Random.Range(0, thuds.Length)], at, killing ? 1f : 0.7f, killing ? 0.85f : 1.05f, 0f);
        }

        /// <summary>The groan of a dying animal, pitched to its size.</summary>
        public static void PlayGroan(AnimalSpecies species, Vector3 at)
        {
            if (!groans.TryGetValue(species.Id, out AudioClip[] clips))
            {
                float length = species.Length;
                clips = MakeClips(i => SoundSynth.Groan(length, 9200 + i), species.Id + " groan");
                groans[species.Id] = clips;
            }
            PlayAt(clips[Random.Range(0, clips.Length)], at, 0.9f, Random.Range(0.96f, 1.04f), GroanDelay);
        }

        private static AudioClip[] MakeClips(System.Func<int, float[]> make, string name)
        {
            var clips = new AudioClip[Takes];
            for (int i = 0; i < Takes; i++)
            {
                float[] samples = make(i);
                clips[i] = AudioClip.Create(name + " " + i, samples.Length, 1, SoundSynth.SampleRate, false);
                clips[i].SetData(samples, 0);
            }
            return clips;
        }

        private static void PlayAt(AudioClip clip, Vector3 at, float volume, float pitch, float delay)
        {
            float gain = SoundSettings.Gain(AudioChannel.Effects) * volume;
            if (gain <= 0f)
            {
                return;
            }
            var go = new GameObject("Animal sound");
            go.transform.position = at;
            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = Mathf.Clamp01(gain);
            source.pitch = pitch;
            source.spatialBlend = 1f;
            source.minDistance = 4f;
            source.maxDistance = 60f;
            source.PlayDelayed(delay);
            Object.Destroy(go, delay + clip.length / Mathf.Max(0.1f, pitch) + 0.2f);
        }
    }
}
