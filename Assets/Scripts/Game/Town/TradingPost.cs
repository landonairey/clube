using System.Collections.Generic;
using System.Globalization;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// What the player has done at a trading post (GL471-GL475), the only part of a settlement that's saved: the town itself
    /// follows from the seed (<see cref="TownSite"/>). Holds each shopkeeper's <see cref="Disposition"/>. Sits on the
    /// settlement's object from the scene's start, so a save restores into it before the town is built; keys start with
    /// <see cref="keyPrefix"/>, so a second settlement keeps its own.
    /// </summary>
    public class TradingPost : MonoBehaviour, ISavedValues
    {
        [Tooltip("What this settlement's saved values are called (unique per settlement).")]
        [SerializeField]
        private string keyPrefix = "tradingpost";

        private readonly Dictionary<ShopKind, Disposition> dispositions = new Dictionary<ShopKind, Disposition>();

        /// <summary>A shopkeeper's disposition towards the player (GL471): 10 until they've traded.</summary>
        public Disposition DispositionOf(ShopKind kind)
        {
            if (!dispositions.TryGetValue(kind, out Disposition disposition))
            {
                disposition = new Disposition();
                dispositions[kind] = disposition;
            }
            return disposition;
        }

        private string Key(string name)
        {
            return $"{keyPrefix}.{name}";
        }

        public void SaveValues(IDictionary<string, string> values)
        {
            foreach (KeyValuePair<ShopKind, Disposition> pair in dispositions)
            {
                values[Key($"disposition.{pair.Key}")] = string.Format(CultureInfo.InvariantCulture, "{0};{1:R}", pair.Value.Value, pair.Value.Carried);
            }
        }

        public void RestoreValues(IReadOnlyDictionary<string, string> values)
        {
            foreach (ShopKind kind in System.Enum.GetValues(typeof(ShopKind)))
            {
                if (!values.TryGetValue(Key($"disposition.{kind}"), out string text))
                {
                    continue;
                }
                string[] parts = text.Split(';');
                if (parts.Length == 2 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                    && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float carried))
                {
                    DispositionOf(kind).Restore(value, carried);
                }
            }
        }
    }
}
