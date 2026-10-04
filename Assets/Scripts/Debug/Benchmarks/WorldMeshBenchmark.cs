using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>One row of the world benchmark: everything loaded at one render distance.</summary>
    public readonly struct WorldMeshResult
    {
        public WorldMeshResult(int renderDistance, int chunks, int triangles, double generateMs, double meshMs)
        {
            RenderDistance = renderDistance;
            Chunks = chunks;
            Triangles = triangles;
            GenerateMs = generateMs;
            MeshMs = meshMs;
        }

        public int RenderDistance { get; }

        public int Chunks { get; }

        public int Triangles { get; }

        /// <summary>Median time to generate (load) every chunk.</summary>
        public double GenerateMs { get; }

        /// <summary>Median time to mesh every chunk with <see cref="ChunkMesher"/>.</summary>
        public double MeshMs { get; }

        public double MeshMsPerChunk => MeshMs / Chunks;
    }

    /// <summary>
    /// K32 at world scale (2D): loads and meshes everything a <see cref="WorldView"/> would
    /// stream in around the origin (<see cref="StreamingArea"/>), from a lab's
    /// <see cref="WorldConfig"/>, timing generation and meshing separately. No renderers
    /// or mesh upload: this is the Core work streaming spreads over frames.
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
            ChunkMeshSettings settings = config.MeshSettings;

            foreach (int distance in renderDistances)
            {
                StreamingArea.Collect(Vector3Int.zero, distance, config.WorldHeightInChunks, coords);
                var generateMs = new double[runs];
                var meshMs = new double[runs];
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

                    if (run >= 0)
                    {
                        generateMs[run] = generated;
                        meshMs[run] = meshed;
                    }
                }

                results.Add(new WorldMeshResult(
                    distance, coords.Count, triangleCount,
                    BenchmarkStats.Median(generateMs), BenchmarkStats.Median(meshMs)));
            }
            return results;
        }

        public static string ToMarkdown(WorldConfig config, IReadOnlyList<WorldMeshResult> results)
        {
            var text = new StringBuilder();
            text.AppendLine(
                $"World: `{config.name}`, {config.ChunkSize.x}×{config.ChunkSize.y}×{config.ChunkSize.z} chunks, " +
                $"{config.WorldHeightInChunks} layers, {config.Terrain.Generator}, {config.Shading} shading.");
            text.AppendLine();
            text.AppendLine("| Render distance | Chunks | Triangles | Generate ms | Mesh ms | Mesh ms per chunk |");
            text.AppendLine("|--:|--:|--:|--:|--:|--:|");
            foreach (WorldMeshResult r in results)
            {
                text.AppendLine(
                    $"| {r.RenderDistance} | {r.Chunks:N0} | {r.Triangles:N0} | {r.GenerateMs:0.0} | {r.MeshMs:0.0} | {r.MeshMsPerChunk:0.00} |");
            }
            return text.ToString();
        }
    }
}
