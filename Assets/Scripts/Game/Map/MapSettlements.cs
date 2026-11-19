using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Clube.Core;
using Unity.Profiling;
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

        private static readonly ProfilerMarker CollectMarker = new ProfilerMarker("MapSettlements.Collect");
        private static readonly ProfilerMarker WorkOutMarker = new ProfilerMarker("MapSettlements.WorkOut");
        private static readonly ProfilerMarker WriteMarker = new ProfilerMarker("MapSettlements.Write");
        private static readonly ProfilerMarker FitMarker = new ProfilerMarker("MapSettlements.Fit");
        private static readonly ProfilerMarker LayMarker = new ProfilerMarker("MapSettlements.Lay");
        private static readonly ProfilerMarker RoadSurfaceMarker = new ProfilerMarker("MapSettlements.RoadSurface");
        private static readonly ProfilerMarker WriterMarker = new ProfilerMarker("MapSettlements.MakeWriter");

        private readonly FrameAllowance allowance = new FrameAllowance();
        // The piece being written over several frames (GL771): one at a time, as what stands on it waits for it.
        private const float LeastWriteMilliseconds = 0.5f;
        private const float CollectMilliseconds = 1.5f;
        private const int AskEveryFrames = 8, PiecesAskedFor = 4;
        // A piece the player is about to walk on holds the ground there: it gets this long a frame (GL771).
        private const float CloseWriteMilliseconds = 6f;
        private bool writingClose;
        // The look over the settlements for pieces to write, and what it has found so far (GL771).
        private readonly List<(int Index, float Distance)> nextNear = new List<(int, float)>();
        private readonly List<(int Settlement, int Piece, float Distance)> nextPending = new List<(int, int, float)>();
        private SlicedWork collecting;
        private SlicedWork writing;
        private SettlementWriter writingSettlement;
        private int writingPiece;
        private int writingKind;
        private float writingMilliseconds;
        // Metres from the viewer within which a piece is written whatever the allowance says (GL770).
        private const float CloseMetres = 64f;
        // Metres from the viewer within which a piece's missing chunks are fetched ahead of the rest (GL770).
        private const float RequestMetres = 128f;
        private readonly List<Vector3Int> missing = new List<Vector3Int>();

        // GL729: the keepers' dispositions (DispositionBook.Save).
        private const string DispositionsKey = "shopdisposition.book";

        // Pieces looked at a frame for loaded chunks, and seconds until the pieces a write made ready are found.
        private const int ChecksPerFrame = 32;
        private const float RecollectSeconds = 0.2f;

        // Metres round a piece's ground that are held with it: a tree cut on a tile reaches over its edge.
        private const float HoldMargin = 2f;

        // Milliseconds a frame while the player waits behind the loading curtain (WorldView.Hurry).
        private const float HurryBudgetMilliseconds = 100f;

        [SerializeField]
        private MapSession session;

        [Tooltip("The low-poly vertex-colour material signs, wares and keepers are made of.")]
        [SerializeField]
        private Material material;

        [Tooltip("The material of the words on shop signs.")]
        [SerializeField]
        private Material textMaterial;

        [Tooltip("GL716: the flame of a settlement's torches and bonfires (Torch flame).")]
        [SerializeField]
        private Material flameMaterial;

        [Tooltip("GL716: the smoke over a settlement's torches and bonfires (Drying steam).")]
        [SerializeField]
        private Material smokeMaterial;

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

        [Tooltip("Milliseconds a frame they may take while the area round the viewer loads in bulk (at the start, after a long jump): the ground is held back until it is written (GL653).")]
        [SerializeField, Range(0.5f, 40f)]
        private float loadingWriteBudgetMilliseconds = 14f;

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
            public bool[] BridgeBuilt;
        }

        private readonly Dictionary<string, string> restored = new Dictionary<string, string>();
        private readonly List<(int Index, float Distance)> near = new List<(int, float)>();
        private readonly List<SettlementKeeper> keepers = new List<SettlementKeeper>();
        private readonly List<(int Settlement, int Piece, float Distance)> pending = new List<(int, int, float)>();
        private readonly List<int> found = new List<int>();
        // GL653: the ground still to write is not shown until it is.
        private readonly ColumnHoldSet hold = new ColumnHoldSet();
        private WorldView holding;
        private float[] reach;
        private bool fitDue;
        private Built[] built;
        private SettlementSite[] laid;
        private KeptHeights heights;
        private Transform root;
        private MapRoads roads;
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

        /// <summary>The low-poly vertex-colour material the map's signs and figures are made of.</summary>
        public Material LowPolyMaterial => material;

        /// <summary>Settlements laid out so far, pieces written, buildings standing and keepers at their shops.</summary>
        public int SettlementsWorkedOut { get; private set; }

        public int PiecesWritten { get; private set; }

        /// <summary>Chunk columns with a laid-out settlement's piece still to write: not shown until it is (GL653).</summary>
        public int HeldColumns { get; private set; }

        /// <summary>Pieces found near the viewer that can be written once their chunks have loaded.</summary>
        public int PendingPieces => pending.Count;

        public int ShopsFitted { get; private set; }
        /// <summary>GL716: settlement lights stood so far.</summary>
        public int LightsStood { get; private set; }

        /// <summary>Bridges stood where a settlement's lanes cross water (GL645).</summary>
        public int BridgesBuilt { get; private set; }

        public IReadOnlyList<SettlementKeeper> Keepers => keepers;

        /// <summary>GL729: how every keeper of the map feels about the player, by settlement and shop; saved.</summary>
        public DispositionBook Dispositions { get; } = new DispositionBook();

        /// <summary>The merchant price list every shop's prices come from.</summary>
        public PriceList Prices => prices;

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

        private void OnEnable()
        {
            // Inventory tooltips show what the merchants pay (GL366, GL660): on the bounded map the price list is this one.
            if (prices != null)
            {
                ItemSlotGui.Prices = prices;
            }
            hold.Rule = HoldsUnplanned;
            holding = session != null ? session.WorldView : null;
            if (holding != null)
            {
                holding.AddHold(hold);
            }
        }

        private void OnDisable()
        {
            if (holding != null)
            {
                holding.RemoveHold(hold);
            }
        }

        private void Update()
        {
            using FrameCost.Scope cost = FrameCost.Of("Settlements");
            hold.Enabled = build;
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
            float started = Time.realtimeSinceStartup;
            if (collecting == null && ((viewer - collectedAt).sqrMagnitude > 6f * 6f || Time.unscaledTime - collectedTime > 1f))
            {
                collecting = new SlicedWork(CollectSteps(viewer));
            }
            if (collecting != null)
            {
                using (CollectMarker.Auto())
                using (FrameCost.Of("Settlements: look for pieces"))
                {
                    if (collecting.Run(session.WorldView.Hurry ? float.PositiveInfinity : CollectMilliseconds))
                    {
                        collecting.Dispose();
                        collecting = null;
                    }
                }
            }
            // GL770: one piece can take several frames' budget: the frames after it pay that back.
            bool mayWrite = allowance.MayWork(writeBudgetMilliseconds, Time.frameCount) && FrameBudget.HasRoom;
            bool inSight = session.WorldView.IsLoadingInSight;
            // A piece close by holds the ground the player is about to walk on: it doesn't wait its turn.
            bool close = pending.Count > 0 && pending[0].Distance < CloseMetres;
            if (inSight || mayWrite || close || writing != null)
            {
                using (WriteMarker.Auto())
                {
                    Write();
                }
            }
            if (!inSight)
            {
                allowance.Spend((Time.realtimeSinceStartup - started) * 1000f);
            }
            HeldColumns = hold.Count;
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
            laid = new SettlementSite[built.Length];
            reach = new float[built.Length];
            for (int i = 0; i < reach.Length; i++)
            {
                reach[i] = ReachOf(session.Plan.Settlements[i]);
            }
            writing?.Dispose();
            writing = null;
            collecting?.Dispose();
            collecting = null;
            hold.Clear();
            pending.Clear();
            heights = new KeptHeights(session.Map.Height);
            // GL734: the tutorial's quarry keepers start at its own disposition.
            Dispositions.StartOf = session.Plan.StartingDisposition;
            roads = FindAnyObjectByType<MapRoads>();
            keepers.Clear();
            near.Clear();
            collectedTime = float.MinValue;
            clock = session.WorldView.GetComponent<DayNightCycle>();
            if (clock == null)
            {
                clock = FindAnyObjectByType<DayNightCycle>();
            }
            SettlementsWorkedOut = PiecesWritten = ShopsFitted = BridgesBuilt = LightsStood = 0;
        }

        /// <summary>
        /// GL718: a settlement as it is laid out on its ground, without making its writer or holding its ground (the far
        /// view's stand-ins are made from it); laid out on the first ask. Null before the map is ready.
        /// </summary>
        public SettlementSite LaidSite(int settlement)
        {
            if (session == null || !session.IsReady || session.Plan == null)
            {
                return null;
            }
            if (session.Version != version)
            {
                StartOver();
            }
            return Lay(settlement);
        }

        // Lays a settlement out on its ground, once.
        private SettlementSite Lay(int index)
        {
            using FrameCost.Scope cost = FrameCost.Of("Settlements: lay out");
            if (laid[index] != null)
            {
                return laid[index];
            }
            WorldPlan plan = session.Plan;
            PlannedSettlement settlement = plan.Settlements[index];
            BoundedMapGenerator map = session.Map;
            MapLayout layout = map.Layout;
            System.Func<float, float, RoadWater> water = WorldRoads.WaterOf(map.Water);
            float side = SettlementLayout.SideOf(settlement.Type, plan.Rules.ParcelSide);
            SettlementSite site;
            using (LayMarker.Auto())
            {
                site = SettlementLayout.Plan(settlement, plan.Rules.ParcelSide, rules, map.Height, WorldRoads.HeightsOf(map),
                    (x, z) => !layout.WalksFreely(x, z) || water(x, z) != RoadWater.Dry,
                    SettlementLayout.RoadsNear(plan, settlement, side + rules.Margin + 40f + (settlement.IsQuarry ? QuarryYard.Reach(rules) : 0f)),
                    session.WorldView.World.Grid.VoxelSize);
            }
            // GL649: its lanes and banks meet the roads' beds, not the land's height before the roads were cut.
            Rect round = site.Footprint;
            using (RoadSurfaceMarker.Auto())
            {
                site.RoadSurface = roads != null ? roads.SurfaceNear(Rect.MinMaxRect(round.xMin - 20f, round.yMin - 20f, round.xMax + 20f, round.yMax + 20f)) : null;
            }
            laid[index] = site;
            return site;
        }

        // Lays a settlement out on its ground, and makes its writer.
        private Built WorkOut(int index)
        {
            using FrameCost.Scope cost = FrameCost.Of("Settlements: work out");
            if (built[index] != null)
            {
                return built[index];
            }
            float started = Time.realtimeSinceStartup;
            BoundedMapGenerator map = session.Map;
            PlannedSettlement settlement = session.Plan.Settlements[index];
            SettlementSite site = Lay(index);
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
            Built made;
            using (WriterMarker.Auto())
            {
                made = new Built { Site = site, Writer = new SettlementWriter(site, street, climb, map.Water, TreeRoom.Of(session.WorldView.Config.Terrain.Trees)), Fitted = new bool[site.Buildings.Count] };
            }
            made.BridgeBuilt = new bool[made.Writer.Bridges.Count];
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
            WorldGrid grid = session.WorldView.World.Grid;
            for (int piece = 0; piece < made.Writer.Pieces; piece++)
            {
                if (!made.Writer.IsWritten(piece))
                {
                    hold.Add(grid, HoldOf(made.Writer, piece));
                }
            }
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
            // GL734: the quarry's parcels reach over its pit, to its wall.
            Rect area = site.Quarry != null ? site.Bounds : site.Footprint;
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

        // How far from the viewer pieces are written: at least the whole loaded area, so nothing loaded is left held.
        private float Range()
        {
            WorldView view = session.WorldView;
            return Mathf.Max(buildRange, view.RenderDistance * view.World.Grid.ChunkWorldSize.x + HoldMargin * 2f);
        }

        // Whether anything is built at a planned settlement.
        private static bool IsBuilt(PlannedSettlement settlement)
        {
            return settlement.Shops.Count + settlement.Population + settlement.Type.Band > 0;
        }

        // The circle round a settlement not laid out yet that nothing is shown in (GL653): its square's corners and margin.
        private float ReachOf(PlannedSettlement settlement)
        {
            return SettlementLayout.SideOf(settlement.Type, session.Plan.Rules.ParcelSide) * 0.71f + rules.Margin + HoldMargin
                + (settlement.IsQuarry ? QuarryYard.Reach(rules) : 0f);
        }

        // GL653: the columns a settlement will be written into before it is laid out; laid out, its pieces are counted.
        private bool HoldsUnplanned(Vector2Int column)
        {
            if (session == null || !session.IsReady || session.Plan == null)
            {
                return false;
            }
            if (session.Version != version || built == null)
            {
                return true;
            }
            float chunk = session.WorldView.World.Grid.ChunkWorldSize.x;
            var middle = new Vector2((column.x + 0.5f) * chunk, (column.y + 0.5f) * chunk);
            IReadOnlyList<PlannedSettlement> settlements = session.Plan.Settlements;
            for (int i = 0; i < built.Length; i++)
            {
                if (built[i] == null && IsBuilt(settlements[i]) && (middle - settlements[i].Position).sqrMagnitude <= reach[i] * reach[i])
                {
                    return true;
                }
            }
            return false;
        }

        private static Rect HoldOf(SettlementWriter writer, int piece)
        {
            Rect box = writer.Footprint(piece);
            return Rect.MinMaxRect(box.xMin - HoldMargin, box.yMin - HoldMargin, box.xMax + HoldMargin, box.yMax + HoldMargin);
        }

        // Counts every unwritten piece of the settlements laid out, afresh (after a restore marked some written).
        private void HoldUnwritten()
        {
            hold.Clear();
            if (built == null)
            {
                return;
            }
            WorldGrid grid = session.WorldView.World.Grid;
            foreach (Built made in built)
            {
                for (int piece = 0; made != null && piece < made.Writer.Pieces; piece++)
                {
                    if (!made.Writer.IsWritten(piece))
                    {
                        hold.Add(grid, HoldOf(made.Writer, piece));
                    }
                }
            }
        }

        // Finds the settlements near the viewer and their pieces that can be written, nearest first; one new one is laid out a frame.
        // GL771: a settlement at a time over several frames. What was found before goes on being written until the new
        // list is ready.
        private IEnumerator CollectSteps(Vector2 viewer)
        {
            collectedAt = viewer;
            collectedTime = Time.unscaledTime;
            nextNear.Clear();
            nextPending.Clear();
            WorldPlan plan = session.Plan;
            float range = Range();
            for (int i = 0; i < plan.Settlements.Count; i++)
            {
                PlannedSettlement settlement = plan.Settlements[i];
                float distance = Mathf.Max(0f, Vector2.Distance(viewer, settlement.Position) - reach[i]);
                if (distance > range || !IsBuilt(settlement))
                {
                    continue;
                }
                if (built[i] == null)
                {
                    using (WorkOutMarker.Auto())
                    {
                        WorkOut(i);
                    }
                    yield return null;
                }
                nextNear.Add((i, distance));
                SettlementWriter writer = built[i].Writer;
                found.Clear();
                writer.Ready(viewer, range, found);
                foreach (int piece in found)
                {
                    nextPending.Add((i, piece, SettlementSite.Beyond(writer.Footprint(piece), viewer)));
                }
                yield return null;
            }
            nextNear.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            nextPending.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            near.Clear();
            near.AddRange(nextNear);
            pending.Clear();
            pending.AddRange(nextPending);
            fitDue = true;
        }

        // GL771: the chunks a piece is missing are listed now and then; otherwise the check stops at the first one missing.
        private static bool AsksForChunks(float distance, int check)
        {
            return check <= PiecesAskedFor && distance < RequestMetres && Time.frameCount % AskEveryFrames == 0;
        }

        // Runs the piece in hand for this long (at least one step); true once no piece is left in hand.
        private bool RunWriting(float milliseconds)
        {
            float before = Time.realtimeSinceStartup;
            bool done;
            using (FrameCost.Of("Settlements: write piece"))
            {
                done = writing.Run(milliseconds);
            }
            writingMilliseconds += (Time.realtimeSinceStartup - before) * 1000f;
            if (!done)
            {
                return false;
            }
            writing.Dispose();
            writing = null;
            writingClose = false;
            hold.Remove(session.WorldView.World.Grid, HoldOf(writingSettlement, writingPiece));
            PiecesWritten++;
            LongestMilliseconds = Mathf.Max(LongestMilliseconds, writingMilliseconds);
            TotalMilliseconds += writingMilliseconds;
            kindCount[writingKind]++;
            kindMilliseconds[writingKind] += writingMilliseconds;
            kindLongest[writingKind] = Mathf.Max(kindLongest[writingKind], writingMilliseconds);
            return true;
        }

        private void Write()
        {
            if (near.Count == 0 && writing == null)
            {
                return;
            }
            WorldView view = session.WorldView;
            WorldConfig config = view.Config;
            World world = view.World;
            MaterialRegistry registry = config.Materials;
            var materials = new SettlementWriter.Materials
            {
                Bank = registry != null ? registry.Find(bankMaterial) : null,
                Stone = registry != null ? registry.Find("Stone") : null,
                Boards = registry != null ? registry.Find("Oak boards") : null,
                Trim = registry != null ? registry.Find("Maple boards") : null,
            };
            // While the area loads in bulk the ground is waiting to be shown: more of the frame goes to writing it.
            float budget = view.IsLoadingInSight ? Mathf.Max(writeBudgetMilliseconds, view.Hurry ? HurryBudgetMilliseconds : loadingWriteBudgetMilliseconds) : writeBudgetMilliseconds;
            budget = view.IsLoadingInSight ? budget
                : writingClose || (pending.Count > 0 && pending[0].Distance < CloseMetres) ? Mathf.Max(budget, CloseWriteMilliseconds)
                : Mathf.Max(LeastWriteMilliseconds, FrameBudget.Limit(budget));
            float started = Time.realtimeSinceStartup;
            bool wrote = false;
            // GL771: a large piece is written over several frames, and the next waits for it.
            if (writing != null)
            {
                if (!RunWriting(budget))
                {
                    return;
                }
                wrote = true;
            }
            int checks = 0;
            for (int i = 0; i < pending.Count && checks < ChecksPerFrame && (Time.realtimeSinceStartup - started) * 1000f < budget; i++)
            {
                (int Settlement, int Piece, float Distance) next = pending[i];
                SettlementWriter writer = built[next.Settlement].Writer;
                if (!writer.IsReady(next.Piece))
                {
                    pending.RemoveAt(i--);
                    continue;
                }
                checks++;
                missing.Clear();
                if (!writer.Loaded(world, config.WorldHeightInChunks, next.Piece, AsksForChunks(next.Distance, checks) ? missing : null))
                {
                    if (missing.Count > 0)
                    {
                        view.RequestChunks(missing);
                    }
                    continue;
                }
                writing = new SlicedWork(writer.WriteSteps(world, registry, materials, heights.At, config.IsoLevel, next.Piece));
                writingSettlement = writer;
                writingPiece = next.Piece;
                writingKind = writer.KindOf(next.Piece);
                writingClose = next.Distance < CloseMetres;
                writingMilliseconds = 0f;
                pending.RemoveAt(i--);
                if (!RunWriting(budget - (Time.realtimeSinceStartup - started) * 1000f))
                {
                    break;
                }
                wrote = true;
            }
            if (wrote)
            {
                // What was written lets the pieces standing on it be written: they are found soon, not a second on.
                collectedTime = Mathf.Min(collectedTime, Time.unscaledTime - 1f + RecollectSeconds);
            }
            if (wrote || fitDue)
            {
                fitDue = false;
                using (FitMarker.Auto())
                {
                    foreach ((int index, float _) in near)
                    {
                        using (FrameCost.Of("Settlements: fit buildings"))
                        {
                            Fit(index, built[index]);
                        }
                    }
                }
            }
        }

        // A lane's bridge over water (GL645) is built once the chunks under it have loaded, so its footings meet the ground.
        private void StandBridges(int index, Built made)
        {
            World world = session.WorldView.World;
            for (int i = 0; i < made.BridgeBuilt.Length; i++)
            {
                if (made.BridgeBuilt[i])
                {
                    continue;
                }
                RouteBridges.Span span = made.Writer.Bridges[i].Span;
                bool loaded = true;
                for (int k = 0; k <= 4 && loaded; k++)
                {
                    Vector2 p = Vector2.Lerp(span.From, span.To, k / 4f);
                    loaded = world.IsLoaded(world.Grid.WorldToChunk(new Vector3(p.x, span.HeightAt(k / 4f), p.y)));
                }
                if (loaded)
                {
                    made.BridgeBuilt[i] = true;
                    RoadBridge.Build(span, material, root, session.Plan.Seed + index * 613 + i, made.Writer.Bridges[i].Width);
                    BridgesBuilt++;
                }
            }
        }

        // A shop that stands in the terrain gets its sign, its wares and its keeper.
        private void Fit(int index, Built made)
        {
            if (material == null)
            {
                return;
            }
            StandBridges(index, made);
            PlannedSettlement settlement = made.Site.Settlement;
            for (int b = 0; b < made.Fitted.Length; b++)
            {
                SettlementBuilding building = made.Site.Buildings[b];
                if (made.Fitted[b] || !made.Writer.IsStanding(b))
                {
                    continue;
                }
                made.Fitted[b] = true;
                FitYard(building, $"{settlement.Name}: yard {b}");
                FitLights(made.Site, b, settlement.Name);
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
                keeper.Setup(specialty, prices, hours, clock, shop, material, seed, dailyCoins,
                    Dispositions.Of(index, settlement.Shops.IndexOf(building.Shop)));
                keepers.Add(keeper);
                ShopsFitted++;
            }
        }

        // GL713/GL714: a building's work yard: its fixtures (anvils, tool racks, spinning wheels) and a tarp over it.
        private void FitYard(SettlementBuilding building, string name)
        {
            BuildingDesign design = building.Design;
            if (design == null || (design.Fixtures.Count == 0 && !design.Tarp))
            {
                return;
            }
            var yard = new GameObject(name).transform;
            yard.SetParent(root, false);
            yard.localPosition = new Vector3(building.Centre.x, building.Pad + TownLayout.FloorHeight, building.Centre.y);
            yard.localRotation = Quaternion.Euler(0f, building.Yaw, 0f);
            foreach (YardFixture fixture in design.Fixtures)
            {
                Mesh mesh = YardFixtureMeshes.For(fixture.Kind);
                if (mesh != null)
                {
                    Place(yard, fixture.Kind, mesh, new Vector3(fixture.Local.x, 0f, fixture.Local.y), fixture.Yaw);
                }
            }
            if (design.Tarp)
            {
                Place(yard, "tarp", YardFixtureMeshes.Tarp(design.Width, design.YardDepth, design.StoryHeight - 0.1f, design.Depth * 0.5f), Vector3.zero, 0f);
            }
        }

        // GL716: the settlement's lights beside a building that now stands: torches, bonfires, lanterns.
        private void FitLights(SettlementSite site, int building, string settlementName)
        {
            foreach (SettlementLight light in site.Lights)
            {
                if (light.Building != building)
                {
                    continue;
                }
                var lamp = new GameObject($"{settlementName}: {light.Kind.Name}");
                lamp.transform.SetParent(root, false);
                lamp.transform.localPosition = new Vector3(light.Position.x, light.Ground, light.Position.y);
                lamp.transform.localRotation = Quaternion.Euler(0f, light.Yaw, 0f);
                lamp.AddComponent<SettlementLamp>().Setup(light.Kind, material, flameMaterial, smokeMaterial, clock);
                LightsStood++;
            }
        }

        private void Place(Transform parent, string name, Mesh mesh, Vector3 local, float yaw)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = local;
            part.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
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

        // Which pieces are written (PieceFlags), or an older save's "written/pieces" (the first so many): a settlement laid
        // out differently since (another count of pieces) starts again.
        private static void Restore(SettlementWriter writer, string saved)
        {
            string[] parts = saved.Split('/');
            if (parts.Length != 2)
            {
                writer.Restore(saved);
                return;
            }
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
            values[DispositionsKey] = Dispositions.Save();
            if (built == null)
            {
                return;
            }
            for (int i = 0; i < built.Length; i++)
            {
                if (built[i] != null && built[i].Writer.Written > 0)
                {
                    values[KeyPrefix + i] = built[i].Writer.Save();
                }
            }
        }

        public void RestoreValues(IReadOnlyDictionary<string, string> values)
        {
            restored.Clear();
            Dispositions.Restore(values.TryGetValue(DispositionsKey, out string book) ? book : null);
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
            HoldUnwritten();
        }
    }
}
