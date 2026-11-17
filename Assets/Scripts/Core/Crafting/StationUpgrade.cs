using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// What an item does when it is fitted to a station (GL703): a data row on the item, so a new upgrade is an asset change.
    /// A hammer fitted to the flat rock strikes harder, so a bun takes fewer clicks to become an ingot.
    /// </summary>
    [Serializable]
    public struct StationUpgrade
    {
        [Tooltip("The kind of station this item can be fitted to.")]
        public StationKind Station;

        [Tooltip("How many strikes' worth of work each strike does at that station. 1 or less: not an upgrade.")]
        [Min(1f)]
        public float StrikePower;

        /// <summary>True when the item can be fitted to a station.</summary>
        public bool IsUpgrade => StrikePower > 1f;

        public StationUpgrade(StationKind station, float strikePower)
        {
            Station = station;
            StrikePower = Mathf.Max(1f, strikePower);
        }
    }
}
