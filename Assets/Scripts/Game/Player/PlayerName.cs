using System.Collections.Generic;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The character's name (GL514), typed for the crew lead at the start of the tutorial and carried into the game by
    /// <see cref="GameSession.CharacterName"/>; saved with the game.
    /// </summary>
    public class PlayerName : MonoBehaviour, ISavedValues
    {
        private const string Key = "characterName";

        /// <summary>The character's name; empty when none was given (the tutorial skipped).</summary>
        public string Name { get; private set; } = "";

        private void Awake()
        {
            Name = GameSession.CharacterName ?? "";
        }

        public void SaveValues(IDictionary<string, string> values)
        {
            values[Key] = Name;
        }

        public void RestoreValues(IReadOnlyDictionary<string, string> values)
        {
            if (values.TryGetValue(Key, out string saved))
            {
                Name = saved;
                GameSession.CharacterName = saved;
            }
        }
    }
}
