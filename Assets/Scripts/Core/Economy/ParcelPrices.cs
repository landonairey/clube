using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// What land costs bought from a settlement's Merchant (GL864): a parcel touching the settlement is
    /// <see cref="AdjacentPrice"/>, each further ring of parcels adds <see cref="RingStep"/>, and the Merchant's disposition
    /// scales the lot by (1 + disposition / 100), the user's formula as written (a higher disposition costs more).
    /// Pure rules: the whole settlement's own price is a row on its type (<c>SettlementType.SalePrice</c>).
    /// </summary>
    public static class ParcelPrices
    {
        /// <summary>Coins a parcel touching the settlement costs before the disposition.</summary>
        public const int AdjacentPrice = 100;

        /// <summary>Coins each further ring of parcels from the settlement adds.</summary>
        public const int RingStep = 200;

        /// <summary>
        /// The base price of a parcel <paramref name="rings"/> parcels from the settlement (1: touching it, sides or corners):
        /// 100, 300, 500... 0 for a parcel of the settlement itself (not sold one by one).
        /// </summary>
        public static int BasePrice(int rings)
        {
            return rings < 1 ? 0 : (int)System.Math.Min(int.MaxValue / 4, AdjacentPrice + RingStep * (long)(rings - 1));
        }

        /// <summary>
        /// Rings from a parcel to the nearest of <paramref name="settlement"/>'s parcels: the Chebyshev distance in parcels, so
        /// the eight around the settlement are ring 1. 0 for a parcel of the settlement; int.MaxValue when it has none.
        /// </summary>
        public static int RingsFrom(Vector2Int parcel, IEnumerable<Vector2Int> settlement)
        {
            int best = int.MaxValue;
            foreach (Vector2Int town in settlement)
            {
                int rings = Mathf.Max(Mathf.Abs(parcel.x - town.x), Mathf.Abs(parcel.y - town.y));
                if (rings < best)
                {
                    best = rings;
                }
            }
            return best;
        }

        /// <summary>
        /// A base price with the Merchant's disposition: base × (1 + disposition / 100), rounded to whole coins
        /// (adjacent at disposition 50: 150).
        /// </summary>
        public static int WithDisposition(int basePrice, int disposition)
        {
            return (int)System.Math.Round(basePrice * (100.0 + disposition) / 100.0, System.MidpointRounding.AwayFromZero);
        }

        /// <summary>What a parcel outside the settlement costs from its Merchant; 0 for one of the settlement's own.</summary>
        public static int PriceOf(Vector2Int parcel, IEnumerable<Vector2Int> settlement, int disposition)
        {
            return WithDisposition(BasePrice(RingsFrom(parcel, settlement)), disposition);
        }
    }
}
