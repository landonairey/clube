using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The keeper of a shop in a planned settlement (GL613, GL614): behind the counter in shop hours, when E opens the
    /// trade screen with the shop's prices and offers (<see cref="ShopTrade"/>, as the trading post's keepers); by the
    /// door after closing; asleep on the roll at night. They keep to their shop: the work NPCs do out in the world is
    /// GL615's. Where they are follows from the clock, so nothing is saved.
    /// </summary>
    public class SettlementKeeper : MonoBehaviour, IClosableInteractable, ITrader, IParcelSeller
    {
        private const float WalkSpeed = 1.4f;

        private ShopTrade trade;
        private KeeperBed bed;
        private ShopHours hours;
        private DayNightCycle clock;
        private FigureRig figure;
        private Transform shop;
        private Vector2 at;
        private Vector2 target;
        private KeeperActivity activity;
        private bool placed;

        /// <summary>Sets the keeper up; called once, before the first frame.</summary>
        /// <param name="shopRoot">The shop's own frame: the middle of its floor, its front out of +z.</param>
        public void Setup(ShopSpecialty specialty, PriceList master, ShopHours hours, DayNightCycle clock, Transform shopRoot, Material material, int seed,
            float dailyCoins, Disposition disposition = null, SleepingRoll bedRoll = null)
        {
            this.hours = hours;
            this.clock = clock;
            shop = shopRoot;
            trade = new ShopTrade(specialty, master, dailyCoins, clock != null ? clock.Day : 0, disposition);
            bed = bedRoll != null ? new KeeperBed(bedRoll, trade.Disposition) : null;
            figure = gameObject.AddComponent<FigureRig>();
            figure.Build(specialty.Kind, material, seed);
        }

        /// <summary>The shop this keeper keeps.</summary>
        public ShopSpecialty Shop => trade.Shop;

        /// <summary>True once the keeper stands behind the counter in shop hours.</summary>
        public bool AtCounter => placed && activity == KeeperActivity.Counter && (at - TownLayout.CounterSpot).sqrMagnitude < 0.01f;

        // Asleep on the roll: E on them offers their bed (GL798), since they lie over it.
        private bool Asleep => placed && activity == KeeperActivity.Sleep && (bed == null || !bed.Woken) && (at - TownLayout.BedSide).sqrMagnitude < 1e-6f;

        private void OnDestroy()
        {
            trade?.Release();
            bed?.Release();
        }

        // ITrader.

        public string DisplayName => trade.Shop.ShopName;

        public PriceList Prices => trade.Prices;

        public IReadOnlyList<ItemPrice> Offers => trade.Offers;

        public IReadOnlyList<SkillType> Trains => trade.Shop.Trains;

        public float CoinsPerExperience => trade.Shop.CoinsPerExperience;

        public Vector3 Position => transform.position;

        public Wallet Purse => trade.Purse;

        public Disposition Disposition => trade.Disposition;

        public ITraderDeal Deal => null;

        /// <summary>GL863: the settlement's land, which a Merchant sells; null for every other keeper.</summary>
        public SettlementLand Land { get; set; }

        public bool IsTrading => AtCounter;

        // IClosableInteractable.

        public bool IsAvailable => AtCounter || (Asleep && bed != null);

        public string Prompt
        {
            get
            {
                string who = trade.Shop.KeeperName;
                if (AtCounter)
                {
                    return $"Talk to {who}";
                }
                if (Asleep && bed != null)
                {
                    return $"{bed.Roll.Label} (wakes {who})";
                }
                string name = char.ToUpperInvariant(who[0]) + who.Substring(1);
                string opens = ShopHours.Format(hours.Open);
                return activity == KeeperActivity.Counter ? $"{name} is on the way to the counter"
                    : activity == KeeperActivity.Sleep && (bed == null || !bed.Woken) ? $"{name} is asleep. The shop opens at {opens}"
                    : $"{name} has shut up shop. It opens at {opens}";
            }
        }

        public void Interact(PlayerInteractor by)
        {
            MerchantPanel panel = by.GetComponent<MerchantPanel>();
            if (panel != null && AtCounter)
            {
                panel.Open(this);
            }
            else if (Asleep && bed != null)
            {
                bed.Interact(by);
            }
        }

        private void Update()
        {
            if (trade == null || shop == null)
            {
                return;
            }
            trade.OnDay(clock != null ? clock.Day : 0);
            activity = hours.At(clock != null ? clock.Hour : 12f);
            bed?.Night(activity == KeeperActivity.Sleep);
            bool woken = bed != null && bed.Woken;
            target = activity == KeeperActivity.Counter ? TownLayout.CounterSpot
                : activity == KeeperActivity.Sleep && !woken ? TownLayout.BedSide
                : TownLayout.InsideDoor;
            if (!placed)
            {
                placed = true;
                at = target;
            }
            Vector2 before = at;
            at = Vector2.MoveTowards(at, target, WalkSpeed * Time.deltaTime);
            bool arrived = (at - target).sqrMagnitude < 1e-6f;
            bool asleep = arrived && activity == KeeperActivity.Sleep && !woken;
            transform.position = shop.TransformPoint(new Vector3(at.x, 0f, at.y));
            Vector2 way = arrived ? new Vector2(0f, 1f) : at - before;
            transform.rotation = shop.rotation * Quaternion.Euler(0f, Mathf.Atan2(way.x, way.y) * Mathf.Rad2Deg, 0f);
            figure.LieAlong(asleep, shop.TransformPoint(new Vector3(TownLayout.BedCentre.x, 0f, TownLayout.BedCentre.y)), shop.right);
            figure.Pose = asleep ? FigureAction.Lie : arrived ? FigureAction.Stand : FigureAction.Walk;
        }
    }
}
