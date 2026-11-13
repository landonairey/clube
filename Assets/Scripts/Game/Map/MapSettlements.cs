using System.Collections.Generic;
using System.Globalization;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Builds the map's settlements in the world (GL613, GL614, GL642): as the viewer nears one, it is laid out on its
    /// ground (<see cref="SettlementLayout"/>: terraces up a hillside, streets, climbs) and written into the terrain a
    /// piece at a time (<see cref="SettlementWriter"/>) once its chunks have loaded, within a few milliseconds a frame.
    /// Each shop that stands gets its sign, its wares on the counter and its keeper, trading its
    /// <see cref="EconomySpecialty"/>'s goods through the trade screen (<see cref="SettlementKeeper"/>). The land
    /// parcels under a settlement are not for sale. How far each settlement is written is saved, so a loaded game
    /// doesn't write it again over the player's digging.
    /// </summary>
    public class MapSettlements : MonoBehaviour, ISavedValues
    {
        private const string KeyPrefix = "mapsettlement.";

        [SerializeField]
        private MapSession session;

        [Tooltip("The low-poly vertex-colour material signs, wares and keepers are made of.")]
        [SerializeField]
        private Material material;

        [Tooltip("The material of the words on shop signs.")]
        [SerializeField]
        private Material textMaterial;

        [Tooltip("The one merchant price list every shop's prices come from.")]
        [SerializeField]
        private PriceList prices;

        [Tooltip("The game's items: a specialty trades the goods of its list that exist.")]
        [SerializeField]
        private ItemCatalog catalog;

        [SerializeField]
        private GameObject sleepingRollPrefab;

        [SerializeField]
        private ShopHours hours = ShopHours.Default;

        [Tooltip("Coins each keeper has to buy with each day.")]
        [SerializeField, Min(0f)]
        private float dailyCoins = 525f;

        [Tooltip("A settlement is built once the viewer is within this many metres of its edge.")]
        [SerializeField, Range(40f, 400f)]
        private float buildRange = 160f;

        [Tooltip("Milliseconds a frame the settlements may take to write.")]
        [SerializeField, Range(0.5f, 10f)]
        private float writeBudgetMilliseconds = 3f;

        [Tooltip("What the steep banks between terraces are faced with.")]
        [SerializeField]
        private string bankMaterial = "Loose stone";

        [Tooltip("Items a merchant offers beside its settlement's other shops.")]
        [SerializeField, Range(1, 16)]
        private int merchantVariety = 8;

        [SerializeField]
        private SettlementSiteRules rules = new SettlementSiteRules();

        [SerializeField]
        private bool build = true;

        // One settlement, laid out once the viewer comes near.
        private sealed class Built
        {
            public SettlementSite Site;
            public SettlementWriter Writer;
            public bool[] Fitted;
        }

        private readonly Dictionary<string, string> restored = new Dictionary<string, string>();
        private readonly List<(int Index, float Distance)> near = new List<(int, float)>();
        private readonly List<SettlementKeeper> keepers = new List<SettlementKeeper>();
        private Built[] built;
        private KeptHeights heights;
        private Transform root;
        private DayNightCycle clock;
        private int version = -1;
        private Vector2 collectedAt;
        private float collectedTime = float.MinValue;

        /// <summary>Whether settlements are being built (off: they stay marks on the map).</summary>
        public bool Build
        {
            get => build;
            set => build = value;
        }

        public SettlementSiteRules Rules => rules;

        /// <summary>Settlements laid out so far, pieces written, buildings standing and keepers at their shops.</summary>
        public int SettlementsWorkedOut { get; private set; }

        public int PiecesWritten { get; private set; }

        public int ShopsFitted { get; private set; }

        public IReadOnlyList<SettlementKeeper> Keepers => keepers;

        /// <summary>The longest a settlement took to lay out, the longest piece to write, and all pieces together, in milliseconds.</summary>
        public float LongestWorkOutMilliseconds { get; private set; }

        public float LongestMilliseconds { get; private set; }

        public float TotalMilliseconds { get; private set; }

        /// <summary>Pieces written and the milliseconds they took, by kind: tiles of ground, metres of lane, slices of buildings.</summary>
        public string Timings => $"ground {kindCount[0]} in {kindMilliseconds[0]:0} ms (longest {kindLongest[0]:0.0}), lanes {kindCount[1]} in {kindMilliseconds[1]:0} ms "
            + $"(longest {kindLongest[1]:0.0}), buildings {kindCount[2]} slices in {kindMilliseconds[2]:0} ms (longest {kindLongest[2]:0.0})";

        private readonly int[] kindCount = new int[3];
        private readonly float[] kindMilliseconds = new float[3], kindLongest = new float[3];

        /// <summary>A settlement as it is laid out on its ground; worked out on the first ask. Null before the map is ready.</summary>
        public SettlementSite SiteOf(int settlement)
        {
            if (session == null || !session.IsReady || session.Plan == null)
            {
                return null;
            }
            if (session.Version != version)
            {
                StartOver();
            }
            return WorkOut(settlement).Site;
        }

        /// <summary>A settlement once all of it stands in the terrain (GL615: its people move in then); null before, and it is not laid out by asking.</summary>
        public SettlementSite BuiltSite(int settlement)
        {
            Built made = built != null && version == session.Version && settlement < built.Length ? built[settlement] : null;
            return made != null && made.Writer.Done ? made.Site : null;
        }

        /// <summary>How many pieces a settlement is written in, and how many are written; (0, 0) until it is laid out.</summary>
        public Vector2Int Progress(int settlement)
        {
            Built made = built != null && settlement < built.Length ? built[settlement] : null;
            return made == null ? Vector2Int.zero : new Vector2Int(made.Writer.Written, made.Writer.Pieces);
        }

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
            if (!build || view == null)
            {
                return;
            }
            Vector3 local = session.WorldView.transform.InverseTransformPoint(view.transform.position);
            var viewer = new Vector2(local.x, local.z);
            if ((viewer - collectedAt).sqrMagnitude > 6f * 6f || Time.unscaledTime - collectedTime > 1f)
            {
                Collect(viewer);
            }
            Write();
        }

        private void StartOver()
        {
            version = session.Version;
            if (root != null)
            {
                Destroy(root.gameObject);
            }
            root = new GameObject("Settlements").transform;
            root.SetParent(session.WorldView.transform, false);
            built = new Built[session.Plan.Settlements.Count];
            heights = new KeptHeights(session.Map.Height);
            keepers.Clear();
            near.Clear();
            collectedTime = float.MinValue;
            clock = session.WorldView.GetComponent<DayNightCycle>();
            if (clock == null)
            {
                clock = FindAnyObjectByType<DayNightCycle>();
            }
            SettlementsWorkedOut = PiecesWritten = ShopsFitted = 0;
        }

        // Lays a settlement out on its ground, and makes its writer.
        private Built WorkOut(int index)
        {
            if (built[index] != null)
            {
                return built[index];
            }
            float started = Time.realtimeSinceStartup;
            WorldPlan plan = session.Plan;
            PlannedSettlement settlement = plan.Settlements[index];
            BoundedMapGenerator map = session.Map;
            MapLayout layout = map.Layout;
            System.Func<float, float, RoadWater> water = WorldRoads.WaterOf(map.Water);
            float side = SettlementLayout.SideOf(settlement.Type, plan.Rules.ParcelSide);
            SettlementSite site = SettlementLayout.Plan(settlement, plan.Rules.ParcelSide, rules, map.Height, WorldRoads.HeightsOf(map),
                (x, z) => !layout.WalksFreely(x, z) || water(x, z) != RoadWater.Dry,
                SettlementLayout.RoadsNear(plan, settlement, side + rules.Margin + 40f), session.WorldView.World.Grid.VoxelSize);
            var street = new RoadClassDefinition
            {
                Class = RoadClass.Footpath, Name = "Street", Width = rules.StreetWidth, Surface = settlement.Type.Street,
                GradeLength = 8f, MaxCut = 8f, Shoulder = 0.75f, EdgeWander = 0f,
            };
            var climb = new RoadClassDefinition
            {
                Class = RoadClass.Footpath, Name = "Climb", Width = rules.StreetWidth * 0.8f, Surface = settlement.Type.Street,
                GradeLength = 14f, MaxCut = 8f, Shoulder = 0.75f, EdgeWander = 0f,
            };
            var made = new Built { Site = site, Writer = new SettlementWriter(site, street, climb), Fitted = new bool[site.Buildings.Count] };
            if (restored.TryGetValue(KeyPrefix + index, out string saved))
            {
                Restore(made.Writer, saved);
            }
            foreach (string note in site.Notes)
            {
                UnityEngine.Debug.Log($"Map settlements: {note}", this);
            }
            LockParcels(site);
            built[index] = made;
            SettlementsWorkedOut++;
            LongestWorkOutMilliseconds = Mathf.Max(LongestWorkOutMilliseconds, (Time.realtimeSinceStartup - started) * 1000f);
            return made;
        }

        // The land parcels a settlement stands on are its own: not for sale.
        private void LockParcels(SettlementSite site)
        {
            PlayerParcels player = FindAnyObjectByType<PlayerParcels>();
            LandParcels parcels = player != null ? player.Parcels : null;
            if (parcels == null || site.Buildings.Count == 0)
            {
                return;
            }
            Rect area = site.Footprint;
            for (float z = area.yMin; z < area.yMax + parcels.Size; z += parcels.Size)
            {
                for (float x = area.xMin; x < area.xMax + parcels.Size; x += parcels.Size)
                {
                    var at = new Vector3(Mathf.Min(x, area.xMax), 0f, Mathf.Min(z, area.yMax));
                    Vector2Int parcel = parcels.ParcelAt(at);
                    if (!parcels.IsOwned(parcel))
                    {
                        parcels.Lock(parcel);
                    }
                }
            }
        }

        // Finds the settlements near the viewer, nearest first; one new one is laid out a frame.
        private void Collect(Vector2 viewer)
        {
            collectedAt = viewer;
            collectedTime = Time.unscaledTime;
            near.Clear();
            WorldPlan plan = session.Plan;
            bool workedOutOne = false;
            for (int i = 0; i < plan.Settlements.Count; i++)
            {
                PlannedSettlement settlement = plan.Settlements[i];
                float half = SettlementLayout.SideOf(settlement.Type, plan.Rules.ParcelSide) * 0.5f;
                float distance = Mathf.Max(0f, Vector2.Distance(viewer, settlement.Position) - half);
                if (distance > buildRange || settlement.Shops.Count + settlement.Population + settlement.Type.Band == 0)
                {
                    continue;
                }
                if (built[i] == null)
                {
                    if (workedOutOne)
                    {
                        collectedTime = float.MinValue;
                        continue;
                    }
                    workedOutOne = true;
                    WorkOut(i);
                }
                near.Add((i, distance));
            }
            near.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        }

        private void Write()
        {
            if (near.Count == 0)
            {
                return;
            }
            WorldConfig config = session.WorldView.Config;
            World world = session.WorldView.World;
            MaterialRegistry registry = config.Materials;
            var materials = new SettlementWriter.Materials
            {
                Bank = registry != null ? registry.Find(bankMaterial) : null,
                Stone = registry != null ? registry.Find("Stone") : null,
                Boards = registry != null ? registry.Find("Oak boards") : null,
                Trim = registry != null ? registry.Find("Maple boards") : null,
            };
            float started = Time.realtimeSinceStartup;
            foreach ((int index, float _) in near)
            {
                Built made = built[index];
                while (!made.Writer.Done && made.Writer.NextLoaded(world, config.WorldHeightInChunks))
                {
                    float before = Time.realtimeSinceStartup;
                    int kind = made.Writer.NextKind;
                    made.Writer.WriteNext(world, registry, materials, heights.At, config.IsoLevel);
                    PiecesWritten++;
                    float took = (Time.realtimeSinceStartup - before) * 1000f;
                    LongestMilliseconds = Mathf.Max(LongestMilliseconds, took);
                    TotalMilliseconds += took;
                    kindCount[kind]++;
                    kindMilliseconds[kind] += took;
                    kindLongest[kind] = Mathf.Max(kindLongest[kind], took);
                    if ((Time.realtimeSinceStartup - started) * 1000f >= writeBudgetMilliseconds)
                    {
                        Fit(index, made);
                        return;
                    }
                }
                Fit(index, made);
            }
        }

        // A shop that stands in the terrain gets its sign, its wares and its keeper.
        private void Fit(int index, Built made)
        {
            if (material == null)
            {
                return;
            }
            PlannedSettlement settlement = made.Site.Settlement;
            for (int b = 0; b < made.Fitted.Length; b++)
            {
                SettlementBuilding building = made.Site.Buildings[b];
                if (made.Fitted[b] || !made.Writer.IsStanding(b))
                {
                    continue;
                }
                made.Fitted[b] = true;
                if (building.Kind != SettlementBuildingKind.Shop || building.Shop == null)
                {
                    continue;
                }
                int seed = session.Plan.Seed + index * 977 + b * 31;
                ShopSpecialty specialty = ShopOf(settlement, building.Shop, seed);
                var shop = new GameObject($"{settlement.Name}: {specialty.ShopName}").transform;
                shop.SetParent(root, false);
                shop.localPosition = new Vector3(building.Centre.x, building.Pad + TownLayout.FloorHeight, building.Centre.y);
                shop.localRotation = Quaternion.Euler(0f, building.Yaw, 0f);
                TownBuilder.FitShop(shop, specialty, material, textMaterial, sleepingRollPrefab, seed);
                var keeperObject = new GameObject($"{specialty.ShopName} keeper");
                keeperObject.transform.SetParent(shop, false);
                SettlementKeeper keeper = keeperObject.AddComponent<SettlementKeeper>();
                keeper.Setup(specialty, prices, hours, clock, shop, material, seed, dailyCoins);
                keepers.Add(keeper);
                ShopsFitted++;
            }
        }

        // The shop a planned shop trades as (GL614): its specialty's goods that exist; a merchant's variety leans to what
        // the settlement's other shops don't sell.
        private ShopSpecialty ShopOf(PlannedSettlement settlement, PlannedShop planned, int seed)
        {
            List<ItemDefinition> variety = null;
            if (planned.Specialty.SellsVariety && catalog != null)
            {
                var others = new List<EconomySpecialty>();
                foreach (PlannedShop other in settlement.Shops)
                {
                    if (other != planned)
                    {
                        others.Add(other.Specialty);
                    }
                }
                variety = EconomySpecialty.VarietyFor(catalog, others, seed, merchantVariety);
            }
            return planned.Specialty.ToShop(catalog, variety);
        }

        // "written/pieces": a settlement laid out differently since (another count of pieces) starts again.
        private static void Restore(SettlementWriter writer, string saved)
        {
            string[] parts = saved.Split('/');
            if (parts.Length == 2 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int written)
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int pieces) && pieces == writer.Pieces)
            {
                writer.Restore(written);
            }
        }

        public void SaveValues(IDictionary<string, string> values)
        {
            // Settlements not laid out this session keep what an earlier one saved.
            foreach (KeyValuePair<string, string> pair in restored)
            {
                values[pair.Key] = pair.Value;
            }
            if (built == null)
            {
                return;
            }
            for (int i = 0; i < built.Length; i++)
            {
                if (built[i] != null && built[i].Writer.Written > 0)
                {
                    values[KeyPrefix + i] = string.Format(CultureInfo.InvariantCulture, "{0}/{1}", built[i].Writer.Written, built[i].Writer.Pieces);
                }
            }
        }

        public void RestoreValues(IReadOnlyDictionary<string, string> values)
        {
            restored.Clear();
            foreach (KeyValuePair<string, string> pair in values)
            {
                if (pair.Key.StartsWith(KeyPrefix, System.StringComparison.Ordinal))
                {
                    restored[pair.Key] = pair.Value;
                }
            }
            if (built == null)
            {
                return;
            }
            for (int i = 0; i < built.Length; i++)
            {
                if (built[i] != null && restored.TryGetValue(KeyPrefix + i, out string saved))
                {
                    Restore(built[i].Writer, saved);
                }
            }
        }
    }
}
