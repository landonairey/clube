using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A town shopkeeper (GL404-GL406, first pass of NP1-NP4): stands behind the counter in shop hours, when E opens the
    /// trade screen with their shop's prices, offers and lessons (GL401, GL402); after closing walks out to the shop's
    /// chores; at night sleeps on the roll in the shop. What they do follows the hour of the <see cref="DayNightCycle"/>
    /// through the shop's <see cref="ShopHours"/>; walks go by the shop's door, never through its walls. Nothing is saved:
    /// where they are follows from the clock.
    /// </summary>
    public class Shopkeeper : MonoBehaviour, IClosableInteractable, ITrader
    {
        private const float WalkSpeed = 1.4f;
        private const float SweepSpeed = 0.45f;

        // Seconds at one chore spot before moving to the other (the blacksmith and the prospector).
        private const float ChoreSwitchSeconds = 30f;

        // Game hours the clock may move in one frame before the keeper skips to where the hour puts them.
        private const float ClockJumpHours = 0.25f;

        private enum Place
        {
            Counter,
            Bed,
            Chore,
        }

        private ShopSpecialty shop;
        private PriceList prices;
        private List<ItemPrice> offers;
        private ShopHours hours;
        private DayNightCycle clock;
        private Transform town;
        private KeeperFigure figure;
        private TownLayout.ShopPlot plot;
        private TownLayout.ChoreSpot[] chores;
        private readonly Queue<Vector2> route = new Queue<Vector2>();
        private Place place;
        private Place heading;
        private int choreIndex;
        private float choreTime;
        private KeeperActivity activity;
        private bool placed;

        // The merchant's walk between the two ends of the sweep: slow, broom going.
        private bool sweeping;
        private float lastHour;

        /// <summary>The shop this keeper keeps.</summary>
        public ShopSpecialty Shop => shop;

        /// <summary>What the keeper is meant to be doing now, by the hour.</summary>
        public KeeperActivity Activity => activity;

        /// <summary>True once the keeper stands behind the counter in shop hours.</summary>
        public bool AtCounter => placed && place == Place.Counter && route.Count == 0 && activity == KeeperActivity.Counter;

        /// <summary>Sets the keeper up for a shop in the town; called once, before the first frame.</summary>
        public void Setup(ShopSpecialty shop, PriceList master, ShopHours hours, DayNightCycle clock, Transform town, Material material, int seed)
        {
            this.shop = shop;
            this.hours = hours;
            this.clock = clock;
            this.town = town;
            prices = shop.BuyingPrices(master);
            offers = shop.Offers(master);
            plot = TownLayout.PlotOf(shop.Kind);
            chores = TownLayout.ChoresOf(shop.Kind);
            figure = gameObject.AddComponent<KeeperFigure>();
            figure.Build(shop.Kind, material, seed);
        }

        private float Hour => clock != null ? clock.Hour : 12f;

        // ITrader.

        public string DisplayName => shop != null ? shop.ShopName : "Shop";

        public PriceList Prices => prices;

        public IReadOnlyList<ItemPrice> Offers => offers;

        public IReadOnlyList<SkillType> Trains => shop.Trains;

        public float CoinsPerExperience => shop.CoinsPerExperience;

        public Vector3 Position => transform.position;

        public bool IsTrading => AtCounter;

        // IClosableInteractable.

        public bool IsAvailable => AtCounter;

        public string Prompt
        {
            get
            {
                string who = shop.KeeperName;
                if (AtCounter)
                {
                    return $"Talk to {who}";
                }
                string opens = ShopHours.Format(hours.Open);
                string name = char.ToUpperInvariant(who[0]) + who.Substring(1);
                if (activity == KeeperActivity.Sleep && place == Place.Bed && route.Count == 0)
                {
                    return $"{name} is asleep. The shop opens at {opens}";
                }
                return activity == KeeperActivity.Counter ? $"{name} is on the way to the counter" : $"{name} is busy. The shop opens at {opens}";
            }
        }

        public void Interact(PlayerInteractor by)
        {
            MerchantPanel panel = by.GetComponent<MerchantPanel>();
            if (panel != null && AtCounter)
            {
                panel.Open(this);
            }
        }

        private void Update()
        {
            if (shop == null)
            {
                return;
            }
            float hour = Hour;
            activity = hours.At(hour);
            Place wanted = activity == KeeperActivity.Counter ? Place.Counter : activity == KeeperActivity.Sleep ? Place.Bed : Place.Chore;
            // A jump in the clock (resting on a sleeping roll, GL290) skips the walk.
            bool jumped = placed && Mathf.Abs(Mathf.DeltaAngle(lastHour * 15f, hour * 15f)) / 15f > ClockJumpHours;
            lastHour = hour;
            if (!placed || jumped)
            {
                // At first, or after the jump, the keeper is simply where the hour puts them.
                placed = true;
                route.Clear();
                sweeping = false;
                figure.LieAlong(false, Vector3.zero, Vector3.zero);
                place = heading = wanted;
                Arrive();
            }
            else if (wanted != heading)
            {
                PlanRoute(place, wanted);
                heading = wanted;
            }

            if (route.Count > 0)
            {
                Walk(sweeping ? SweepSpeed : WalkSpeed);
                return;
            }
            if (place == Place.Chore)
            {
                DoChores();
            }
        }

        // The way from one place to another through the shop's door: inside places by the inside door, the chores
        // outside by the outside one.
        private void PlanRoute(Place from, Place to)
        {
            route.Clear();
            sweeping = false;
            figure.LieAlong(false, Vector3.zero, Vector3.zero);
            bool fromInside = from != Place.Chore;
            bool toInside = to != Place.Chore;
            if (fromInside)
            {
                route.Enqueue(plot.ToTown(TownLayout.InsideDoor));
            }
            if (fromInside != toInside)
            {
                route.Enqueue(plot.ToTown(TownLayout.OutsideDoor));
                if (!fromInside)
                {
                    route.Enqueue(plot.ToTown(TownLayout.InsideDoor));
                }
            }
            route.Enqueue(SpotOf(to));
            place = to;
        }

        private Vector2 SpotOf(Place where)
        {
            switch (where)
            {
                case Place.Counter:
                    return plot.ToTown(TownLayout.CounterSpot);
                case Place.Bed:
                    return plot.ToTown(TownLayout.BedSide);
                default:
                    return chores[choreIndex % chores.Length].At;
            }
        }

        // Walks the route at a speed, facing the way; arriving at its end settles into the place's pose.
        private void Walk(float speed)
        {
            Vector2 target = route.Peek();
            Vector3 goal = TownPoint(target);
            Vector3 to = goal - transform.position;
            to.y = 0f;
            float step = speed * Time.deltaTime;
            figure.Pose = sweeping ? KeeperPose.Sweep : KeeperPose.Walk;
            if (to.magnitude <= step)
            {
                transform.position = goal;
                route.Dequeue();
                if (route.Count == 0)
                {
                    Arrive();
                }
                return;
            }
            transform.position += to.normalized * step;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to, town.up), Time.deltaTime * 8f);
        }

        // Settles into the place: behind the counter facing out, lying on the roll, or at the chore.
        private void Arrive()
        {
            switch (place)
            {
                case Place.Counter:
                    transform.position = TownPoint(plot.ToTown(TownLayout.CounterSpot));
                    transform.rotation = town.rotation * Quaternion.Euler(0f, plot.Yaw, 0f);
                    figure.Pose = KeeperPose.Stand;
                    break;
                case Place.Bed:
                {
                    transform.position = TownPoint(plot.ToTown(TownLayout.BedSide));
                    Vector3 centre = TownPoint(plot.ToTown(TownLayout.BedCentre));
                    Vector3 along = town.rotation * (Quaternion.Euler(0f, plot.Yaw, 0f) * Vector3.right);
                    figure.LieAlong(true, centre, along);
                    figure.Pose = KeeperPose.Lie;
                    break;
                }
                default:
                    TownLayout.ChoreSpot spot = chores[choreIndex % chores.Length];
                    transform.position = TownPoint(spot.At);
                    transform.rotation = town.rotation * Quaternion.Euler(0f, spot.Yaw, 0f);
                    figure.Pose = ChorePose(choreIndex % chores.Length);
                    choreTime = 0f;
                    break;
            }
        }

        // The chores: the merchant sweeps back and forth; the blacksmith and prospector move between their two spots now and
        // then; the others stay put.
        private void DoChores()
        {
            choreTime += Time.deltaTime;
            if (shop.Kind == ShopKind.Wares)
            {
                choreIndex = (choreIndex + 1) % chores.Length;
                route.Enqueue(chores[choreIndex].At);
                // Sweeping goes slowly, broom in hand, along the line.
                sweeping = true;
                figure.Pose = KeeperPose.Sweep;
                return;
            }
            if (chores.Length > 1 && choreTime > ChoreSwitchSeconds)
            {
                choreIndex = (choreIndex + 1) % chores.Length;
                route.Enqueue(chores[choreIndex].At);
            }
        }

        private KeeperPose ChorePose(int index)
        {
            switch (shop.Kind)
            {
                case ShopKind.Blacksmith:
                    return index == 0 ? KeeperPose.Hammer : KeeperPose.Stoke;
                case ShopKind.Lumber:
                    return KeeperPose.Whittle;
                case ShopKind.Mining:
                    return KeeperPose.Drink;
                case ShopKind.Prospector:
                    return index == 0 ? KeeperPose.Examine : KeeperPose.Read;
                default:
                    return KeeperPose.Sweep;
            }
        }

        // A point of the town's ground plan in the world.
        private Vector3 TownPoint(Vector2 local)
        {
            return town.TransformPoint(new Vector3(local.x, 0f, local.y));
        }
    }
}
