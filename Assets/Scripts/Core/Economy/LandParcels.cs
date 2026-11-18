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
        private readonly HashSet<Vector2Int> locked = new HashSet<Vector2Int>();

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

        /// <summary>Keeps a parcel from ever being bought or granted (GL408: the town's land). False when it was already locked.</summary>
        public bool Lock(Vector2Int parcel)
        {
            if (!locked.Add(parcel))
            {
                return false;
            }
            Changed?.Invoke();
            return true;
        }

        /// <summary>Lets a locked parcel be owned again (GL473: the player bought the trading post). False when it wasn't locked.</summary>
        public bool Unlock(Vector2Int parcel)
        {
            if (!locked.Remove(parcel))
            {
                return false;
            }
            Changed?.Invoke();
            return true;
        }

        /// <summary>True for a parcel nobody can buy (the town's land, GL408).</summary>
        public bool IsLocked(Vector2Int parcel)
        {
            return locked.Contains(parcel);
        }

        /// <summary>
        /// Where land is for sale (GL666), by a parcel's centre (x, z, metres from the world origin): on a bounded map the playable
        /// area, so nothing between its edge and the map's outer boundary can be bought. Null: everywhere.
        /// </summary>
        public Func<Vector2, bool> SaleArea { get; set; }

        /// <summary>The centre of a parcel (x, z, metres from the world origin).</summary>
        public Vector2 CentreOf(Vector2Int parcel)
        {
            return new Vector2((parcel.x + 0.5f) * Size, (parcel.y + 0.5f) * Size);
        }

        /// <summary>The square of land a parcel covers (x, z, metres from the world origin).</summary>
        public Rect AreaOf(Vector2Int parcel)
        {
            return new Rect(parcel.x * Size, parcel.y * Size, Size, Size);
        }

        /// <summary>
        /// GL744: the points <paramref name="margin"/> metres outside a parcel's four sides, level with a point (x, z), the
        /// nearest side first: where someone can be set down to walk onto the land.
        /// </summary>
        public Vector2[] PointsOutside(Vector2Int parcel, Vector2 from, float margin)
        {
            Rect area = AreaOf(parcel);
            float x = Mathf.Clamp(from.x, area.xMin, area.xMax), z = Mathf.Clamp(from.y, area.yMin, area.yMax);
            var points = new[]
            {
                new Vector2(area.xMin - margin, z), new Vector2(area.xMax + margin, z),
                new Vector2(x, area.yMin - margin), new Vector2(x, area.yMax + margin),
            };
            System.Array.Sort(points, (a, b) => (a - from).sqrMagnitude.CompareTo((b - from).sqrMagnitude));
            return points;
        }

        /// <summary>True when a parcel's centre is inside <see cref="SaleArea"/> (always, with none).</summary>
        public bool IsInSaleArea(Vector2Int parcel)
        {
            return SaleArea == null || SaleArea(CentreOf(parcel));
        }

        /// <summary>
        /// True for a parcel not owned yet that shares a side with owned land (the first one: any); never a locked one, nor one
        /// whose centre is outside <see cref="SaleArea"/>.
        /// </summary>
        public bool IsBuyable(Vector2Int parcel)
        {
            if (owned.Contains(parcel) || locked.Contains(parcel) || !IsInSaleArea(parcel))
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
            if (locked.Contains(parcel) || !owned.Add(parcel))
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
