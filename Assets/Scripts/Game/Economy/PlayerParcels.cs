using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The land the player owns (GL42): <see cref="LandParcels"/> of a set number of chunks
    /// across. The parcel the player first lands in is theirs; looking at the ground of a
    /// locked parcel next to their land and pressing Interact buys it with coins. The player's
    /// tools, brush, drops and building ask <see cref="CanEdit"/> first, so locked land can't be
    /// changed. Without this component every tool works everywhere, as in the labs.
    /// </summary>
    [RequireComponent(typeof(PlayerWallet))]
    public class PlayerParcels : MonoBehaviour
    {
        [SerializeField]
        private WorldView worldView;

        [Tooltip("Width of a parcel, in chunk columns along x and z.")]
        [SerializeField, Min(1)]
        private int sizeInChunks = 8;

        [Tooltip("Coins the first parcel bought costs.")]
        [SerializeField, Min(0)]
        private int firstPrice = 100;

        [Tooltip("Coins each parcel bought after that adds to the price.")]
        [SerializeField, Min(0)]
        private int priceStep = 100;

        private PlayerWallet wallet;
        private PlayerSpawn spawn;
        private ParcelOffer offer;
        private NotOwnedNotice notOwned;

        // Parcels a save owned (GL205), granted as soon as the land is set up.
        private List<Vector2Int> restoring;

        /// <summary>Owns what a save owned (GL205), in place of the parcel the player lands in.</summary>
        public void Restore(IEnumerable<Vector2Int> owned)
        {
            restoring = new List<Vector2Int>(owned);
        }

        /// <summary>The land and its prices; null until the world exists.</summary>
        public LandParcels Parcels { get; private set; }

        public WorldView WorldView => worldView;

        /// <summary>Raised when the player buys a parcel, with it.</summary>
        public event System.Action<Vector2Int> Bought;

        private void Awake()
        {
            wallet = GetComponent<PlayerWallet>();
            spawn = GetComponent<PlayerSpawn>();
            offer = new ParcelOffer(this);
            notOwned = new NotOwnedNotice();
        }

        private void Update()
        {
            if (Parcels == null && worldView != null && worldView.IsReady)
            {
                Parcels = new LandParcels(sizeInChunks * worldView.World.Grid.ChunkWorldSize.x, firstPrice, priceStep);
            }
            if (Parcels != null && restoring != null)
            {
                foreach (Vector2Int parcel in restoring)
                {
                    Parcels.Grant(parcel);
                }
                restoring = null;
            }

            // The first parcel is the one the player lands in.
            if (Parcels != null && Parcels.Owned.Count == 0 && (spawn == null || spawn.IsPlaced))
            {
                Parcels.Grant(Parcels.ParcelAt(Local(transform.position)));
            }
        }

        /// <summary>True when the player may change the ground at a world-space point: always, until the land is set up.</summary>
        public bool CanEdit(Vector3 worldPoint)
        {
            return Parcels == null || Parcels.Owned.Count == 0 || Parcels.IsOwnedAt(Local(worldPoint));
        }

        /// <summary>True when the player may change a global sample of the world.</summary>
        public bool CanEditSample(Vector3Int sample)
        {
            return Parcels == null || Parcels.Owned.Count == 0 || Parcels.IsOwnedAt(worldView.World.Grid.SampleToWorld(sample));
        }

        /// <summary>
        /// The offer to buy the parcel under a terrain hit, for <see cref="PlayerInteractor"/>: null on the player's own
        /// land, and a "You don't own this land" notice on land too far from it to buy (GL179).
        /// </summary>
        public IInteractable Find(RaycastHit hit)
        {
            if (Parcels == null || Parcels.Owned.Count == 0 || hit.collider.GetComponentInParent<WorldView>() != worldView)
            {
                return null;
            }
            // Just inside the surface, so a hit on a wall between parcels counts for the parcel it faces.
            Vector2Int parcel = Parcels.ParcelAt(Local(hit.point - hit.normal * 0.05f));
            if (Parcels.IsOwned(parcel))
            {
                return null;
            }
            if (!Parcels.IsBuyable(parcel))
            {
                // Land further than a parcel from the player's own (GL179): it only says so.
                return notOwned;
            }
            offer.Parcel = parcel;
            return offer;
        }

        private Vector3 Local(Vector3 worldPoint)
        {
            return worldView != null ? worldView.transform.InverseTransformPoint(worldPoint) : worldPoint;
        }

        // Land that can't be bought from here (GL179): Interact does nothing.
        private sealed class NotOwnedNotice : INoticeOnly
        {
            public string Prompt => "You don't own this land";

            public void Interact(PlayerInteractor by)
            {
            }
        }

        // Buying the parcel being looked at.
        private sealed class ParcelOffer : IInteractable
        {
            private readonly PlayerParcels owner;

            public ParcelOffer(PlayerParcels owner)
            {
                this.owner = owner;
            }

            public Vector2Int Parcel { get; set; }

            public string Prompt
            {
                get
                {
                    int price = owner.Parcels.NextPrice;
                    float coins = owner.wallet.Wallet.Coins;
                    return coins >= price ? $"Buy this land: {price} coins" : $"This land costs {price} coins (you have {Wallet.Format(coins)})";
                }
            }

            public void Interact(PlayerInteractor by)
            {
                if (owner.Parcels.TryBuy(Parcel, owner.wallet.Wallet))
                {
                    owner.Bought?.Invoke(Parcel);
                }
            }
        }
    }
}
