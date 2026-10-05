using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Clube.Core;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>One row of the world benchmark: everything loaded at one render distance.</summary>
    public readonly struct WorldMeshResult
    {
        public WorldMeshResult(
            int renderDistance, int chunks, int triangles, double generateMs, double meshMs, double burstMs, double burstParallelMs)
        {
            RenderDistance = renderDistance;
            Chunks = chunks;
            Triangles = triangles;
            GenerateMs = generateMs;
            MeshMs = meshMs;
            BurstMs = burstMs;
            BurstParallelMs = burstParallelMs;
        }

        public int RenderDistance { get; }

        public int Chunks { get; }

        public int Triangles { get; }

        /// <summary>Median time to generate (load) every chunk.</summary>
        public double GenerateMs { get; }

        /// <summary>Median time to mesh every chunk with the managed <see cref="ChunkMesher"/>.</summary>
        public double MeshMs { get; }

        /// <summary>Median time to mesh every chunk with the Burst job, one chunk after another on this thread (K12).</summary>
        public double BurstMs { get; }

        /// <summary>Median time to mesh every chunk with Burst jobs all scheduled at once, across the worker threads (K12).</summary>
        public double BurstParallelMs { get; }

        public double MeshMsPerChunk => MeshMs / Chunks;
    }

    /// <summary>
    /// K32 and K12 at world scale (2D): loads and meshes everything a <see cref="WorldView"/>
    /// would stream in around the origin (<see cref="StreamingArea"/>), from a lab's
    /// <see cref="WorldConfig"/>, timing generation and each way of meshing separately: the
    /// managed mesher, the Burst job one chunk at a time, and Burst jobs for every chunk at
    /// once on the worker threads. No renderers or mesh upload: this is the Core work
    /// streaming spreads over frames.
    /// </summary>
    public static class WorldMeshBenchmark
    {
        public static IReadOnlyList<WorldMeshResult> Run(WorldConfig config, IReadOnlyList<int> renderDistances, int warmup = 1, int runs = 5)
        {
            var results = new List<WorldMeshResult>();
            var coords = new List<Vector3Int>();
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var nativeVertices = new NativeList<float3>(Allocator.Persistent);
            var nativeTriangles = new NativeList<int>(Allocator.Persistent);
            ITerrainGenerator generator = TerrainGenerators.Create(config.Terrain);
            ChunkMeshSettings settings = config.MeshSettings;

            try
            {
                foreach (int distance in renderDistances)
                {
                    StreamingArea.Collect(Vector3Int.zero, distance, config.WorldHeightInChunks, coords);
                    var generateMs = new double[runs];
                    var meshMs = new double[runs];
                    var burstMs = new double[runs];
                    var parallelMs = new double[runs];
                    int triangleCount = 0;
                    for (int run = -warmup; run < runs; run++)
                    {
                        var world = new World(config.ChunkSize, config.VoxelSize);
                        var stopwatch = Stopwatch.StartNew();
                        foreach (Vector3Int coord in coords)
                        {
                            world.Load(coord, generator);
                        }
                        double generated = stopwatch.Elapsed.TotalMilliseconds;

                        triangleCount = 0;
                        stopwatch.Restart();
                        foreach (Chunk chunk in world.Chunks.Values)
                        {
                            ChunkMesher.Build(chunk, settings, vertices, triangles);
                            triangleCount += triangles.Count / 3;
                        }
                        double meshed = stopwatch.Elapsed.TotalMilliseconds;

                        stopwatch.Restart();
                        foreach (Chunk chunk in world.Chunks.Values)
                        {
                            BurstChunkMesher.Build(chunk, settings, nativeVertices, nativeTriangles);
                        }
                        double burst = stopwatch.Elapsed.TotalMilliseconds;

                        double parallel = MeshInParallel(world, settings);

                        if (run >= 0)
                        {
                            generateMs[run] = generated;
                            meshMs[run] = meshed;
                            burstMs[run] = burst;
                            parallelMs[run] = parallel;
                        }
                    }

                    results.Add(new WorldMeshResult(
                        distance, coords.Count, triangleCount, BenchmarkStats.Median(generateMs), BenchmarkStats.Median(meshMs),
                        BenchmarkStats.Median(burstMs), BenchmarkStats.Median(parallelMs)));
                }
            }
            finally
            {
                nativeVertices.Dispose();
                nativeTriangles.Dispose();
            }
            return results;
        }

        public static string ToMarkdown(WorldConfig config, IReadOnlyList<WorldMeshResult> results)
        {
            var text = new StringBuilder();
            text.AppendLine(
                $"World: `{config.name}`, {config.ChunkSize.x}×{config.ChunkSize.y}×{config.ChunkSize.z} chunks, " +
                $"{config.WorldHeightInChunks} layers, {config.Terrain.Generator}, {config.Shading} shading, " +
                $"{SystemInfo.processorCount} threads.");
            text.AppendLine();
            text.AppendLine("| Render distance | Chunks | Triangles | Generate ms | Mesh ms (managed) | Mesh ms (Burst) | Mesh ms (Burst, parallel) | Managed ms per chunk |");
            text.AppendLine("|--:|--:|--:|--:|--:|--:|--:|--:|");
            foreach (WorldMeshResult r in results)
            {
                text.AppendLine(
                    $"| {r.RenderDistance} | {r.Chunks:N0} | {r.Triangles:N0} | {r.GenerateMs:0.0} | {r.MeshMs:0.0} | " +
                    $"{r.BurstMs:0.0} | {r.BurstParallelMs:0.0} | {r.MeshMsPerChunk:0.00} |");
            }
            return text.ToString();
        }

        // One job per chunk, all scheduled before any is waited on, so they spread over the workers.
        private static double MeshInParallel(World world, ChunkMeshSettings settings)
        {
            int count = world.Chunks.Count;
            var outputs = new (NativeList<float3> Vertices, NativeList<int> Triangles)[count];
            var handles = new NativeArray<JobHandle>(count, Allocator.Temp);
            try
            {
                var stopwatch = Stopwatch.StartNew();
                int i = 0;
                foreach (Chunk chunk in world.Chunks.Values)
                {
                    outputs[i] = (new NativeList<float3>(Allocator.TempJob), new NativeList<int>(Allocator.TempJob));
                    handles[i] = BurstChunkMesher.Schedule(chunk, settings, outputs[i].Vertices, outputs[i].Triangles);
                    i++;
                }
                JobHandle.ScheduleBatchedJobs();
                JobHandle.CompleteAll(handles);
                return stopwatch.Elapsed.TotalMilliseconds;
            }
            finally
            {
                handles.Dispose();
                foreach ((NativeList<float3> vertexList, NativeList<int> triangleList) in outputs)
                {
                    if (vertexList.IsCreated)
                    {
                        vertexList.Dispose();
                        triangleList.Dispose();
                    }
                }
            }
        }
    }
}
