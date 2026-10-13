using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Land the player owns (GL42, first pass of EC7): the world is divided into square parcels
    /// of chunk columns, the player starts with one and buys neighbouring ones with coins, and
    /// only owned land can be dug or built on. Plain data: parcels are numbered by their
    /// position over the world (x, z), so they need no chunk to be loaded.
    /// </summary>
    /// <remarks>
    /// A parcel covers [index × size, (index + 1) × size) metres along x and z from the world
    /// origin, so a sample on the line between two parcels belongs to the one after it.
    /// Ownership isn't saved yet (S2).
    /// </remarks>
    public sealed class LandParcels
    {
        private static readonly Vector2Int[] Sides = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        private readonly HashSet<Vector2Int> owned = new HashSet<Vector2Int>();

        /// <param name="size">Width of a parcel along x and z, in metres.</param>
        /// <param name="firstPrice">Coins the first parcel bought costs.</param>
        /// <param name="priceStep">Coins each parcel bought after that adds to the price.</param>
        public LandParcels(float size, int firstPrice, int priceStep)
        {
            Size = Mathf.Max(0.01f, size);
            FirstPrice = Mathf.Max(0, firstPrice);
            PriceStep = Mathf.Max(0, priceStep);
        }

        /// <summary>Raised after a parcel is granted or bought.</summary>
        public event Action Changed;

        /// <summary>Width of a parcel along x and z, in metres.</summary>
        public float Size { get; }

        public int FirstPrice { get; }

        public int PriceStep { get; }

        public IReadOnlyCollection<Vector2Int> Owned => owned;

        /// <summary>Parcels bought so far: those owned beyond the first, which was given.</summary>
        public int Bought => Mathf.Max(0, owned.Count - 1);

        /// <summary>The parcel holding a position relative to the world origin.</summary>
        public Vector2Int ParcelAt(Vector3 position)
        {
            return new Vector2Int(Mathf.FloorToInt(position.x / Size), Mathf.FloorToInt(position.z / Size));
        }

        public bool IsOwned(Vector2Int parcel)
        {
            return owned.Contains(parcel);
        }

        /// <summary>True when a position relative to the world origin is on owned land.</summary>
        public bool IsOwnedAt(Vector3 position)
        {
            return owned.Contains(ParcelAt(position));
        }

        /// <summary>True for a parcel not owned yet that shares a side with owned land (the first one: any).</summary>
        public bool IsBuyable(Vector2Int parcel)
        {
            if (owned.Contains(parcel))
            {
                return false;
            }
            if (owned.Count == 0)
            {
                return true;
            }
            foreach (Vector2Int side in Sides)
            {
                if (owned.Contains(parcel + side))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>What the next parcel costs: the first price, plus a step for each one bought before.</summary>
        public int NextPrice => FirstPrice + PriceStep * Bought;

        /// <summary>Gives a parcel without paying (the one the player starts on). False when already owned.</summary>
        public bool Grant(Vector2Int parcel)
        {
            if (!owned.Add(parcel))
            {
                return false;
            }
            Changed?.Invoke();
            return true;
        }

        /// <summary>Buys a buyable parcel at <see cref="NextPrice"/>. False, changing nothing, when it isn't buyable or the wallet is short.</summary>
        public bool TryBuy(Vector2Int parcel, Wallet wallet)
        {
            if (!IsBuyable(parcel) || wallet == null || !wallet.Spend(NextPrice))
            {
                return false;
            }
            owned.Add(parcel);
            Changed?.Invoke();
            return true;
        }
    }
}
