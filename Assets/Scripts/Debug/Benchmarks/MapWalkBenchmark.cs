using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Clube.Core;
using Clube.Game;
using Unity.Profiling;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Frame times while walking the bounded map (GL770). Run the exe with <c>-walkbench</c> (optionally
    /// <c>-walkseconds 150</c> and the screen size), or use *Clube → Debug → Measure walking on the map* in the Editor. It
    /// starts a new game on MapGen as the menu does, stands a while, carries the player along the nearest long road at a
    /// run and back, stands again, and writes <c>walkbench.txt</c> (beside the exe; in <c>Temp/</c> in the Editor): for each
    /// stretch the frame rate, thread times, the costliest systems and, where the engine times them, the costliest markers.
    /// Frame limits and vsync are off while it runs.
    /// </summary>
    public sealed class MapWalkBenchmark : MonoBehaviour
    {
        private const string Flag = "-walkbench";
        private const int Seed = 1;
        private const float DefaultWalkSeconds = 150f;
        private const float StandSeconds = 15f;
        private const float WindowSeconds = 15f;
        private const float Speed = 6f;
        private const float ShortestRoute = 400f;
        // Over the ground, clear of road beds raised above it: a player inside the ground is lifted out by PlayerSpawn.
        private const float CarriedHeight = 2.5f;
        private const int MarkersShown = 70, LongFrameMarkers = 16, LoopStagesShown = 12;
        private const double SlowSystemMilliseconds = 8.0;
        private const double OverFrameMilliseconds = 16.0;
        // Chunks round the player whose ground is checked, and how often.
        private const int UnderfootColumns = 4, NearColumns = 12, GroundLookFrames = 30;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartIfAsked()
        {
            if (System.Environment.GetCommandLineArgs().Contains(Flag))
            {
                Run();
            }
        }

        /// <summary>Starts the benchmark, from the main menu scene.</summary>
        /// <param name="renderDistance">Chunks to load round the player; 0 keeps the player's setting.</param>
        public static void Run(int renderDistance = 0)
        {
            if (FindAnyObjectByType<MapWalkBenchmark>() != null)
            {
                return;
            }
            var go = new GameObject("Map walk benchmark");
            DontDestroyOnLoad(go);
            go.AddComponent<MapWalkBenchmark>().distanceOverride = renderDistance;
        }

        private readonly StringBuilder text = new StringBuilder();
        private readonly FrameTiming[] timing = new FrameTiming[1];
        private readonly FrameMarkers markers = new FrameMarkers();
        private readonly PlayerLoopTimes loopTimes = new PlayerLoopTimes();
        private static readonly string[] PhaseNames = { "poll", "area", "apply meshes", "add chunks", "start generating", "start meshing", "colliders" };
        private readonly double[] phaseTotals = new double[PhaseNames.Length];
        private readonly double[] phaseWorst = new double[PhaseNames.Length];
        private readonly ProcessLoad load = new ProcessLoad();
        private readonly Dictionary<Vector2Int, int> groundLayers = new Dictionary<Vector2Int, int>();
        private readonly Dictionary<string, (double total, double worst, int slow)> systems = new Dictionary<string, (double, double, int)>();
        // Systems that alone took more than a frame's 16 ms while walking (GL771), past the first stretch.
        private readonly Dictionary<string, (int frames, double worst)> overFrame = new Dictionary<string, (int, double)>();
        private bool walkingNow;
        private readonly List<float> batches = new List<float>();
        private readonly List<float> setPasses = new List<float>();
        private readonly List<float> triangles = new List<float>();
        private readonly List<float> frames = new List<float>();
        private readonly List<float> walkFrames = new List<float>();
        private readonly List<float> cpu = new List<float>();
        private readonly List<float> render = new List<float>();
        private readonly List<float> gpu = new List<float>();
        private readonly List<float> streamer = new List<float>();
        private readonly List<float> far = new List<float>();
        private readonly List<float> lod = new List<float>();
        private readonly List<float> trees = new List<float>();
        private readonly List<float> water = new List<float>();
        private readonly List<float> grass = new List<float>();
        private readonly List<float> drawCalls = new List<float>();
        private readonly List<float> allocated = new List<float>();
        private readonly List<Vector2> route = new List<Vector2>();
        private readonly List<float> along = new List<float>();
        private int distanceOverride;
        private ProfilerRecorder drawCallsRecorder;
        private ProfilerRecorder allocatedRecorder;
        private ProfilerRecorder batchesRecorder;
        private ProfilerRecorder setPassRecorder;
        private ProfilerRecorder trianglesRecorder;
        private int collectionsAtStart;
        private long heapAtStart;
        private MapSession session;
        private WorldView world;
        private Transform body;
        private float walked;
        private float underfootGround = 1f, nearGround = 1f, drawnGround = 1f;
        private string underfootWhy = "";
        private FarTerrainView farView;
        private float longestFrame;
        private string longestFrameWent = "";

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            allocatedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            batchesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            setPassRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            trianglesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            float started = Time.realtimeSinceStartup;
            GameSession.StartNew(GameMode.Demo, Seed, "Benchmark");
            GameSession.ArrivesByCart = false;
            GameScenes.LoadGame();

            PlayerSpawn spawn = null;
            while (Time.realtimeSinceStartup - started < 400f && (session == null || !session.IsReady || spawn == null || !spawn.IsPlaced))
            {
                session = MapSession.Current;
                spawn = spawn != null ? spawn : FindAnyObjectByType<PlayerSpawn>();
                yield return null;
            }
            if (session == null || !session.IsReady || spawn == null || !spawn.IsPlaced)
            {
                Finish("The map never finished loading.");
                yield break;
            }
            world = session.WorldView;
            if (distanceOverride > 0)
            {
                // The setting would carry this into the player settings asset.
                if (world.TryGetComponent(out RenderDistanceSetting setting))
                {
                    setting.enabled = false;
                }
                world.RenderDistance = distanceOverride;
            }

            PlayerController player = spawn.GetComponent<PlayerController>();
            player.enabled = false;
            if (player.TryGetComponent(out CharacterController capsule))
            {
                capsule.enabled = false;
            }
            body = player.transform;
            Vector3 home = world.transform.InverseTransformPoint(body.position);
            PickRoute(new Vector2(home.x, home.z));

            float walkSeconds = Argument("-walkseconds", DefaultWalkSeconds);
            text.AppendLine($"Map walk benchmark, {Screen.width} x {Screen.height}, {SystemInfo.graphicsDeviceName}, {SystemInfo.processorType}");
            text.AppendLine($"Version {BuildInfo.Version}; seed {Seed}; render distance {world.RenderDistance}, detail distance {world.DetailDistance}; "
                            + $"{(Application.isEditor ? "Editor" : "player")}; {Speed} m/s along {RouteLength():0} m of road for {walkSeconds:0} s; "
                            + $"stood on the map {Time.realtimeSinceStartup - started:0} s after Start");

            loopTimes.Install();
            FrameCost.Measuring = true;
            yield return Stretch("stand at the start, before", StandSeconds, false);
            float done = 0f;
            int window = 0;
            while (done < walkSeconds)
            {
                window++;
                // The first stretch is still the load settling.
                walkingNow = window > 1;
                yield return Stretch($"walk {window}", WindowSeconds, true);
                done += WindowSeconds;
            }
            walkingNow = false;
            yield return Stretch("stand where the walk ended", StandSeconds, false);
            Summary();
            Finish(text.ToString());
        }

        // The longest-reaching road near the start; the walk runs along it and back.
        private void PickRoute(Vector2 home)
        {
            PlannedRoad best = null;
            float nearest = float.MaxValue;
            foreach (PlannedRoad road in session.Plan.Roads)
            {
                if (road.Points.Count < 2 || road.Length < ShortestRoute)
                {
                    continue;
                }
                float distance = road.Points.Min(p => (p - home).sqrMagnitude);
                if (distance < nearest)
                {
                    nearest = distance;
                    best = road;
                }
            }
            route.Clear();
            along.Clear();
            if (best != null)
            {
                route.AddRange(best.Points);
            }
            else
            {
                // No long road: straight across the map through the start.
                route.Add(home);
                route.Add(home + Vector2.right * 1000f);
            }
            float length = 0f;
            along.Add(0f);
            for (int i = 1; i < route.Count; i++)
            {
                length += Vector2.Distance(route[i - 1], route[i]);
                along.Add(length);
            }
        }

        private float RouteLength() => along.Count > 0 ? along[along.Count - 1] : 0f;

        private IEnumerator Stretch(string name, float seconds, bool walking)
        {
            float t = 0f;
            markers.WatchNew();
            markers.Clear();
            loopTimes.Clear();
            Clear();
            while (t < seconds)
            {
                yield return null;
                t += Time.unscaledDeltaTime;
                if (walking)
                {
                    walked += Speed * Time.unscaledDeltaTime;
                    walkFrames.Add(Time.unscaledDeltaTime * 1000f);
                }
                Carry();
                Sample();
                if (Time.frameCount % GroundLookFrames == 0)
                {
                    underfootGround = Mathf.Min(underfootGround, GroundReady(UnderfootColumns));
                    nearGround = Mathf.Min(nearGround, GroundReady(NearColumns));
                    drawnGround = Mathf.Min(drawnGround, GroundReady(world.DetailDistance));
                }
            }
            Report(name, t);
        }

        // The share of the columns within so many chunks of the player whose ground is loaded and meshed: frames are no use if the ground lags behind.
        private float GroundReady(int radius)
        {
            World chunks = world.World;
            Vector3 size = chunks.Grid.ChunkWorldSize;
            Vector3Int centre = chunks.Grid.WorldToChunk(world.transform.InverseTransformPoint(body.position));
            int columns = 0, ready = 0, unloaded = 0, held = 0, unmeshed = 0;
            for (int z = -radius; z <= radius; z++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (x * x + z * z > radius * radius)
                    {
                        continue;
                    }
                    columns++;
                    int cx = centre.x + x, cz = centre.z + z;
                    // Kept: some 2300 heights at every look were 30 ms of the benchmark's own in every thirtieth frame.
                    var column = new Vector2Int(cx, cz);
                    if (!groundLayers.TryGetValue(column, out int ground))
                    {
                        ground = Mathf.FloorToInt(session.Map.Height((cx + 0.5f) * size.x, (cz + 0.5f) * size.z) / size.y);
                        groundLayers.Add(column, ground);
                    }
                    bool shown = true, loaded = true;
                    for (int y = Mathf.Max(0, ground - 1); y <= ground + 1 && shown; y++)
                    {
                        // A chunk waiting to be meshed again after an edit still shows its last mesh.
                        var coord = new Vector3Int(cx, y, cz);
                        loaded = chunks.IsLoaded(coord);
                        shown = loaded && (!chunks.TryPeekChunk(coord, out Chunk chunk) || !chunk.IsDirty || world.Renderers.ContainsKey(coord));
                    }
                    ready += shown ? 1 : 0;
                    if (!shown)
                    {
                        world.ColumnState(new Vector2Int(cx, cz), out _, out _, out bool waiting);
                        unloaded += loaded ? 0 : 1;
                        held += loaded && waiting ? 1 : 0;
                        unmeshed += loaded && !waiting ? 1 : 0;
                    }
                }
            }
            float share = columns > 0 ? (float)ready / columns : 1f;
            if (radius == UnderfootColumns && share < underfootGround)
            {
                underfootWhy = $"{unloaded} columns not loaded, {held} held for roads and settlements, {unmeshed} waiting for a mesh";
            }
            return share;
        }

        // Stands the player on the route at the distance walked, there and back, facing the way it goes.
        private void Carry()
        {
            float length = Mathf.Max(1f, RouteLength());
            float position = Mathf.PingPong(walked, length);
            int i = 1;
            while (i < along.Count - 1 && along[i] < position)
            {
                i++;
            }
            Vector2 a = route[i - 1], b = route[i];
            float span = Mathf.Max(0.001f, along[i] - along[i - 1]);
            Vector2 at = Vector2.Lerp(a, b, Mathf.Clamp01((position - along[i - 1]) / span));
            bool back = Mathf.Repeat(walked, length * 2f) > length;
            Vector2 facing = (back ? a - b : b - a).normalized;
            float heading = Mathf.Atan2(facing.x, facing.y) * Mathf.Rad2Deg;
            float ground = session.Map.Height(at.x, at.y) + CarriedHeight;
            body.SetPositionAndRotation(world.transform.TransformPoint(new Vector3(at.x, ground, at.y)), Quaternion.Euler(0f, heading, 0f));
        }

        private void Sample()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timing) > 0)
            {
                cpu.Add((float)timing[0].cpuMainThreadFrameTime);
                render.Add((float)timing[0].cpuRenderThreadFrameTime);
                gpu.Add((float)timing[0].gpuFrameTime);
            }
            frames.Add(Time.unscaledDeltaTime * 1000f);
            if (Time.unscaledDeltaTime * 1000f > longestFrame)
            {
                longestFrame = Time.unscaledDeltaTime * 1000f;
                longestFrameWent = markers.TopOfLastFrame(LongFrameMarkers);
            }
            streamer.Add((float)world.Stats.FrameMilliseconds);
            farView = farView != null ? farView : FindAnyObjectByType<FarTerrainView>();
            far.Add(Cost(farView, v => v.LastUpdateMilliseconds));
            lod.Add(Cost(world.GetComponent<TerrainLodView>(), v => v.LastUpdateMilliseconds));
            trees.Add(Cost(world.GetComponent<DistantTreeView>(), v => v.LastUpdateMilliseconds));
            water.Add(Cost(world.GetComponent<WaterView>(), v => v.LastUpdateMilliseconds));
            grass.Add(Cost(world.GetComponent<GrassScatter>(), v => v.LastUpdateMilliseconds));
            drawCalls.Add(drawCallsRecorder.Valid ? drawCallsRecorder.LastValue : 0f);
            allocated.Add(allocatedRecorder.Valid ? allocatedRecorder.LastValue / 1024f : 0f);
            SampleSystems();
            StreamingPhases phases = world.Stats.Phases;
            phaseTotals[0] += phases.Poll;
            phaseTotals[1] += phases.Area;
            phaseTotals[2] += phases.ApplyMeshes;
            phaseTotals[3] += phases.AddChunks;
            phaseTotals[4] += phases.StartGeneration;
            phaseTotals[5] += phases.StartMeshing;
            phaseTotals[6] += phases.Colliders;
            double[] thisFrame = { phases.Poll, phases.Area, phases.ApplyMeshes, phases.AddChunks, phases.StartGeneration, phases.StartMeshing, phases.Colliders };
            for (int i = 0; i < thisFrame.Length; i++)
            {
                phaseWorst[i] = System.Math.Max(phaseWorst[i], thisFrame[i]);
            }
            batches.Add(batchesRecorder.Valid ? batchesRecorder.LastValue : 0f);
            setPasses.Add(setPassRecorder.Valid ? setPassRecorder.LastValue : 0f);
            triangles.Add(trianglesRecorder.Valid ? trianglesRecorder.LastValue / 1000f : 0f);
            markers.Sample();
        }

        // Each system's time this frame: its average, its worst frame, and how many frames it alone took a long time.
        private void SampleSystems()
        {
            foreach (FrameCost cost in FrameCost.All)
            {
                double milliseconds = cost.TakeMilliseconds();
                systems.TryGetValue(cost.Name, out (double total, double worst, int slow) seen);
                systems[cost.Name] = (seen.total + milliseconds, System.Math.Max(seen.worst, milliseconds), seen.slow + (milliseconds > SlowSystemMilliseconds ? 1 : 0));
                if (walkingNow && milliseconds > OverFrameMilliseconds)
                {
                    overFrame.TryGetValue(cost.Name, out (int frames, double worst) over);
                    overFrame[cost.Name] = (over.frames + 1, System.Math.Max(over.worst, milliseconds));
                }
            }
        }

        private string SystemsReport()
        {
            int count = Mathf.Max(1, frames.Count);
            return string.Join("; ", systems.OrderByDescending(s => s.Value.total)
                .Select(s => $"{s.Key} {s.Value.total / count:0.00} (worst {s.Value.worst:0}, {s.Value.slow} slow)"));
        }

        private static float Cost<T>(T view, System.Func<T, float> read) where T : Behaviour
        {
            return view != null && view.isActiveAndEnabled ? read(view) : 0f;
        }

        private void Report(string name, float seconds)
        {
            text.AppendLine($"== {name}: {frames.Count / seconds:0.0} FPS over {seconds:0} s");
            text.AppendLine(Line("  frame", frames) + " | " + Line("cpu main", cpu) + " | " + Line("render", render) + " | " + Line("gpu", gpu));
            text.AppendLine(Line("  streamer", streamer) + " | " + Line("far terrain", far) + " | " + Line("coarse terrain", lod) + " | " + Line("distant trees", trees)
                            + " | " + Line("water", water) + " | " + Line("grass", grass));
            text.AppendLine(Line("  draw calls", drawCalls, "") + " | " + Line("batches", batches, "") + " | " + Line("set-pass calls", setPasses, "")
                            + " | " + Line("triangles", triangles, " k") + " | " + Line("allocated", allocated, " KB"));
            text.AppendLine($"  {load.Report()}; {System.GC.CollectionCount(0) - collectionsAtStart} garbage collections, managed heap {heapAtStart / 1048576f:0} -> {System.GC.GetTotalMemory(false) / 1048576f:0} MB");
            text.AppendLine($"  ground ready at its worst: {underfootGround * 100f:0}% within {UnderfootColumns * 4} m, {nearGround * 100f:0}% within {NearColumns * 4} m, {drawnGround * 100f:0}% within {world.DetailDistance * 4} m"
                            + (underfootGround < 1f ? $" (underfoot: {underfootWhy})" : ""));
            underfootGround = nearGround = drawnGround = 1f;
            text.AppendLine($"  pending {world.PendingCount}, proven uniform {world.World.CountProvenLayers()}, chunks held {world.World.LoadedCount}, loading {world.IsLoading}, in sight {world.IsLoadingInSight}, renderers {world.Stats.Renderers}; phases (last frame): {world.Stats.Phases}");
            text.AppendLine($"  player loop (ms a frame): {loopTimes.Top(LoopStagesShown, frames.Count)}");
            text.AppendLine($"  systems (ms a frame; worst frame; frames over {SlowSystemMilliseconds:0} ms): {SystemsReport()}");
            int phaseFrames = Mathf.Max(1, frames.Count);
            text.AppendLine("  streaming phases (ms a frame): " + string.Join(", ", PhaseNames.Select((phase, i) => $"{phase} {phaseTotals[i] / phaseFrames:0.00} (worst {phaseWorst[i]:0})")));
            string top = markers.Top(MarkersShown, frames.Count);
            if (top.Length > 0)
            {
                text.AppendLine($"  markers (ms a frame): {top}");
                text.AppendLine($"  longest frame {longestFrame:0} ms: {longestFrameWent}");
            }
            longestFrame = 0f;
            UnityEngine.Debug.Log($"[walkbench] {name}: {frames.Count / seconds:0.0} FPS");
        }

        private void Summary()
        {
            if (walkFrames.Count == 0)
            {
                return;
            }
            List<float> sorted = walkFrames.OrderBy(f => f).ToList();
            float seconds = walkFrames.Sum() / 1000f;
            float low = sorted[Mathf.Min(sorted.Count - 1, (int)(sorted.Count * 0.99f))];
            text.AppendLine($"Walking overall: {walkFrames.Count / seconds:0.0} FPS average, 1% low {1000f / low:0.0} FPS; "
                            + $"median frame {sorted[sorted.Count / 2]:0.0} ms, worst {sorted[sorted.Count - 1]:0.0} ms; "
                            + $"{100f * walkFrames.Count(f => f <= 16.7f) / walkFrames.Count:0}% of frames within 16.7 ms");
            text.AppendLine($"Over {OverFrameMilliseconds:0} ms in a frame while walking, past the first stretch (frames, worst ms): "
                            + (overFrame.Count == 0 ? "nothing" : string.Join("; ", overFrame.OrderByDescending(o => o.Value.worst).Select(o => $"{o.Key} {o.Value.frames}, {o.Value.worst:0}"))));
        }

        private void Clear()
        {
            foreach (List<float> values in new[] { frames, cpu, render, gpu, streamer, far, lod, trees, water, grass, drawCalls, allocated, batches, setPasses, triangles })
            {
                values.Clear();
            }
            systems.Clear();
            System.Array.Clear(phaseTotals, 0, phaseTotals.Length);
            System.Array.Clear(phaseWorst, 0, phaseWorst.Length);
            load.Clear();
            collectionsAtStart = System.GC.CollectionCount(0);
            heapAtStart = System.GC.GetTotalMemory(false);
        }

        private static string Line(string name, List<float> values, string unit = " ms")
        {
            if (values.Count == 0)
            {
                return $"{name}: none";
            }
            List<float> sorted = values.OrderBy(v => v).ToList();
            float p95 = sorted[Mathf.Min(sorted.Count - 1, (int)(sorted.Count * 0.95f))];
            return $"{name} avg {values.Average():0.0}{unit}, 95th {p95:0.0}, worst {sorted[sorted.Count - 1]:0.0}";
        }

        // A number after a command-line flag, e.g. -walkseconds 60.
        private static float Argument(string flag, float fallback)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int at = System.Array.IndexOf(args, flag);
            return at >= 0 && at + 1 < args.Length && float.TryParse(args[at + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value) ? value : fallback;
        }

        private void Finish(string report)
        {
            UnityEngine.Debug.Log(report);
            string folder = System.IO.Path.GetDirectoryName(Application.dataPath);
            if (Application.isEditor)
            {
                folder = System.IO.Path.Combine(folder, "Temp");
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "walkbench.txt"), report);
            FrameCost.Measuring = false;
            markers.Dispose();
            loopTimes.Dispose();
            drawCallsRecorder.Dispose();
            allocatedRecorder.Dispose();
            batchesRecorder.Dispose();
            setPassRecorder.Dispose();
            trianglesRecorder.Dispose();
            GameScenes.Quit();
        }
    }
}
