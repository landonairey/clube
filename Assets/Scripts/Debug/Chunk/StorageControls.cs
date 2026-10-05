using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// The storage section of the lab panel (2G): pick the scheme (and octree depth), see what
    /// the target chunk's storage holds (memory, saved size, runs or nodes), and show its
    /// structure (<see cref="StorageView"/>, K27). A new scheme makes new chunks, so it regenerates.
    /// </summary>
    public static class StorageControls
    {
        private static readonly string[] SchemeNames = { "Flat float", "Flat byte", "Runs X", "Runs Y", "Runs Z", "Octree" };

        // Serializing a chunk to measure it costs up to a megabyte of writes, so the readout is
        // measured at most this often, and again whenever the chunk's storage object changes.
        private const float StatsInterval = 0.5f;

        private static IVoxelStorage measuredStorage;
        private static float measuredAt = float.NegativeInfinity;
        private static long memoryBytes;
        private static long savedBytes;

        public static void Draw(LabPanelFrame frame, WorldConfig config, LabChunkTarget target, StorageView view)
        {
            var scheme = (VoxelStorageType)GUILayout.SelectionGrid(
                (int)config.Storage, SchemeNames, 3, frame.Button, GUILayout.Width(frame.InnerWidth));
            int depth = config.OctreeMaxDepth;
            if (scheme == VoxelStorageType.Octree)
            {
                depth = Mathf.RoundToInt(frame.Slider("Octree max depth", depth, 1f, 8f, "0"));
            }
            if (scheme != config.Storage || depth != config.OctreeMaxDepth)
            {
                config.Storage = scheme;
                config.OctreeMaxDepth = depth;
                config.NotifyChanged();
            }

            Chunk chunk = target != null ? target.Chunk : null;
            if (chunk != null)
            {
                IVoxelStorage storage = chunk.Storage;
                if (storage != measuredStorage || Time.unscaledTime - measuredAt >= StatsInterval)
                {
                    measuredStorage = storage;
                    measuredAt = Time.unscaledTime;
                    memoryBytes = storage.MemoryBytes;
                    savedBytes = VoxelStorages.SerializedBytes(storage);
                }
                long flat = (long)storage.SampleCount.x * storage.SampleCount.y * storage.SampleCount.z * sizeof(float);
                frame.Line($"Memory {BenchmarkStats.Bytes(memoryBytes)} ({(double)memoryBytes / flat:0.00}× flat)");
                frame.Line($"Saved size {BenchmarkStats.Bytes(savedBytes)}");
            }
            else if (target != null && target.FollowsFocus)
            {
                GUILayout.Label("Focus a chunk to see its storage.", frame.Hint);
            }

            if (view == null)
            {
                return;
            }
            view.Show = frame.ToggleField("Show the storage structure", view.Show);
            if (view.Show)
            {
                view.HideAirRuns = frame.ToggleField("Hide runs of air", view.HideAirRuns);
                view.HideAirLeaves = frame.ToggleField("Hide octree leaves of air", view.HideAirLeaves);
                if (view.Summary.Length > 0)
                {
                    GUILayout.Label(view.Summary, frame.Hint);
                }
            }
        }
    }
}
