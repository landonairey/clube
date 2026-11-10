using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The menu button click (GL491): a short synthesised tick (<see cref="SoundSynth.Click"/>), made once and played
    /// from one hidden source that survives scene loads, so menus without a player can click too. Back-style buttons
    /// play it pitched slightly down.
    /// </summary>
    public static class UiClick
    {
        private const float BackPitch = 0.82f;
        private const float Volume = 0.6f;

        private static AudioSource source;
        private static AudioClip clip;

        /// <summary>Plays the click, lower for Back, Close and Cancel; at the effects volume.</summary>
        public static void Play(bool back)
        {
            if (source == null)
            {
                var go = new GameObject("UI click") { hideFlags = HideFlags.HideAndDontSave };
                Object.DontDestroyOnLoad(go);
                source = go.AddComponent<AudioSource>();
                source.spatialBlend = 0f;
                source.playOnAwake = false;
            }
            if (clip == null)
            {
                float[] samples = SoundSynth.Click(7);
                clip = AudioClip.Create("UI click", samples.Length, 1, SoundSynth.SampleRate, false);
                clip.SetData(samples, 0);
            }
            source.pitch = back ? BackPitch : 1f;
            source.PlayOneShot(clip, Volume * SoundSettings.Gain(AudioChannel.Effects));
        }
    }
}
