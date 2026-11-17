using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// GL729: "when you take from a cart or boat, the shops that the cart or boat are going to will reduce their
    /// disposition with you." Goods taken from a cart's or vessel's crate (<see cref="CargoCrate"/>) wrong the shop of
    /// the settlement they are bound for that takes them (<see cref="TradeStop.ShopTaking"/>); goods bound off the map
    /// or to a road's end wrong the merchant of the settlement they left. The keeper's disposition falls by
    /// <see cref="Theft.PointsFor"/> their worth, and the player is told.
    /// </summary>
    public static class CargoTheft
    {
        /// <summary>Lowers the wronged keeper's disposition for <paramref name="taken"/>; returns the points it fell.</summary>
        /// <param name="boundFor">The settlement the goods are bound for (<see cref="TradeStop.GoodsBoundFor"/>), -1 for none on the map.</param>
        /// <param name="cameFrom">The settlement they come from, -1 for none on the map.</param>
        public static int Report(MapSettlements settlements, WorldPlan plan, int boundFor, int cameFrom,
            IReadOnlyList<(ItemDefinition Item, float Amount)> taken)
        {
            if (settlements == null || plan == null || taken == null || taken.Count == 0)
            {
                return 0;
            }
            bool bound = boundFor >= 0 && boundFor < plan.Settlements.Count;
            int wronged = bound ? boundFor : cameFrom;
            if (wronged < 0 || wronged >= plan.Settlements.Count)
            {
                return 0;
            }
            PlannedSettlement here = plan.Settlements[wronged];
            PlannedSettlement source = bound && cameFrom >= 0 && cameFrom < plan.Settlements.Count ? plan.Settlements[cameFrom] : null;
            int shop = TradeStop.ShopTaking(source, here);
            if (shop < 0)
            {
                return 0;
            }
            float coins = 0f;
            foreach ((ItemDefinition item, float amount) in taken)
            {
                coins += Theft.Worth(settlements.Prices, item, amount);
            }
            // Anything taken is theft, even goods with no listed price.
            int fell = settlements.Dispositions.Of(wronged, shop).Lower(Theft.PointsFor(Mathf.Max(coins, 0.01f)));
            ToolHint hint = Object.FindAnyObjectByType<ToolHint>();
            if (hint != null)
            {
                string keeper = here.Shops[shop].Specialty.Name;
                hint.Show(fell > 0 ? $"Stolen goods: the {keeper} of {here.Name} will hear of it (disposition -{fell})"
                    : $"Stolen goods: the {keeper} of {here.Name} already thinks the worst of you");
            }
            return fell;
        }
    }
}
