using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>One row of the streaming benchmark: loading a world from nothing at one render distance.</summary>
    public readonly struct StreamingResult
    {
        public StreamingResult(int renderDistance, int chunks, int surfaceChunks, int frames, double totalMs, double meanFrameMs, double p95FrameMs, double maxFrameMs)
        {
            RenderDistance = renderDistance;
            Chunks = chunks;
            SurfaceChunks = surfaceChunks;
            Frames = frames;
            TotalMs = totalMs;
            MeanFrameMs = meanFrameMs;
            P95FrameMs = p95FrameMs;
            MaxFrameMs = maxFrameMs;
        }

        public int RenderDistance { get; }
        public int Chunks { get; }
        public int SurfaceChunks { get; }

        /// <summary>Frames until every wanted chunk was loaded, meshed and (near the focus) collidable.</summary>
        public int Frames { get; }

        /// <summary>Wall time until then.</summary>
        public double TotalMs { get; }

        /// <summary>Main-thread time the streamer took per frame: what it costs the frame rate.</summary>
        public double MeanFrameMs { get; }

        public double P95FrameMs { get; }

        public double MaxFrameMs { get; }
    }

    /// <summary>
    /// P3 at world scale: streams a world in from nothing with the real <see cref="WorldStreamer"/>
    /// (jobs, renderers, mesh upload, colliders near the focus), one simulated 60 fps frame at a
    /// time, and records the streamer's main-thread time per frame and how long until the world
    /// settles. The frame rate a player sees while loading is bounded by these frame times; the
    /// rest of each frame (rendering) is simulated by sleeping, which is when the workers run.
    /// </summary>
    public static class StreamingBenchmark
    {
        private const double FrameMs = 1000.0 / 60.0;
        private const int MaxFrames = 20000;

        public static IReadOnlyList<StreamingResult> Run(WorldConfig config, IReadOnlyList<int> renderDistances, float frameBudgetMs = 3f)
        {
            var results = new List<StreamingResult>();
            ITerrainGenerator generator = TerrainGenerators.Create(config.Terrain);
            OreField ores = OreField.Create(config.Terrain, generator);
            TreeField trees = TreeField.Create(config.Terrain, generator);
            var materials = TerrainRenderMaterials.For(new Material[0], config);

            foreach (int distance in renderDistances)
            {
                var root = new GameObject("Streaming Benchmark") { hideFlags = HideFlags.HideAndDontSave };
                var world = new World(config.ChunkSize, config.VoxelSize, config.CreateStorage);
                var pipeline = new ChunkPipeline(world.Grid, generator, config.Terrain.Layers, ores, world.PreferredFormat, world.StorageFactory, trees);
                var pool = new ChunkRendererPool(root.transform, true, materials);
                var streamer = new WorldStreamer(world, pipeline, pool, () => config.MeshSettings, new StreamingSettings
                {
                    RenderDistance = distance,
                    HeightInChunks = config.WorldHeightInChunks,
                    FrameBudgetMilliseconds = frameBudgetMs,
                    Colliders = true,
                    ColliderRadius = 24f,
                    MaxEditsPerFrame = 32,
                });

                var frameTimes = new List<double>();
                var total = Stopwatch.StartNew();
                var frame = new Stopwatch();
                try
                {
                    for (int i = 0; i < MaxFrames; i++)
                    {
                        frame.Restart();
                        streamer.Update(Vector3.zero);
                        streamer.RebuildEdited(Vector3.zero);
                        frameTimes.Add(frame.Elapsed.TotalMilliseconds);

                        StreamingStats stats = streamer.Stats;
                        if (stats.Pending == 0 && stats.Generating == 0 && stats.Meshing == 0 && stats.Baking == 0 && i > 0)
                        {
                            break;
                        }

                        // The rest of the frame (rendering), while the workers carry on.
                        int rest = (int)(FrameMs - frame.Elapsed.TotalMilliseconds);
                        if (rest > 0)
                        {
                            Thread.Sleep(rest);
                        }
                    }
                    double totalMs = total.Elapsed.TotalMilliseconds;
                    double[] times = frameTimes.ToArray();
                    System.Array.Sort(times);
                    results.Add(new StreamingResult(
                        distance, world.LoadedCount, pool.Active.Count, times.Length, totalMs,
                        BenchmarkStats.Mean(times), times[(int)(times.Length * 0.95)], times[times.Length - 1]));
                }
                finally
                {
                    streamer.UnloadAll();
                    streamer.Dispose();
                    Object.DestroyImmediate(root);
                }
            }
            return results;
        }

        public static string ToMarkdown(WorldConfig config, IReadOnlyList<StreamingResult> results, float frameBudgetMs)
        {
            var text = new StringBuilder();
            text.AppendLine(
                $"World: `{config.name}`, {config.ChunkSize.x}³ chunks of {config.VoxelSize} m voxels, " +
                $"{config.WorldHeightInChunks} layers, {config.Terrain.Generator}, materials {config.MaterialDisplay}, " +
                $"storage {config.Storage}, frame budget {frameBudgetMs} ms, {SystemInfo.processorCount} threads.");
            text.AppendLine();
            text.AppendLine("| Render distance | Chunks | With surface | Frames to settle | Time to settle ms | Streamer ms/frame (mean) | p95 | max |");
            text.AppendLine("|--:|--:|--:|--:|--:|--:|--:|--:|");
            foreach (StreamingResult r in results)
            {
                text.AppendLine(
                    $"| {r.RenderDistance} | {r.Chunks:N0} | {r.SurfaceChunks:N0} | {r.Frames:N0} | {r.TotalMs:0} | " +
                    $"{r.MeanFrameMs:0.00} | {r.P95FrameMs:0.00} | {r.MaxFrameMs:0.00} |");
            }
            return text.ToString();
        }
    }
}
