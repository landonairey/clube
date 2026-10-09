using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Clube.Core;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>One row of the world benchmark: everything loaded at one render distance.</summary>
    public readonly struct WorldMeshResult
    {
        public WorldMeshResult(
            int renderDistance, int chunks, int surfaceChunks, int triangles, double generateMs, double generateParallelMs,
            double meshMs, double burstMs, double burstParallelMs)
        {
            RenderDistance = renderDistance;
            Chunks = chunks;
            SurfaceChunks = surfaceChunks;
            Triangles = triangles;
            GenerateMs = generateMs;
            GenerateParallelMs = generateParallelMs;
            MeshMs = meshMs;
            BurstMs = burstMs;
            BurstParallelMs = burstParallelMs;
        }

        public int RenderDistance { get; }

        public int Chunks { get; }

        /// <summary>Chunks with a surface: the rest are uniform (all air or all solid) and need no mesh.</summary>
        public int SurfaceChunks { get; }

        public int Triangles { get; }

        /// <summary>Median time to generate (load) every chunk, one after another on this thread (each fill a Burst job, waited for).</summary>
        public double GenerateMs { get; }

        /// <summary>Median time to generate every chunk through the <see cref="ChunkPipeline"/>, all jobs in flight at once (K35, P3).</summary>
        public double GenerateParallelMs { get; }

        /// <summary>Median time to mesh every surface chunk with the managed <see cref="ChunkMesher"/>.</summary>
        public double MeshMs { get; }

        /// <summary>Median time to mesh every surface chunk with the Burst job, one chunk after another on this thread (K12).</summary>
        public double BurstMs { get; }

        /// <summary>Median time to mesh every surface chunk with Burst jobs all scheduled at once, across the worker threads (K12).</summary>
        public double BurstParallelMs { get; }
    }

    /// <summary>
    /// K32, K12 and K35 at world scale (2D): loads and meshes everything a <see cref="WorldView"/>
    /// would stream in around the origin (<see cref="StreamingArea"/>), from a lab's
    /// <see cref="WorldConfig"/>, timing generation (one by one, and through the pipeline in
    /// parallel) and each way of meshing separately: the managed mesher, the Burst job one chunk
    /// at a time, and Burst jobs for every chunk at once on the worker threads. Mesh jobs here
    /// include the material pass when the config shows materials. No renderers or mesh upload:
    /// <see cref="StreamingBenchmark"/> measures the frames.
    /// </summary>
    public static class WorldMeshBenchmark
    {
        public static IReadOnlyList<WorldMeshResult> Run(WorldConfig config, IReadOnlyList<int> renderDistances, int warmup = 1, int runs = 5)
        {
            var results = new List<WorldMeshResult>();
            var coords = new List<Vector3Int>();
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            ITerrainGenerator generator = TerrainGenerators.Create(config.Terrain);
            OreField ores = OreField.Create(config.Terrain, generator);
            TreeField trees = TreeField.Create(config.Terrain, generator);
            ChunkMeshSettings settings = config.MeshSettings;

            foreach (int distance in renderDistances)
            {
                StreamingArea.Collect(Vector3Int.zero, distance, config.WorldHeightInChunks, coords);
                var generateMs = new double[runs];
                var parallelGenerateMs = new double[runs];
                var meshMs = new double[runs];
                var burstMs = new double[runs];
                var parallelMs = new double[runs];
                int triangleCount = 0;
                int surfaceChunks = 0;
                for (int run = -warmup; run < runs; run++)
                {
                    var world = new World(config.ChunkSize, config.VoxelSize, config.CreateStorage);
                    var stopwatch = Stopwatch.StartNew();
                    foreach (Vector3Int coord in coords)
                    {
                        world.Load(coord, generator, config.Terrain.Layers, ores, trees);
                    }
                    double generated = stopwatch.Elapsed.TotalMilliseconds;
                    double generatedInParallel = GenerateInParallel(config, generator, ores, trees, coords);

                    var surface = new List<Chunk>();
                    foreach (Chunk chunk in world.Chunks.Values)
                    {
                        if (!chunk.IsUniform)
                        {
                            surface.Add(chunk);
                        }
                    }

                    triangleCount = 0;
                    stopwatch.Restart();
                    foreach (Chunk chunk in surface)
                    {
                        ChunkMesher.Build(chunk, settings, vertices, triangles);
                        triangleCount += triangles.Count / 3;
                    }
                    double meshed = stopwatch.Elapsed.TotalMilliseconds;

                    stopwatch.Restart();
                    foreach (Chunk chunk in surface)
                    {
                        Mesh.MeshDataArray data = Mesh.AllocateWritableMeshData(1);
                        ChunkMeshJobs.Schedule(ChunkMeshInput.Snapshot(chunk, Allocator.TempJob), settings, data).Complete();
                        data.Dispose();
                    }
                    double burst = stopwatch.Elapsed.TotalMilliseconds;

                    double parallel = MeshInParallel(surface, settings);
                    surfaceChunks = surface.Count;

                    if (run >= 0)
                    {
                        generateMs[run] = generated;
                        parallelGenerateMs[run] = generatedInParallel;
                        meshMs[run] = meshed;
                        burstMs[run] = burst;
                        parallelMs[run] = parallel;
                    }
                }

                results.Add(new WorldMeshResult(
                    distance, coords.Count, surfaceChunks, triangleCount, BenchmarkStats.Median(generateMs),
                    BenchmarkStats.Median(parallelGenerateMs), BenchmarkStats.Median(meshMs),
                    BenchmarkStats.Median(burstMs), BenchmarkStats.Median(parallelMs)));
            }
            return results;
        }

        public static string ToMarkdown(WorldConfig config, IReadOnlyList<WorldMeshResult> results)
        {
            var text = new StringBuilder();
            text.AppendLine(
                $"World: `{config.name}`, {config.ChunkSize.x}³ chunks of {config.VoxelSize} m voxels, " +
                $"{config.WorldHeightInChunks} layers, {config.Terrain.Generator}, {config.Shading} shading, " +
                $"materials {config.MaterialDisplay}, storage {config.Storage}, {SystemInfo.processorCount} threads.");
            text.AppendLine();
            text.AppendLine("| Render distance | Chunks | With surface | Triangles | Generate ms | Generate ms (parallel) | Mesh ms (managed) | Mesh ms (Burst) | Mesh ms (Burst, parallel) |");
            text.AppendLine("|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            foreach (WorldMeshResult r in results)
            {
                text.AppendLine(
                    $"| {r.RenderDistance} | {r.Chunks:N0} | {r.SurfaceChunks:N0} | {r.Triangles:N0} | {r.GenerateMs:0.0} | " +
                    $"{r.GenerateParallelMs:0.0} | {r.MeshMs:0.0} | {r.BurstMs:0.0} | {r.BurstParallelMs:0.0} |");
            }
            return text.ToString();
        }

        // Every chunk through the pipeline at once (no job limit), waited for, then taken.
        private static double GenerateInParallel(WorldConfig config, ITerrainGenerator generator, OreField ores, TreeField trees, List<Vector3Int> coords)
        {
            var world = new World(config.ChunkSize, config.VoxelSize, config.CreateStorage);
            var stopwatch = Stopwatch.StartNew();
            using (var pipeline = new ChunkPipeline(world.Grid, generator, config.Terrain.Layers, ores, world.PreferredFormat, world.StorageFactory, trees))
            {
                foreach (Vector3Int coord in coords)
                {
                    pipeline.StartGeneration(coord);
                }
                pipeline.Flush();
                while (world.LoadedCount < coords.Count)
                {
                    pipeline.Poll();
                    while (pipeline.TryTakeGenerated(out Vector3Int coord, out Chunk chunk))
                    {
                        world.Add(coord, chunk);
                    }
                }
            }
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        // One job per chunk, all scheduled before any is waited on, so they spread over the workers.
        private static double MeshInParallel(List<Chunk> chunks, ChunkMeshSettings settings)
        {
            var outputs = new Mesh.MeshDataArray[chunks.Count];
            var handles = new NativeArray<JobHandle>(chunks.Count, Allocator.Temp);
            try
            {
                var stopwatch = Stopwatch.StartNew();
                for (int i = 0; i < chunks.Count; i++)
                {
                    outputs[i] = Mesh.AllocateWritableMeshData(1);
                    handles[i] = ChunkMeshJobs.Schedule(ChunkMeshInput.Snapshot(chunks[i], Allocator.TempJob), settings, outputs[i]);
                }
                JobHandle.ScheduleBatchedJobs();
                JobHandle.CompleteAll(handles);
                return stopwatch.Elapsed.TotalMilliseconds;
            }
            finally
            {
                handles.Dispose();
                foreach (Mesh.MeshDataArray output in outputs)
                {
                    output.Dispose();
                }
            }
        }
    }
}
