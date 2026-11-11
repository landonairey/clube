using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Clube.Core;
using Clube.Game;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Clube.Debug
{
    /// <summary>
    /// Frame times while roaming the map (GL560), measured in the built game: run the exe with <c>-roambench</c> (optionally <c>-roamseconds 60</c>; and the
    /// screen size). It loads WorldLab, stands at the spawn for a while, walks the player at a run over the whole fixed area,
    /// then stands at the spawn again, and writes <c>roambench.txt</c> beside the exe: per stretch of the walk, frame and
    /// thread times, draw calls, renderers, loaded chunks, objects and memory, and the costliest systems' own timings.
    /// What grows with the distance walked (the same spot slower after the walk than before) is a leak. Then it quits.
    /// </summary>
    public sealed class RoamBenchmark : MonoBehaviour
    {
        private const string Flag = "-roambench";
        private const float StandSeconds = 15f;
        private const float DefaultRoamSeconds = 180f;
        private const float WindowSeconds = 15f;
        private const float Speed = 9f;
        private const float Reach = 125f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartIfAsked()
        {
            if (!System.Environment.GetCommandLineArgs().Contains(Flag) || FindAnyObjectByType<RoamBenchmark>() != null)
            {
                return;
            }
            var go = new GameObject("Roam benchmark");
            DontDestroyOnLoad(go);
            go.AddComponent<RoamBenchmark>();
        }

        private readonly StringBuilder text = new StringBuilder();
        private readonly List<float> frames = new List<float>();
        private readonly List<float> cpu = new List<float>();
        private readonly List<float> render = new List<float>();
        private readonly List<float> gpu = new List<float>();
        private readonly List<float> streamer = new List<float>();
        private readonly List<float> grass = new List<float>();
        private readonly List<float> water = new List<float>();
        private readonly List<float> trees = new List<float>();
        private readonly List<float> lod = new List<float>();
        private readonly List<float> far = new List<float>();
        private readonly List<float> drawCalls = new List<float>();
        private readonly List<float> batches = new List<float>();
        private readonly FrameTiming[] timing = new FrameTiming[1];
        private ProfilerRecorder drawCallsRecorder;
        private ProfilerRecorder batchesRecorder;
        // Main-thread markers, timed in development builds: scripts, culling and the render loop.
        private static readonly (ProfilerCategory category, string name)[] MarkerNames =
        {
            (ProfilerCategory.Internal, "Update.ScriptRunBehaviourUpdate"),
            (ProfilerCategory.Internal, "PreLateUpdate.ScriptRunBehaviourLateUpdate"),
            (ProfilerCategory.Render, "Inl_RenderCameraStack"),
            (ProfilerCategory.Render, "CullScriptable"),
        };
        private readonly List<ProfilerRecorder> markers = new List<ProfilerRecorder>();
        private readonly List<List<float>> markerTimes = new List<List<float>>();
        private readonly List<string> markerNames = new List<string>();
        private WorldView world;
        private GrassScatter grassView;
        private WaterView waterView;
        private DistantTreeView treeView;
        private TerrainLodView lodView;
        private FarTerrainView farView;
        private Transform body;
        private float pathT;

        private IEnumerator Start()
        {
            if (SceneManager.GetActiveScene().name != "WorldLab")
            {
                SceneManager.LoadScene("WorldLab");
                yield return null;
                yield return null;
            }
            drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            batchesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            foreach ((ProfilerCategory category, string name) in MarkerNames)
            {
                markers.Add(ProfilerRecorder.StartNew(category, name));
                markerNames.Add(name);
                markerTimes.Add(new List<float>());
            }
            float waited = 0f;
            PlayerController player = null;
            while ((world == null || !world.IsReady || player == null) && waited < 60f)
            {
                waited += Time.unscaledDeltaTime;
                world = world != null ? world : FindAnyObjectByType<WorldView>();
                // Started straight into WorldLab (not from the menu) the lab flies; walk as the player instead.
                var toggle = FindAnyObjectByType<PlayerCameraToggle>();
                if (toggle != null && toggle.IsSetUp && !toggle.IsPlayer)
                {
                    toggle.IsPlayer = true;
                }
                player = player != null ? player : FindAnyObjectByType<PlayerController>();
                yield return null;
            }
            if (world == null || !world.IsReady || player == null)
            {
                Write($"No world or player to measure (world {world != null}, player {player != null}).");
                Application.Quit();
                yield break;
            }
            // The first load, capped: the walk starts anyway after a minute and a half.
            float loading = 0f;
            while (world.IsLoading && loading < 90f)
            {
                loading += Time.unscaledDeltaTime;
                yield return null;
            }
            grassView = world.GetComponent<GrassScatter>();
            waterView = world.GetComponent<WaterView>();
            treeView = world.GetComponent<DistantTreeView>();
            lodView = world.GetComponent<TerrainLodView>();
            farView = FindAnyObjectByType<FarTerrainView>();
            // The benchmark carries the player: no walking, falling or looking of its own.
            player.enabled = false;
            var capsule = player.GetComponent<CharacterController>();
            if (capsule != null)
            {
                capsule.enabled = false;
            }
            body = player.transform;
            Vector3 home = world.transform.InverseTransformPoint(body.position);
            float roamSeconds = Argument("-roamseconds", DefaultRoamSeconds);

            text.AppendLine($"Roam benchmark, {Screen.width} x {Screen.height}, {SystemInfo.graphicsDeviceName}, {SystemInfo.processorType}");
            text.AppendLine($"Version {BuildInfo.Version}; render distance {world.RenderDistance}, detail distance {world.DetailDistance}; " +
                            $"run {Speed} m/s over ±{Reach} m for {roamSeconds} s; first load {loading:0} s" +
                            (world.IsLoading ? $" and still loading ({world.PendingCount} pending)" : ""));

            yield return Stand(home, "stand at spawn, before");
            float roamed = 0f;
            int window = 0;
            while (roamed < roamSeconds)
            {
                float t = 0f;
                Begin();
                while (t < WindowSeconds)
                {
                    t += Time.unscaledDeltaTime;
                    roamed += Time.unscaledDeltaTime;
                    PlaceOnPath(Time.unscaledDeltaTime);
                    Sample();
                    yield return null;
                }
                window++;
                Report($"roam {window} ({roamed * Speed:0} m walked)");
            }
            yield return Stand(home, "stand at spawn, after");
            Write(text.ToString());
            drawCallsRecorder.Dispose();
            batchesRecorder.Dispose();
            foreach (ProfilerRecorder marker in markers)
            {
                marker.Dispose();
            }
            Application.Quit();
        }

        private IEnumerator Stand(Vector3 home, string name)
        {
            float t = 0f;
            // A few seconds to arrive and load, then the measured stretch.
            while (t < 5f)
            {
                t += Time.unscaledDeltaTime;
                Place(home, t);
                Unlimit();
                yield return null;
            }
            t = 0f;
            Begin();
            while (t < StandSeconds)
            {
                t += Time.unscaledDeltaTime;
                Place(home, t);
                Sample();
                yield return null;
            }
            Report(name);
        }

        // A Lissajous figure over the fixed area, walked at a constant speed.
        private void PlaceOnPath(float dt)
        {
            Vector2 velocity = PathAt(pathT + 0.001f) - PathAt(pathT);
            float step = velocity.magnitude / 0.001f;
            pathT += step > 0.001f ? Speed * dt / step : 0.01f;
            Vector2 at = PathAt(pathT);
            Vector2 ahead = PathAt(pathT + 0.05f) - at;
            Vector3 local = new Vector3(at.x, 0f, at.y);
            float heading = Mathf.Atan2(ahead.x, ahead.y) * Mathf.Rad2Deg + Mathf.Sin(pathT * 3f) * 40f;
            Put(local, heading);
        }

        private static Vector2 PathAt(float t)
        {
            return new Vector2(Mathf.Sin(t) * Reach, Mathf.Sin(t * 2f + 0.7f) * Reach);
        }

        private void Place(Vector3 home, float t)
        {
            Put(new Vector3(home.x, 0f, home.z), t * 12f);
        }

        // Stands the player on the ground at a world-local spot, facing a heading.
        private void Put(Vector3 local, float heading)
        {
            Vector3 top = world.transform.TransformPoint(new Vector3(local.x, 400f, local.z));
            Vector3 at = world.Raycast(new Ray(top, Vector3.down), out Vector3 ground) ? ground : world.transform.TransformPoint(local + Vector3.up * 40f);
            body.SetPositionAndRotation(at + Vector3.up * 0.05f, Quaternion.Euler(0f, heading, 0f));
        }

        private void Begin()
        {
            frames.Clear();
            cpu.Clear();
            render.Clear();
            gpu.Clear();
            streamer.Clear();
            grass.Clear();
            water.Clear();
            trees.Clear();
            lod.Clear();
            far.Clear();
            drawCalls.Clear();
            batches.Clear();
            foreach (List<float> times in markerTimes)
            {
                times.Clear();
            }
        }

        private void Sample()
        {
            Unlimit();
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timing) > 0)
            {
                cpu.Add((float)timing[0].cpuMainThreadFrameTime);
                render.Add((float)timing[0].cpuRenderThreadFrameTime);
                gpu.Add((float)timing[0].gpuFrameTime);
            }
            frames.Add(Time.unscaledDeltaTime * 1000f);
            streamer.Add((float)world.Stats.FrameMilliseconds);
            grass.Add(grassView != null ? grassView.LastUpdateMilliseconds : 0f);
            water.Add(waterView != null ? waterView.LastUpdateMilliseconds : 0f);
            trees.Add(treeView != null ? treeView.LastUpdateMilliseconds : 0f);
            lod.Add(lodView != null ? lodView.LastUpdateMilliseconds : 0f);
            far.Add(farView != null ? farView.LastUpdateMilliseconds : 0f);
            drawCalls.Add(drawCallsRecorder.Valid ? drawCallsRecorder.LastValue : 0);
            batches.Add(batchesRecorder.Valid ? batchesRecorder.LastValue : 0);
            for (int i = 0; i < markers.Count; i++)
            {
                markerTimes[i].Add(markers[i].Valid ? markers[i].LastValue / 1e6f : -1f);
            }
        }

        private void Report(string name)
        {
            StreamingStats stats = world.Stats;
            text.AppendLine($"== {name}: {frames.Count} frames");
            text.AppendLine(Line("  frame", frames));
            text.AppendLine(Line("  cpu main", cpu) + " | " + Line("render", render) + " | " + Line("gpu", gpu));
            text.AppendLine(Line("  draw calls", drawCalls, "") + " | " + Line("batches", batches, ""));
            // Markers are timed in development builds only.
            var markerLine = new StringBuilder("  markers:");
            for (int i = 0; i < markers.Count; i++)
            {
                if (markerTimes[i].Count > 0 && markerTimes[i][0] >= 0f)
                {
                    markerLine.Append($" {markerNames[i]} {markerTimes[i].Average():0.00} ms;");
                }
            }
            text.AppendLine(markerLine.ToString());
            text.AppendLine(Line("  streamer", streamer) + " | " + Line("grass", grass) + " | " + Line("water", water));
            text.AppendLine(Line("  distant trees", trees) + " | " + Line("coarse terrain", lod) + " | " + Line("far terrain", far));
            text.AppendLine($"  renderers {stats.Renderers}, chunks loaded {world.World.LoadedCount}, kept edited {world.World.KeptChunks.Count}, " +
                            $"pending {stats.Pending}, triangles {stats.Triangles}, coarse chunks switched off {(lodView != null ? lodView.HiddenCount : 0)}; " +
                            (grassView != null ? $"grass {grassView.TuftCount} tufts on {grassView.KeptChunkCount} chunks; " : "") +
                            $"game objects {FindObjectsByType<Transform>(FindObjectsSortMode.None).Length}, " +
                            $"meshes {Resources.FindObjectsOfTypeAll<UnityEngine.Mesh>().Length}, " +
                            $"managed heap {System.GC.GetTotalMemory(false) / (1024 * 1024)} MB, " +
                            $"collections {System.GC.CollectionCount(0)}");
            text.AppendLine($"  streaming phases (last frame): {stats.Phases}" +
                            (waterView != null && waterView.Live != null
                                ? $"; water awake {waterView.Live.AwakeCount}, flowing {waterView.Live.Flowing.Count}"
                                : ""));
            // Enabled renderers by the object they sit under, the most first; and how many cast shadows.
            Renderer[] all = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var shown = all.Where(r => r.enabled && r.gameObject.activeInHierarchy).ToList();
            text.AppendLine($"  renderers on: {shown.Count}, casting shadows {shown.Count(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)}, " +
                            $"material slots {shown.Sum(r => r.sharedMaterials.Length)}; " + string.Join(", ", shown
                                .GroupBy(r => r.transform.parent != null ? r.transform.parent.name : r.name)
                                .OrderByDescending(g => g.Count()).Take(8).Select(g => $"{g.Key} {g.Count()}")));
            UnityEngine.Debug.Log($"[roambench] {name}");
        }

        // Frame limits off: vsync and the graphics setting's cap would hide what a frame costs.
        private static void Unlimit()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
        }

        // A number after a command-line flag, e.g. -roamseconds 60.
        private static float Argument(string flag, float fallback)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int at = System.Array.IndexOf(args, flag);
            return at >= 0 && at + 1 < args.Length && float.TryParse(args[at + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value) ? value : fallback;
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

        private static void Write(string report)
        {
            UnityEngine.Debug.Log(report);
            string folder = System.IO.Path.GetDirectoryName(Application.dataPath);
            System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "roambench.txt"), report);
        }
    }
}
