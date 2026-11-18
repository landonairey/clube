using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Clube.Core;
using Clube.Game;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Frame times while loading into the tutorial (GL759). Run the exe with <c>-tutorialbench</c> (optionally
    /// <c>-benchseconds 120</c> and the screen size), or use *Clube → Debug → Measure tutorial start* in the Editor. It starts a new
    /// game with the tutorial as the menu does, leaves the player standing where the tutorial sets them down, and writes
    /// <c>tutorialbench.txt</c> (beside the exe; in <c>Temp/</c> in the Editor): for each stretch of five seconds the frame and
    /// thread times, the costliest systems' own timings and, where the engine times them (the Editor and development builds),
    /// the costliest profiler markers on the main thread. Frame limits and vsync are off while it runs.
    /// </summary>
    public sealed class TutorialBenchmark : MonoBehaviour
    {
        private const string Flag = "-tutorialbench";
        private const float WindowSeconds = 5f;
        private const float DefaultSeconds = 180f;
        private const int Seed = 1;
        private const int MarkersShown = 24;
        private const int MostRecorders = 6000;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartIfAsked()
        {
            if (System.Environment.GetCommandLineArgs().Contains(Flag))
            {
                Run();
            }
        }

        /// <summary>Starts the benchmark, from the main menu scene.</summary>
        public static void Run()
        {
            if (FindAnyObjectByType<TutorialBenchmark>() != null)
            {
                return;
            }
            var go = new GameObject("Tutorial benchmark");
            DontDestroyOnLoad(go);
            go.AddComponent<TutorialBenchmark>();
        }

        private readonly StringBuilder text = new StringBuilder();
        private readonly System.Diagnostics.Stopwatch clock = new System.Diagnostics.Stopwatch();
        private readonly FrameTiming[] timing = new FrameTiming[1];
        private readonly List<float> frames = new List<float>();
        private readonly List<float> cpu = new List<float>();
        private readonly List<float> render = new List<float>();
        private readonly List<float> gpu = new List<float>();
        private readonly List<float> streamer = new List<float>();
        private readonly List<float> far = new List<float>();
        private readonly List<float> lod = new List<float>();
        private readonly List<float> trees = new List<float>();
        private readonly List<float> water = new List<float>();
        private readonly List<float> grass = new List<float>();
        private readonly List<float> allocated = new List<float>();
        private readonly List<float> drawCalls = new List<float>();
        private readonly List<ProfilerRecorderHandle> handles = new List<ProfilerRecorderHandle>();
        private readonly HashSet<string> watched = new HashSet<string>();
        private readonly List<(string name, ProfilerRecorder recorder)> markers = new List<(string, ProfilerRecorder)>();
        private readonly Dictionary<string, double> markerTotals = new Dictionary<string, double>();
        private ProfilerRecorder allocatedRecorder;
        private ProfilerRecorder drawCallsRecorder;
        private double surveyed = -1, stood = -1, shown = -1, loaded = -1;

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            allocatedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            float seconds = Argument("-benchseconds", DefaultSeconds);
            text.AppendLine($"Tutorial benchmark, {Screen.width} x {Screen.height}, {SystemInfo.graphicsDeviceName}, {SystemInfo.processorType}");
            text.AppendLine($"Version {BuildInfo.Version}; seed {Seed}; {seconds:0} s from pressing Start; {(Application.isEditor ? "Editor" : "player")}");

            GameSession.StartNew(GameMode.Demo, Seed, "Benchmark", tutorial: true);
            clock.Start();
            GameScenes.LoadGame();

            double windowEnd = WindowSeconds;
            WatchNewMarkers();
            while (clock.Elapsed.TotalSeconds < seconds)
            {
                yield return null;
                Sample();
                if (clock.Elapsed.TotalSeconds >= windowEnd)
                {
                    Report(windowEnd - WindowSeconds, windowEnd);
                    windowEnd += WindowSeconds;
                    WatchNewMarkers();
                }
            }
            text.AppendLine($"Milestones: map surveyed {surveyed:0.0} s, player stood {stood:0.0} s, view shown {shown:0.0} s, area loaded {loaded:0.0} s");
            Write(text.ToString());
            foreach ((string _, ProfilerRecorder recorder) in markers)
            {
                recorder.Dispose();
            }
            allocatedRecorder.Dispose();
            drawCallsRecorder.Dispose();
            GameScenes.Quit();
        }

        // Script code and the engine's main-thread work, by name; new names appear as systems first run.
        private void WatchNewMarkers()
        {
            handles.Clear();
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach (ProfilerRecorderHandle handle in handles)
            {
                if (markers.Count >= MostRecorders)
                {
                    return;
                }
                ProfilerRecorderDescription description = ProfilerRecorderHandle.GetDescription(handle);
                if (description.UnitType != ProfilerMarkerDataUnit.TimeNanoseconds || !IsWatched(description) || !watched.Add(description.Name))
                {
                    continue;
                }
                var recorder = new ProfilerRecorder(handle, 1, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
                recorder.Start();
                markers.Add((description.Name, recorder));
            }
        }

        // Every script's own code, and the engine's main stages by name: the Editor has thousands of other markers.
        private static bool IsWatched(ProfilerRecorderDescription description)
        {
            string name = description.Name;
            return description.Category == ProfilerCategory.Scripts || EngineMarkers.Contains(name)
                || name.EndsWith(".Update()") || name.EndsWith(".LateUpdate()") || name.EndsWith(".FixedUpdate()") || name.EndsWith(".OnGUI()");
        }

        private static readonly HashSet<string> EngineMarkers = new HashSet<string>
        {
            "PlayerLoop", "Update.ScriptRunBehaviourUpdate", "PreLateUpdate.ScriptRunBehaviourLateUpdate", "FixedBehaviourUpdate",
            "Update.ScriptRunDelayedDynamicFrameRate", "PostLateUpdate.FinishFrameRendering", "PostLateUpdate.UpdateAllRenderers",
            "FixedUpdate.PhysicsFixedUpdate", "Physics.Simulate", "Physics.BakePhysXCollisionMeshData", "Physics.SyncColliderTransform",
            "Inl_RenderCameraStack", "CullScriptable", "GC.Collect", "JobHandle.Complete", "Semaphore.WaitForSignal",
            "Gfx.WaitForPresentOnGfxThread", "Application.Integrate Assets in Background", "GameObject.AddComponent",
            "GameObject.Activate", "Mesh.UploadMeshData", "UGUI.Rendering.RenderOverlays", "GUI.Repaint",
        };

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
            allocated.Add(allocatedRecorder.Valid ? allocatedRecorder.LastValue / 1024f : 0f);
            drawCalls.Add(drawCallsRecorder.Valid ? drawCallsRecorder.LastValue : 0f);
            foreach ((string name, ProfilerRecorder recorder) in markers)
            {
                long value = recorder.Valid ? recorder.LastValue : 0;
                if (value > 0)
                {
                    markerTotals.TryGetValue(name, out double total);
                    markerTotals[name] = total + value * 1e-6;
                }
            }

            MapSession session = MapSession.Current;
            WorldView world = session != null ? session.WorldView : null;
            if (world == null || !world.IsReady)
            {
                return;
            }
            streamer.Add((float)world.Stats.FrameMilliseconds);
            far.Add(Cost(FindAnyObjectByType<FarTerrainView>(), v => v.LastUpdateMilliseconds));
            lod.Add(Cost(world.GetComponent<TerrainLodView>(), v => v.LastUpdateMilliseconds));
            trees.Add(Cost(world.GetComponent<DistantTreeView>(), v => v.LastUpdateMilliseconds));
            water.Add(Cost(world.GetComponent<WaterView>(), v => v.LastUpdateMilliseconds));
            grass.Add(Cost(world.GetComponent<GrassScatter>(), v => v.LastUpdateMilliseconds));

            double now = clock.Elapsed.TotalSeconds;
            if (surveyed < 0 && session.IsReady)
            {
                surveyed = now;
            }
            PlayerSpawn spawn = FindAnyObjectByType<PlayerSpawn>();
            if (stood < 0 && spawn != null && spawn.IsPlaced)
            {
                stood = now;
            }
            LoadCurtain curtain = FindAnyObjectByType<LoadCurtain>();
            if (shown < 0 && stood >= 0 && (curtain == null || !curtain.IsUp))
            {
                shown = now;
            }
            if (loaded < 0 && session.IsReady && !world.IsLoading && world.PendingCount == 0)
            {
                loaded = now;
            }
        }

        private static float Cost<T>(T view, System.Func<T, float> read) where T : Behaviour
        {
            return view != null && view.isActiveAndEnabled ? read(view) : 0f;
        }

        private void Report(double from, double to)
        {
            MapSession session = MapSession.Current;
            WorldView world = session != null ? session.WorldView : null;
            string state = shown >= 0 ? "playing" : stood >= 0 ? "standing behind the curtain" : session != null ? "loading the map" : "loading the scene";
            text.AppendLine($"== {from:0}-{to:0} s, {state}: {frames.Count} frames ({frames.Count / WindowSeconds:0.0} FPS)");
            text.AppendLine(Line("  frame", frames) + " | " + Line("cpu main", cpu) + " | " + Line("render", render) + " | " + Line("gpu", gpu));
            text.AppendLine(Line("  streamer", streamer) + " | " + Line("far terrain", far) + " | " + Line("coarse terrain", lod) + " | " + Line("distant trees", trees)
                            + " | " + Line("water", water) + " | " + Line("grass", grass));
            text.AppendLine(Line("  allocated", allocated, " KB") + " | " + Line("draw calls", drawCalls, ""));
            if (world != null && world.IsReady)
            {
                DistantTreeView treeView = world.GetComponent<DistantTreeView>();
                string treeCounts = treeView != null ? $"; distant trees {treeView.TreeCount} placed, {treeView.DrawnCount} in view, {treeView.PendingTiles} tiles waiting" : "";
                text.AppendLine($"  render distance {world.RenderDistance}, pending {world.PendingCount}, renderers {world.Stats.Renderers}{treeCounts}; phases (last frame): {world.Stats.Phases}");
            }
            if (markerTotals.Count > 0 && frames.Count > 0)
            {
                string top = string.Join("; ", markerTotals.OrderByDescending(m => m.Value).Take(MarkersShown).Select(m => $"{m.Key} {m.Value / frames.Count:0.00}"));
                text.AppendLine($"  markers (ms a frame): {top}");
            }
            UnityEngine.Debug.Log($"[tutorialbench] {from:0}-{to:0} s: {frames.Count / WindowSeconds:0.0} FPS, {state}");
            foreach (List<float> values in new[] { frames, cpu, render, gpu, streamer, far, lod, trees, water, grass, allocated, drawCalls })
            {
                values.Clear();
            }
            markerTotals.Clear();
        }

        private static string Line(string name, List<float> values, string unit = " ms")
        {
            if (values.Count == 0)
            {
                return $"{name}: none";
            }
            var sorted = values.OrderBy(v => v).ToList();
            float p95 = sorted[Mathf.Min(sorted.Count - 1, (int)(sorted.Count * 0.95f))];
            return $"{name} avg {values.Average():0.0}{unit}, 95th {p95:0.0}, worst {sorted[sorted.Count - 1]:0.0}";
        }

        // A number after a command-line flag, e.g. -benchseconds 60.
        private static float Argument(string flag, float fallback)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int at = System.Array.IndexOf(args, flag);
            return at >= 0 && at + 1 < args.Length && float.TryParse(args[at + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value) ? value : fallback;
        }

        private static void Write(string report)
        {
            UnityEngine.Debug.Log(report);
            string folder = System.IO.Path.GetDirectoryName(Application.dataPath);
            if (Application.isEditor)
            {
                folder = System.IO.Path.Combine(folder, "Temp");
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "tutorialbench.txt"), report);
        }
    }
}
