using System.Collections.Generic;
using Clube.Core;
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

        // Pieces looked at each frame for whether their chunks have loaded.
        private const int ChecksPerFrame = 24;

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

        // One road, worked out once the viewer comes near.
        private sealed class Built
        {
            public RoadLine Line;
            public RoadWriter Writer;
            public List<RouteBridges.Span> Bridges;
            public bool[] BridgeBuilt;
        }

        private readonly List<(int Road, int Piece, float Distance)> pending = new List<(int, int, float)>();
        private readonly List<int> found = new List<int>();
        private KeptHeights heights;
        private readonly Dictionary<string, string> restored = new Dictionary<string, string>();
        private Rect[] bounds;
        private Built[] built;
        private bool[] postBuilt;
        private Transform root;
        private int version = -1;
        private Vector2 collectedAt;
        private float collectedTime = float.MinValue;
        private bool moreToWorkOut;

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

        /// <summary>Pieces near the viewer still waiting for their chunks.</summary>
        public int PiecesWaiting => pending.Count;

        /// <summary>The longest a road took to work out (its line, heights, bridges and bed), in milliseconds: once per road, as the viewer nears it.</summary>
        public float LongestWorkOutMilliseconds { get; private set; }

        /// <summary>Milliseconds the longest piece took to write, and all of them together.</summary>
        public float LongestMilliseconds { get; private set; }

        public float TotalMilliseconds { get; private set; }

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
            if (moreToWorkOut || (viewer - collectedAt).sqrMagnitude > 6f * 6f || Time.unscaledTime - collectedTime > 1f)
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
            pending.Clear();
            heights = new KeptHeights(session.Map.Height);
            collectedTime = float.MinValue;
            RoadsWorkedOut = PiecesWritten = BridgesBuilt = SignpostsBuilt = 0;
        }

        // The ground as generated, kept as it is asked for: road pieces ask for the same square metres over and over.
        private float Natural(float x, float z)
        {
            return heights.At(x, z);
        }

        // Works out a road in detail: its line with heights, its bridges, its bed and its writer.
        private Built WorkOut(int index)
        {
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
            var line = new RoadLine(road.Points, session.Map.Height, 1f, WorldRoads.HeightsOf(session.Map));
            List<RouteBridges.Span> bridges = RouteBridges.Find(line, session.Map.Water, true);
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
            var bed = new RoadBed(line, definition, bridges, plan.Seed + index, endHeight);
            var made = new Built { Line = line, Writer = new RoadWriter(bed), Bridges = bridges, BridgeBuilt = new bool[bridges.Count] };
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
            if (restored.TryGetValue(KeyPrefix + index, out string saved))
            {
                made.Writer.Restore(saved);
            }
            built[index] = made;
            RoadsWorkedOut++;
            LongestWorkOutMilliseconds = Mathf.Max(LongestWorkOutMilliseconds, (Time.realtimeSinceStartup - started) * 1000f);
            return made;
        }

        // Finds what there is to do round the viewer: pieces to write (nearest first), bridges and signposts to stand.
        private void Collect(Vector2 viewer)
        {
            collectedAt = viewer;
            collectedTime = Time.unscaledTime;
            moreToWorkOut = false;
            pending.Clear();
            World world = session.WorldView.World;
            bool workedOutOne = false;
            for (int i = 0; i < bounds.Length; i++)
            {
                Rect box = bounds[i];
                if (viewer.x < box.xMin - buildRange || viewer.x > box.xMax + buildRange || viewer.y < box.yMin - buildRange || viewer.y > box.yMax + buildRange)
                {
                    continue;
                }
                if (built[i] == null)
                {
                    // A road's heights are a few tens of milliseconds: one new road a frame.
                    if (workedOutOne)
                    {
                        moreToWorkOut = true;
                        continue;
                    }
                    workedOutOne = true;
                }
                Built road = WorkOut(i);
                if (road == null)
                {
                    continue;
                }
                found.Clear();
                road.Writer.Unwritten(viewer, buildRange, found);
                foreach (int piece in found)
                {
                    pending.Add((i, piece, (road.Writer.Middle(piece) - viewer).sqrMagnitude));
                }
                BuildBridges(i, road, viewer, world);
            }
            pending.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            BuildSignposts(viewer, world);
        }

        private void Write()
        {
            if (pending.Count == 0)
            {
                return;
            }
            WorldConfig config = session.WorldView.Config;
            World world = session.WorldView.World;
            float started = Time.realtimeSinceStartup;
            int checks = 0;
            for (int i = 0; i < pending.Count && checks < ChecksPerFrame; i++)
            {
                (int Road, int Piece, float Distance) next = pending[i];
                RoadWriter writer = built[next.Road].Writer;
                checks++;
                if (!writer.Loaded(world, config.WorldHeightInChunks, next.Piece))
                {
                    continue;
                }
                float before = Time.realtimeSinceStartup;
                writer.Write(world, config.Materials, Natural, config.IsoLevel, next.Piece);
                pending.RemoveAt(i--);
                PiecesWritten++;
                float took = (Time.realtimeSinceStartup - before) * 1000f;
                LongestMilliseconds = Mathf.Max(LongestMilliseconds, took);
                TotalMilliseconds += took;
                if ((Time.realtimeSinceStartup - started) * 1000f >= writeBudgetMilliseconds)
                {
                    return;
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
                if (road.BridgeBuilt[i] || ((span.From + span.To) * 0.5f - viewer).sqrMagnitude > buildRange * buildRange)
                {
                    continue;
                }
                bool loaded = true;
                for (int k = 0; k <= 4 && loaded; k++)
                {
                    Vector2 p = Vector2.Lerp(span.From, span.To, k / 4f);
                    float y = Mathf.Lerp(span.FromHeight, span.ToHeight, k / 4f);
                    loaded = world.IsLoaded(world.Grid.WorldToChunk(new Vector3(p.x, y, p.y)));
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
            }
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
            }
        }
    }
}
