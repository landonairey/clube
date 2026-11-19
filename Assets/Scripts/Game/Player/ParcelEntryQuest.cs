using System.Linq;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The first quest (GL744): a big yellow arrow points down over the family's parcel, also marked on the minimap, until the
    /// player walks onto it, so they know which land is theirs to dig. The arrow bobs 1.5 m, and the parcel stands in yellow
    /// hologram walls (<see cref="ParcelHologram"/>, GL783) until then. Added by <see cref="PlayerQuests"/>.
    /// </summary>
    public class ParcelEntryQuest : MonoBehaviour
    {
        // How many times the size of the tutorial's arrow it is, and how far its middle is over its tip at that size.
        private const float ArrowScale = 6f;
        private const float TipBelowMiddle = 0.9f;
        // The arrow's bob (GL783): metres of travel top to bottom, and seconds for one bob.
        private const float BobTravel = 1.5f;
        private const float BobCycle = 1.5f;

        private PlayerQuests quests;
        private PlayerParcels parcels;
        private PlayerController player;
        private Minimap minimap;
        private GameObject anchor;
        private ParcelBorders borders;
        private ParcelHologram hologram;

        private void Start()
        {
            quests = GetComponent<PlayerQuests>();
            parcels = GetComponent<PlayerParcels>();
            borders = GetComponent<ParcelBorders>();
            player = GetComponent<PlayerController>();
            minimap = GetComponentInChildren<Minimap>(true);
        }

        private void Update()
        {
            if (quests.Log.IsDone(PlayerQuests.EnterParcel))
            {
                Finish();
                return;
            }
            // The tutorial has no parcels, and its quests are frozen.
            LandParcels land = parcels != null && parcels.enabled ? parcels.Parcels : null;
            if (quests.Log.Frozen || land == null || land.Owned.Count == 0)
            {
                return;
            }
            if (anchor == null)
            {
                Build(land);
            }
            if (hologram == null && borders != null && borders.BandMaterial != null && parcels.WorldView.IsReady)
            {
                hologram = ParcelHologram.Build(parcels.WorldView, land, land.Home ?? land.Owned.First(), borders.FenceHeight, borders.BandMaterial);
            }
            Vector3 local = parcels.WorldView.transform.InverseTransformPoint(transform.position);
            if (!ArrivalCutscene.IsPlaying && player != null && player.enabled && land.IsOwnedAt(local))
            {
                quests.Log.Complete(PlayerQuests.EnterParcel);
            }
        }

        private void Build(LandParcels land)
        {
            WorldView world = parcels.WorldView;
            Vector2Int parcel = land.Home ?? land.Owned.First();
            anchor = new GameObject("Family parcel arrow");
            anchor.transform.SetParent(world.transform, true);
            anchor.transform.position = ParcelArrowSpot.TipOver(world, land, parcel, transform.position.y);
            QuestArrow arrow = QuestArrow.Build(anchor.transform, TipBelowMiddle, new Material(Shader.Find("Clube/Low Poly")));
            arrow.transform.localScale = Vector3.one * ArrowScale;
            arrow.SetBob(BobTravel, BobCycle);
            arrow.gameObject.SetActive(true);
            if (minimap != null)
            {
                minimap.Marker = anchor.transform;
            }
        }

        private void Finish()
        {
            if (anchor != null)
            {
                if (minimap != null && minimap.Marker == anchor.transform)
                {
                    minimap.Marker = null;
                }
                Destroy(anchor);
            }
            if (hologram != null)
            {
                Destroy(hologram.gameObject);
            }
            enabled = false;
        }
    }
}
