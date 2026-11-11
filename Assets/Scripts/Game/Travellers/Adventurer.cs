using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The lone adventurer by their campfire (GL476): buys everything on the price list and sells all of it too, dearer
    /// than the town (<see cref="AdventurerVisit.SellMarkup"/>), out of one purse that isn't refilled each day. They
    /// also teach climbing for <see cref="AdventurerVisit.ClimbingPrice"/> coins, once (GL585, <see cref="PlayerUpgrades"/>). Their
    /// disposition sets their prices like a shopkeeper's (GL471). Built by <see cref="AdventurerCamp"/>, which saves them.
    /// </summary>
    public class Adventurer : MonoBehaviour, IClosableInteractable, ITrader
    {
        private PriceList fairPrices;
        private PriceList prices;
        private readonly List<ItemPrice> fairOffers = new List<ItemPrice>();
        private readonly List<ItemPrice> offers = new List<ItemPrice>();

        /// <summary>Gives them their goods, purse and disposition, and dresses them.</summary>
        public void Setup(PriceList master, Wallet purse, Disposition disposition, Material material, int seed)
        {
            fairPrices = master != null ? master : PriceList.Create();
            fairOffers.Clear();
            foreach (ItemPrice price in fairPrices.Prices)
            {
                if (price.Item != null)
                {
                    int asked = Mathf.Max(1, Mathf.CeilToInt(price.Coins * AdventurerVisit.SellMarkup - 1e-4f));
                    fairOffers.Add(new ItemPrice(price.Item, asked, price.Lot));
                }
            }
            Purse = purse;
            Disposition = disposition;
            Disposition.Changed += Reprice;
            Reprice();
            gameObject.AddComponent<KeeperFigure>().BuildAdventurer(material, seed);
        }

        private void Reprice()
        {
            if (prices != null && prices != fairPrices)
            {
                Destroy(prices);
            }
            prices = fairPrices.Scaled(Disposition.PayShare);
            offers.Clear();
            foreach (ItemPrice offer in fairOffers)
            {
                offers.Add(new ItemPrice(offer.Item, Disposition.SellingPrice(offer.Coins), offer.Units, offer.ByVolume));
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

        public string DisplayName => "Lone adventurer";

        public PriceList Prices => prices;

        public IReadOnlyList<ItemPrice> Offers => offers;

        public IReadOnlyList<SkillType> Trains => System.Array.Empty<SkillType>();

        public float CoinsPerExperience => 0f;

        public Vector3 Position => transform.position;

        public Wallet Purse { get; private set; }

        public Disposition Disposition { get; private set; }

        /// <summary>The climbing upgrade (GL585), set up when the player talks to them; null before that.</summary>
        public ITraderDeal Deal { get; private set; }

        public bool IsTrading => isActiveAndEnabled;

        // IClosableInteractable.

        public bool IsAvailable => isActiveAndEnabled;

        public string Prompt => "Talk to the lone adventurer";

        public void Interact(PlayerInteractor by)
        {
            PlayerUpgrades upgrades = by.GetComponent<PlayerUpgrades>();
            Deal = upgrades != null ? upgrades.ClimbingDeal(Purse) : null;
            MerchantPanel panel = by.GetComponent<MerchantPanel>();
            if (panel != null)
            {
                panel.Open(this);
            }
        }
    }
}
