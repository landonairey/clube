using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A town station the player may use once its keeper likes them enough (GL472: the blacksmith's anvil and furnace at
    /// disposition 25). Until then it's the station's look only (<see cref="ModelCopy"/>) and says why when looked at;
    /// once the disposition is reached it turns into the game's own station (the prefab, fixed in place, GL421), for good.
    /// </summary>
    public class TownStation : MonoBehaviour, IClosableInteractable
    {
        private GameObject prefab;
        private Disposition disposition;
        private int needed;
        private string keeper;
        private string stationName;

        /// <summary>The station's real prefab, whose disposition opens it, the value needed and who keeps it ("the blacksmith").</summary>
        public void Setup(GameObject prefab, Disposition disposition, int needed, string keeper, string stationName)
        {
            this.prefab = prefab;
            this.disposition = disposition;
            this.needed = needed;
            this.keeper = keeper;
            this.stationName = stationName;
        }

        public bool IsAvailable => false;

        public string Prompt => $"{char.ToUpperInvariant(keeper[0])}{keeper.Substring(1)} won't let you use the {stationName} yet (disposition {disposition.Value} of {needed})";

        public void Interact(PlayerInteractor by)
        {
        }

        private void Update()
        {
            if (prefab == null || disposition == null || disposition.Value < needed)
            {
                return;
            }
            GameObject station = Instantiate(prefab, transform.parent);
            station.name = gameObject.name;
            station.transform.SetPositionAndRotation(transform.position, transform.rotation);
            // Part of the town: never picked up, and with no item it isn't saved as the player's (PlacedObjectSaving).
            if (station.TryGetComponent(out PlacedObject placed))
            {
                placed.Fixed = true;
                placed.Item = null;
            }
            Destroy(gameObject);
        }
    }
}
