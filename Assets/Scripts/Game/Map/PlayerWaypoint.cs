using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The player's waypoint (GL866): set on the world map by a left click on a settlement or anywhere else (a pin),
    /// removed by a right click; one at a time. Saved in the save's named values. The map highlights it and the
    /// <see cref="Minimap"/> points to it (GL867).
    /// </summary>
    public class PlayerWaypoint : MonoBehaviour, ISavedValues
    {
        private const string Key = "waypoint";

        /// <summary>The waypoint now; <see cref="MapWaypoint.None"/> for none.</summary>
        public MapWaypoint Current { get; private set; }

        /// <summary>Goes up by one at every change, so views can tell when to redraw.</summary>
        public int Version { get; private set; }

        /// <summary>Makes the settlement the waypoint, replacing any other.</summary>
        public void SetSettlement(PlannedSettlement settlement)
        {
            if (settlement != null)
            {
                Set(MapWaypoint.AtSettlement(settlement.Index, settlement.Position));
            }
        }

        /// <summary>Drops a pin at this map position (x east, y north, in the map's metres), replacing any other waypoint.</summary>
        public void SetPin(Vector2 position)
        {
            Set(MapWaypoint.Pin(position));
        }

        /// <summary>Removes the waypoint.</summary>
        public void Clear()
        {
            if (Current.IsSet)
            {
                Set(MapWaypoint.None);
            }
        }

        /// <summary>The waypoint's settlement in this plan; null for a pin, none, or an index the plan doesn't have.</summary>
        public PlannedSettlement SettlementIn(WorldPlan plan)
        {
            int index = Current.Settlement;
            if (!Current.IsSet || index < 0 || plan == null)
            {
                return null;
            }
            foreach (PlannedSettlement settlement in plan.Settlements)
            {
                if (settlement.Index == index)
                {
                    return settlement;
                }
            }
            return null;
        }

        private void Set(MapWaypoint waypoint)
        {
            Current = waypoint;
            Version++;
        }

        public void SaveValues(IDictionary<string, string> values)
        {
            values[Key] = Current.Encode();
        }

        public void RestoreValues(IReadOnlyDictionary<string, string> values)
        {
            if (values.TryGetValue(Key, out string saved))
            {
                Set(MapWaypoint.Decode(saved));
            }
        }
    }
}
