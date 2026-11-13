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
        private readonly List<ItemDefinition> goods = new List<ItemDefinition>();
        private List<CartRun> runs = new List<CartRun>();
        private RoadCart[] carts = System.Array.Empty<RoadCart>();
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
            return run >= 0 && run < carts.Length ? carts[run] : null;
        }

        /// <summary>Real seconds since the clock started, at its normal rate: what the carts' timetables run on.</summary>
        public double Seconds => CargoLoad.Seconds(clock);

        private void Update()
        {
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
            for (int i = 0; i < runs.Count; i++)
            {
                CartRun run = runs[i];
                if (!roads.TryLine(run.Road, out RoadLine line, out IReadOnlyList<RouteBridges.Span> bridges))
                {
                    Hide(i);
                    continue;
                }
                RunPlace state = run.At(seconds);
                float distance = (line.Flat(state.Fraction * line.Length) - viewer).magnitude;
                if (carts[i] == null && distance <= showRange && material != null)
                {
                    var body = new GameObject($"{run.Kind.Name} on road {run.Road}");
                    body.transform.SetParent(root, false);
                    carts[i] = body.AddComponent<RoadCart>();
                    carts[i].Build(run, material, cratePrefab);
                    Showing++;
                }
                else if (carts[i] != null && distance > showRange + 30f)
                {
                    Hide(i);
                }
                if (carts[i] != null)
                {
                    carts[i].Load(state.Trip, GoodsOf(state.Outbound || run.To < 0 ? run.From : run.To), session.Plan.Seed);
                    carts[i].Place(session.WorldView, line, bridges, state);
                }
            }
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
            runs = CartRuns.Plan(session.Plan, kinds, session.Plan.Seed);
            carts = new RoadCart[runs.Count];
            Showing = 0;
            clock = CargoLoad.ClockOf(session.WorldView);
        }

        // What a settlement's shops sell, of the items that exist.
        private List<ItemDefinition> GoodsOf(int settlement)
        {
            CargoLoad.GoodsOf(session.Plan, catalog, settlement, goods);
            return goods;
        }
    }
}
