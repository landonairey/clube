using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// What stealing costs the player with the keepers it wrongs (GL729: "when you take from a cart or boat, the shops
    /// that the cart or boat are going to will reduce their disposition with you"). A taking costs at least
    /// <see cref="LeastPoints"/> points of <see cref="Disposition"/>, and a point for every <see cref="CoinsPerPoint"/>
    /// coins the goods are worth at their listed price: ten times what honest trade earns back
    /// (<see cref="Disposition.CoinsPerPoint"/>).
    /// </summary>
    public static class Theft
    {
        /// <summary>The fewest points any taking costs.</summary>
        public const int LeastPoints = 5;

        /// <summary>Coins of stolen goods for each point.</summary>
        public const float CoinsPerPoint = 10f;

        /// <summary>The points of disposition goods worth <paramref name="coins"/> cost; 0 for nothing taken.</summary>
        public static int PointsFor(float coins)
        {
            if (coins <= 1e-4f)
            {
                return 0;
            }
            return Mathf.Max(LeastPoints, Mathf.CeilToInt(coins / CoinsPerPoint - 1e-4f));
        }

        /// <summary>What an amount of an item is worth at its listed price (its lot's coins spread over the lot); 0 when it isn't listed.</summary>
        public static float Worth(PriceList prices, ItemDefinition item, float amount)
        {
            if (prices == null || item == null || amount <= 0f || !prices.TryGetLot(item, out int coins, out int lot))
            {
                return 0f;
            }
            return amount * coins / Mathf.Max(1, lot);
        }
    }
}
