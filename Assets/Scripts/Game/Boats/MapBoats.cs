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
        private readonly List<ItemDefinition> goods = new List<ItemDefinition>();
        private readonly Dictionary<long, RoadLine> lanes = new Dictionary<long, RoadLine>();
        private List<BoatRun> runs = new List<BoatRun>();
        private Boat[] boats = System.Array.Empty<Boat>();
        private double[] delays = System.Array.Empty<double>();
        private double[] heldUntil = System.Array.Empty<double>();
        private DayNightCycle clock;
        private Transform root;
        private int version = -1;
        private double lastSeconds;

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
            long key = ((long)routeIndex << 20) | (uint)classes.IndexOf(vessel);
            if (!lanes.TryGetValue(key, out lane))
            {
                float start = Time.realtimeSinceStartup;
                List<Vector2> points = WaterLanes.For(session.Plan.WaterRoutes[routeIndex], vessel, session.Map.Water, out bool whole);
                lane = points != null && points.Count >= 2 && WaterLanes.Length(points) > vessel.Length * 2f ? new RoadLine(points, (x, z) => 0f, 1000f) : null;
                lanes[key] = lane;
                float took = (Time.realtimeSinceStartup - start) * 1000f;
                LongestLaneMilliseconds = Mathf.Max(LongestLaneMilliseconds, took);
                if (lane == null || !whole)
                {
                    UnityEngine.Debug.Log($"Boats: water route {routeIndex} "
                        + (lane == null ? $"has no water a {vessel.Name.ToLowerInvariant()} floats on" : "is not open water end to end: its lane stops short") + ".", this);
                }
            }
            return lane != null;
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
            if (session == null || !session.IsReady || session.Plan == null)
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
                bool held = seconds < heldUntil[i];
                delays[i] += held ? passed : 0.0;
                BoatRun run = runs[i];
                RunPlace state = run.At(seconds - delays[i]);
                if (held)
                {
                    state = new RunPlace(state.Fraction, state.Outbound, false, state.Trip);
                }
                WaterRoute route = session.Plan.WaterRoutes[run.Route];
                float distance = (WaterLanes.Along(route.Points, state.Fraction * route.Length) - viewer).magnitude;
                if (boats[i] == null && distance <= showRange && Showing < mostBoats && material != null && TryLane(run.Route, run.Class, out _))
                {
                    boats[i] = Launch(run);
                    Showing++;
                }
                else if (boats[i] != null && distance > showRange + 60f)
                {
                    Hide(i);
                }
                if (boats[i] != null && TryLane(run.Route, run.Class, out RoadLine lane))
                {
                    CargoLoad.GoodsOf(session.Plan, catalog, state.Outbound || run.To < 0 ? run.From : run.To, goods);
                    CargoLoad.Only(goods, catalog, run.CargoWords);
                    boats[i].Load(state.Trip, goods, session.Plan.Seed + run.Index * 977);
                    // It lies off each end by its own length, bow to the water it will sail. Sea routes share a harbour and the
                    // water out of it: each vessel has a berth of its own along the lane and keeps to its own side of it.
                    bool sea = run.Class.Kind == WaterRouteKind.Sea;
                    float berth = sea ? run.Index % 4 * (run.Class.Length + 4f) : 0f;
                    float keep = Mathf.Min(run.Class.Length * 0.6f + 1f + berth, lane.Length * 0.5f);
                    float along = Mathf.Clamp(state.Fraction * lane.Length, keep, lane.Length - keep);
                    Vector2 at = lane.Flat(along);
                    Vector2 ahead = lane.Direction(along);
                    Vector2 aside = at + new Vector2(ahead.y, -ahead.x) * ((run.Index % 3 - 1) * (run.Class.Beam + 2f));
                    if (sea && WaterLanes.TrySurfaceAt(session.Map.Water, aside, out _))
                    {
                        at = aside;
                    }
                    boats[i].Float(at, SurfaceAt(at, boats[i].Surface), ahead * (state.Outbound ? 1f : -1f), state.Moving);
                }
            }
        }

        private Boat Launch(BoatRun run)
        {
            var body = new GameObject($"{run.Name} ({run.Class.Name})");
            body.transform.SetParent(root, false);
            Boat boat = body.AddComponent<Boat>();
            boat.Build(run.Class, run.Name, material, cratePrefab, FigurePreset.Sailor, run.Index * 7919 + 3);
            boat.Surface = SeaLevel;
            WorldPlan plan = session.Plan;
            string from = plan.Settlements[run.From].Name, to = run.To >= 0 ? plan.Settlements[run.To].Name : "the open sea";
            boat.Describe($"The {run.Name}, {run.Class.Name.ToLowerInvariant()}: " + (string.IsNullOrEmpty(run.Cargo) ? "goods" : run.Cargo) + $" between {from} and {to}");
            return boat;
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
            runs = BoatRuns.Plan(session.Plan, classes, session.Plan.Seed);
            boats = new Boat[runs.Count];
            delays = new double[runs.Count];
            heldUntil = new double[runs.Count];
            lanes.Clear();
            Showing = 0;
            clock = CargoLoad.ClockOf(session.WorldView);
            lastSeconds = Seconds;
        }
    }
}
