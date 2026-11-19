using Unity.Profiling;

namespace Clube.Core
{
    // TEMPORARY (GL774 measuring): profiler markers inside the streaming phases.
    internal static class StreamingMarkers
    {
        public static readonly ProfilerMarker CollectTrees = new ProfilerMarker("Stream.CollectTrees");
        public static readonly ProfilerMarker CollectOres = new ProfilerMarker("Stream.CollectOres");
        public static readonly ProfilerMarker ScheduleFill = new ProfilerMarker("Stream.ScheduleFill");
        public static readonly ProfilerMarker FromOutput = new ProfilerMarker("Stream.FromOutput");
        public static readonly ProfilerMarker WorldAdd = new ProfilerMarker("Stream.WorldAdd");
        public static readonly ProfilerMarker ChunkLoaded = new ProfilerMarker("Stream.ChunkLoadedEvent");
        public static readonly ProfilerMarker Snapshot = new ProfilerMarker("Stream.Snapshot");
        public static readonly ProfilerMarker AllocMesh = new ProfilerMarker("Stream.AllocMeshData");
        public static readonly ProfilerMarker ScheduleMesh = new ProfilerMarker("Stream.ScheduleMesh");
        public static readonly ProfilerMarker CollectDirty = new ProfilerMarker("Stream.CollectDirty");
        public static readonly ProfilerMarker Heap = new ProfilerMarker("Stream.Heap");
        public static readonly ProfilerMarker MeshLoop = new ProfilerMarker("Stream.MeshLoop");
        public static readonly ProfilerMarker IsHeld = new ProfilerMarker("Stream.IsHeld");
        public static readonly ProfilerMarker Waiting = new ProfilerMarker("Stream.GenWaiting");
        public static readonly ProfilerMarker Requested = new ProfilerMarker("Stream.GenRequested");
        public static readonly ProfilerMarker GroundFirst = new ProfilerMarker("Stream.GenGroundFirst");
        public static readonly ProfilerMarker Cursor = new ProfilerMarker("Stream.GenCursor");
        public static readonly ProfilerMarker StartGeneration = new ProfilerMarker("Stream.PipelineStartGeneration");
        public static readonly ProfilerMarker ProveColumn = new ProfilerMarker("Stream.TryProveColumn");
        public static readonly ProfilerMarker WorldProve = new ProfilerMarker("Stream.WorldProveColumn");
        public static readonly ProfilerMarker RebuildScan = new ProfilerMarker("Stream.RebuildScan");
        public static readonly ProfilerMarker ApplyMesh = new ProfilerMarker("Stream.ApplyMesh");
        public static readonly ProfilerMarker Unload = new ProfilerMarker("Stream.Unload");
        public static readonly ProfilerMarker GatherTrees = new ProfilerMarker("Stream.GatherTrees");
        public static readonly ProfilerMarker GatherRocks = new ProfilerMarker("Stream.GatherRocks");
        public static readonly ProfilerMarker GatherOres = new ProfilerMarker("Stream.GatherOres");
        public static readonly ProfilerMarker GatherMarks = new ProfilerMarker("Stream.GatherMarks");
        public static readonly ProfilerMarker WaterTop = new ProfilerMarker("Stream.WaterTop");
        public static readonly ProfilerMarker OutputAlloc = new ProfilerMarker("Stream.OutputAlloc");
        public static readonly ProfilerMarker Select = new ProfilerMarker("Stream.ScheduleSelect");
    }
}
