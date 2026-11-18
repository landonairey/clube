using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;

namespace Clube.Debug
{
    /// <summary>
    /// What the main thread spent a stretch of frames on, by profiler marker, for the benchmarks (GL759, GL770): every
    /// script's own code and the engine's main stages. The engine times these in the Editor and development builds only;
    /// each script's Update while the Profiler records.
    /// </summary>
    internal sealed class FrameMarkers : IDisposable
    {
        private const int MostRecorders = 6000;

        private static readonly HashSet<string> EngineMarkers = new HashSet<string>
        {
            "PlayerLoop", "Update.ScriptRunBehaviourUpdate", "PreLateUpdate.ScriptRunBehaviourLateUpdate", "FixedBehaviourUpdate",
            "Update.ScriptRunDelayedDynamicFrameRate", "PostLateUpdate.FinishFrameRendering", "PostLateUpdate.UpdateAllRenderers",
            "FixedUpdate.PhysicsFixedUpdate", "Physics.Simulate", "Physics.BakePhysXCollisionMeshData", "Physics.SyncColliderTransform",
            "Inl_RenderCameraStack", "CullScriptable", "GC.Collect", "JobHandle.Complete", "Semaphore.WaitForSignal",
            "Gfx.WaitForPresentOnGfxThread", "Application.Integrate Assets in Background", "GameObject.AddComponent",
            "GameObject.Activate", "Mesh.UploadMeshData", "UGUI.Rendering.RenderOverlays", "GUI.Repaint",
            "GC.Alloc", "WaitForJobGroupID", "Gfx.WaitForGfxCommandsFromMainThread", "Gfx.PresentFrame", "UpdateRendererBoundingVolumes",
            "RenderPipelineManager.DoRenderLoop_Internal()", "CullResults.CreateSharedRendererScene", "Light.Shadows", "Shadows.CullLights",
        };

        private static readonly Regex AssemblyPrefix = new Regex(@"^[\w\.]+\.dll!(?:[\w\.]+::)?");

        private readonly List<ProfilerRecorderHandle> handles = new List<ProfilerRecorderHandle>();
        private readonly HashSet<string> watched = new HashSet<string>();
        private readonly List<(string name, ProfilerRecorder recorder)> markers = new List<(string, ProfilerRecorder)>();
        private readonly Dictionary<string, double> totals = new Dictionary<string, double>();

        /// <summary>Starts timing the markers that have appeared since the last call: systems add theirs as they first run.</summary>
        public void WatchNew()
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

        /// <summary>Adds the last frame's times to the stretch.</summary>
        public void Sample()
        {
            foreach ((string name, ProfilerRecorder recorder) in markers)
            {
                long value = recorder.Valid ? recorder.LastValue : 0;
                if (value > 0)
                {
                    totals.TryGetValue(name, out double total);
                    totals[name] = total + value * 1e-6;
                }
            }
        }

        /// <summary>The costliest markers of the last frame alone, in milliseconds: what a long frame went on.</summary>
        public string TopOfLastFrame(int count)
        {
            return string.Join("; ", markers
                .Select(m => (m.name, value: m.recorder.Valid ? m.recorder.LastValue : 0L))
                .Where(m => m.value > 0)
                .OrderByDescending(m => m.value)
                .Take(count)
                .Select(m => $"{Short(m.name)} {m.value * 1e-6:0.0}"));
        }

        /// <summary>The costliest markers of the stretch, in milliseconds a frame; empty where the engine times none.</summary>
        public string Top(int count, int frames)
        {
            if (totals.Count == 0 || frames == 0)
            {
                return "";
            }
            return string.Join("; ", totals.OrderByDescending(m => m.Value).Take(count).Select(m => $"{Short(m.Key)} {m.Value / frames:0.00}"));
        }

        /// <summary>Starts a new stretch.</summary>
        public void Clear()
        {
            totals.Clear();
        }

        public void Dispose()
        {
            foreach ((string _, ProfilerRecorder recorder) in markers)
            {
                recorder.Dispose();
            }
            markers.Clear();
        }

        private static string Short(string name)
        {
            return AssemblyPrefix.Replace(name, "").Replace(" [Invoke]", "");
        }

        private static bool IsWatched(ProfilerRecorderDescription description)
        {
            string name = description.Name;
            return description.Category == ProfilerCategory.Scripts || description.Category == ProfilerCategory.Gui
                || description.Category == ProfilerCategory.Physics || EngineMarkers.Contains(name)
                || name.EndsWith(".Update()") || name.EndsWith(".LateUpdate()") || name.EndsWith(".FixedUpdate()") || name.EndsWith(".OnGUI()")
                || name.Contains(".Update() [Invoke]") || name.Contains(".LateUpdate() [Invoke]") || name.Contains(".FixedUpdate() [Invoke]");
        }
    }
}
