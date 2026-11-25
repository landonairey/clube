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

        /// <summary>The family's own parcel, the first one given (GL764); null until then.</summary>
        public Vector2Int? Home { get; private set; }

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
            // Not a change of owner (GL770): a settlement locks hundreds at once, and each one redrew the player's whole fence.
            return locked.Add(parcel);
        }

        /// <summary>Lets a locked parcel be owned again (GL473: the player bought the trading post). False when it wasn't locked.</summary>
        public bool Unlock(Vector2Int parcel)
        {
            return locked.Remove(parcel);
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
        /// GL744, GL766: points all the way round a parcel, <paramref name="margin"/> metres outside its sides and about
        /// <paramref name="spacing"/> metres apart, the nearest to (x, z) first: where someone can be set down to walk onto the land.
        /// </summary>
        public Vector2[] PointsAround(Vector2Int parcel, Vector2 from, float margin, float spacing)
        {
            Rect area = AreaOf(parcel);
            float west = area.xMin - margin, east = area.xMax + margin, south = area.yMin - margin, north = area.yMax + margin;
            int steps = Mathf.Max(1, Mathf.CeilToInt((east - west) / Mathf.Max(0.01f, spacing)));
            var points = new Vector2[steps * 4];
            for (int i = 0; i < steps; i++)
            {
                float t = i / (float)steps;
                points[i * 4] = new Vector2(Mathf.Lerp(west, east, t), south);
                points[i * 4 + 1] = new Vector2(east, Mathf.Lerp(south, north, t));
                points[i * 4 + 2] = new Vector2(Mathf.Lerp(east, west, t), north);
                points[i * 4 + 3] = new Vector2(west, Mathf.Lerp(north, south, t));
            }
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
            Home ??= parcel;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// The parcels an area touches (x, z, metres from the world origin), its far edges included: the land a settlement stands
        /// on (GL408, GL864).
        /// </summary>
        public List<Vector2Int> ParcelsCovering(Rect area)
        {
            Vector2Int min = ParcelAt(new Vector3(area.xMin, 0f, area.yMin));
            Vector2Int max = ParcelAt(new Vector3(area.xMax, 0f, area.yMax));
            var parcels = new List<Vector2Int>((max.x - min.x + 1) * (max.y - min.y + 1));
            for (int z = min.y; z <= max.y; z++)
            {
                for (int x = min.x; x <= max.x; x++)
                {
                    parcels.Add(new Vector2Int(x, z));
                }
            }
            return parcels;
        }

        /// <summary>
        /// GL865: buys any parcel for sale at a price set by the seller (a Merchant's ring price), touching the player's land or
        /// not. False, changing nothing, when it is owned, locked, outside <see cref="SaleArea"/>, or the wallet is short.
        /// </summary>
        public bool TryBuyAt(Vector2Int parcel, int price, Wallet wallet)
        {
            if (owned.Contains(parcel) || locked.Contains(parcel) || !IsInSaleArea(parcel) || wallet == null || !wallet.Spend(price))
            {
                return false;
            }
            owned.Add(parcel);
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// GL865: buys a whole settlement's land at one price: its locked parcels are freed and every one not owned yet becomes
        /// the player's. False, changing nothing, when all are owned already or the wallet is short.
        /// </summary>
        public bool TryBuyAll(IReadOnlyCollection<Vector2Int> parcels, int price, Wallet wallet)
        {
            bool any = false;
            foreach (Vector2Int parcel in parcels)
            {
                any |= !owned.Contains(parcel);
            }
            if (!any || wallet == null || !wallet.Spend(price))
            {
                return false;
            }
            foreach (Vector2Int parcel in parcels)
            {
                locked.Remove(parcel);
                owned.Add(parcel);
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
