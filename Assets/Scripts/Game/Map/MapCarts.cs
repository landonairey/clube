using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The carts on the map's cart paths (GL616), "carrying goods between settlements": <see cref="CartRuns"/> gives
    /// every cart path its carts, hand-pulled and horse-drawn, and where each is follows from the clock. Only the carts
    /// near the viewer are in the world (<see cref="RoadCart"/>), on roads <see cref="MapRoads"/> has worked out; one
    /// that comes into view is where its timetable has it, loaded with what the settlement it left sells.
    /// </summary>
    public class MapCarts : MonoBehaviour
    {
        // Frames between looks at a cart out of sight (GL770).
        private const int HiddenLookFrames = 8;

        [SerializeField]
        private MapSession session;

        [SerializeField]
        private MapRoads roads;

        [Tooltip("The low-poly vertex-colour material carts, carters and horses are made of.")]
        [SerializeField]
        private Material material;

        [Tooltip("The game's items: a cart's load is what the settlement it left sells.")]
        [SerializeField]
        private ItemCatalog catalog;

        [Tooltip("The wooden crate (a storage chest) the goods ride in.")]
        [SerializeField]
        private GameObject cratePrefab;

        [Tooltip("A cart is in the world while it is within this many metres of the viewer.")]
        [SerializeField, Range(40f, 400f)]
        private float showRange = 170f;

        private readonly List<CartKind> kinds = CartKind.Defaults();
        // What each settlement's shops sell, worked out once (GL778: it was rebuilt, with its enumerators, every frame for every cart at a stop).
        private readonly Dictionary<int, List<ItemDefinition>> goodsBySettlement = new Dictionary<int, List<ItemDefinition>>();
        private readonly Dictionary<int, OffMapPath> offMapPaths = new Dictionary<int, OffMapPath>();
        private readonly Dictionary<int, RoadLine> offMapLines = new Dictionary<int, RoadLine>();
        private readonly Dictionary<long, TownWay> townWays = new Dictionary<long, TownWay>();
        private readonly Dictionary<int, SettlementWalks> walks = new Dictionary<int, SettlementWalks>();
        private MapSettlements settlements;
        private List<CartRun> runs = new List<CartRun>();
        private RoadTraveller[] carts = System.Array.Empty<RoadTraveller>();
        private Disposition[] moods = System.Array.Empty<Disposition>();
        private double[] delays = System.Array.Empty<double>(), heldUntil = System.Array.Empty<double>();
        private double lastSeconds;
        private DayNightCycle clock;
        private Transform root;
        private int version = -1;

        /// <summary>The kinds of cart; change a row to change every cart.</summary>
        public List<CartKind> Kinds => kinds;

        /// <summary>Every cart of the map, in the world or not.</summary>
        public IReadOnlyList<CartRun> Runs => runs;

        /// <summary>Carts in the world now.</summary>
        public int Showing { get; private set; }

        /// <summary>The cart of a run, when it is in the world; null otherwise.</summary>
        public RoadCart CartOf(int run)
        {
            return run >= 0 && run < carts.Length ? carts[run] as RoadCart : null;
        }

        /// <summary>Real seconds since the clock started, at its normal rate: what the carts' timetables run on.</summary>
        public double Seconds => CargoLoad.Seconds(clock);

        /// <summary>
        /// Holds a cart where it is for some real seconds from now (GL617: stopped by thieves). Its timetable slips by as
        /// long as it stood, so it goes on from there.
        /// </summary>
        public void HoldFor(int run, float seconds)
        {
            if (run >= 0 && run < heldUntil.Length)
            {
                heldUntil[run] = System.Math.Max(heldUntil[run], Seconds + seconds);
            }
        }

        /// <summary>True while a cart is held.</summary>
        public bool IsHeld(int run)
        {
            return run >= 0 && run < heldUntil.Length && Seconds < heldUntil[run];
        }

        private void Update()
        {
            using FrameCost.Scope cost = FrameCost.Of("Carts");
            if (session == null || roads == null || !session.IsReady || session.Plan == null)
            {
                return;
            }
            if (session.Version != version)
            {
                StartOver();
            }
            Camera view = Camera.main;
            if (view == null)
            {
                return;
            }
            Vector3 local = session.WorldView.transform.InverseTransformPoint(view.transform.position);
            var viewer = new Vector2(local.x, local.z);
            double seconds = Seconds;
            double passed = System.Math.Max(0.0, seconds - lastSeconds);
            lastSeconds = seconds;
            for (int i = 0; i < runs.Count; i++)
            {
                CartRun run = runs[i];
                bool held = seconds < heldUntil[i];
                delays[i] += held ? passed : 0.0;
                // GL770: a cart nobody can see is only looked at now and then, for whether it has come into range.
                if (carts[i] == null && (i + Time.frameCount) % HiddenLookFrames != 0)
                {
                    continue;
                }
                if (!roads.TryLine(run.Road, out RoadLine line, out IReadOnlyList<RouteBridges.Span> bridges))
                {
                    Hide(i);
                    continue;
                }
                RunPlace timed = run.At(seconds - delays[i]);
                RunPlace state = timed;
                if (held)
                {
                    state = new RunPlace(state.Fraction, state.Outbound, false, state.Trip, state.Rested);
                }
                // GL725: a road off the map goes on past its end; the trip runs the whole way.
                RoadLine beyond = run.LeavesMap ? OffMapLine(run.Road) : null;
                float along = state.Fraction * (line.Length + (beyond != null ? beyond.Length : 0f));
                Vector2 flat = beyond != null && along > line.Length ? beyond.Flat(along - line.Length) : line.Flat(Mathf.Min(along, line.Length));
                float distance = (flat - viewer).magnitude;
                if (carts[i] == null && distance <= showRange && material != null)
                {
                    var body = new GameObject($"{run.Kind.Name} on road {run.Road}");
                    body.transform.SetParent(root, false);
                    // GL868: a kind on foot is a sherpa with a pack, every other kind a cart.
                    carts[i] = run.Kind.OnFoot ? body.AddComponent<Sherpa>() : body.AddComponent<RoadCart>();
                    carts[i].Build(run, KitFor(i));
                    int robbed = i;
                    carts[i].Robbed += taken => Robbed(robbed, taken);
                    Showing++;
                }
                else if (carts[i] != null && distance > showRange + 30f)
                {
                    Hide(i);
                }
                if (carts[i] != null)
                {
                    // GL726: standing at a settlement, it draws up to the shop that matches its goods, unloads, loads and goes.
                    // One held on the road (stopped by thieves, or trading, GL868) stays on the road.
                    int at = timed.Moving ? -1 : state.Outbound ? run.To : run.From;
                    TownWay way = at >= 0 ? WayTo(run, at, line) : null;
                    if (way != null)
                    {
                        StopPlace stop = TradeStop.At(state.Rested, run.Kind.RestSeconds, TradeStop.LegSeconds(way.Line.Length, run.Kind.Speed, run.Kind.RestSeconds));
                        bool parked = stop.Phase == StopPhase.Unloading || stop.Phase == StopPhase.Loading;
                        float inTown = stop.Phase == StopPhase.Arriving ? stop.Progress : stop.Phase == StopPhase.Leaving ? 1f - stop.Progress : 1f;
                        var drive = new RunPlace(0f, stop.Phase != StopPhase.Leaving, !parked, state.Trip);
                        carts[i].Place(session.WorldView, way.Line, null, null, inTown * way.Line.Length, drive);
                        List<ItemDefinition> arrived = GoodsOf(state.Outbound || run.To < 0 ? run.From : run.To);
                        List<ItemDefinition> nextGoods = GoodsOf(at);
                        carts[i].Stop(stop, state.Trip, arrived, state.Trip + 1, nextGoods, session.Plan.Seed, session.WorldView.transform.TransformPoint(new Vector3(way.Door.x, Ground(way.Door), way.Door.y)));
                        continue;
                    }
                    carts[i].Load(state.Trip, GoodsOf(state.Outbound || run.To < 0 ? run.From : run.To), session.Plan.Seed);
                    carts[i].Place(session.WorldView, line, bridges, beyond, along, state);
                    if (beyond != null)
                    {
                        OffMapPath path = offMapPaths[run.Road];
                        carts[i].Presence.Set(OffMap.Interactable(path, along - line.Length), OffMap.Fade(path, along - line.Length));
                    }
                }
            }
        }

        // GL729: goods taken from a cart wrong the shop they are bound for.
        private void Robbed(int index, IReadOnlyList<(ItemDefinition Item, float Amount)> taken)
        {
            if (index >= runs.Count || session.Plan == null)
            {
                return;
            }
            CartRun run = runs[index];
            int boundFor = TradeStop.GoodsBoundFor(run.At(Seconds - delays[index]), run.Kind.RestSeconds, run.From, run.To);
            int cameFrom = boundFor == run.To ? run.From : boundFor == run.From ? run.To : -1;
            CargoTheft.Report(settlements, session.Plan, boundFor, cameFrom, taken);
        }

        // What a run's traveller is built with; its disposition lasts while the map does (GL868: a sherpa remembers a theft).
        private TravellerKit KitFor(int index)
        {
            moods[index] ??= new Disposition();
            return new TravellerKit
            {
                Material = material,
                CratePrefab = cratePrefab,
                Prices = settlements != null ? settlements.Prices : null,
                Disposition = moods[index],
                Hold = seconds => HoldFor(index, seconds),
            };
        }

        private void Hide(int index)
        {
            if (carts[index] != null)
            {
                Destroy(carts[index].gameObject);
                carts[index] = null;
                Showing--;
            }
        }

        private void StartOver()
        {
            version = session.Version;
            if (root != null)
            {
                Destroy(root.gameObject);
            }
            root = new GameObject("Carts").transform;
            root.SetParent(session.WorldView.transform, false);
            offMapPaths.Clear();
            offMapLines.Clear();
            townWays.Clear();
            goodsBySettlement.Clear();
            walks.Clear();
            settlements = GetComponent<MapSettlements>();
            if (settlements == null)
            {
                settlements = FindAnyObjectByType<MapSettlements>();
            }
            MapLayout layout = session.Layout;
            foreach (PlannedRoad road in session.Plan.Roads)
            {
                if (road.LeavesMap && layout != null && road.Points.Count >= 2)
                {
                    Vector2 end = road.Points[road.Points.Count - 1];
                    offMapPaths[road.Index] = OffMap.Lay(end, end - road.Points[road.Points.Count - 2], p => layout.WalksFreely(p.x, p.y),
                        p => layout.LimitAt(p.x, p.y), p => layout.OutwardAt(p.x, p.y));
                }
            }
            runs = CartRuns.Plan(session.Plan, kinds, session.Plan.Seed, road => offMapPaths.TryGetValue(road.Index, out OffMapPath path) ? path.Length : 0f);
            carts = new RoadTraveller[runs.Count];
            moods = new Disposition[runs.Count];
            delays = new double[runs.Count];
            heldUntil = new double[runs.Count];
            Showing = 0;
            clock = CargoLoad.ClockOf(session.WorldView);
        }

        // GL725: the way on past the end of a road that leaves the map, on the land's own heights; worked out once.
        private RoadLine OffMapLine(int road)
        {
            if (!offMapLines.TryGetValue(road, out RoadLine line))
            {
                line = offMapPaths.TryGetValue(road, out OffMapPath path) && path.Points.Count >= 2 ? new RoadLine(path.Points, session.Map.Height, 4f) : null;
                offMapLines[road] = line;
            }
            return line;
        }

        // GL726: the way in a settlement from the end of a cart's road to the front of the shop that matches what it brings,
        // along the lanes; worked out once for each road end. Null where the settlement is not laid out or has no shop to go to.
        private TownWay WayTo(CartRun run, int at, RoadLine line)
        {
            long key = ((long)run.Road << 1) | (at == run.From ? 0L : 1L);
            if (townWays.TryGetValue(key, out TownWay way))
            {
                return way;
            }
            townWays[key] = null;
            if (settlements == null)
            {
                return null;
            }
            SettlementSite site = settlements.LaidSite(at);
            WorldPlan plan = session.Plan;
            int from = at == run.From ? run.To : run.From;
            PlannedSettlement here = plan.Settlements[at];
            // Goods from beyond the map, or nothing that matches: the merchant takes them, else the first shop.
            int shop = TradeStop.ShopTaking(from >= 0 ? plan.Settlements[from] : null, here);
            int building = site != null && shop >= 0 ? site.Buildings.FindIndex(b => b.Shop == here.Shops[shop]) : -1;
            if (building < 0)
            {
                return null;
            }
            if (!walks.TryGetValue(at, out SettlementWalks web))
            {
                web = new SettlementWalks(site);
                walks[at] = web;
            }
            // Its road's end: as far in as a cart draws up on the road itself.
            Vector2 start = at == run.From ? line.Flat(Mathf.Min(4f, line.Length * 0.5f)) : line.Flat(Mathf.Max(line.Length - 4f, line.Length * 0.5f));
            Vector2 front = web.Front(building, 0f, 3.2f);
            var corners = new List<Vector2>();
            web.Route(start, front, corners);
            corners.Insert(0, start);
            if (corners.Count < 2 || WaterLanes.Length(corners) > 300f || WaterLanes.Length(corners) < 1f)
            {
                return null;
            }
            Vector2 door = web.Front(building, 0f, 0.5f);
            way = new TownWay
            {
                Line = new RoadLine(corners, session.Map.Height, 1f),
                Door = door,
            };
            townWays[key] = way;
            return way;
        }

        private float Ground(Vector2 at)
        {
            float height = session.Map.Height(at.x, at.y);
            return GroundProbe.TryGround(session.WorldView, at.x, at.y, height + 3f, out float ground) ? ground : height;
        }

        private sealed class TownWay
        {
            public RoadLine Line;
            public Vector2 Door;
        }

        // What a settlement's shops sell, of the items that exist.
        private List<ItemDefinition> GoodsOf(int settlement)
        {
            if (!goodsBySettlement.TryGetValue(settlement, out List<ItemDefinition> sold))
            {
                sold = new List<ItemDefinition>();
                CargoLoad.GoodsOf(session.Plan, catalog, settlement, sold);
                goodsBySettlement[settlement] = sold;
            }
            return sold;
        }
    }
}
