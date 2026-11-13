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

        private readonly List<Duty> duties = Duty.Defaults();
        private readonly List<Occupation> occupations = Occupation.Defaults();
        private readonly Dictionary<int, List<Villager>> living = new Dictionary<int, List<Villager>>();
        private readonly Dictionary<ToolDefinition, Mesh> toolMeshes = new Dictionary<ToolDefinition, Mesh>();
        private readonly List<int> leaving = new List<int>();
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
            height = 0f;
            World world = View.World;
            var ray = new Ray(new Vector3(x, near + 1.2f, z), Vector3.down);
            if (world == null || !world.Raycast(ray, View.Config.MeshSettings, 8f, out WorldHit hit))
            {
                return false;
            }
            height = hit.Point.y;
            return true;
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
                toolMeshes[tool] = mesh;
            }
            return mesh;
        }

        private void Update()
        {
            if (session == null || settlements == null || !session.IsReady || session.Plan == null)
            {
                return;
            }
            if (session.Version != version)
            {
                StartOver();
            }
            Camera view = Camera.main;
            if (view == null || Time.unscaledTime < nextLook)
            {
                return;
            }
            nextLook = Time.unscaledTime + 0.5f;
            Vector3 local = ToLocal(view.transform.position);
            var viewer = new Vector2(local.x, local.z);
            leaving.Clear();
            foreach (KeyValuePair<int, List<Villager>> each in living)
            {
                SettlementSite site = settlements.BuiltSite(each.Key);
                if (site == null || SettlementSite.Beyond(site.Bounds, viewer) > liveRange + leaveMargin)
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
            }
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
            Living = 0;
            heights = new KeptHeights(session.Map.Height);
            water = WorldRoads.WaterOf(session.Map.Water);
            clock = View.GetComponent<DayNightCycle>();
            if (clock == null)
            {
                clock = FindAnyObjectByType<DayNightCycle>();
            }
            Feller = FindAnyObjectByType<TreeFeller>();
            Herds = FindAnyObjectByType<AnimalHerds>();
            Trees = View.Config.Terrain != null && View.Config.Terrain.Trees != null ? TreeMaterials.From(View.Config.Terrain.Trees) : null;
        }

        // Puts a settlement's residents in the world, as many as the cap leaves room for.
        private void Settle(int index, SettlementSite site)
        {
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
                if (Living >= mostPeople)
                {
                    break;
                }
                var body = new GameObject($"{site.Settlement.Name}: {resident.Occupation.Name} {resident.Index + 1}");
                body.transform.SetParent(root, false);
                Villager villager = body.AddComponent<Villager>();
                villager.Setup(this, site, walks, resident, material);
                people.Add(villager);
                Living++;
            }
        }
    }
}
