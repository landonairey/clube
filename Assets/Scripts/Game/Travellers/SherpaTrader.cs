using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Trading with a travelling sherpa (GL868) in the <see cref="MerchantPanel"/>: they sell what is in their pack at
    /// <see cref="SherpaTrade.SellMarkup"/> times the list (no more than they carry, <see cref="IStockedTrader"/>), buy
    /// anything listed out of a small purse, and their disposition sets both like a shopkeeper's. They stand still while
    /// the screen is open. Once a noticed theft has dropped their disposition below <see cref="SherpaTrade.RefuseBelow"/>
    /// they will not trade.
    /// </summary>
    public class SherpaTrader : MonoBehaviour, IClosableInteractable, IStockedTrader
    {
        // Real seconds each look holds the sherpa while the screen is open.
        private const float HoldSeconds = 1f;

        private readonly List<ItemPrice> offers = new List<ItemPrice>();
        private PriceList fairPrices;
        private PriceList prices;
        private Inventory pack;
        private System.Action<float> hold;
        private MerchantPanel trading;
        private OffMapPresence presence;

        /// <summary>True while the player has the trade screen open with them.</summary>
        public bool IsHeldToTrade => trading != null && ReferenceEquals(trading.Current, this);

        /// <summary>Gives them the list their prices come from, their pack, their disposition and a way to stop and trade.</summary>
        public void Setup(PriceList master, Inventory pack, Disposition disposition, System.Action<float> hold)
        {
            fairPrices = master != null ? master : PriceList.Create();
            this.pack = pack;
            this.hold = hold;
            Purse = new Wallet(SherpaTrade.Purse);
            Disposition = disposition ?? new Disposition();
            Disposition.Changed += Reprice;
            Reprice();
        }

        private void Update()
        {
            if (IsHeldToTrade)
            {
                hold?.Invoke(HoldSeconds);
            }
            else
            {
                trading = null;
            }
        }

        private void Reprice()
        {
            if (prices != null && prices != fairPrices)
            {
                Destroy(prices);
            }
            prices = fairPrices.Scaled(Disposition.PayShare);
            Restock();
        }

        // One offer for each listed item in the pack.
        private void Restock()
        {
            offers.Clear();
            if (pack == null)
            {
                return;
            }
            for (int slot = 0; slot < pack.SlotCount; slot++)
            {
                ItemStack stack = pack[slot];
                if (stack.IsEmpty || offers.Exists(o => o.Item == stack.Item) || !fairPrices.TryGetLot(stack.Item, out int coins, out int lot))
                {
                    continue;
                }
                offers.Add(new ItemPrice(stack.Item, Disposition.SellingPrice(SherpaTrade.Asking(coins)), lot));
            }
        }

        private void OnDestroy()
        {
            if (Disposition != null)
            {
                Disposition.Changed -= Reprice;
            }
            if (prices != null && prices != fairPrices)
            {
                Destroy(prices);
            }
        }

        // ITrader.

        public string DisplayName => "Travelling sherpa";

        public PriceList Prices => prices;

        public IReadOnlyList<ItemPrice> Offers => offers;

        public IReadOnlyList<SkillType> Trains => System.Array.Empty<SkillType>();

        public float CoinsPerExperience => 0f;

        public Vector3 Position => transform.position;

        public Wallet Purse { get; private set; }

        public Disposition Disposition { get; private set; }

        public ITraderDeal Deal => null;

        public bool IsTrading => IsAvailable;

        // IStockedTrader.

        public float StockOf(ItemDefinition item)
        {
            return pack != null && item != null ? pack.AmountOf(item) : 0f;
        }

        public void SoldToPlayer(ItemDefinition item, float amount)
        {
            for (int slot = 0; pack != null && slot < pack.SlotCount && amount > ItemStack.PartEpsilon; slot++)
            {
                if (pack[slot].Item == item)
                {
                    amount -= pack.RemoveAmount(slot, amount);
                }
            }
        }

        // IClosableInteractable.

        public bool IsAvailable => isActiveAndEnabled && SherpaTrade.Trades(Disposition != null ? Disposition.Value : 0) && Present;

        public string Prompt => "Trade with the sherpa";

        public void Interact(PlayerInteractor by)
        {
            MerchantPanel panel = by.GetComponent<MerchantPanel>();
            if (panel == null || !IsAvailable)
            {
                return;
            }
            Restock();
            trading = panel;
            hold?.Invoke(HoldSeconds);
            panel.Open(this);
        }

        // Not half gone off the map's edge (GL725).
        private bool Present
        {
            get
            {
                if (presence == null)
                {
                    presence = GetComponent<OffMapPresence>();
                }
                return presence == null || presence.Interactable;
            }
        }
    }
}
