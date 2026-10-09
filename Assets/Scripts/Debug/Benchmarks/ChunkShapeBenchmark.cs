using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Clube.Core;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>One chunk shape over one area: what the same terrain costs built that way.</summary>
    public readonly struct ChunkShapeResult
    {
        public ChunkShapeResult(
            string area, string shape, int chunks, int surfaceChunks, long storedBytes, double generateMs, double generateParallelMs,
            double meshMs, double meshParallelMs, long vertices, int largestMesh, int meshesOver16Bit,
            double editMedianMs, double editMaxMs, double editChunks)
        {
            Area = area;
            Shape = shape;
            Chunks = chunks;
            SurfaceChunks = surfaceChunks;
            StoredBytes = storedBytes;
            GenerateMs = generateMs;
            GenerateParallelMs = generateParallelMs;
            MeshMs = meshMs;
            MeshParallelMs = meshParallelMs;
            Vertices = vertices;
            LargestMesh = largestMesh;
            MeshesOver16Bit = meshesOver16Bit;
            EditMedianMs = editMedianMs;
            EditMaxMs = editMaxMs;
            EditChunks = editChunks;
        }

        public string Area { get; }

        public string Shape { get; }

        public int Chunks { get; }

        /// <summary>Chunks with a surface; the others are uniform (all air or all solid) and keep no data and no mesh.</summary>
        public int SurfaceChunks { get; }

        /// <summary>Density and material bytes kept for the area (uniform chunks and uniform materials keep none).</summary>
        public long StoredBytes { get; }

        public double GenerateMs { get; }

        public double GenerateParallelMs { get; }

        /// <summary>Burst mesh jobs one chunk after another (with the material pass).</summary>
        public double MeshMs { get; }

        /// <summary>Burst mesh jobs for every surface chunk at once, over the worker threads.</summary>
        public double MeshParallelMs { get; }

        public long Vertices { get; }

        /// <summary>Most vertices in one chunk's mesh (one GameObject).</summary>
        public int LargestMesh { get; }

        /// <summary>Meshes past 65,535 vertices, which need 32-bit indices.</summary>
        public int MeshesOver16Bit { get; }

        /// <summary>Median time from a 1 m dig to the dirty chunks remeshed, uploaded and their colliders baked: the frame cost of an edit.</summary>
        public double EditMedianMs { get; }

        public double EditMaxMs { get; }

        /// <summary>Chunks each dig dirtied, on average.</summary>
        public double EditChunks { get; }
    }

    /// <summary>
    /// The chunk shape decision (M17): the same terrain built as stacked cubic chunks (the
    /// current design) and as tall columns the full world height, over the same areas. Measures
    /// what each keeps in memory, how long generation and meshing take (one by one and over the
    /// workers), how big the meshes get, and what a dig costs: remeshing every chunk it dirtied,
    /// uploading the meshes and baking their colliders, which the streamer does in the frame of
    /// the edit.
    /// </summary>
    public static class ChunkShapeBenchmark
    {
        private const float DigRadius = 1f;

        /// <summary>An area to measure: a square of chunk columns, by its centre column (x, z) and width.</summary>
        public readonly struct Area
        {
            public Area(string name, Vector2Int centre, int width)
            {
                Name = name;
                Centre = centre;
                Width = width;
            }

            public string Name { get; }

            public Vector2Int Centre { get; }

            public int Width { get; }
        }

        public static IReadOnlyList<ChunkShapeResult> Run(WorldConfig config, IReadOnlyList<Area> areas, int digs = 25, int runs = 3)
        {
            var results = new List<ChunkShapeResult>();
            ITerrainGenerator generator = TerrainGenerators.Create(config.Terrain);
            OreField ores = OreField.Create(config.Terrain, generator);
            int side = config.ChunkSize.x;
            int layers = config.WorldHeightInChunks;
            var shapes = new[]
            {
                ($"{side}³ cubes x {layers} layers", new Vector3Int(side, side, side), layers),
                ($"{side} x {side * layers} x {side} columns", new Vector3Int(side, side * layers, side), 1),
            };
            foreach (Area area in areas)
            {
                foreach ((string name, Vector3Int chunkSize, int height) in shapes)
                {
                    results.Add(Measure(config, generator, ores, area, name, chunkSize, height, digs, runs));
                }
            }
            return results;
        }

        public static string ToMarkdown(WorldConfig config, IReadOnlyList<ChunkShapeResult> results)
        {
            var text = new StringBuilder();
            text.AppendLine(
                $"Terrain: `{config.name}`, {config.Terrain.Generator}, {config.VoxelSize} m voxels, " +
                $"{config.ChunkSize.x * config.VoxelSize * config.WorldHeightInChunks} m tall, storage {config.Storage}, " +
                $"{config.Shading} shading, materials {config.MaterialDisplay}, {SystemInfo.processorCount} threads. " +
                $"Edits: {DigRadius} m digs at surface spots, remesh + upload + collider bake of every chunk dirtied.");
            text.AppendLine();
            text.AppendLine("| Area | Shape | Chunks | With surface | Stored | Generate ms | Generate ms (parallel) | Mesh ms | Mesh ms (parallel) | Vertices | Largest mesh | Over 16-bit | Dig ms (median) | Dig ms (max) | Chunks per dig |");
            text.AppendLine("|--|--|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            foreach (ChunkShapeResult r in results)
            {
                text.AppendLine(
                    $"| {r.Area} | {r.Shape} | {r.Chunks:N0} | {r.SurfaceChunks:N0} | {BenchmarkStats.Bytes(r.StoredBytes)} | " +
                    $"{r.GenerateMs:0.0} | {r.GenerateParallelMs:0.0} | {r.MeshMs:0.0} | {r.MeshParallelMs:0.0} | {r.Vertices:N0} | " +
                    $"{r.LargestMesh:N0} | {r.MeshesOver16Bit} | {r.EditMedianMs:0.00} | {r.EditMaxMs:0.00} | {r.EditChunks:0.0} |");
            }
            return text.ToString();
        }

        private static ChunkShapeResult Measure(
            WorldConfig config, ITerrainGenerator generator, OreField ores, Area area, string shape, Vector3Int chunkSize, int height,
            int digs, int runs)
        {
            var coords = new List<Vector3Int>();
            int half = area.Width / 2;
            for (int z = area.Centre.y - half; z < area.Centre.y - half + area.Width; z++)
            {
                for (int x = area.Centre.x - half; x < area.Centre.x - half + area.Width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        coords.Add(new Vector3Int(x, y, z));
                    }
                }
            }
            ChunkMeshSettings settings = config.MeshSettings.WithSeal(MeshSeal.None);

            var generateMs = new double[runs];
            var parallelGenerateMs = new double[runs];
            var meshMs = new double[runs];
            var parallelMeshMs = new double[runs];
            World world = null;
            for (int run = 0; run < runs; run++)
            {
                world = new World(chunkSize, config.VoxelSize, config.CreateStorage);
                var stopwatch = Stopwatch.StartNew();
                foreach (Vector3Int coord in coords)
                {
                    world.Load(coord, generator, config.Terrain.Layers, ores);
                }
                generateMs[run] = stopwatch.Elapsed.TotalMilliseconds;
                parallelGenerateMs[run] = GenerateInParallel(config, chunkSize, generator, ores, coords);

                List<Chunk> surface = SurfaceChunks(world);
                stopwatch.Restart();
                foreach (Chunk chunk in surface)
                {
                    Mesh.MeshDataArray data = Mesh.AllocateWritableMeshData(1);
                    ChunkMeshJobs.Schedule(ChunkMeshInput.Snapshot(chunk, Allocator.TempJob), settings, data).Complete();
                    data.Dispose();
                }
                meshMs[run] = stopwatch.Elapsed.TotalMilliseconds;
                parallelMeshMs[run] = MeshInParallel(surface, settings);
                if (run < runs - 1)
                {
                    world.Clear();
                }
            }

            // Sizes, from the last run's world.
            long stored = 0;
            long vertices = 0;
            int largest = 0;
            int over = 0;
            int surfaceCount = 0;
            var meshes = new Dictionary<Vector3Int, Mesh>();
            foreach (KeyValuePair<Vector3Int, Chunk> entry in world.Chunks)
            {
                Chunk chunk = entry.Value;
                long samples = (long)chunk.SampleCount.x * chunk.SampleCount.y * chunk.SampleCount.z;
                if (!chunk.IsUniform)
                {
                    stored += samples * (chunk.StoresBytes ? 1 : 4);
                    surfaceCount++;
                    var chunkMesh = new Mesh();
                    BuildInto(chunk, settings, chunkMesh);
                    meshes.Add(entry.Key, chunkMesh);
                    vertices += chunkMesh.vertexCount;
                    largest = Mathf.Max(largest, chunkMesh.vertexCount);
                    over += chunkMesh.vertexCount > ushort.MaxValue ? 1 : 0;
                }
                if (!chunk.Materials.IsUniform)
                {
                    stored += samples;
                }
            }

            // Everything is meshed now: only a dig's own chunks should count as dirty.
            foreach (Chunk chunk in world.Chunks.Values)
            {
                chunk.MarkClean();
            }

            // Digs at surface spots spread over the area: the frame cost of an edit.
            var editMs = new List<double>();
            double dirtied = 0;
            var random = new System.Random(17);
            float width = area.Width * config.ChunkSize.x * config.VoxelSize;
            Vector2 origin = ((Vector2)area.Centre - Vector2.one * half) * config.ChunkSize.x * config.VoxelSize;
            float top = config.ChunkSize.x * config.VoxelSize * config.WorldHeightInChunks + 1f;
            var brush = new BrushSettings(DigRadius);
            for (int i = 0; i < digs; i++)
            {
                var spot = new Vector3(origin.x + (float)random.NextDouble() * width, top, origin.y + (float)random.NextDouble() * width);
                if (!world.Raycast(new Ray(spot, Vector3.down), settings, top + 1f, out WorldHit hit))
                {
                    continue;
                }
                var stopwatch = Stopwatch.StartNew();
                world.ApplyBrush(hit.Point, brush, BrushOperation.Remove);
                int count = 0;
                foreach (KeyValuePair<Vector3Int, Chunk> entry in world.Chunks)
                {
                    if (!entry.Value.IsDirty)
                    {
                        continue;
                    }
                    count++;
                    if (!meshes.TryGetValue(entry.Key, out Mesh chunkMesh))
                    {
                        chunkMesh = new Mesh();
                        meshes.Add(entry.Key, chunkMesh);
                    }
                    BuildInto(entry.Value, settings, chunkMesh);
                    if (chunkMesh.vertexCount > 0)
                    {
                        EntityId meshId = chunkMesh.GetInstanceID();
                        Physics.BakeMesh(meshId, false);
                    }
                    entry.Value.MarkClean();
                }
                editMs.Add(stopwatch.Elapsed.TotalMilliseconds);
                dirtied += count;
            }

            foreach (Mesh chunkMesh in meshes.Values)
            {
                Object.DestroyImmediate(chunkMesh);
            }
            world.Clear();

            double[] edits = editMs.ToArray();
            double editMax = 0;
            foreach (double ms in edits)
            {
                editMax = System.Math.Max(editMax, ms);
            }
            return new ChunkShapeResult(
                area.Name, shape, coords.Count, surfaceCount, stored, BenchmarkStats.Median(generateMs), BenchmarkStats.Median(parallelGenerateMs),
                BenchmarkStats.Median(meshMs), BenchmarkStats.Median(parallelMeshMs), vertices, largest, over,
                edits.Length > 0 ? BenchmarkStats.Median(edits) : 0, editMax, edits.Length > 0 ? dirtied / edits.Length : 0);
        }

        // A chunk's mesh as the world builds it: the Burst job, then the upload.
        private static void BuildInto(Chunk chunk, ChunkMeshSettings settings, Mesh mesh)
        {
            Mesh.MeshDataArray data = Mesh.AllocateWritableMeshData(1);
            ChunkMeshJobs.Schedule(ChunkMeshInput.Snapshot(chunk, Allocator.TempJob), settings, data).Complete();
            ChunkMeshJobs.Apply(data, mesh, chunk.SampleCount, settings.VoxelSize);
        }

        private static List<Chunk> SurfaceChunks(World world)
        {
            var surface = new List<Chunk>();
            foreach (Chunk chunk in world.Chunks.Values)
            {
                if (!chunk.IsUniform)
                {
                    surface.Add(chunk);
                }
            }
            return surface;
        }

        // Every chunk through the pipeline at once, waited for, then taken.
        private static double GenerateInParallel(WorldConfig config, Vector3Int chunkSize, ITerrainGenerator generator, OreField ores, List<Vector3Int> coords)
        {
            var world = new World(chunkSize, config.VoxelSize, config.CreateStorage);
            var stopwatch = Stopwatch.StartNew();
            using (var pipeline = new ChunkPipeline(world.Grid, generator, config.Terrain.Layers, ores, world.PreferredFormat, world.StorageFactory))
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
            double ms = stopwatch.Elapsed.TotalMilliseconds;
            world.Clear();
            return ms;
        }

        // One job per chunk, all scheduled before any is waited on.
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
