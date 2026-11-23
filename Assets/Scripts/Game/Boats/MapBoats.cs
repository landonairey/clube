using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The vessels on the map's water routes (GL618): "smaller boats can use rivers and larger vessels use the sea".
    /// <see cref="BoatRuns"/> gives every route its vessels, the <i>Edmund Fitzgerald</i> among them, and where each is
    /// follows from the clock. Far from the viewer a vessel is only that: a place on its planned route. Near, it is a
    /// <see cref="Boat"/> in the world, on a lane found on the real water (<see cref="WaterLanes"/>, worked out the
    /// first time a vessel of the route comes near), loaded with what the settlement it left sells.
    /// </summary>
    public class MapBoats : MonoBehaviour
    {
        // Frames between looks at a boat out of sight (GL770).
        private const int HiddenLookFrames = 8;

        private readonly Dictionary<(int, IReadOnlyList<string>), List<ItemDefinition>> goodsFor = new Dictionary<(int, IReadOnlyList<string>), List<ItemDefinition>>();

        [SerializeField]
        private MapSession session;

        [Tooltip("The low-poly vertex-colour material hulls and hands are made of.")]
        [SerializeField]
        private Material material;

        [Tooltip("The game's items: a vessel's load is what the settlement it left sells.")]
        [SerializeField]
        private ItemCatalog catalog;

        [Tooltip("The wooden crate (a storage chest) the goods ride in.")]
        [SerializeField]
        private GameObject cratePrefab;

        [Tooltip("A vessel is in the world while it is within this many metres of the viewer.")]
        [SerializeField, Range(60f, 800f)]
        private float showRange = 380f;

        [Tooltip("The most vessels in the world at once.")]
        [SerializeField, Range(0, 40)]
        private int mostBoats = 10;

        private readonly List<VesselClass> classes = VesselClass.Defaults();
        private readonly Dictionary<long, RoadLine> lanes = new Dictionary<long, RoadLine>();
        // The lane being searched for (GL771).
        private const float LaneSearchMilliseconds = 2f;
        private SlicedWork laneWork;
        private WaterLanes.LaneFound laneFound;
        private int laneRoute;
        private VesselClass laneVessel;
        private float laneMilliseconds;
        private readonly Dictionary<long, OffMapPath> offMapLanes = new Dictionary<long, OffMapPath>();
        private readonly Dictionary<long, float> offMapStarts = new Dictionary<long, float>();
        private RunWay[] runWays = System.Array.Empty<RunWay>();
        private SettlementDocks docks;
        private MapSettlements settlements;
        private List<BoatRun> runs = new List<BoatRun>();
        private Boat[] boats = System.Array.Empty<Boat>();
        private double[] delays = System.Array.Empty<double>();
        private double[] heldUntil = System.Array.Empty<double>();
        private DayNightCycle clock;
        private Transform root;
        private int version = -1;
        private double lastSeconds;
        private bool loggedLaneProblem;

        /// <summary>The classes of vessel; change a row to change every vessel of the class.</summary>
        public List<VesselClass> Classes => classes;

        /// <summary>Every vessel of the map, in the world or not.</summary>
        public IReadOnlyList<BoatRun> Runs => runs;

        /// <summary>Vessels in the world now.</summary>
        public int Showing { get; private set; }

        /// <summary>Metres from the viewer within which a vessel is in the world.</summary>
        public float ShowRange => showRange;

        /// <summary>The longest a lane took to find, in milliseconds.</summary>
        public float LongestLaneMilliseconds { get; private set; }

        /// <summary>Route and vessel class pairs whose lane is missing or stops short of the route's end (GL687).</summary>
        public int UnusableLanes { get; private set; }

        public MapSession Session => session;

        public Material Material => material;

        public GameObject CratePrefab => cratePrefab;

        /// <summary>What the boats hang under: the world's own metres.</summary>
        public Transform Root => root;

        /// <summary>Real seconds since the clock started, at its normal rate: what the timetables run on.</summary>
        public double Seconds => CargoLoad.Seconds(clock);

        /// <summary>The vessel of a run, when it is in the world; null otherwise.</summary>
        public Boat BoatOf(int run)
        {
            return run >= 0 && run < boats.Length ? boats[run] : null;
        }

        /// <summary>Where a run's vessel is on its timetable now (hold-ups counted).</summary>
        public RunPlace PlaceOf(int run)
        {
            return runs[run].At(Seconds - delays[run]);
        }

        /// <summary>Where a run's vessel is on the map now (x, z), by its planned route: good from any distance.</summary>
        public Vector2 PositionOf(int run)
        {
            // GL847: a vessel in the world is marked where it really floats, so the marker and the boat never part.
            if (run >= 0 && run < boats.Length && boats[run] != null)
            {
                Vector3 local = boats[run].transform.localPosition;
                return new Vector2(local.x, local.z);
            }
            WaterRoute route = session.Plan.WaterRoutes[runs[run].Route];
            return WaterLanes.Along(route.Points, PlaceOf(run).Fraction * route.Length);
        }

        /// <summary>
        /// Holds a vessel where it is for some real seconds from now (GL617: boarded). Its timetable slips by as long as
        /// it lay, so it sails on from there.
        /// </summary>
        public void HoldFor(int run, float seconds)
        {
            if (run >= 0 && run < heldUntil.Length)
            {
                heldUntil[run] = System.Math.Max(heldUntil[run], Seconds + seconds);
            }
        }

        /// <summary>
        /// The lane a class of vessel sails on a water route: on the real water, worked out once. False when the water
        /// by the route cannot float it.
        /// </summary>
        public bool TryLane(int routeIndex, VesselClass vessel, out RoadLine lane)
        {
            long key = LaneKey(routeIndex, vessel);
            if (lanes.TryGetValue(key, out lane))
            {
                return lane != null;
            }
            // GL771: a lane is searched for over several frames, one at a time; the boat sets out once it is known.
            if (laneWork == null)
            {
                laneFound = new WaterLanes.LaneFound();
                laneRoute = routeIndex;
                laneVessel = vessel;
                laneMilliseconds = 0f;
                laneWork = new SlicedWork(WaterLanes.ForSteps(session.Plan.WaterRoutes[routeIndex], vessel, session.Map.Water, laneFound));
            }
            return false;
        }

        // Works on the lane being searched for, and keeps it once it is found.
        private void SearchLane()
        {
            if (laneWork == null)
            {
                return;
            }
            float before = Time.realtimeSinceStartup;
            bool done = laneWork.Run(session.WorldView.Hurry ? float.PositiveInfinity : LaneSearchMilliseconds);
            laneMilliseconds += (Time.realtimeSinceStartup - before) * 1000f;
            if (!done)
            {
                return;
            }
            laneWork.Dispose();
            laneWork = null;
            FinishLane(laneRoute, laneVessel, laneFound.Points, laneFound.Whole);
        }

        private void FinishLane(int routeIndex, VesselClass vessel, List<Vector2> points, bool whole)
        {
            long key = LaneKey(routeIndex, vessel);
            RoadLine lane;
            {
                float start = Time.realtimeSinceStartup - laneMilliseconds * 0.001f;
                WaterRoute route = session.Plan.WaterRoutes[routeIndex];
                if (route.To < 0 && points != null && points.Count >= 2 && session.Layout != null)
                {
                    // GL725: a route out to the open sea sails on past the map's edge, to ports beyond it: where its lane stops
                    // short (the water map ends), the way out goes on from there, so the lane is still whole.
                    whole = true;
                    OffMapPath path = OffSea(points[points.Count - 1], points[points.Count - 1] - points[points.Count - 2]);
                    offMapStarts[key] = WaterLanes.Length(points);
                    offMapLanes[key] = path;
                    points.AddRange(path.Points.GetRange(1, path.Points.Count - 1));
                }
                lane = points != null && points.Count >= 2 && WaterLanes.Length(points) > vessel.Length * 2f ? new RoadLine(points, (x, z) => 0f, 1000f) : null;
                lanes[key] = lane;
                float took = (Time.realtimeSinceStartup - start) * 1000f;
                LongestLaneMilliseconds = Mathf.Max(LongestLaneMilliseconds, took);
                if (lane == null || !whole)
                {
                    // Every route and class that fails is counted; only the first is logged, once a session (GL687).
                    // A route is planned between two harbours across the map's water graph; where the real water along it is too
                    // shallow or too narrow for this class of vessel, its lane is cut short and the vessel sails what's there.
                    UnusableLanes++;
                    if (!loggedLaneProblem)
                    {
                        loggedLaneProblem = true;
                        UnityEngine.Debug.Log($"Boats: water route {routeIndex} "
                            + (lane == null ? $"has no water a {vessel.Name.ToLowerInvariant()} floats on" : "is not open water end to end: its lane stops short")
                            + ". Such routes are counted in MapBoats.UnusableLanes; this is logged once per session.", this);
                    }
                }
            }
        }

        /// <summary>
        /// GL726: the way a run's vessel sails: its route's lane, from and to the berths of the docks at its ends (a short
        /// approach along the berth, so it comes alongside), and on past the map's edge for a route to the open sea.
        /// Worked out once a run.
        /// </summary>
        private bool RunLane(int index, out RunWay way)
        {
            way = runWays[index];
            if (way != null)
            {
                return way.Lane != null;
            }
            BoatRun run = runs[index];
            way = new RunWay();
            runWays[index] = way;
            if (!TryLane(run.Route, run.Class, out RoadLine baseLane))
            {
                return false;
            }
            var points = new List<Vector2>(baseLane.Points);
            long key = LaneKey(run.Route, run.Class);
            bool sea = run.Class.Kind == WaterRouteKind.Sea;
            way.FromDock = sea ? docks.DockOf(run.From) : null;
            way.ToDock = sea && run.To >= 0 ? docks.DockOf(run.To) : null;
            float before = 0f;
            if (way.FromDock != null)
            {
                List<Vector2> come = Alongside(way.FromDock, run);
                way.FromLength = WaterLanes.Length(come);
                come.Reverse();
                points.InsertRange(0, come);
                before = WaterLanes.Length(points.GetRange(0, come.Count + 1));
            }
            if (way.ToDock != null)
            {
                List<Vector2> come = Alongside(way.ToDock, run);
                way.ToLength = WaterLanes.Length(come);
                points.AddRange(come);
            }
            if (offMapLanes.TryGetValue(key, out OffMapPath offMap))
            {
                way.OffMap = offMap;
                way.OffMapStart = offMapStarts[key] + before;
            }
            way.Lane = points.Count >= 2 ? new RoadLine(points, (x, z) => 0f, 1000f) : null;
            // GL847: a lane with no dock approach or way off the map is timed by the route it follows, not by its own length.
            if (way.Lane != null && way.FromDock == null && way.ToDock == null && way.OffMap == null)
            {
                way.Timetable = new LaneTimetable(way.Lane.Points, session.Plan.WaterRoutes[run.Route].Points);
            }
            return way.Lane != null;
        }

        // The last of the way in to a berth: from off its side, along it, to where the vessel lies.
        private static List<Vector2> Alongside(DockLayout dock, BoatRun run)
        {
            DockBerth berth = dock.Berth(run.Index);
            Vector2 moored = berth.Mooring(run.Class.Beam);
            return new List<Vector2>
            {
                moored + berth.Along * (run.Class.Length * 1.4f) + berth.Outward * (run.Class.Length * 0.8f),
                moored + berth.Along * (run.Class.Length * 0.6f),
                moored,
            };
        }

        private sealed class RunWay
        {
            public RoadLine Lane;
            public DockLayout FromDock, ToDock;
            public float FromLength, ToLength;
            public OffMapPath OffMap;
            public float OffMapStart;
            public LaneTimetable Timetable;
        }

        private long LaneKey(int routeIndex, VesselClass vessel)
        {
            return ((long)routeIndex << 20) | (uint)classes.IndexOf(vessel);
        }

        // GL725: the way on out of the map from where a lane to the open sea ends.
        private OffMapPath OffSea(Vector2 end, Vector2 along)
        {
            MapLayout layout = session.Layout;
            return OffMap.Lay(end, along, p => layout.WalksFreely(p.x, p.y), p => layout.LimitAt(p.x, p.y), p => layout.OutwardAt(p.x, p.y));
        }

        /// <summary>The water's surface at a place (x, z): the river's or the sea's there; <paramref name="otherwise"/> off the water.</summary>
        public float SurfaceAt(Vector2 at, float otherwise)
        {
            return WaterLanes.TrySurfaceAt(session.Map.Water, at, out float surface) ? surface : otherwise;
        }

        /// <summary>The sea's level on this map.</summary>
        public float SeaLevel => session.Map.Water.SeaLevel;

        private void Update()
        {
            using FrameCost.Scope cost = FrameCost.Of("Boats");
            if (session == null || !session.IsReady || session.Plan == null)
            {
                return;
            }
            if (session.Version != version)
            {
                goodsFor.Clear();
                StartOver();
            }
            SearchLane();
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
                bool held = seconds < heldUntil[i];
                delays[i] += held ? passed : 0.0;
                // GL770: a boat nobody can see is only looked at now and then, for whether it has come into range.
                if (boats[i] == null && (i + Time.frameCount) % HiddenLookFrames != 0)
                {
                    continue;
                }
                BoatRun run = runs[i];
                RunPlace state = run.At(seconds - delays[i]);
                if (held)
                {
                    state = new RunPlace(state.Fraction, state.Outbound, false, state.Trip, state.Rested);
                }
                WaterRoute route = session.Plan.WaterRoutes[run.Route];
                float distance = (WaterLanes.Along(route.Points, state.Fraction * route.Length) - viewer).magnitude;
                if (boats[i] == null && distance <= showRange && Showing < mostBoats && material != null && TryLane(run.Route, run.Class, out _))
                {
                    boats[i] = Launch(run);
                    int robbed = i;
                    boats[i].Robbed += taken => Robbed(robbed, taken);
                    Showing++;
                }
                else if (boats[i] != null && distance > showRange + 60f)
                {
                    Hide(i);
                }
                if (boats[i] != null && RunLane(i, out RunWay way))
                {
                    RoadLine lane = way.Lane;
                    int seed = session.Plan.Seed + run.Index * 977;
                    List<ItemDefinition> goods = GoodsFor(state.Outbound || run.To < 0 ? run.From : run.To, run);
                    // GL726: lying at a settlement's dock, it is moored at its berth while its hands unload and load.
                    DockLayout dock = state.Moving ? null : state.Outbound ? way.ToDock : way.FromDock;
                    if (dock != null)
                    {
                        Vector2 moored = lane.Flat(state.Outbound ? lane.Length : 0f);
                        Vector2 heading = lane.Direction(state.Outbound ? lane.Length : 0f) * (state.Outbound ? 1f : -1f);
                        boats[i].Float(moored, SurfaceAt(moored, boats[i].Surface), heading, false);
                        List<ItemDefinition> nextGoods = GoodsFor(state.Outbound ? run.To : run.From, run);
                        Vector2 drop = dock.SetDown(run.Index);
                        Vector3 dropAt = session.WorldView.transform.TransformPoint(new Vector3(drop.x, docks.DeckHeight(dock), drop.y));
                        boats[i].Stop(TradeStop.At(state.Rested, run.Class.RestSeconds, 0f), state.Trip, goods, state.Trip + 1, nextGoods, seed, dropAt);
                        boats[i].Presence.Set(true, 1f);
                        continue;
                    }
                    boats[i].Load(state.Trip, goods, seed);
                    // It lies off each undocked end by its own length, bow to the water it will sail. Sea routes share a harbour
                    // and the water out of it: each vessel has a berth of its own along the lane and keeps to its own side of it.
                    bool sea = run.Class.Kind == WaterRouteKind.Sea;
                    float berth = sea ? run.Index % 4 * (run.Class.Length + 4f) : 0f;
                    float keep = Mathf.Min(run.Class.Length * 0.6f + 1f + berth, lane.Length * 0.5f);
                    float timed = way.Timetable != null ? way.Timetable.LaneDistance(state.Fraction * route.Length) : state.Fraction * lane.Length;
                    float along = Mathf.Clamp(timed,way.FromDock != null ? 0f : keep, way.ToDock != null ? lane.Length : lane.Length - keep);
                    Vector2 at = lane.Flat(along);
                    Vector2 ahead = lane.Direction(along);
                    Vector2 aside = at + new Vector2(ahead.y, -ahead.x) * ((run.Index % 3 - 1) * (run.Class.Beam + 2f));
                    bool nearDock = (way.FromDock != null && along < way.FromLength + 10f) || (way.ToDock != null && along > lane.Length - way.ToLength - 10f);
                    if (sea && !nearDock && WaterLanes.TrySurfaceAt(session.Map.Water, aside, out _))
                    {
                        at = aside;
                    }
                    boats[i].Float(at, SurfaceAt(at, boats[i].Surface), ahead * (state.Outbound ? 1f : -1f), state.Moving);
                    if (way.OffMap != null)
                    {
                        float past = along - way.OffMapStart;
                        boats[i].Presence.Set(OffMap.Interactable(way.OffMap, past), OffMap.Fade(way.OffMap, past));
                    }
                }
            }
        }

        // GL770: what a settlement loads onto a vessel of this cargo, worked out once: looked through the catalog for every boat in sight, every frame, it was most of what the boats cost.
        private List<ItemDefinition> GoodsFor(int settlement, BoatRun run)
        {
            using FrameCost.Scope cost = FrameCost.Of("Boats: goods");
            var key = (settlement, run.CargoWords);
            if (!goodsFor.TryGetValue(key, out List<ItemDefinition> found))
            {
                found = new List<ItemDefinition>();
                CargoLoad.GoodsOf(session.Plan, catalog, settlement, found);
                CargoLoad.Only(found, catalog, run.CargoWords);
                goodsFor[key] = found;
            }
            return found;
        }

        private Boat Launch(BoatRun run)
        {
            using FrameCost.Scope cost = FrameCost.Of("Boats: launch");
            var body = new GameObject($"{run.Name} ({run.Class.Name})");
            body.transform.SetParent(root, false);
            Boat boat = body.AddComponent<Boat>();
            boat.Build(run.Class, run.Name, material, cratePrefab, FigurePreset.Sailor, run.Index * 7919 + 3);
            // GL847: a river boat starts on its river's surface, never the sea's level, which can lie under the river's banks:
            // where the first place it floats has no surface found, it keeps the last one it had.
            boat.Surface = run.Class.Kind == WaterRouteKind.River ? RiverSurface(run) : SeaLevel;
            WorldPlan plan = session.Plan;
            string from = plan.Settlements[run.From].Name, to = run.To >= 0 ? plan.Settlements[run.To].Name : "the open sea";
            boat.Describe($"The {run.Name}, {run.Class.Name.ToLowerInvariant()}: " + (string.IsNullOrEmpty(run.Cargo) ? "goods" : run.Cargo) + $" between {from} and {to}");
            return boat;
        }

        // GL847: the surface of the river a run's vessel sails, near where its timetable has it: at its place on the route,
        // or else at the nearest point of its lane that has water found under it.
        private float RiverSurface(BoatRun run)
        {
            Vector2 at = PositionOf(run.Index);
            if (WaterLanes.TrySurfaceAt(session.Map.Water, at, out float surface))
            {
                return surface;
            }
            float found = SeaLevel, nearest = float.MaxValue;
            if (TryLane(run.Route, run.Class, out RoadLine lane))
            {
                foreach (Vector2 point in lane.Points)
                {
                    float distance = (point - at).sqrMagnitude;
                    if (distance < nearest && WaterLanes.TrySurfaceAt(session.Map.Water, point, out surface))
                    {
                        nearest = distance;
                        found = surface;
                    }
                }
            }
            return found;
        }

        // GL729: goods taken from a vessel wrong the shop they are bound for.
        private void Robbed(int index, IReadOnlyList<(ItemDefinition Item, float Amount)> taken)
        {
            if (index >= runs.Count || session.Plan == null)
            {
                return;
            }
            settlements = settlements != null ? settlements : FindAnyObjectByType<MapSettlements>();
            BoatRun run = runs[index];
            int boundFor = TradeStop.GoodsBoundFor(PlaceOf(index), run.Class.RestSeconds, run.From, run.To);
            int cameFrom = boundFor == run.To ? run.From : boundFor == run.From ? run.To : -1;
            CargoTheft.Report(settlements, session.Plan, boundFor, cameFrom, taken);
        }

        private void Hide(int index)
        {
            if (boats[index] != null)
            {
                Destroy(boats[index].gameObject);
                boats[index] = null;
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
            root = new GameObject("Boats").transform;
            root.SetParent(session.WorldView.transform, false);
            // GL725: a route out to the open sea is timed for the way on past the map's edge too.
            runs = BoatRuns.Plan(session.Plan, classes, session.Plan.Seed, route => route.To < 0 && route.Points.Count >= 2 && session.Layout != null
                ? OffSea(route.Points[route.Points.Count - 1], route.Points[route.Points.Count - 1] - route.Points[route.Points.Count - 2]).Length : 0f);
            boats = new Boat[runs.Count];
            delays = new double[runs.Count];
            heldUntil = new double[runs.Count];
            laneWork?.Dispose();
            laneWork = null;
            lanes.Clear();
            offMapLanes.Clear();
            offMapStarts.Clear();
            runWays = new RunWay[runs.Count];
            docks = GetComponent<SettlementDocks>();
            if (docks == null)
            {
                docks = gameObject.AddComponent<SettlementDocks>();
            }
            docks.Bind(session, material);
            UnusableLanes = 0;
            Showing = 0;
            clock = CargoLoad.ClockOf(session.WorldView);
            lastSeconds = Seconds;
        }
    }
}
