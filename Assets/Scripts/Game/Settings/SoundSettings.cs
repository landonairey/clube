using UnityEngine;

namespace Clube.Game
{
    /// <summary>What a sound is, for its volume slider (GL276).</summary>
    public enum AudioChannel
    {
        /// <summary>Tool hits, breaking and gathering, crafting, selling, building.</summary>
        Effects,

        /// <summary>The player's footsteps.</summary>
        Footsteps,

        /// <summary>Wind, leaves and birds.</summary>
        Ambient,
    }

    /// <summary>
    /// The player's audio mix (GL276): a volume for sound effects, footsteps and ambience, each 0-100, 50 by default,
    /// where 50 is the game's own mix, 0 silent and 100 twice as loud. Kept on the player's machine with
    /// <see cref="PlayerPrefs"/>, like <see cref="GraphicsSettings"/>; set on the Audio popup (<see cref="AudioSettingsPage"/>).
    /// </summary>
    public static class SoundSettings
    {
        public const int Default = 50;

        private static readonly string[] Keys = { "audio.effects", "audio.footsteps", "audio.ambient" };
        private static readonly int[] levels = { Default, Default, Default };
        private static bool loaded;

        /// <summary>A channel's slider, 0-100.</summary>
        public static int Level(AudioChannel channel)
        {
            Load();
            return levels[(int)channel];
        }

        public static void SetLevel(AudioChannel channel, int level)
        {
            Load();
            level = Mathf.Clamp(level, 0, 100);
            if (levels[(int)channel] == level)
            {
                return;
            }
            levels[(int)channel] = level;
            PlayerPrefs.SetInt(Keys[(int)channel], level);
            PlayerPrefs.Save();
        }

        /// <summary>What a channel's sounds are multiplied by: 1 at the default 50.</summary>
        public static float Gain(AudioChannel channel)
        {
            return Level(channel) / (float)Default;
        }

        private static void Load()
        {
            if (loaded)
            {
                return;
            }
            loaded = true;
            for (int i = 0; i < Keys.Length; i++)
            {
                levels[i] = Mathf.Clamp(PlayerPrefs.GetInt(Keys[i], Default), 0, 100);
            }
        }
    }
}
