using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>One row of the world storage benchmark: one scheme streaming one world.</summary>
    public readonly struct WorldStorageResult
    {
        public WorldStorageResult(
            string world, string storage, int chunks, long memoryBytes, double loadMs, double meshMs,
            double strokeMs, int editedChunks, long savedBytes, double walkStepMs, long memoryAfterWalk)
        {
            World = world;
            Storage = storage;
            Chunks = chunks;
            MemoryBytes = memoryBytes;
            LoadMs = loadMs;
            MeshMs = meshMs;
            StrokeMs = strokeMs;
            EditedChunks = editedChunks;
            SavedBytes = savedBytes;
            WalkStepMs = walkStepMs;
            MemoryAfterWalk = memoryAfterWalk;
        }

        public string World { get; }
        public string Storage { get; }

        /// <summary>Chunks loaded around the start point.</summary>
        public int Chunks { get; }

        /// <summary>Every loaded chunk's storage, after loading.</summary>
        public long MemoryBytes { get; }

        /// <summary>Loading every chunk: generation and the storage's writes.</summary>
        public double LoadMs { get; }

        /// <summary>Meshing every chunk (managed mesher, reads through ReadLayer).</summary>
        public double MeshMs { get; }

        /// <summary>Median brush stroke across chunk borders (World.ApplyBrush).</summary>
        public double StrokeMs { get; }

        public int EditedChunks { get; }

        /// <summary>The edited chunks serialized: what a save holds (S2).</summary>
        public long SavedBytes { get; }

        /// <summary>Median time to move one chunk: unload what left the area, load what entered it.</summary>
        public double WalkStepMs { get; }

        /// <summary>Loaded chunks plus edited chunks kept in memory, after the walk.</summary>
        public long MemoryAfterWalk { get; }
    }

    /// <summary>
    /// M12: the K26 storage comparison across a streamed world. For each scheme it loads
    /// everything a <see cref="WorldView"/> would around the origin (<see cref="StreamingArea"/>),
    /// meshes it, brushes across it, measures what a save of the edited chunks would hold, then
    /// walks the area along X a chunk at a time, unloading and loading as streaming does.
    /// </summary>
    public static class WorldStorageBenchmark
    {
        private const int Strokes = 60;
        private const int WalkSteps = 8;

        /// <summary>A world to stream: its config, and how far around the origin it loads.</summary>
        public readonly struct Scenario
        {
            public Scenario(string name, WorldConfig config, int renderDistance)
            {
                Name = name;
                Config = config;
                RenderDistance = renderDistance;
            }

            public string Name { get; }
            public WorldConfig Config { get; }
            public int RenderDistance { get; }
        }

        public static IReadOnlyList<WorldStorageResult> Run(IReadOnlyList<Scenario> scenarios)
        {
            var results = new List<WorldStorageResult>();
            foreach (Scenario scenario in scenarios)
            {
                foreach ((string label, Func<Vector3Int, IVoxelStorage> create) in Schemes(scenario.Config.ChunkSize + Vector3Int.one))
                {
                    results.Add(Measure(scenario, label, create));
                }
            }
            return results;
        }

        public static string ToMarkdown(IReadOnlyList<WorldStorageResult> results)
        {
            var text = new StringBuilder();
            text.AppendLine("| World | Storage | Chunks | Memory | vs flat | Load ms | Mesh ms | Stroke ms | Edited | Save size | Walk ms/step | Memory after walk |");
            text.AppendLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            long flat = 0;
            foreach (WorldStorageResult r in results)
            {
                if (r.Storage == "Flat float")
                {
                    flat = r.MemoryBytes;
                }
                text.AppendLine(
                    $"| {r.World} | {r.Storage} | {r.Chunks} | {BenchmarkStats.Bytes(r.MemoryBytes)} | " +
                    $"{(flat > 0 ? (double)r.MemoryBytes / flat : 1):0.00}× | {r.LoadMs:0} | {r.MeshMs:0.0} | {r.StrokeMs:0.00} | " +
                    $"{r.EditedChunks} | {BenchmarkStats.Bytes(r.SavedBytes)} | {r.WalkStepMs:0.0} | {BenchmarkStats.Bytes(r.MemoryAfterWalk)} |");
            }
            return text.ToString();
        }

        private static IEnumerable<(string, Func<Vector3Int, IVoxelStorage>)> Schemes(Vector3Int samples)
        {
            yield return ("Flat float", s => new FlatVoxelStorage(s));
            yield return ("Flat byte", s => new ByteVoxelStorage(s));
            yield return ("Run-length X", s => new RunLengthVoxelStorage(s, 0));
            yield return ("Run-length Y", s => new RunLengthVoxelStorage(s, 1));

            // 4³ bricks, the smaller octree in K26.
            int depth = new OctreeVoxelStorage(samples, 1).DeepestDepth - 2;
            yield return ("Octree, 4³ bricks", s => new OctreeVoxelStorage(s, depth));
        }

        private static WorldStorageResult Measure(Scenario scenario, string label, Func<Vector3Int, IVoxelStorage> create)
        {
            WorldConfig config = scenario.Config;
            ITerrainGenerator generator = TerrainGenerators.Create(config.Terrain);
            var world = new World(config.ChunkSize, config.VoxelSize, create);
            var coords = new List<Vector3Int>();
            StreamingArea.Collect(Vector3Int.zero, scenario.RenderDistance, config.WorldHeightInChunks, coords);
            int chunkCount = coords.Count;

            // Load (generate) everything around the origin.
            var stopwatch = Stopwatch.StartNew();
            foreach (Vector3Int coord in coords)
            {
                world.Load(coord, generator);
            }
            double loadMs = stopwatch.Elapsed.TotalMilliseconds;
            long memory = MemoryOf(world.Chunks.Values);

            // Mesh it all.
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            ChunkMeshSettings settings = config.MeshSettings;
            stopwatch.Restart();
            foreach (Chunk chunk in world.Chunks.Values)
            {
                ChunkMesher.Build(chunk, settings, vertices, triangles);
            }
            double meshMs = stopwatch.Elapsed.TotalMilliseconds;

            // Brush strokes near the surface across the loaded area, the same for every scheme.
            var random = new System.Random(12);
            float extent = scenario.RenderDistance * world.Grid.ChunkWorldSize.x;
            float height = config.Terrain.SurfaceLevel;
            var brush = new BrushSettings(3f);
            var strokeMs = new double[Strokes];
            for (int i = 0; i < Strokes; i++)
            {
                var centre = new Vector3(
                    ((float)random.NextDouble() * 2f - 1f) * extent * 0.6f,
                    height + ((float)random.NextDouble() * 2f - 1f) * config.Terrain.Amplitude,
                    ((float)random.NextDouble() * 2f - 1f) * extent * 0.6f);
                stopwatch.Restart();
                world.ApplyBrush(centre, brush, i % 2 == 0 ? BrushOperation.Remove : BrushOperation.Add);
                strokeMs[i] = stopwatch.Elapsed.TotalMilliseconds;
            }

            // Edited chunks only: a save regenerates the rest from the seed (S2).
            long saved = 0;
            foreach (KeyValuePair<Vector3Int, Chunk> entry in world.Chunks)
            {
                if (world.IsEdited(entry.Key))
                {
                    saved += VoxelStorages.SerializedBytes(entry.Value.Storage);
                }
            }
            int edited = world.EditedCount;

            // Walk along +X a chunk at a time: unload what leaves the area, load what enters.
            var wanted = new HashSet<Vector3Int>();
            var stepMs = new double[WalkSteps];
            for (int step = 1; step <= WalkSteps; step++)
            {
                StreamingArea.Collect(new Vector3Int(step, 0, 0), scenario.RenderDistance, config.WorldHeightInChunks, coords);
                wanted.Clear();
                wanted.UnionWith(coords);
                stopwatch.Restart();
                var leaving = new List<Vector3Int>();
                foreach (Vector3Int coord in world.Chunks.Keys)
                {
                    if (!wanted.Contains(coord))
                    {
                        leaving.Add(coord);
                    }
                }
                foreach (Vector3Int coord in leaving)
                {
                    world.Unload(coord);
                }
                foreach (Vector3Int coord in coords)
                {
                    world.Load(coord, generator);
                }
                stepMs[step - 1] = stopwatch.Elapsed.TotalMilliseconds;
            }
            long afterWalk = MemoryOf(world.Chunks.Values) + MemoryOf(world.KeptChunks.Values);

            return new WorldStorageResult(
                scenario.Name, label, chunkCount, memory, loadMs, meshMs, BenchmarkStats.Median(strokeMs),
                edited, saved, BenchmarkStats.Median(stepMs), afterWalk);
        }

        private static long MemoryOf(IEnumerable<Chunk> chunks)
        {
            long bytes = 0;
            foreach (Chunk chunk in chunks)
            {
                bytes += chunk.Storage.MemoryBytes;
            }
            return bytes;
        }
    }
}
