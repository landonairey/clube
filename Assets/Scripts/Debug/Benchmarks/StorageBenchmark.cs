using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>One row of the storage benchmark: one scheme on one terrain at one chunk size.</summary>
    public readonly struct StorageResult
    {
        public StorageResult(
            string terrain, int chunkSize, string storage, long memoryBytes, long serializedBytes, string structure,
            double fillMs, double meshMs, double brushMs)
        {
            Terrain = terrain;
            ChunkSize = chunkSize;
            Storage = storage;
            MemoryBytes = memoryBytes;
            SerializedBytes = serializedBytes;
            Structure = structure;
            FillMs = fillMs;
            MeshMs = meshMs;
            BrushMs = brushMs;
        }

        public string Terrain { get; }
        public int ChunkSize { get; }
        public string Storage { get; }
        public long MemoryBytes { get; }
        public long SerializedBytes { get; }

        /// <summary>Runs or nodes and bricks: how much structure the scheme needed.</summary>
        public string Structure { get; }

        /// <summary>Median time to generate the chunk into the storage (bulk writes).</summary>
        public double FillMs { get; }

        /// <summary>Median time to mesh it (reads through ReadLayer); NaN when not measured.</summary>
        public double MeshMs { get; }

        /// <summary>Median time per brush stroke (scattered writes); NaN when not measured.</summary>
        public double BrushMs { get; }
    }

    /// <summary>
    /// K26: compares the voxel storage schemes (K23-K28) on generated terrain: memory held,
    /// serialized size, how much structure they needed, and the time to fill a chunk (writes),
    /// mesh it (reads, through <see cref="IVoxelStorage.ReadLayer"/>) and apply brush strokes
    /// (scattered writes). A quantized pass refills the compressed schemes with densities
    /// rounded to 1/255, to see what single-byte values would do to their compression.
    /// </summary>
    public static class StorageBenchmark
    {
        public static readonly int[] DefaultSizes = { 16, 32, 64 };

        private const int FillRuns = 5;
        private const int MeshRuns = 15;
        private const int BrushStrokes = 40;

        private static readonly VoxelStorageType[] Schemes =
        {
            VoxelStorageType.Flat, VoxelStorageType.FlatByte,
            VoxelStorageType.RunLengthX, VoxelStorageType.RunLengthY, VoxelStorageType.RunLengthZ,
        };

        public static IReadOnlyList<StorageResult> Run(IReadOnlyList<int> sizes)
        {
            var results = new List<StorageResult>();
            foreach (int size in sizes)
            {
                foreach ((string name, ITerrainGenerator generator) in Terrains(size))
                {
                    var samples = new Vector3Int(size + 1, size + 1, size + 1);
                    foreach ((string label, Func<IVoxelStorage> create) in Variants(samples))
                    {
                        results.Add(Measure(name, size, label, create, generator));
                    }
                    foreach ((string label, Func<IVoxelStorage> create) in Variants(samples))
                    {
                        if (label.StartsWith("Run-length Y") || label.StartsWith("Octree"))
                        {
                            results.Add(MeasureQuantized(name, size, label, create, generator));
                        }
                    }
                }
            }
            return results;
        }

        public static string ToMarkdown(IReadOnlyList<StorageResult> results)
        {
            var text = new StringBuilder();
            text.AppendLine("| Terrain | Chunk | Storage | Memory | vs flat | Serialized | Structure | Fill ms | Mesh ms | Brush ms/stroke |");
            text.AppendLine("|---|---|---|--:|--:|--:|---|--:|--:|--:|");
            long flatBytes = 0;
            foreach (StorageResult r in results)
            {
                if (r.Storage == "Flat float")
                {
                    flatBytes = r.MemoryBytes;
                }
                text.AppendLine(
                    $"| {r.Terrain} | {r.ChunkSize}³ | {r.Storage} | {BenchmarkStats.Bytes(r.MemoryBytes)} | " +
                    $"{(flatBytes > 0 ? (double)r.MemoryBytes / flatBytes : 1):0.00}× | {BenchmarkStats.Bytes(r.SerializedBytes)} | {r.Structure} | " +
                    $"{Ms(r.FillMs)} | {Ms(r.MeshMs)} | {Ms(r.BrushMs)} |");
            }
            return text.ToString();
        }

        // Rolling hills (mostly solid below, air above) and 3D noise (caves and overhangs).
        private static IEnumerable<(string, ITerrainGenerator)> Terrains(int size)
        {
            yield return ("Hills", new FractalPerlin2DGenerator(1, size * 0.5f, size * 0.25f, 1.2f / size, octaves: 4));
            yield return ("Caves", new Perlin3DGenerator(1, size * 0.5f, size * 0.5f, 2.5f / size));
        }

        private static IEnumerable<(string, Func<IVoxelStorage>)> Variants(Vector3Int samples)
        {
            foreach (VoxelStorageType scheme in Schemes)
            {
                yield return (Label(scheme), () => VoxelStorages.Create(scheme, samples));
            }

            // Two octree depths: bricks of 8 and of 4 samples a side.
            int deepest = new OctreeVoxelStorage(samples, 1).DeepestDepth;
            foreach (int brick in new[] { 8, 4 })
            {
                int depth = deepest - (int)Math.Round(Math.Log(brick, 2));
                yield return ($"Octree, {brick}³ bricks", () => new OctreeVoxelStorage(samples, depth));
            }
        }

        private static StorageResult Measure(string terrain, int size, string label, Func<IVoxelStorage> create, ITerrainGenerator generator)
        {
            // Fill: a new storage each time, so every run writes into empty storage.
            var fillMs = new double[FillRuns];
            Chunk chunk = null;
            var stopwatch = new Stopwatch();
            for (int i = 0; i < FillRuns; i++)
            {
                chunk = new Chunk(create());
                stopwatch.Restart();
                ChunkGenerator.Fill(chunk, generator, Vector3.zero, 1f);
                fillMs[i] = stopwatch.Elapsed.TotalMilliseconds;
            }

            IVoxelStorage storage = chunk.Storage;
            long memory = storage.MemoryBytes;
            long serialized = VoxelStorages.SerializedBytes(storage);
            string structure = Describe(storage);

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var settings = new ChunkMeshSettings(0.5f, 1f);
            ChunkMesher.Build(chunk, settings, vertices, triangles);
            var meshMs = new double[MeshRuns];
            for (int i = 0; i < MeshRuns; i++)
            {
                stopwatch.Restart();
                ChunkMesher.Build(chunk, settings, vertices, triangles);
                meshMs[i] = stopwatch.Elapsed.TotalMilliseconds;
            }

            // Strokes near the surface, digging and adding in turn, at the same places for every scheme.
            var random = new System.Random(size);
            var brush = new BrushSettings(size * 0.12f);
            var brushMs = new double[BrushStrokes];
            for (int i = 0; i < BrushStrokes; i++)
            {
                var centre = new Vector3(
                    (float)random.NextDouble() * size, size * (0.35f + 0.3f * (float)random.NextDouble()), (float)random.NextDouble() * size);
                stopwatch.Restart();
                TerrainBrush.Apply(chunk, centre, 1f, brush, i % 2 == 0 ? BrushOperation.Remove : BrushOperation.Add);
                brushMs[i] = stopwatch.Elapsed.TotalMilliseconds;
            }

            return new StorageResult(
                terrain, size, label, memory, serialized, structure,
                BenchmarkStats.Median(fillMs), BenchmarkStats.Median(meshMs), BenchmarkStats.Median(brushMs));
        }

        // The same terrain with every density rounded to 1/255 first: more equal neighbours, so longer runs and more collapse.
        private static StorageResult MeasureQuantized(string terrain, int size, string label, Func<IVoxelStorage> create, ITerrainGenerator generator)
        {
            var chunk = new Chunk(create());
            ChunkGenerator.Fill(chunk, new Quantized(generator), Vector3.zero, 1f);
            IVoxelStorage storage = chunk.Storage;
            return new StorageResult(
                terrain, size, label + ", quantized", storage.MemoryBytes, VoxelStorages.SerializedBytes(storage), Describe(storage),
                double.NaN, double.NaN, double.NaN);
        }

        private static string Describe(IVoxelStorage storage)
        {
            switch (storage)
            {
                case RunLengthVoxelStorage runs:
                    return $"{runs.RunCount:N0} runs";
                case OctreeVoxelStorage octree:
                    return $"{octree.NodeCount:N0} nodes, {octree.BrickCount:N0} bricks";
                default:
                    return "–";
            }
        }

        private static string Label(VoxelStorageType scheme)
        {
            switch (scheme)
            {
                case VoxelStorageType.Flat: return "Flat float";
                case VoxelStorageType.FlatByte: return "Flat byte";
                case VoxelStorageType.RunLengthX: return "Run-length X";
                case VoxelStorageType.RunLengthY: return "Run-length Y";
                case VoxelStorageType.RunLengthZ: return "Run-length Z";
                default: return scheme.ToString();
            }
        }

        private static string Ms(double value)
        {
            return double.IsNaN(value) ? "–" : value.ToString("0.00");
        }

        /// <summary>A generator whose densities are rounded to 1/255, as single-byte storage would keep them.</summary>
        private sealed class Quantized : ITerrainGenerator
        {
            private readonly ITerrainGenerator inner;

            public Quantized(ITerrainGenerator inner)
            {
                this.inner = inner;
            }

            public float Density(Vector3 position)
            {
                return ByteVoxelStorage.Quantize(inner.Density(position)) / 255f;
            }
        }
    }
}
