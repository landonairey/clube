using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The audio popup (GL276), opened from the pause popup beside Video: sliders 0-100 for sound effects (tool hits and
    /// using things), footsteps, and ambience (wind, leaves, birds), 50 by default. Changes apply at once and are kept
    /// (<see cref="SoundSettings"/>); Back returns to the pause popup.
    /// </summary>
    public sealed class AudioSettingsPage
    {
        public const float Width = 420f;
        public const float Height = 330f;

        /// <summary>Draws the popup's contents in the current layout area; true when Back was pressed.</summary>
        public bool Draw(GUIStyle title, GUIStyle label, GUIStyle hint, GUIStyle button)
        {
            GUILayout.Label("Audio", title);
            GUILayout.Space(8f);
            Slider("Sound effects", "Tool hits, breaking, gathering, crafting", AudioChannel.Effects, label, hint);
            Slider("Footsteps", null, AudioChannel.Footsteps, label, hint);
            Slider("Ambient", "Wind, leaves and birds", AudioChannel.Ambient, label, hint);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Defaults", button, GUILayout.Height(30f)))
            {
                foreach (AudioChannel channel in System.Enum.GetValues(typeof(AudioChannel)))
                {
                    SoundSettings.SetLevel(channel, SoundSettings.Default);
                }
            }
            GUILayout.Space(6f);
            return GUILayout.Button("Back", button, GUILayout.Height(36f));
        }

        private static void Slider(string name, string about, AudioChannel channel, GUIStyle label, GUIStyle hint)
        {
            int level = SoundSettings.Level(channel);
            GUILayout.Label($"{name}  {level}", label);
            SoundSettings.SetLevel(channel, Mathf.RoundToInt(GUILayout.HorizontalSlider(level, 0f, 100f)));
            if (about != null)
            {
                GUILayout.Label(about, hint);
            }
            GUILayout.Space(8f);
        }
    }
}
