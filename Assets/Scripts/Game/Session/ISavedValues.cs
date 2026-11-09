using System.Collections.Generic;

namespace Clube.Game
{
    /// <summary>
    /// A scene component that keeps a few small values in the save's named values (<c>GameSave.Values</c>, GL442).
    /// <see cref="GameSessionHost"/> finds every one in the scene. Keys must be unique across the game: prefix them
    /// with the component, e.g. "volume.taken".
    /// </summary>
    public interface ISavedValues
    {
        /// <summary>Writes this component's values into the save.</summary>
        void SaveValues(IDictionary<string, string> values);

        /// <summary>Restores them; a key that's missing (an older save) keeps the default.</summary>
        void RestoreValues(IReadOnlyDictionary<string, string> values);
    }
}
