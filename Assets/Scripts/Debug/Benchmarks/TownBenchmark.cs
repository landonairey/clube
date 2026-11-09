using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Clube.Core;
using Clube.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Clube.Debug
{
    /// <summary>
    /// Frame times in the town (GL416), measured in the built game: run the exe with <c>-townbench</c> (and the screen
    /// size, e.g. <c>-screen-width 2560 -screen-height 1440</c>). It loads WorldLab, waits for the town to be written into the
    /// terrain, flies the camera round the square at eye height for a while and writes the frame times (whole frame, CPU
    /// main thread, render thread and GPU, from <see cref="FrameTimingManager"/>) and the costliest systems' own timings to
    /// <c>townbench.txt</c> beside the exe, then quits. Frame limits and vsync are off while it runs.
    /// </summary>
    public sealed class TownBenchmark : MonoBehaviour
    {
        private const string Flag = "-townbench";
        private const float SettleSeconds = 6f;
        private const float MeasureSeconds = 25f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartIfAsked()
        {
            if (!System.Environment.GetCommandLineArgs().Contains(Flag) || FindAnyObjectByType<TownBenchmark>() != null)
            {
                return;
            }
            var go = new GameObject("Town benchmark");
            DontDestroyOnLoad(go);
            go.AddComponent<TownBenchmark>();
        }

        private readonly List<FrameTiming[]> timings = new List<FrameTiming[]>();

        private IEnumerator Start()
        {
            if (SceneManager.GetActiveScene().name != "WorldLab")
            {
                SceneManager.LoadScene("WorldLab");
                yield return null;
                yield return null;
            }
            var site = FindAnyObjectByType<TownSite>();
            var world = FindAnyObjectByType<WorldView>();
            Camera view = Camera.main;
            float waited = 0f;
            while ((site == null || site.Plan == null) && waited < 60f)
            {
                waited += Time.unscaledDeltaTime;
                site = site != null ? site : FindAnyObjectByType<TownSite>();
                yield return null;
            }
            if (site == null || site.Plan == null || world == null || view == null)
            {
                Write("No town to measure.");
                Application.Quit();
                yield break;
            }
            Vector2 centre = site.Site.Centre;
            float ground = site.Site.Ground;
            // Over the town until it has been written into the terrain.
            while (!site.IsLevelled && waited < 120f)
            {
                waited += Time.unscaledDeltaTime;
                Place(view, world, new Vector3(centre.x, ground + 30f, centre.y - 40f), new Vector3(centre.x, ground, centre.y));
                yield return null;
            }
            float settle = 0f;
            while (settle < SettleSeconds)
            {
                settle += Time.unscaledDeltaTime;
                Walk(view, world, site, settle);
                Unlimit();
                yield return null;
            }

            var frames = new List<float>();
            var cpu = new List<float>();
            var render = new List<float>();
            var gpu = new List<float>();
            var grass = new List<float>();
            var grassScan = new List<float>();
            var grassDraw = new List<float>();
            var grassLawn = new List<float>();
            var water = new List<float>();
            var trees = new List<float>();
            var lod = new List<float>();
            var gs = world.GetComponent<GrassScatter>();
            var wv = world.GetComponent<WaterView>();
            var dt = world.GetComponent<DistantTreeView>();
            var tl = world.GetComponent<TerrainLodView>();
            float t = 0f;
            var timing = new FrameTiming[1];
            int lastCount = 0;
            int pendingAtLast = -1;
            while (t < MeasureSeconds)
            {
                t += Time.unscaledDeltaTime;
                Walk(view, world, site, SettleSeconds + t);
                Unlimit();
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timing) > 0)
                {
                    cpu.Add((float)timing[0].cpuMainThreadFrameTime);
                    render.Add((float)timing[0].cpuRenderThreadFrameTime);
                    gpu.Add((float)timing[0].gpuFrameTime);
                }
                frames.Add(Time.unscaledDeltaTime * 1000f);
                if (t > MeasureSeconds - 10f)
                {
                    if (pendingAtLast < 0)
                    {
                        pendingAtLast = world.PendingCount;
                    }
                    lastCount++;
                }
                grass.Add(gs != null ? gs.LastUpdateMilliseconds : 0f);
                grassScan.Add(gs != null ? gs.LastScanMilliseconds : 0f);
                grassDraw.Add(gs != null ? gs.LastDrawMilliseconds : 0f);
                grassLawn.Add(gs != null ? gs.LastLawnMilliseconds : 0f);
                water.Add(wv != null ? wv.LastUpdateMilliseconds : 0f);
                trees.Add(dt != null ? dt.LastUpdateMilliseconds : 0f);
                lod.Add(tl != null ? tl.LastUpdateMilliseconds : 0f);
                yield return null;
            }

            var text = new StringBuilder();
            text.AppendLine($"Town benchmark, {Screen.width} x {Screen.height}, {SystemInfo.graphicsDeviceName}, {SystemInfo.processorType}");
            text.AppendLine($"Version {BuildInfo.Version}; town written in {site.LevelMilliseconds:0} ms; {frames.Count} frames over {MeasureSeconds:0} s");
            text.AppendLine(Line("frame", frames));
            text.AppendLine(Line("  frame, last 10 s", frames.GetRange(Mathf.Max(0, frames.Count - lastCount), Mathf.Min(frames.Count, lastCount))));
            text.AppendLine($"  frames over 16.7 ms: {frames.Count(f => f > 16.7f)} of {frames.Count}; in the last 10 s {frames.Skip(Mathf.Max(0, frames.Count - lastCount)).Count(f => f > 16.7f)} of {Mathf.Min(frames.Count, lastCount)}");
            text.AppendLine($"  chunks still to load or mesh 10 s before the end: {pendingAtLast}");
            text.AppendLine(Line("cpu main", cpu));
            text.AppendLine(Line("cpu render", render));
            text.AppendLine(Line("gpu", gpu));
            text.AppendLine(Line("grass", grass));
            text.AppendLine(Line("  grass scan", grassScan));
            text.AppendLine(Line("  grass draw", grassDraw));
            text.AppendLine(Line("  grass lawn", grassLawn));
            text.AppendLine(gs != null ? $"  grass: {gs.TuftCount} tufts on {gs.KeptChunkCount} chunks" : "");
            text.AppendLine(Line("water", water));
            text.AppendLine(Line("distant trees", trees));
            text.AppendLine(Line("coarse terrain", lod));
            text.AppendLine($"streaming phases: {world.Stats}");
            text.AppendLine($"chunks still to load or mesh at the end: {world.PendingCount}; renderers {world.Renderers.Count}");
            Write(text.ToString());
            Application.Quit();
        }

        // Frame limits off: vsync and the graphics setting's cap would hide what a frame costs.
        private static void Unlimit()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
        }

        // Round the square at eye height (about walking pace), looking about.
        private static void Walk(Camera view, WorldView world, TownSite site, float t)
        {
            Vector2 c = site.Site.Centre;
            float a = t * 0.12f;
            var at = new Vector3(c.x + Mathf.Cos(a) * 8f, 0f, c.y + Mathf.Sin(a) * 8f);
            at.y = site.Plan.GroundAt(site.Plan.ToTown(new Vector2(at.x, at.z))) + 1.7f;
            Vector3 look = at + new Vector3(Mathf.Cos(a * 2.3f), -0.12f, Mathf.Sin(a * 2.3f));
            Place(view, world, at, look);
        }

        private static void Place(Camera view, WorldView world, Vector3 local, Vector3 lookAt)
        {
            Vector3 at = world.transform.TransformPoint(local);
            Vector3 target = world.transform.TransformPoint(lookAt);
            view.transform.SetPositionAndRotation(at, Quaternion.LookRotation(target - at));
        }

        private static string Line(string name, List<float> values)
        {
            if (values.Count == 0)
            {
                return $"{name}: none";
            }
            var sorted = values.OrderBy(v => v).ToList();
            float average = values.Average();
            float p95 = sorted[Mathf.Min(sorted.Count - 1, (int)(sorted.Count * 0.95f))];
            return $"{name}: average {average:0.0} ms, median {sorted[sorted.Count / 2]:0.0}, 95th {p95:0.0}, worst {sorted[sorted.Count - 1]:0.0}";
        }

        private static void Write(string text)
        {
            UnityEngine.Debug.Log(text);
            string folder = System.IO.Path.GetDirectoryName(Application.dataPath);
            System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "townbench.txt"), text);
        }
    }
}
