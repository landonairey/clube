using System.Diagnostics;
using System.Text;
using Clube.Core;
using Clube.Game;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Times dropping into a map scene (GL653): *Clube → Debug → Measure load time* enters Play mode and logs, in
    /// seconds from the scene's start, when the map was surveyed, when the player stood, when the area finished
    /// loading and when the last road and settlement piece went into the ground, with the streamer's phases summed
    /// over the load. Frames keep coming while the Editor is unfocused. The result is logged and kept in
    /// <c>SessionState</c> under <see cref="ResultKey"/>.
    /// </summary>
    public static class LoadTimeReport
    {
        public const string ResultKey = "Clube.LoadTimeReport.Result";
        private const string ArmedKey = "Clube.LoadTimeReport.Armed";

        // The load is over once the area has loaded and no piece has been written for this long.
        private const double QuietSeconds = 6.0;

        [MenuItem("Clube/Debug/Measure load time")]
        public static void Measure()
        {
            SessionState.SetBool(ArmedKey, true);
            SessionState.EraseString(ResultKey);
            EditorApplication.isPlaying = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Begin()
        {
            if (!SessionState.GetBool(ArmedKey, false))
            {
                return;
            }
            SessionState.SetBool(ArmedKey, false);
            Application.runInBackground = true;
            var probe = new Probe();
            EditorApplication.update += probe.Tick;
        }

        private sealed class Probe
        {
            private readonly Stopwatch clock = Stopwatch.StartNew();
            private double surveyed = -1, placed = -1, loaded = -1, shown = -1, lastRoad, lastSettlement, lastMeshed;
            private int roadPieces, settlementPieces, frames, lastFrame = -1, renderers;
            private double longestFrame, previous;
            private StreamingPhases total;
            private double streamingTotal;
            // Until the area has loaded: the whole player loop, scripts' Update and LateUpdate, and the far views.
            private readonly ProfilerRecorder loop = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "PlayerLoop");
            private readonly ProfilerRecorder updates = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Update.ScriptRunBehaviourUpdate");
            private readonly ProfilerRecorder lateUpdates = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "PreLateUpdate.ScriptRunBehaviourLateUpdate");
            private double loopTotal, updateTotal, lateTotal, farTotal, lodTotal, treesTotal, waterTotal, grassTotal;
            private int loadFrames;

            public void Tick()
            {
                if (!EditorApplication.isPlaying)
                {
                    EditorApplication.update -= Tick;
                    return;
                }
                EditorApplication.QueuePlayerLoopUpdate();
                if (Time.frameCount == lastFrame)
                {
                    return;
                }
                lastFrame = Time.frameCount;
                double now = clock.Elapsed.TotalSeconds;
                frames++;
                longestFrame = System.Math.Max(longestFrame, now - previous);
                previous = now;

                MapSession session = MapSession.Current;
                WorldView world = session != null ? session.WorldView : Object.FindAnyObjectByType<WorldView>();
                if (world == null || !world.IsReady)
                {
                    return;
                }
                StreamingStats stats = world.Stats;
                StreamingPhases p = stats.Phases;
                total.Poll += p.Poll;
                total.Area += p.Area;
                total.AddChunks += p.AddChunks;
                total.StartGeneration += p.StartGeneration;
                total.ApplyMeshes += p.ApplyMeshes;
                total.StartMeshing += p.StartMeshing;
                total.Colliders += p.Colliders;
                streamingTotal += stats.FrameMilliseconds;
                if (loaded < 0)
                {
                    loadFrames++;
                    loopTotal += loop.LastValue * 1e-6;
                    updateTotal += updates.LastValue * 1e-6;
                    lateTotal += lateUpdates.LastValue * 1e-6;
                    farTotal += Milliseconds(Object.FindAnyObjectByType<FarTerrainView>(), v => v.LastUpdateMilliseconds);
                    lodTotal += Milliseconds(Object.FindAnyObjectByType<TerrainLodView>(), v => v.LastUpdateMilliseconds);
                    treesTotal += Milliseconds(Object.FindAnyObjectByType<DistantTreeView>(), v => v.LastUpdateMilliseconds);
                    waterTotal += Milliseconds(Object.FindAnyObjectByType<WaterView>(), v => v.LastUpdateMilliseconds);
                    grassTotal += Milliseconds(Object.FindAnyObjectByType<GrassScatter>(), v => v.LastUpdateMilliseconds);
                }
                if (stats.Renderers != renderers)
                {
                    renderers = stats.Renderers;
                    lastMeshed = now;
                }

                if (surveyed < 0 && (session == null || session.IsReady))
                {
                    surveyed = now;
                }
                PlayerSpawn spawn = Object.FindAnyObjectByType<PlayerSpawn>();
                if (placed < 0 && spawn != null && spawn.IsPlaced)
                {
                    placed = now;
                }
                if (placed < 0 && surveyed >= 0 && spawn != null && now - lastRing >= 3.0)
                {
                    lastRing = now;
                    LogRing(world, spawn.transform.position, now);
                }
                LoadCurtain curtain = Object.FindAnyObjectByType<LoadCurtain>();
                if (shown < 0 && placed >= 0 && (curtain == null || !curtain.IsUp))
                {
                    shown = now;
                }
                MapRoads roads = Object.FindAnyObjectByType<MapRoads>();
                if (roads != null && roads.PiecesWritten + roads.TunnelPiecesBored != roadPieces)
                {
                    roadPieces = roads.PiecesWritten + roads.TunnelPiecesBored;
                    lastRoad = now;
                }
                MapSettlements settlements = Object.FindAnyObjectByType<MapSettlements>();
                if (settlements != null && settlements.PiecesWritten != settlementPieces)
                {
                    settlementPieces = settlements.PiecesWritten;
                    lastSettlement = now;
                }
                bool areaLoaded = surveyed >= 0 && !world.IsLoading && world.PendingCount == 0;
                if (loaded < 0 && areaLoaded)
                {
                    loaded = now;
                }
                if (!areaLoaded)
                {
                    loaded = -1;
                }
                double quietSince = System.Math.Max(System.Math.Max(lastRoad, lastSettlement), System.Math.Max(loaded, lastMeshed));
                if (loaded >= 0 && placed >= 0 && now - quietSince > QuietSeconds)
                {
                    Finish();
                }
            }

            private double lastRing;

            // What the columns within 32 m of the waiting player still lack: why the player isn't standing yet.
            private static void LogRing(WorldView world, Vector3 at, double now)
            {
                Vector3Int centre = world.World.Grid.WorldToChunk(world.transform.InverseTransformPoint(at));
                int reach = Mathf.CeilToInt(32f / world.World.Grid.ChunkWorldSize.x), layers = world.Config.WorldHeightInChunks;
                int columns = 0, unloaded = 0, unmeshed = 0, heldColumns = 0, done = 0;
                for (int z = -reach; z <= reach; z++)
                {
                    for (int x = -reach; x <= reach; x++)
                    {
                        if (x * x + z * z > reach * reach)
                        {
                            continue;
                        }
                        columns++;
                        world.ColumnState(new Vector2Int(centre.x + x, centre.z + z), out int loaded, out int meshed, out bool held);
                        if (loaded < layers)
                        {
                            unloaded++;
                        }
                        else if (held)
                        {
                            heldColumns++;
                        }
                        else if (meshed < layers)
                        {
                            unmeshed++;
                        }
                        else
                        {
                            done++;
                        }
                    }
                }
                StreamingStats stats = world.Stats;
                MapRoads roads = Object.FindAnyObjectByType<MapRoads>();
                MapSettlements settlements = Object.FindAnyObjectByType<MapSettlements>();
                UnityEngine.Debug.Log($"Load ring at {now:0.0} s (frame {Time.frameCount}): {columns} columns, {done} ready, {unloaded} loading, {heldColumns} held, {unmeshed} to mesh; "
                    + $"pending {stats.Pending}, generating {stats.Generating}, meshing {stats.Meshing}"
                    + (roads != null ? $"; roads {roads.PiecesWritten} written in {roads.TotalMilliseconds:0} ms, {roads.PendingPieces} waiting" : "")
                    + (settlements != null ? $"; settlements {settlements.PiecesWritten} written in {settlements.TotalMilliseconds:0} ms, {settlements.PendingPieces} waiting" : ""));
            }

            private static double Milliseconds<T>(T view, System.Func<T, float> read) where T : Behaviour
            {
                return view != null && view.isActiveAndEnabled ? read(view) : 0.0;
            }

            private void Finish()
            {
                EditorApplication.update -= Tick;
                loop.Dispose();
                updates.Dispose();
                lateUpdates.Dispose();
                var text = new StringBuilder();
                text.Append($"Load time: surveyed {surveyed:0.0} s, player stood {placed:0.0} s, view shown {shown:0.0} s, area loaded {loaded:0.0} s, ");
                text.Append($"last chunk shown {lastMeshed:0.0} s, last road piece {lastRoad:0.0} s ({roadPieces}), ");
                text.Append($"last settlement piece {lastSettlement:0.0} s ({settlementPieces}); {frames} frames, longest {longestFrame * 1000:0} ms; ");
                text.Append($"streaming {streamingTotal:0} ms in all: {total}");
                text.Append($"; over the {loadFrames} frames of the load: player loop {loopTotal:0} ms, Update {updateTotal:0} ms, LateUpdate {lateTotal:0} ms, ");
                text.Append($"far terrain {farTotal:0} ms, coarse terrain {lodTotal:0} ms, distant trees {treesTotal:0} ms, water {waterTotal:0} ms, grass {grassTotal:0} ms");
                MapRoads roads = Object.FindAnyObjectByType<MapRoads>();
                MapSettlements settlements = Object.FindAnyObjectByType<MapSettlements>();
                if (roads != null)
                {
                    text.Append($"; roads wrote {roads.TotalMilliseconds:0} ms");
                }
                if (settlements != null)
                {
                    text.Append($"; settlements wrote {settlements.TotalMilliseconds:0} ms ({settlements.Timings})");
                }
                SessionState.SetString(ResultKey, text.ToString());
                UnityEngine.Debug.Log(text.ToString());
            }
        }
    }
}
