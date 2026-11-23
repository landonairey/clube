using System;
using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>One row of <see cref="YardStations"/>: the player's own prefab for a yard fixture kind (GL833).</summary>
    [Serializable]
    public struct YardStationPrefab
    {
        [Tooltip("The yard fixture kind it stands for: anvil, furnace, campfire, workbench, spinning-wheel.")]
        public string Kind;

        [Tooltip("The prefab the player builds it from.")]
        public GameObject Prefab;
    }

    /// <summary>
    /// Puts the player's real workstations in a settlement's work yards (GL833): a fixture whose kind has a prefab here
    /// (the anvil, furnace, campfire, workbench and spinning wheel the player builds) is that prefab's look, standing
    /// solid, instead of the low-poly stand-in. A fixture that is a working station (it has a <see cref="StationObject"/>)
    /// turns into the real, usable one once its shop's keeper likes the player enough, like the town's smithy (<see cref="TownStation"/>).
    /// Kinds with no row keep the stand-in model.
    /// </summary>
    public static class YardStations
    {
        /// <summary>The keeper's disposition that opens a yard's stations to the player, as at the town's smithy (GL472).</summary>
        public const int OpenAt = TownBuilder.SmithyDisposition;

        /// <summary>The prefab for a fixture kind, or null for a kind that keeps its stand-in model.</summary>
        public static GameObject PrefabFor(IReadOnlyList<YardStationPrefab> rows, string kind)
        {
            if (rows == null || kind == null)
            {
                return null;
            }
            foreach (YardStationPrefab row in rows)
            {
                if (row.Kind == kind && row.Prefab != null)
                {
                    return row.Prefab;
                }
            }
            return null;
        }

        /// <summary>
        /// Stands the fixture's prefab in the yard at <paramref name="local"/> (the yard's frame), facing <paramref name="yaw"/>.
        /// A working station is locked behind <paramref name="disposition"/> of <paramref name="keeper"/> (null: always open to look at only).
        /// </summary>
        public static GameObject Stand(GameObject prefab, Transform yard, string kind, Vector3 local, float yaw, Disposition disposition, string keeper)
        {
            GameObject copy = ModelCopy.Copy(prefab, yard, kind, true);
            copy.transform.localPosition = local;
            copy.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (prefab.GetComponent<StationObject>() != null && disposition != null)
            {
                copy.AddComponent<TownStation>().Setup(prefab, disposition, OpenAt, keeper ?? "the keeper", kind);
            }
            return copy;
        }
    }
}
