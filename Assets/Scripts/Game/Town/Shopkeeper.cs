using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A town shopkeeper (GL404-GL406, GL415; first pass of NP1-NP4): stands behind the counter in shop hours, when E opens
    /// the trade screen with their shop's prices, offers and lessons (GL401, GL402); after closing walks out to the shop's
    /// chores (the miner down a mine shaft to mine its node, a different shaft each day); at night sleeps on the roll in the
    /// shop. What they do follows the hour of the <see cref="DayNightCycle"/> through the shop's <see cref="ShopHours"/>;
    /// walks go by the shop's door and the town's gate, over the ground as it is (shops stand at their own heights, GL413)
    /// and down the shafts' stairs. Nothing is saved: where they are follows from the clock.
    /// </summary>
    public class Shopkeeper : MonoBehaviour, IClosableInteractable, ITrader
    {
        private const float WalkSpeed = 1.4f;
        private const float SweepSpeed = 0.45f;

        // Seconds at one chore spot before moving to the other (the blacksmith and the prospector).
        private const float ChoreSwitchSeconds = 30f;

        // Game hours the clock may move in one frame before the keeper skips to where the hour puts them.
        private const float ClockJumpHours = 0.25f;

        // Real seconds between samples the miner takes from a node (GL415): slow on purpose.
        private const float MineSeconds = 6f;

        // Samples dug out of an open pit with each swing (GL474): a quarter of a cubic metre at 0.25 m voxels.
        private const int PitSamplesPerSwing = 16;

        private enum Place
        {
            Counter,
            Bed,
            Chore,
            Shaft,
        }

        /// <summary>A point of a walk, in the world's local metres; on the ground ones follow the terrain, exact ones are stairs.</summary>
        private readonly struct Waypoint
        {
            public Waypoint(Vector3 at, bool exact)
            {
                At = at;
                Exact = exact;
            }

            public Vector3 At { get; }

            public bool Exact { get; }
        }

        private ShopSpecialty shop;
        private PriceList prices;
        private List<ItemPrice> offers;
        private PriceList fairPrices;
        private List<ItemPrice> fairOffers;
        private ShopHours hours;
        private DayNightCycle clock;
        private TownContext town;
        private FigureRig figure;
        private TownLayout.ShopPlot plot;
        private TownLayout.ChoreSpot[] chores;
        private readonly Queue<Waypoint> route = new Queue<Waypoint>();
        private Vector3 local;
        private Place place;
        private Place heading;
        private int choreIndex;
        private float choreTime;
        private float mineTime;
        private TownShaft shaft;
        private KeeperActivity activity;
        private bool placed;
        private bool sweeping;
        private float lastHour;
        private float dailyCoins;
        private int purseDay;

        /// <summary>The shop this keeper keeps.</summary>
        public ShopSpecialty Shop => shop;

        /// <summary>What the keeper is meant to be doing now, by the hour.</summary>
        public KeeperActivity Activity => activity;

        /// <summary>True once the keeper stands behind the counter in shop hours.</summary>
        public bool AtCounter => placed && place == Place.Counter && route.Count == 0 && activity == KeeperActivity.Counter;

        /// <summary>True while the keeper is down a shaft at its node (the miner, GL415).</summary>
        public bool IsMining => placed && place == Place.Shaft && route.Count == 0;

        /// <summary>Samples the keeper has mined (the miner, GL415).</summary>
        public int Mined { get; private set; }

        /// <summary>Sets the keeper up for a shop in the town; called once, before the first frame.</summary>
        public void Setup(ShopSpecialty shop, PriceList master, ShopHours hours, DayNightCycle clock, TownContext town, Material material, int seed,
            float dailyCoins = 525f, Disposition disposition = null)
        {
            this.shop = shop;
            this.hours = hours;
            this.clock = clock;
            this.town = town;
            this.dailyCoins = dailyCoins;
            Purse = new Wallet(dailyCoins);
            purseDay = clock != null ? clock.Day : 0;
            fairPrices = shop.BuyingPrices(master);
            fairOffers = shop.Offers(master);
            Disposition = disposition ?? new Disposition();
            Disposition.Changed += Reprice;
            Reprice();
            plot = TownLayout.PlotOf(shop.Kind);
            chores = TownLayout.ChoresOf(shop.Kind);
            figure = gameObject.AddComponent<FigureRig>();
            figure.Build(shop.Kind, material, seed);
        }

        // The prices for the disposition (GL471): a point of markup on what they sell and a point off what they pay for each
        // point under 100.
        private void Reprice()
        {
            if (prices != null && prices != fairPrices)
            {
                Destroy(prices);
            }
            prices = fairPrices.Scaled(Disposition.PayShare);
            offers = new List<ItemPrice>(fairOffers.Count);
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
        }

        private float Hour => clock != null ? clock.Hour : 12f;

        private TownPlan Plan => town.Plan;

        // The miner's chore is the mine when there's a shaft (GL415).
        private bool MinesAfterWork => shop.Kind == ShopKind.Mining && Plan.Shafts.Count > 0;

        // ITrader.

        public string DisplayName => shop != null ? shop.ShopName : "Shop";

        public PriceList Prices => prices;

        public IReadOnlyList<ItemPrice> Offers => offers;

        public IReadOnlyList<SkillType> Trains => shop.Trains;

        public float CoinsPerExperience => shop.CoinsPerExperience;

        public Vector3 Position => transform.position;

        public bool IsTrading => AtCounter;

        /// <summary>The coins the keeper has to buy with today (GL426): back to the day's amount each morning.</summary>
        public Wallet Purse { get; private set; }

        /// <summary>How the keeper feels about the player (GL471), saved by the <see cref="TradingPost"/>.</summary>
        public Disposition Disposition { get; private set; }

        /// <summary>Something special the keeper offers (GL473: the merchant sells the trading post); null for none.</summary>
        public ITraderDeal Deal { get; set; }

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
                if (IsMining)
                {
                    return $"{name} is mining. The shop opens at {opens}";
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
            // A new day: the purse is full again (GL426).
            int day = clock != null ? clock.Day : 0;
            if (day != purseDay)
            {
                purseDay = day;
                Purse = new Wallet(dailyCoins);
            }
            activity = hours.At(hour);
            Place wanted = activity == KeeperActivity.Counter ? Place.Counter
                : activity == KeeperActivity.Sleep ? Place.Bed
                : MinesAfterWork ? Place.Shaft : Place.Chore;
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
                PickShaft();
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
            else if (place == Place.Shaft)
            {
                Mine();
            }
        }

        // The miner works a different shaft each day.
        private void PickShaft()
        {
            int count = Plan.Shafts.Count;
            shaft = count > 0 ? Plan.Shafts[Mathf.Abs(clock != null ? clock.Day : 0) % count] : null;
        }

        // A point of the town's frame on the ground, at the height it was planned at (the walk finds the real ground).
        private Waypoint OnGround(Vector2 townPoint, float height)
        {
            Vector2 world = Plan.ToWorld(townPoint);
            return new Waypoint(new Vector3(world.x, height, world.y), false);
        }

        private Waypoint InShop(Vector2 shopPoint)
        {
            return OnGround(plot.ToTown(shopPoint), Plan.FloorHeight(plot.Kind));
        }

        // Just inside and just outside the gate, for shafts outside the wall.
        private IEnumerable<Waypoint> ThroughGate(bool outward)
        {
            float middle = (TownLayout.InnerHalf + TownLayout.OuterHalf) * 0.5f;
            var inside = new Vector2(0f, -middle + 2f);
            var outside = new Vector2(0f, -middle - 2f);
            yield return OnGround(outward ? inside : outside, Plan.GateGround);
            yield return OnGround(outward ? outside : inside, Plan.GateGround);
        }

        // The way out of a place to the square.
        private IEnumerable<Waypoint> Leave(Place from)
        {
            switch (from)
            {
                case Place.Counter:
                case Place.Bed:
                    yield return InShop(TownLayout.InsideDoor);
                    yield return InShop(TownLayout.OutsideDoor);
                    break;
                case Place.Shaft when shaft != null:
                    // Out of an open pit (GL474) over the ground; up a shaft's stairs on its line.
                    for (int i = shaft.Path.Count - 1; i >= 0; i--)
                    {
                        yield return new Waypoint(shaft.Path[i], !shaft.IsPit);
                    }
                    if (!TownLayout.IsInside(shaft.Head))
                    {
                        foreach (Waypoint w in ThroughGate(false))
                        {
                            yield return w;
                        }
                    }
                    break;
            }
        }

        // The way from the square into a place.
        private IEnumerable<Waypoint> Enter(Place to)
        {
            switch (to)
            {
                case Place.Counter:
                    yield return InShop(TownLayout.OutsideDoor);
                    yield return InShop(TownLayout.InsideDoor);
                    yield return InShop(TownLayout.CounterSpot);
                    break;
                case Place.Bed:
                    yield return InShop(TownLayout.OutsideDoor);
                    yield return InShop(TownLayout.InsideDoor);
                    yield return InShop(TownLayout.BedSide);
                    break;
                case Place.Shaft when shaft != null:
                    if (!TownLayout.IsInside(shaft.Head))
                    {
                        foreach (Waypoint w in ThroughGate(true))
                        {
                            yield return w;
                        }
                    }
                    for (int i = 0; i < shaft.Path.Count; i++)
                    {
                        yield return new Waypoint(shaft.Path[i], i > 0);
                    }
                    break;
                default:
                    TownLayout.ChoreSpot spot = chores[choreIndex % chores.Length];
                    yield return OnGround(spot.At, Plan.GroundAt(spot.At));
                    break;
            }
        }

        private void PlanRoute(Place from, Place to)
        {
            route.Clear();
            sweeping = false;
            figure.LieAlong(false, Vector3.zero, Vector3.zero);
            bool fromInside = from == Place.Counter || from == Place.Bed;
            bool toInside = to == Place.Counter || to == Place.Bed;
            if (fromInside && toInside)
            {
                // Counter to bed and back: straight across the shop.
                route.Enqueue(InShop(to == Place.Counter ? TownLayout.CounterSpot : TownLayout.BedSide));
            }
            else
            {
                foreach (Waypoint w in Leave(from))
                {
                    route.Enqueue(w);
                }
                if (to == Place.Shaft)
                {
                    PickShaft();
                }
                foreach (Waypoint w in Enter(to))
                {
                    route.Enqueue(w);
                }
            }
            place = to;
        }

        // Walks the route at a speed, facing the way; on the ground it follows the terrain, on stairs the exact line.
        private void Walk(float speed)
        {
            Waypoint target = route.Peek();
            Vector3 to = target.At - local;
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            float step = speed * Time.deltaTime;
            figure.Pose = sweeping ? FigureAction.Sweep : FigureAction.Walk;
            float remaining = target.Exact ? to.magnitude : flat.magnitude;
            if (remaining <= step)
            {
                local = target.Exact ? target.At : new Vector3(target.At.x, town.GroundAt(target.At.x, target.At.z, local.y), target.At.z);
                route.Dequeue();
                MoveFigure();
                if (route.Count == 0)
                {
                    Arrive();
                }
                return;
            }
            if (target.Exact)
            {
                local += to / remaining * step;
            }
            else
            {
                local += flat / remaining * step;
                local.y = town.GroundAt(local.x, local.z, local.y);
            }
            MoveFigure();
            if (flat.sqrMagnitude > 1e-6f)
            {
                Quaternion facing = town.SceneRotation(Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg);
                transform.rotation = Quaternion.Slerp(transform.rotation, facing, Time.deltaTime * 8f);
            }
        }

        private void MoveFigure()
        {
            transform.position = town.ToScene(local);
        }

        private void Face(float townYaw)
        {
            transform.rotation = town.SceneRotation(Plan.Yaw + townYaw);
        }

        // Settles into the place: behind the counter facing out, lying on the roll, at the chore, or at the node's face.
        private void Arrive()
        {
            switch (place)
            {
                case Place.Counter:
                    local = InShop(TownLayout.CounterSpot).At;
                    MoveFigure();
                    Face(plot.Yaw);
                    figure.Pose = FigureAction.Stand;
                    break;
                case Place.Bed:
                {
                    local = InShop(TownLayout.BedSide).At;
                    MoveFigure();
                    Vector3 centre = town.ToScene(InShop(TownLayout.BedCentre).At);
                    Vector3 along = town.SceneRotation(Plan.Yaw + plot.Yaw) * Vector3.right;
                    figure.LieAlong(true, centre, along);
                    figure.Pose = FigureAction.Lie;
                    break;
                }
                case Place.Shaft when shaft != null && shaft.IsPit:
                {
                    // In the pit's middle, on its floor as dug so far (GL474), facing the town.
                    StandInPit();
                    Vector2 towards = Plan.Centre - shaft.PitCentre;
                    transform.rotation = town.SceneRotation(Mathf.Atan2(towards.x, towards.y) * Mathf.Rad2Deg);
                    figure.Pose = FigureAction.Mine;
                    mineTime = 0f;
                    break;
                }
                case Place.Shaft when shaft != null:
                {
                    local = shaft.Path[shaft.Path.Count - 1];
                    MoveFigure();
                    Vector3 toNode = shaft.Node - local;
                    transform.rotation = town.SceneRotation(Mathf.Atan2(toNode.x, toNode.z) * Mathf.Rad2Deg);
                    figure.Pose = FigureAction.Mine;
                    mineTime = 0f;
                    break;
                }
                default:
                {
                    TownLayout.ChoreSpot spot = chores[choreIndex % chores.Length];
                    Vector2 world = Plan.ToWorld(spot.At);
                    float near = Plan.GroundAt(spot.At);
                    local = new Vector3(world.x, town.GroundAt(world.x, world.y, near), world.y);
                    MoveFigure();
                    Face(spot.Yaw);
                    figure.Pose = ChorePose(choreIndex % chores.Length);
                    choreTime = 0f;
                    break;
                }
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
                TownLayout.ChoreSpot spot = chores[choreIndex];
                route.Enqueue(OnGround(spot.At, Plan.GroundAt(spot.At)));
                // Sweeping goes slowly, broom in hand, along the line.
                sweeping = true;
                figure.Pose = FigureAction.Sweep;
                return;
            }
            if (chores.Length > 1 && choreTime > ChoreSwitchSeconds)
            {
                choreIndex = (choreIndex + 1) % chores.Length;
                TownLayout.ChoreSpot spot = chores[choreIndex];
                route.Enqueue(OnGround(spot.At, Plan.GroundAt(spot.At)));
            }
        }

        // At the node's face (GL415): a swing of the pick now and then takes one sample of its ore.
        private void Mine()
        {
            if (shaft == null)
            {
                return;
            }
            mineTime += Time.deltaTime;
            if (mineTime < MineSeconds)
            {
                return;
            }
            mineTime = 0f;
            if (shaft.IsPit)
            {
                // A swing in the open pit (GL474) digs a spadeful, ore or not, and the miner stands on the new floor.
                Mined += town.DigPit(shaft, PitSamplesPerSwing);
                StandInPit();
                return;
            }
            Vector3 face = local + Vector3.up * 1.1f;
            Vector3 toNode = shaft.Node - local;
            toNode.y = 0f;
            if (toNode.sqrMagnitude > 1e-4f)
            {
                face += toNode.normalized * 0.9f;
            }
            if (town.MineAt(face, shaft.Group))
            {
                Mined++;
            }
        }

        private void StandInPit()
        {
            Vector2 centre = shaft.PitCentre;
            float near = placed && route.Count == 0 && place == Place.Shaft ? local.y : shaft.Path[0].y;
            local = new Vector3(centre.x, Mathf.Max(shaft.PitFloor, town.GroundAt(centre.x, centre.y, near)), centre.y);
            MoveFigure();
        }

        private FigureAction ChorePose(int index)
        {
            switch (shop.Kind)
            {
                case ShopKind.Blacksmith:
                    return index == 0 ? FigureAction.Hammer : FigureAction.Stoke;
                case ShopKind.Lumber:
                    return FigureAction.Whittle;
                case ShopKind.Mining:
                    return FigureAction.Sit;
                case ShopKind.Prospector:
                    return index == 0 ? FigureAction.Examine : FigureAction.Read;
                default:
                    return FigureAction.Sweep;
            }
        }
    }
}
