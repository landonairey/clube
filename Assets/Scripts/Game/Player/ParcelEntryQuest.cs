using System.Linq;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The first quest (GL744): a big yellow arrow points down over the family's parcel, also marked on the minimap, until the
    /// player walks onto it, so they know which land is theirs to dig. Added by <see cref="PlayerQuests"/>.
    /// </summary>
    public class ParcelEntryQuest : MonoBehaviour
    {
        // Metres the arrow's tip hangs over the ground, and how many times the size of the tutorial's arrow it is.
        private const float ArrowHeight = 9f;
        private const float ArrowScale = 6f;

        private PlayerQuests quests;
        private PlayerParcels parcels;
        private PlayerController player;
        private Minimap minimap;
        private GameObject anchor;

        private void Start()
        {
            quests = GetComponent<PlayerQuests>();
            parcels = GetComponent<PlayerParcels>();
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
            Vector3 local = parcels.WorldView.transform.InverseTransformPoint(transform.position);
            if (!ArrivalCutscene.IsPlaying && player != null && player.enabled && land.IsOwnedAt(local))
            {
                quests.Log.Complete(PlayerQuests.EnterParcel);
            }
        }

        private void Build(LandParcels land)
        {
            WorldView world = parcels.WorldView;
            Vector2 centre = land.CentreOf(land.Owned.First());
            Vector3 top = world.transform.TransformPoint(new Vector3(centre.x, world.Config.WorldHeightInChunks * world.World.Grid.ChunkWorldSize.y, centre.y));
            Vector3 ground = world.Raycast(new Ray(top, -world.transform.up), out Vector3 hit) ? hit : new Vector3(top.x, transform.position.y, top.z);
            anchor = new GameObject("Family parcel arrow");
            anchor.transform.SetParent(world.transform, true);
            anchor.transform.position = ground;
            QuestArrow arrow = QuestArrow.Build(anchor.transform, ArrowHeight, new Material(Shader.Find("Clube/Low Poly")));
            arrow.transform.localScale = Vector3.one * ArrowScale;
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
            enabled = false;
        }
    }
}
