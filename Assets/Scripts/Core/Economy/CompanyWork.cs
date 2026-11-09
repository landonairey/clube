using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The rules of the opening tutorial's job at the Mining Corp quarry (GL479): where the quarry is dug, what the
    /// company pays for the stone handed in, and when the worker counts as stealing. Kept apart from the scene so
    /// they can be tested.
    /// </summary>
    public static class CompanyWork
    {
        /// <summary>The worker's share of the stone's price: 10c for every 100c.</summary>
        public const float WageShare = 0.1f;

        /// <summary>Metres from the town's gate, out along the road, to the quarry.</summary>
        public const float OutFromGate = 12f;

        /// <summary>Metres to the side of the road, so the cart track misses the pit.</summary>
        public const float BesideRoad = 18f;

        /// <summary>The quarry's floor, in metres under the ground at its centre.</summary>
        public const float Depth = 5f;

        /// <summary>Metres past the pit's rim the worker may stand while carrying company stone.</summary>
        public const float Margin = 5f;

        /// <summary>What the company pays for stone of this price.</summary>
        public static float Wage(float price)
        {
            return Mathf.Max(0f, price) * WageShare;
        }

        /// <summary>The quarry's centre (world metres, x and z): out of the gate and to the right of the road.</summary>
        public static Vector2 QuarryCentre(Vector2 gate, Vector2 outward)
        {
            outward = outward.sqrMagnitude > 1e-6f ? outward.normalized : Vector2.down;
            var side = new Vector2(outward.y, -outward.x);
            return gate + outward * OutFromGate + side * BesideRoad;
        }

        /// <summary>
        /// True when the worker is stealing: carrying company stone more than <see cref="Margin"/> past the pit's rim,
        /// or holding something that was never the company's to give (mined elsewhere, taken from a crate, a shop or
        /// the land).
        /// </summary>
        public static bool IsStealing(float distanceFromQuarry, float rimRadius, bool carriesCompanyGoods, bool holdsOtherGoods)
        {
            return holdsOtherGoods || (carriesCompanyGoods && distanceFromQuarry > rimRadius + Margin);
        }
    }
}
