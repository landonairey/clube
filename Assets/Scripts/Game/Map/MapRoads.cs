using System.Collections;
using System.Collections.Generic;
using Clube.Core;
using Unity.Profiling;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Builds the map's roads in the world (GL613, GL631-GL634): round the viewer, each road of the
    /// <see cref="WorldPlan"/> is written into the terrain a piece at a time (<see cref="RoadWriter"/>: its class's
    /// width, bed and surface) once its chunks have loaded, within a few milliseconds a frame; its bridges
    /// (<see cref="RoadBridge"/>) and the signposts (<see cref="Signpost"/>) are built as their ground loads. A road is
    /// only worked out in detail (its heights every metre, its bed, its bridges) when the viewer first comes near it.
    /// Which pieces are written is saved, so a loaded game doesn't write them again over the player's digging.
    /// </summary>
    public class MapRoads : MonoBehaviour, ISavedValues
    {
        private const string KeyPrefix = "maproad.";

        private static readonly ProfilerMarker CollectMarker = new ProfilerMarker("MapRoads.Collect");
        private static readonly ProfilerMarker WriteMarker = new ProfilerMarker("MapRoads.Write");

        // Seconds between looks for pieces whose chunks have loaded, while none are ready (GL759).
        private const float IdleLookSeconds = 0.2f;
        private float nextLookTime;
        private readonly FrameAllowance allowance = new FrameAllowance();
        // The piece being written over several frames (GL771): one at a time, as each cuts down the trees in its way first.
        private const float LeastWriteMilliseconds = 0.5f;
        private const float CollectMilliseconds = 1.5f;
        private const int AskEveryFrames = 8;
        // The look over the roads for pieces to write, and what it has found so far (GL771).
        private readonly List<(int Road, int Piece, float Distance)> nextPending = new List<(int, int, float)>();
        private readonly List<(int Road, int Tunnel, int Piece, float Distance)> nextBoring = new List<(int, int, int, float)>();
        private SlicedWork collecting;
        private SlicedWork writing;
        private RoadWriter writingRoad;
        private int writingPiece;
        private float writingMilliseconds;
        // Metres from the viewer within which a piece is written whatever the allowance says (GL770).
        private const float CloseMetres = 64f;
        // Metres from the viewer within which a piece's missing chunks are fetched ahead of the rest (GL770).
        private const float RequestMetres = 128f;
        private readonly List<Vector3Int> missing = new List<Vector3Int>();

        // Pieces looked at each frame for whether their chunks have loaded.
        private const int ChecksPerFrame = 32;

        // A tunnel is this much wider than its road, and this high at the middle of its arch (GL642).
        private const float TunnelExtraWidth = 1f;
        private const float TunnelHeight = 3.5f;
        private const float LongDeck = 40f;

        // Metres a parcel's fence keeps clear of a road's edge.
        private const float FenceGap = 0.75f;

        [SerializeField]
        private MapSession session;

        [Tooltip("The low-poly vertex-colour material bridges and signposts are made of.")]
        [SerializeField]
        private Material material;

        [Tooltip("The material of the words on signposts (hidden behind things, unlike the built-in text shader).")]
        [SerializeField]
        private Material textMaterial;

        [Tooltip("Roads, bridges and signposts are built within this many metres of the viewer.")]
        [SerializeField, Range(40f, 400f)]
        private float buildRange = 150f;

        [Tooltip("Milliseconds a frame the roads may take to write.")]
        [SerializeField, Range(0.5f, 10f)]
        private float writeBudgetMilliseconds = 3f;

        [Tooltip("Milliseconds a frame they may take while the area round the viewer loads in bulk (at the start, after a long jump): the ground is held back until it is written (GL653).")]
        [SerializeField, Range(0.5f, 40f)]
        private float loadingWriteBudgetMilliseconds = 8f;

        // One road, worked out once the viewer comes near.
        private sealed class Built
        {
            public RoadLine Line;
            public RoadWriter Writer;
            public List<RouteBridges.Span> Bridges;
            public bool[] BridgeBuilt;
            public List<TunnelWriter> Tunnels = new List<TunnelWriter>();
            public bool[] PortalBuilt;
        }

        private readonly List<(int Road, int Piece, float Distance)> pending = new List<(int, int, float)>();
        private readonly List<int> found = new List<int>();
        private readonly List<(int Road, int Tunnel, int Piece, float Distance)> boring = new List<(int, int, int, float)>();
        private KeptHeights heights;
        private readonly Dictionary<string, string> restored = new Dictionary<string, string>();
        private Rect[] bounds;
        private Built[] built;
        private bool[] postBuilt;
        private Transform root;
        private readonly List<VesselClass> vessels = VesselClass.Defaults();
        private int version = -1;
        private Vector2 collectedAt;
        private float collectedTime = float.MinValue;

        // GL653: the ground a road is still to be written into is not shown until it is. A road not worked out holds
        // the box round its line, widened by what its bed may reach either side.
        private const float HoldMargin = 2f;
        private const float UnworkedReach = 14f;

        // Milliseconds a frame while the player waits behind the loading curtain (WorldView.Hurry).
        private const float HurryBudgetMilliseconds = 50f;
        private readonly ColumnHoldSet hold = new ColumnHoldSet();
        private WorldView holding;

        /// <summary>Chunk columns with a worked-out road's piece still to write: not shown until it is (GL653).</summary>
        public int HeldColumns => hold.Count;

        /// <summary>Pieces found near the viewer and not written yet (their chunks may still be loading).</summary>
        public int PendingPieces => pending.Count;

        private void OnEnable()
        {
            // GL728: the horse-hire stands by the capital's and cities' name boards; no scene needs editing.
            if (!TryGetComponent(out MapHorseHire hire))
            {
                hire = gameObject.AddComponent<MapHorseHire>();
            }
            hire.Setup(session, material);
            hold.Rule = HoldsUnworked;
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

        // How far from the viewer pieces are written: at least the whole loaded area, so nothing loaded is left held.
        private float Range()
        {
            WorldView view = session.WorldView;
            return Mathf.Max(buildRange, view.RenderDistance * view.World.Grid.ChunkWorldSize.x + UnworkedReach + 4f);
        }

        private bool HoldsUnworked(Vector2Int column)
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
            float x = (column.x + 0.5f) * chunk, z = (column.y + 0.5f) * chunk, reach = UnworkedReach + chunk * 0.5f;
            for (int i = 0; i < built.Length; i++)
            {
                Rect box = bounds[i];
                if (built[i] == null && x >= box.xMin - reach && x <= box.xMax + reach && z >= box.yMin - reach && z <= box.yMax + reach
                    && session.Plan.Roads[i].Points.Count >= 2)
                {
                    return true;
                }
            }
            return false;
        }

        private static Rect HoldOf(RoadWriter writer, int piece)
        {
            Rect box = writer.Footprint(piece);
            return Rect.MinMaxRect(box.xMin - HoldMargin, box.yMin - HoldMargin, box.xMax + HoldMargin, box.yMax + HoldMargin);
        }

        // Counts a road's unwritten pieces; all the roads' afresh after a restore marked some written.
        private void HoldUnwritten(Built road)
        {
            WorldGrid grid = session.WorldView.World.Grid;
            for (int piece = 0; piece < road.Writer.Pieces; piece++)
            {
                if (!road.Writer.IsWritten(piece))
                {
                    hold.Add(grid, HoldOf(road.Writer, piece));
                }
            }
        }

        /// <summary>Whether roads are being built (off: the plan stays lines on the map).</summary>
        public bool Build
        {
            get => build;
            set => build = value;
        }

        [SerializeField]
        private bool build = true;

        /// <summary>Roads worked out in detail so far, pieces written, bridges and signposts standing.</summary>
        public int RoadsWorkedOut { get; private set; }

        public int PiecesWritten { get; private set; }

        public int BridgesBuilt { get; private set; }

        public int SignpostsBuilt { get; private set; }
        /// <summary>GL717: settlements' hanging name boards standing.</summary>
        public int SignboardsBuilt { get; private set; }

        /// <summary>Metres of tunnel bored and portals standing (GL642).</summary>
        public int TunnelPiecesBored { get; private set; }

        public int PortalsBuilt { get; private set; }

        /// <summary>Pieces near the viewer still waiting for their chunks.</summary>
        public int PiecesWaiting => pending.Count;

        /// <summary>A road as it runs on the ground, with its bridges, once it has been worked out (the viewer came near it); false before (GL616: carts ride it).</summary>
        public bool TryLine(int road, out RoadLine line, out IReadOnlyList<RouteBridges.Span> bridges)
        {
            Built made = built != null && session != null && version == session.Version && road >= 0 && road < built.Length ? built[road] : null;
            line = made?.Line;
            bridges = made?.Bridges;
            return made != null && made.Line != null;
        }

        /// <summary>
        /// GL649: the height the roads set the ground to round an area, as (x, z, the land's height) to a height: a road's
        /// bed on it and the slope of its shoulder beside it; the land's own height elsewhere. Null when no road comes
        /// near. Works out the roads that do.
        /// </summary>
        public System.Func<float, float, float, float> SurfaceNear(Rect area)
        {
            if (session == null || !session.IsReady || session.Plan == null)
            {
                return null;
            }
            if (session.Version != version)
            {
                StartOver();
            }
            var beds = new List<(RoadBed Bed, List<Vector2Int> Stretches)>();
            for (int i = 0; i < bounds.Length; i++)
            {
                if (!bounds[i].Overlaps(area))
                {
                    continue;
                }
                Built road = WorkOut(i);
                if (road == null || road.Writer.Bed.FollowsGround)
                {
                    continue;
                }
                List<Vector2Int> stretches = road.Writer.Bed.StretchesNear(area, road.Writer.Bed.Reach);
                if (stretches.Count > 0)
                {
                    beds.Add((road.Writer.Bed, stretches));
                }
            }
            if (beds.Count == 0)
            {
                return null;
            }
            return (x, z, ground) =>
            {
                var point = new Vector2(x, z);
                foreach ((RoadBed bed, List<Vector2Int> stretches) in beds)
                {
                    if (bed.IsBed(point, stretches))
                    {
                        return bed.Surface(point, ground, stretches);
                    }
                }
                return ground;
            };
        }

        /// <summary>The longest a road took to work out (its line, heights, bridges and bed), in milliseconds: once per road, as the viewer nears it.</summary>
        public float LongestWorkOutMilliseconds { get; private set; }

        /// <summary>Milliseconds the longest piece took to write, and all of them together.</summary>
        public float LongestMilliseconds { get; private set; }

        public float TotalMilliseconds { get; private set; }

        private void Update()
        {
            using FrameCost.Scope cost = FrameCost.Of("Roads");
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
                using (FrameCost.Of("Roads: look for pieces"))
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
            bool close = pending.Count > 0 && pending[0].Distance < CloseMetres * CloseMetres;
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
        }

        private void StartOver()
        {
            version = session.Version;
            if (root != null)
            {
                Destroy(root.gameObject);
            }
            root = new GameObject("Roads").transform;
            root.SetParent(session.WorldView.transform, false);
            WorldPlan plan = session.Plan;
            bounds = new Rect[plan.Roads.Count];
            built = new Built[plan.Roads.Count];
            postBuilt = new bool[plan.Signposts.Count];
            for (int i = 0; i < bounds.Length; i++)
            {
                List<Vector2> points = plan.Roads[i].Points;
                Vector2 min = points.Count > 0 ? points[0] : Vector2.zero, max = min;
                foreach (Vector2 p in points)
                {
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }
                bounds[i] = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }
            writing?.Dispose();
            writing = null;
            collecting?.Dispose();
            collecting = null;
            pending.Clear();
            hold.Clear();
            heights = new KeptHeights(session.Map.Height);
            // A parcel's fence leaves a gap where a road crosses its border.
            ParcelBorders borders = FindAnyObjectByType<ParcelBorders>();
            if (borders != null)
            {
                borders.SetOpenings(point => plan.IsOnRoad(point, FenceGap));
            }
            collectedTime = float.MinValue;
            boring.Clear();
            RoadsWorkedOut = PiecesWritten = BridgesBuilt = SignpostsBuilt = SignboardsBuilt = TunnelPiecesBored = PortalsBuilt = 0;
        }

        // The ground as generated, kept as it is asked for: road pieces ask for the same square metres over and over.
        private float Natural(float x, float z)
        {
            return heights.At(x, z);
        }

        // Works out a road in detail: its line with heights, its bridges, its bed and its writer.
        private Built WorkOut(int index)
        {
            using FrameCost.Scope cost = FrameCost.Of("Roads: work out");
            if (built[index] != null)
            {
                return built[index];
            }
            WorldPlan plan = session.Plan;
            PlannedRoad road = plan.Roads[index];
            if (road.Points.Count < 2)
            {
                return null;
            }
            RoadClassDefinition definition = plan.Rules.Road(road.Class);
            // The line keeps its own heights, so it asks the generator itself rather than filling the store with a road's length of them.
            float started = Time.realtimeSinceStartup;
            // In a tunnel (GL642) the line runs on the tunnel's floor, so the bed leads into it.
            // GL737: on a graded stretch it runs on the planned bed (cut-and-fill), so the bed is built as the plan priced it.
            var line = new RoadLine(road.Points, RoadGrading.HeightAt(road, session.Map.Height), 1f,
                RoadGrading.HeightsAt(road, WorldRoads.HeightsOf(session.Map)));
            // GL651: over the sea the deck stands high enough for what sails under; a ravine gets a dry span.
            List<RouteBridges.Span> bridges = RouteBridges.Find(line, session.Map.Water, true, BridgeClearance.For(plan, vessels));
            bridges.AddRange(RouteBridges.Ravines(line, bridges));
            bridges.Sort((a, b) => a.Start.CompareTo(b.Start));
            // Where it ends on another road, its bed meets that road's bed.
            float endHeight = float.NaN;
            Built joinedRoad = null;
            if (road.To < 0 && road.Joins >= 0 && road.Joins < index)
            {
                Built joined = WorkOut(road.Joins);
                joinedRoad = joined;
                if (joined != null)
                {
                    joined.Line.DistanceTo(line.Flat(line.Length), out float along);
                    endHeight = joined.Writer.Bed.HeightAlong(along);
                }
            }
            var bed = new RoadBed(line, definition, bridges, plan.Seed + index, endHeight) { WetAt = SettlementWriter.WetOf(session.Map.Water) };
            var made = new Built { Line = line, Writer = new RoadWriter(bed) { Trees = TreeRoom.Of(session.WorldView.Config.Terrain.Trees) }, Bridges = bridges, BridgeBuilt = new bool[bridges.Count] };
            if (joinedRoad != null)
            {
                // Its last metres lie on the joined road's own surface: that road keeps it. A piece is left out only
                // when its middle lies well inside that road's width, so the two surfaces meet and no strip of ground is
                // left between them.
                RoadBed other = joinedRoad.Writer.Bed;
                for (int piece = made.Writer.Pieces - 1; piece >= 0; piece--)
                {
                    float distance = joinedRoad.Line.DistanceTo(made.Writer.Middle(piece), out float at);
                    if (distance > other.HalfWidthAt(at) - RoadWriter.PieceLength * 0.5f)
                    {
                        break;
                    }
                    made.Writer.Skip(piece);
                }
            }
            // Its tunnels: the bore is carved by a writer of its own, and the road leaves the ground over it alone.
            foreach (RoadTunnel tunnel in road.Tunnels)
            {
                var bore = new TunnelWriter(tunnel, definition.Width + TunnelExtraWidth, TunnelHeight, definition.Surface, session.Map.Height);
                if (restored.TryGetValue(KeyPrefix + index + ".t" + made.Tunnels.Count, out string bored))
                {
                    bore.Restore(bored);
                }
                made.Tunnels.Add(bore);
                for (int piece = 0; piece < made.Writer.Pieces; piece++)
                {
                    if (bore.IsBored(made.Writer.Middle(piece)))
                    {
                        made.Writer.Skip(piece);
                    }
                }
            }
            made.PortalBuilt = new bool[made.Tunnels.Count * 2];
            if (restored.TryGetValue(KeyPrefix + index, out string saved))
            {
                made.Writer.Restore(saved);
            }
            built[index] = made;
            HoldUnwritten(made);
            RoadsWorkedOut++;
            LongestWorkOutMilliseconds = Mathf.Max(LongestWorkOutMilliseconds, (Time.realtimeSinceStartup - started) * 1000f);
            return made;
        }

        // Finds what there is to do round the viewer: pieces to write (nearest first), bridges and signposts to stand.
        // GL771: a road at a time over several frames: looking over them all was over 16 ms about once a second. What was
        // found before goes on being written until the new list is ready.
        private IEnumerator CollectSteps(Vector2 viewer)
        {
            collectedAt = viewer;
            collectedTime = Time.unscaledTime;
            nextPending.Clear();
            nextBoring.Clear();
            World world = session.WorldView.World;
            float buildRange = Range();
            for (int i = 0; i < bounds.Length; i++)
            {
                Rect box = bounds[i];
                if (viewer.x < box.xMin - buildRange || viewer.x > box.xMax + buildRange || viewer.y < box.yMin - buildRange || viewer.y > box.yMax + buildRange)
                {
                    continue;
                }
                // A road's heights are a few tens of milliseconds: a step of their own.
                bool isNew = built[i] == null;
                Built road = WorkOut(i);
                if (isNew)
                {
                    yield return null;
                }
                if (road == null)
                {
                    continue;
                }
                found.Clear();
                road.Writer.Unwritten(viewer, buildRange, found);
                foreach (int piece in found)
                {
                    nextPending.Add((i, piece, (road.Writer.Middle(piece) - viewer).sqrMagnitude));
                }
                BuildBridges(i, road, viewer, world);
                CollectTunnels(i, road, viewer, world);
                yield return null;
            }
            nextPending.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            nextBoring.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            pending.Clear();
            pending.AddRange(nextPending);
            boring.Clear();
            boring.AddRange(nextBoring);
            nextLookTime = 0f;
            yield return null;
            BuildSignposts(viewer, world);
        }

        // GL771: the chunks a piece is missing are listed now and then: listing them for 32 pieces every frame was over
        // 10 ms, and the streamer remembers what it was asked for. Otherwise the check stops at the first one missing.
        private static bool AsksForChunks(float distanceSquared)
        {
            return distanceSquared < RequestMetres * RequestMetres && Time.frameCount % AskEveryFrames == 0;
        }

        // Runs the piece in hand for this long (at least one step); true once no piece is left in hand.
        private bool RunWriting(float milliseconds)
        {
            if (writing == null)
            {
                return true;
            }
            float before = Time.realtimeSinceStartup;
            bool done;
            using (FrameCost.Of("Roads: write piece"))
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
            hold.Remove(session.WorldView.World.Grid, HoldOf(writingRoad, writingPiece));
            PiecesWritten++;
            nextLookTime = 0f;
            LongestMilliseconds = Mathf.Max(LongestMilliseconds, writingMilliseconds);
            TotalMilliseconds += writingMilliseconds;
            return true;
        }

        private void Write()
        {
            WorldConfig config = session.WorldView.Config;
            World world = session.WorldView.World;
            // While the area loads in bulk the ground is waiting to be shown: more of the frame goes to writing it.
            bool inSight = session.WorldView.IsLoadingInSight;
            float writeBudgetMilliseconds = inSight
                ? Mathf.Max(this.writeBudgetMilliseconds, session.WorldView.Hurry ? HurryBudgetMilliseconds : loadingWriteBudgetMilliseconds)
                : Mathf.Max(LeastWriteMilliseconds, FrameBudget.Limit(this.writeBudgetMilliseconds));
            float started = Time.realtimeSinceStartup;
            // GL771: a piece through a wood is written over several frames, and the next waits for it.
            if (!RunWriting(writeBudgetMilliseconds) || (Time.realtimeSinceStartup - started) * 1000f >= writeBudgetMilliseconds)
            {
                return;
            }
            if (pending.Count == 0 && boring.Count == 0)
            {
                return;
            }
            // GL759: nothing was ready at the last look, and chunks take a while to load: no need to ask again every frame.
            if (!inSight && Time.unscaledTime < nextLookTime)
            {
                return;
            }
            nextLookTime = Time.unscaledTime + IdleLookSeconds;
            int checks = 0;
            for (int i = 0; i < pending.Count && checks < ChecksPerFrame; i++)
            {
                (int Road, int Piece, float Distance) next = pending[i];
                RoadWriter writer = built[next.Road].Writer;
                if (writer.IsWritten(next.Piece))
                {
                    pending.RemoveAt(i--);
                    continue;
                }
                checks++;
                missing.Clear();
                if (!writer.Loaded(world, config.WorldHeightInChunks, next.Piece, AsksForChunks(next.Distance) ? missing : null))
                {
                    if (missing.Count > 0)
                    {
                        session.WorldView.RequestChunks(missing);
                    }
                    continue;
                }
                writing = new SlicedWork(writer.WriteSteps(world, config.Materials, Natural, config.IsoLevel, next.Piece));
                writingRoad = writer;
                writingPiece = next.Piece;
                writingMilliseconds = 0f;
                pending.RemoveAt(i--);
                float left = writeBudgetMilliseconds - (Time.realtimeSinceStartup - started) * 1000f;
                if (!RunWriting(left) || (Time.realtimeSinceStartup - started) * 1000f >= writeBudgetMilliseconds)
                {
                    return;
                }
            }
            // Then the tunnels, a metre of bore at a time.
            VoxelMaterial lining = config.Materials != null ? config.Materials.Find("Stone") : null;
            for (int i = 0; i < boring.Count && checks < ChecksPerFrame * 2; i++)
            {
                (int Road, int Tunnel, int Piece, float Distance) next = boring[i];
                TunnelWriter bore = built[next.Road].Tunnels[next.Tunnel];
                checks++;
                missing.Clear();
                if (!bore.Loaded(world, config.WorldHeightInChunks, next.Piece, AsksForChunks(next.Distance) ? missing : null))
                {
                    if (missing.Count > 0)
                    {
                        session.WorldView.RequestChunks(missing);
                    }
                    continue;
                }
                float before = Time.realtimeSinceStartup;
                using (FrameCost.Of("Roads: bore tunnel"))
                {
                    bore.Write(world, config.Materials, lining, config.IsoLevel, next.Piece);
                }
                boring.RemoveAt(i--);
                TunnelPiecesBored++;
                nextLookTime = 0f;
                float took = (Time.realtimeSinceStartup - before) * 1000f;
                LongestMilliseconds = Mathf.Max(LongestMilliseconds, took);
                TotalMilliseconds += took;
                if ((Time.realtimeSinceStartup - started) * 1000f >= writeBudgetMilliseconds)
                {
                    return;
                }
            }
        }

        // The metres of bore near the viewer still to carve, and a portal at each end of a bore once its ground has loaded.
        private void CollectTunnels(int index, Built road, Vector2 viewer, World world)
        {
            for (int t = 0; t < road.Tunnels.Count; t++)
            {
                TunnelWriter bore = road.Tunnels[t];
                found.Clear();
                bore.Unwritten(viewer, buildRange, found);
                foreach (int piece in found)
                {
                    Vector3 middle = bore.Middle(piece);
                    nextBoring.Add((index, t, piece, (new Vector2(middle.x, middle.z) - viewer).sqrMagnitude));
                }
                for (int end = 0; end < 2 && bore.Pieces > 0 && material != null; end++)
                {
                    Vector3 at = bore.At(end == 0 ? bore.BoreStart : bore.BoreEnd);
                    if (road.PortalBuilt[t * 2 + end] || (new Vector2(at.x, at.z) - viewer).sqrMagnitude > buildRange * buildRange
                        || !world.IsLoaded(world.Grid.WorldToChunk(at)))
                    {
                        continue;
                    }
                    road.PortalBuilt[t * 2 + end] = true;
                    Vector2 outward = (end == 0 ? bore.Tunnel.From - bore.Tunnel.To : bore.Tunnel.To - bore.Tunnel.From).normalized;
                    GameObject portal = TunnelPortal.Build(bore.Width, bore.Height, material, root, session.Plan.Seed + index * 53 + t * 2 + end);
                    portal.transform.localPosition = at;
                    portal.transform.localRotation = Quaternion.LookRotation(new Vector3(outward.x, 0f, outward.y), Vector3.up);
                    PortalsBuilt++;
                    // GL650: with a bore's first portal, the wood frames along its length.
                    if (!road.PortalBuilt[t * 2 + 1 - end])
                    {
                        Vector2 way = (bore.Tunnel.To - bore.Tunnel.From).normalized;
                        TunnelPortal.BuildSupports(bore.At, bore.BoreStart + 2f, bore.BoreEnd - 2f, way, bore.Width, bore.Height, material, root,
                            session.Plan.Seed + index * 59 + t);
                    }
                }
            }
        }

        // A bridge is built once the chunks round its ends and middle have loaded, so its footings meet the ground.
        private void BuildBridges(int index, Built road, Vector2 viewer, World world)
        {
            if (material == null)
            {
                return;
            }
            float width = road.Writer.Bed.Definition.BridgeWidth;
            for (int i = 0; i < road.Bridges.Count; i++)
            {
                RouteBridges.Span span = road.Bridges[i];
                // GL651: a long deck (over the sea) is longer than the ground loaded round the viewer, so it stands once
                // the end the viewer comes to has loaded; a short one once all the ground under it has.
                bool longDeck = span.Length > LongDeck;
                float nearest = Mathf.Min((span.From - viewer).sqrMagnitude, (span.To - viewer).sqrMagnitude, ((span.From + span.To) * 0.5f - viewer).sqrMagnitude);
                if (road.BridgeBuilt[i] || nearest > buildRange * buildRange)
                {
                    continue;
                }
                bool loaded = !longDeck;
                for (int k = 0; k <= 4; k++)
                {
                    Vector2 p = Vector2.Lerp(span.From, span.To, k / 4f);
                    float y = span.HeightAt(k / 4f);
                    bool here = world.IsLoaded(world.Grid.WorldToChunk(new Vector3(p.x, y, p.y)));
                    loaded = longDeck ? loaded || (here && (k == 0 || k == 4)) : loaded && here;
                }
                if (loaded)
                {
                    road.BridgeBuilt[i] = true;
                    RoadBridge.Build(span, material, root, session.Plan.Seed + index * 31 + i, width);
                    BridgesBuilt++;
                }
            }
        }

        private void BuildSignposts(Vector2 viewer, World world)
        {
            if (material == null)
            {
                return;
            }
            List<PlannedSignpost> posts = session.Plan.Signposts;
            for (int i = 0; i < posts.Count; i++)
            {
                PlannedSignpost post = posts[i];
                if (postBuilt[i] || (post.Position - viewer).sqrMagnitude > buildRange * buildRange)
                {
                    continue;
                }
                float ground = Natural(post.Position.x, post.Position.y);
                if (!world.IsLoaded(world.Grid.WorldToChunk(new Vector3(post.Position.x, ground, post.Position.y))))
                {
                    continue;
                }
                postBuilt[i] = true;
                GameObject made = Signpost.Build(post, material, textMaterial, root, session.Plan.Seed + i * 17);
                made.transform.localPosition = new Vector3(post.Position.x, ground, post.Position.y);
                SignpostsBuilt++;
                if (post.Settlement >= 0 && post.Settlement < session.Plan.Settlements.Count && !session.Plan.Settlements[post.Settlement].Type.Hostile)
                {
                    BuildSignboard(post, session.Plan.Seed + i * 23);
                }
            }
        }

        // GL717: a settlement's hanging name board beside its edge post, further from the road, its faces along the road;
        // an arm off the side always reaches away from the road.
        private void BuildSignboard(PlannedSignpost post, int seed)
        {
            PlannedSettlement settlement = session.Plan.Settlements[post.Settlement];
            Vector2 at = post.Position + post.Roadside * 1.6f;
            var facing = new Vector3(post.Outward.x, 0f, post.Outward.y);
            if (facing.sqrMagnitude < 1e-6f)
            {
                facing = Vector3.forward;
            }
            Vector3 right = Vector3.Cross(Vector3.up, facing);
            if (right.x * post.Roadside.x + right.z * post.Roadside.y < 0f)
            {
                facing = -facing;
            }
            bool sideArm = (seed & 1) == 0;
            GameObject board = Signboard.Build(settlement.Name, sideArm, material, textMaterial, root, seed);
            board.transform.localPosition = new Vector3(at.x, Natural(at.x, at.y), at.y);
            board.transform.localRotation = Quaternion.LookRotation(facing, Vector3.up);
            SignboardsBuilt++;
        }

        public void SaveValues(IDictionary<string, string> values)
        {
            // Roads not worked out this session keep what an earlier one saved.
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
                if (built[i] != null && built[i].Writer.Written > built[i].Writer.OnBridges + built[i].Writer.Skipped)
                {
                    values[KeyPrefix + i] = built[i].Writer.Save();
                }
                for (int t = 0; built[i] != null && t < built[i].Tunnels.Count; t++)
                {
                    if (built[i].Tunnels[t].Written > 0)
                    {
                        values[KeyPrefix + i + ".t" + t] = built[i].Tunnels[t].Save();
                    }
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
                    built[i].Writer.Restore(saved);
                }
                for (int t = 0; built[i] != null && t < built[i].Tunnels.Count; t++)
                {
                    if (restored.TryGetValue(KeyPrefix + i + ".t" + t, out string bored))
                    {
                        built[i].Tunnels[t].Restore(bored);
                    }
                }
            }
            hold.Clear();
            foreach (Built road in built)
            {
                if (road != null)
                {
                    HoldUnwritten(road);
                }
            }
        }
    }
}
