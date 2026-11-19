using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The people of the map's settlements (GL615): once a settlement stands in the terrain (<see cref="MapSettlements"/>)
    /// and the viewer is near, its residents (<see cref="SettlementRoster"/>: GL593's population less the shops' keepers)
    /// are put in the world as <see cref="Villager"/>s, and taken out again when the viewer has gone, so only the
    /// settlements round the player are lived in, up to <see cref="mostPeople"/> at once. It keeps the tables the
    /// villagers read (<see cref="Duty"/>, <see cref="Occupation"/>) and what their work needs of the scene: the world,
    /// the clock, the tree feller, the herds, the ground.
    /// </summary>
    public class SettlementPeople : MonoBehaviour
    {
        [SerializeField]
        private MapSession session;

        [SerializeField]
        private MapSettlements settlements;

        [Tooltip("The low-poly vertex-colour material the people are made of.")]
        [SerializeField]
        private Material material;

        [Tooltip("The game's items: the tools the duties name.")]
        [SerializeField]
        private ItemCatalog catalog;

        [Tooltip("A settlement is lived in once the viewer is within this many metres of its edge.")]
        [SerializeField, Range(40f, 400f)]
        private float liveRange = 150f;

        [Tooltip("Metres further out at which its people are taken away again.")]
        [SerializeField, Range(10f, 200f)]
        private float leaveMargin = 50f;

        [Tooltip("The most residents in the world at once (keepers not counted).")]
        [SerializeField, Range(0, 200)]
        private int mostPeople = 64;

        private const int ArrivalsPerFrame = 2;

        // Someone waiting to come into the world.
        private struct Arrival
        {
            public int Settlement;
            public SettlementSite Site;
            public SettlementWalks Walks;
            public Resident Resident;
            public List<Villager> People;
        }

        private readonly List<Duty> duties = Duty.Defaults();
        private readonly List<Occupation> occupations = Occupation.Defaults();
        private readonly Dictionary<int, List<Villager>> living = new Dictionary<int, List<Villager>>();
        private readonly Dictionary<ToolDefinition, Mesh> toolMeshes = new Dictionary<ToolDefinition, Mesh>();
        private readonly List<int> leaving = new List<int>();
        private readonly Queue<Arrival> arriving = new Queue<Arrival>();
        private readonly List<WorkRate> rates = WorkRate.Defaults();
        private readonly Dictionary<int, SettlementWorks> works = new Dictionary<int, SettlementWorks>();
        private DutyHours dutyHours;
        private KeptHeights heights;
        private System.Func<float, float, RoadWater> water;
        private DayNightCycle clock;
        private Transform root;
        private int version = -1;
        private float nextLook;

        /// <summary>The duties and occupations the residents live by; change a row to change every settlement.</summary>
        public List<Duty> Duties => duties;

        public List<Occupation> Occupations => occupations;

        public WorldView View => session.WorldView;

        public WorldPlan Plan => session.Plan;

        /// <summary>True once the map is ready and its people can act.</summary>
        public bool IsRunning => session != null && session.IsReady && session.Plan != null && version == session.Version;

        /// <summary>The scene's tree feller (the player's): what makes a cut tree fall. Null in a scene without one.</summary>
        public TreeFeller Feller { get; private set; }

        public AnimalHerds Herds { get; private set; }

        /// <summary>The thieves' camps (GL617); null in a scene without them.</summary>
        public ThievesCamps Thieves { get; private set; }

        /// <summary>Where the viewer was at the last look, in the world's own metres (x, z).</summary>
        public Vector2 Viewer { get; private set; }

        /// <summary>
        /// True when someone put in the world in the middle of a duty done at a far post should stand at the post: the
        /// viewer is nearer it than the settlement.
        /// </summary>
        public bool StartsAtPost(SettlementSite site, Vector2 post)
        {
            return (Viewer - post).sqrMagnitude < (Viewer - site.Settlement.Position).sqrMagnitude;
        }

        /// <summary>Which materials are trees in this world.</summary>
        public TreeMaterials Trees { get; private set; }

        /// <summary>Residents in the world now.</summary>
        public int Living { get; private set; }

        /// <summary>The residents of a settlement in the world now; empty when it is not lived in.</summary>
        public IReadOnlyList<Villager> PeopleOf(int settlement)
        {
            return living.TryGetValue(settlement, out List<Villager> people) ? people : (IReadOnlyList<Villager>)System.Array.Empty<Villager>();
        }

        /// <summary>The day of the game (1 the first) and the hour of the day.</summary>
        public int Day => clock != null ? clock.Day : 1;

        public float Hour => clock != null ? clock.Hour : 12f;

        /// <summary>Hours on the clock since the game began (midnight before day 1), as the duty shifts count them (GL730).</summary>
        public double Now => clock != null ? (clock.Day - 1) * 24d + clock.ClockHour : 12d;

        /// <summary>How fast each work gets on (GL730); change a row to change every settlement.</summary>
        public List<WorkRate> Rates => rates;

        /// <summary>The hours the occupations spend on each work (GL730).</summary>
        public DutyHours DutyHours => dutyHours ??= new DutyHours(duties);

        /// <summary>Where settlements' people may fell and dig (GL731: never round the player's start); null before the map is ready.</summary>
        public WorkLand Land { get; private set; }

        /// <summary>
        /// What a settlement's people work and how far they have got (GL730), worked out on the first ask from the
        /// settlement as laid out; null before the map is ready.
        /// </summary>
        public SettlementWorks WorksOf(int settlement)
        {
            if (!IsRunning || settlement < 0 || settlement >= Plan.Settlements.Count)
            {
                return null;
            }
            if (!works.TryGetValue(settlement, out SettlementWorks found))
            {
                SettlementSite site = settlements.LaidSite(settlement);
                if (site == null)
                {
                    return null;
                }
                found = new SettlementWorks(site, SettlementRoster.Build(site, occupations, Plan.Seed), duties, Land,
                    View.Trees, View.World.Grid, Ground, IsDry);
                works[settlement] = found;
            }
            return found;
        }

        /// <summary>The duty an occupation gives right now.</summary>
        public Duty DutyNow(Occupation occupation)
        {
            return Duty.Find(duties, occupation.DutyAt(Occupation.DayOfWeek(Day), Hour)) ?? duties[0];
        }

        /// <summary>The natural ground's height at (x, z).</summary>
        public float Ground(float x, float z)
        {
            return heights.At(x, z);
        }

        /// <summary>True where the map has neither sea nor river.</summary>
        public bool IsDry(Vector2 spot)
        {
            return session.Layout.WalksFreely(spot.x, spot.y) && water(spot.x, spot.y) == RoadWater.Dry;
        }

        /// <summary>The terrain's height under a point, looking down from a little over <paramref name="near"/>; false with none loaded there.</summary>
        public bool TryGround(float x, float z, float near, out float height)
        {
            return GroundProbe.TryGround(View, x, z, near, out height);
        }

        /// <summary>A scene position in the world's own metres, and back.</summary>
        public Vector3 ToLocal(Vector3 scene)
        {
            return View.transform.InverseTransformPoint(scene);
        }

        public Vector3 ToWorld(Vector3 local)
        {
            return View.transform.TransformPoint(local);
        }

        /// <summary>The tool an item id names; null for none or an item that is not a tool.</summary>
        public ToolDefinition ToolOf(string id)
        {
            return catalog != null && !string.IsNullOrEmpty(id) ? catalog.Find(id) as ToolDefinition : null;
        }

        /// <summary>A tool's model for a hand, built once.</summary>
        public Mesh ToolMesh(ToolDefinition tool)
        {
            if (!toolMeshes.TryGetValue(tool, out Mesh mesh))
            {
                mesh = HeldToolMeshes.Build(tool, withHand: false);
                // GL667: a held tool is modelled with its haft up +y from the grip, but a figure's hand frame runs -y down
                // the forearm and +z forward out of the fist (FigureProps): turned as it was, the head sat in the shoulder.
                ToHandFrame(mesh);
                toolMeshes[tool] = mesh;
            }
            return mesh;
        }

        // Turns a tool model from the view's frame (haft along +y) to a figure's hand frame (haft along +z, out of the fist).
        private static void ToHandFrame(Mesh mesh)
        {
            Quaternion turn = HandTurn;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            mesh.GetVertices(vertices);
            mesh.GetNormals(normals);
            for (int i = 0; i < vertices.Count; i++)
            {
                vertices[i] = turn * vertices[i];
            }
            for (int i = 0; i < normals.Count; i++)
            {
                normals[i] = turn * normals[i];
            }
            mesh.SetVertices(vertices);
            if (normals.Count == vertices.Count)
            {
                mesh.SetNormals(normals);
            }
            mesh.RecalculateBounds();
        }

        /// <summary>The turn from a held tool's model frame (haft +y) to a figure's hand frame (haft +z).</summary>
        public static Quaternion HandTurn => Quaternion.Euler(90f, 0f, 0f);

        private void Update()
        {
            using FrameCost.Scope cost = FrameCost.Of("People (SettlementPeople)");
            if (session == null || settlements == null || !session.IsReady || session.Plan == null)
            {
                return;
            }
            if (session.Version != version)
            {
                StartOver();
            }
            Arrive();
            Camera view = Camera.main;
            if (view == null || Time.unscaledTime < nextLook)
            {
                return;
            }
            nextLook = Time.unscaledTime + 0.5f;
            Vector3 local = ToLocal(view.transform.position);
            var viewer = new Vector2(local.x, local.z);
            Viewer = viewer;
            leaving.Clear();
            foreach (KeyValuePair<int, List<Villager>> each in living)
            {
                SettlementSite site = settlements.BuiltSite(each.Key);
                bool home = site != null && SettlementSite.Beyond(site.Bounds, viewer) <= liveRange + leaveMargin;
                // GL670: workers out far from their town (lumberjacks) stay while the viewer is with them.
                if (!home && !NearPost(each.Key, viewer, liveRange + leaveMargin) && !AnyNear(each.Value, viewer, liveRange + leaveMargin))
                {
                    leaving.Add(each.Key);
                }
            }
            foreach (int index in leaving)
            {
                foreach (Villager villager in living[index])
                {
                    if (villager != null)
                    {
                        Destroy(villager.gameObject);
                    }
                }
                Living -= living[index].Count;
                living.Remove(index);
            }
            for (int i = 0; i < session.Plan.Settlements.Count; i++)
            {
                if (living.ContainsKey(i))
                {
                    continue;
                }
                SettlementSite site = settlements.BuiltSite(i);
                if (site != null && SettlementSite.Beyond(site.Bounds, viewer) <= liveRange)
                {
                    Settle(i, site);
                }
                else if (NearPost(i, viewer, liveRange))
                {
                    // A band out at its post (GL617): it is there for whoever comes by, though its camp is far and unbuilt.
                    Settle(i, settlements.SiteOf(i));
                }
            }
        }

        // True when any of these people stands within a distance of the viewer.
        private static bool AnyNear(List<Villager> people, Vector2 viewer, float within)
        {
            foreach (Villager villager in people)
            {
                if (villager != null && (new Vector2(villager.At.x, villager.At.z) - viewer).sqrMagnitude <= within * within)
                {
                    return true;
                }
            }
            return false;
        }

        // True when a settlement's people are out at one far place by their duty of the hour, and the viewer is near it.
        private bool NearPost(int settlement, Vector2 viewer, float within)
        {
            return Thieves != null && session.Plan.Settlements[settlement].Type.Band > 0 && Thieves.TryPost(settlement, out Vector2 post)
                && (post - viewer).sqrMagnitude <= within * within;
        }

        private void StartOver()
        {
            version = session.Version;
            if (root != null)
            {
                Destroy(root.gameObject);
            }
            root = new GameObject("People").transform;
            root.SetParent(View.transform, false);
            living.Clear();
            arriving.Clear();
            works.Clear();
            Living = 0;
            Thieves = FindAnyObjectByType<ThievesCamps>();
            heights = new KeptHeights(session.Map.Height);
            water = WorldRoads.WaterOf(session.Map.Water);
            clock = View.GetComponent<DayNightCycle>();
            if (clock == null)
            {
                clock = FindAnyObjectByType<DayNightCycle>();
            }
            Feller = FindAnyObjectByType<TreeFeller>();
            Herds = FindAnyObjectByType<AnimalHerds>();
            Land = new WorkLand(Plan, StartOf(), ParcelSize());
            Trees = View.Config.Terrain != null && View.Config.Terrain.Trees != null ? TreeMaterials.From(View.Config.Terrain.Trees) : null;
        }

        // Where the player starts (x, z, the world's own metres).
        private Vector2? StartOf()
        {
            Vector3 start = ToLocal(session.StartPosition);
            return new Vector2(start.x, start.z);
        }

        // The land parcels' width: the player's own, else the default eight chunks.
        private float ParcelSize()
        {
            PlayerParcels parcels = FindAnyObjectByType<PlayerParcels>();
            return (parcels != null ? parcels.SizeInChunks : 8) * View.World.Grid.ChunkWorldSize.x;
        }

        // GL730: the work done while nobody watched is applied by a component of its own, which goes with the people
        // (added here so MapGen's scene needs no change).
        private void Awake()
        {
            if (GetComponent<SettlementWorkings>() == null)
            {
                gameObject.AddComponent<SettlementWorkings>();
            }
        }

        // Puts a settlement's residents in the world, as many as the cap leaves room for.
        private void Settle(int index, SettlementSite site)
        {
            using FrameCost.Scope cost = FrameCost.Of("People: settle");
            List<Resident> residents = SettlementRoster.Build(site, occupations, session.Plan.Seed);
            var people = new List<Villager>();
            living[index] = people;
            if (residents.Count == 0 || material == null)
            {
                return;
            }
            var walks = new SettlementWalks(site);
            foreach (Resident resident in residents)
            {
                arriving.Enqueue(new Arrival { Settlement = index, Site = site, Walks = walks, Resident = resident, People = people });
            }
        }

        // GL771: a settlement's people come into the world a few a frame: all at once, their first look round took 80 ms.
        private void Arrive()
        {
            int arrived = 0;
            bool hurry = View.Hurry;
            while (arriving.Count > 0 && (hurry || arrived < ArrivalsPerFrame))
            {
                Arrival next = arriving.Dequeue();
                // Its settlement was left behind while it waited, or the map is full.
                if (!living.TryGetValue(next.Settlement, out List<Villager> people) || !ReferenceEquals(people, next.People) || Living >= mostPeople)
                {
                    continue;
                }
                Resident resident = next.Resident;
                var body = new GameObject($"{next.Site.Settlement.Name}: {resident.Occupation.Name} {resident.Index + 1}");
                body.transform.SetParent(root, false);
                Villager villager = body.AddComponent<Villager>();
                villager.Setup(this, next.Site, next.Walks, resident, material, people);
                if (resident.Occupation.Outlaw)
                {
                    body.AddComponent<Bandit>().Setup(villager, Thieves);
                }
                people.Add(villager);
                Living++;
                arrived++;
            }
        }
    }
}
