using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// What a keeper trades with (GL401, GL426, GL471): their shop's prices and offers off the one merchant price list,
    /// moved by their disposition towards the player, and the purse of coins they have to buy with each day. The town's
    /// <see cref="Shopkeeper"/> and a planned settlement's <see cref="SettlementKeeper"/> both hold one, so every shop
    /// trades by the same rules.
    /// </summary>
    public sealed class ShopTrade
    {
        private readonly PriceList fairPrices;
        private readonly List<ItemPrice> fairOffers;
        private readonly float dailyCoins;
        private List<ItemPrice> offers;
        private int purseDay;

        public ShopTrade(ShopSpecialty shop, PriceList master, float dailyCoins, int day, Disposition disposition = null)
        {
            Shop = shop;
            this.dailyCoins = dailyCoins;
            Purse = new Wallet(dailyCoins);
            purseDay = day;
            fairPrices = shop.BuyingPrices(master);
            fairOffers = shop.Offers(master);
            Disposition = disposition ?? new Disposition();
            Disposition.Changed += Reprice;
            Reprice();
        }

        public ShopSpecialty Shop { get; }

        /// <summary>What the keeper pays, for their disposition.</summary>
        public PriceList Prices { get; private set; }

        /// <summary>What the keeper sells and for how much, for their disposition.</summary>
        public IReadOnlyList<ItemPrice> Offers => offers;

        /// <summary>The coins the keeper has to buy with today (GL426): back to the day's amount each morning.</summary>
        public Wallet Purse { get; private set; }

        /// <summary>How the keeper feels about the player (GL471).</summary>
        public Disposition Disposition { get; }

        /// <summary>Tells the trade which day it is: on a new one the purse is full again.</summary>
        public void OnDay(int day)
        {
            if (day != purseDay)
            {
                purseDay = day;
                Purse = new Wallet(dailyCoins);
            }
        }

        /// <summary>Lets go of the disposition and the price list made for it; call when the keeper is destroyed.</summary>
        public void Release()
        {
            Disposition.Changed -= Reprice;
        }

        // The prices for the disposition (GL471): a point of markup on what they sell and a point off what they pay for each
        // point under 100.
        private void Reprice()
        {
            if (Prices != null && Prices != fairPrices)
            {
                Object.Destroy(Prices);
            }
            Prices = fairPrices.Scaled(Disposition.PayShare);
            offers = new List<ItemPrice>(fairOffers.Count);
            foreach (ItemPrice offer in fairOffers)
            {
                offers.Add(new ItemPrice(offer.Item, Disposition.SellingPrice(offer.Coins), offer.Units, offer.ByVolume));
            }
        }
    }
}
