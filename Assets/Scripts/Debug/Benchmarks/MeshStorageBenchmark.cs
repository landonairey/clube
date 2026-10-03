using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>One row of the K11 benchmark: one storage variant at one chunk size.</summary>
    public readonly struct MeshStorageResult
    {
        public MeshStorageResult(
            string variant, int chunkSize, int triangles, double meshMedianMs, double meshMeanMs,
            double uploadMedianMs, double allocatedBytesPerBuild, long retainedBytes, bool matchesCore)
        {
            Variant = variant;
            ChunkSize = chunkSize;
            Triangles = triangles;
            MeshMedianMs = meshMedianMs;
            MeshMeanMs = meshMeanMs;
            UploadMedianMs = uploadMedianMs;
            AllocatedBytesPerBuild = allocatedBytesPerBuild;
            RetainedBytes = retainedBytes;
            MatchesCore = matchesCore;
        }

        public string Variant { get; }

        /// <summary>Voxels along each axis of the cubic chunk.</summary>
        public int ChunkSize { get; }

        public int Triangles { get; }

        public double MeshMedianMs { get; }

        /// <summary>Includes any garbage collections the builds triggered, unlike the median.</summary>
        public double MeshMeanMs { get; }

        public double UploadMedianMs { get; }

        /// <summary>
        /// Managed memory allocated by one build (meshing only), from heap growth averaged
        /// over the runs; the heap grows in pages, so small amounts are approximate.
        /// </summary>
        public double AllocatedBytesPerBuild { get; }

        /// <summary>Memory the variant keeps between builds.</summary>
        public long RetainedBytes { get; }

        /// <summary>The variant built exactly the same vertices and indices as <see cref="ChunkMesher"/>.</summary>
        public bool MatchesCore { get; }
    }

    /// <summary>
    /// K11: times mesh building with <see cref="List{T}"/> vs preallocated arrays.
    /// Each variant runs the same flat-shaded loop (<see cref="StorageMesher"/>) over
    /// the same generated terrain: warmup builds first, then timed builds (Stopwatch)
    /// with their managed allocations counted. The core <see cref="ChunkMesher"/> runs
    /// too, as the reference the variants' output must match.
    /// </summary>
    public static class MeshStorageBenchmark
    {
        public static readonly int[] DefaultSizes = { 8, 16, 32, 64 };

        public static IReadOnlyList<MeshStorageResult> Run(IReadOnlyList<int> sizes, int warmup = 5, int runs = 30)
        {
            var results = new List<MeshStorageResult>();
            var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                foreach (int size in sizes)
                {
                    Chunk chunk = TerrainChunk(size);
                    var settings = new ChunkMeshSettings(0.5f, 1f, EdgePlacement.Interpolated, Shading.Flat);

                    var expectedVertices = new List<Vector3>();
                    var expectedIndices = new List<int>();
                    results.Add(MeasureCore(chunk, settings, mesh, warmup, runs, expectedVertices, expectedIndices));

                    var context = new Context(chunk, settings, mesh, warmup, runs, expectedVertices, expectedIndices);
                    results.Add(Measure("List, new each build", new ListMeshStorage(reuse: false), context));
                    results.Add(Measure("List, reused", new ListMeshStorage(reuse: true), context));
                    results.Add(Measure("Array, preallocated", new ArrayMeshStorage(size * size * size), context));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
            return results;
        }

        /// <summary>The results as a Markdown table, one block of rows per chunk size.</summary>
        public static string ToMarkdown(IReadOnlyList<MeshStorageResult> results)
        {
            var text = new StringBuilder();
            text.AppendLine("| Chunk | Variant | Triangles | Mesh ms (median) | Mesh ms (mean) | Upload ms (median) | Allocated per build | Kept between builds | Matches core |");
            text.AppendLine("|---|---|--:|--:|--:|--:|--:|--:|:-:|");
            foreach (MeshStorageResult r in results)
            {
                text.AppendLine(
                    $"| {r.ChunkSize}³ | {r.Variant} | {r.Triangles:N0} | {r.MeshMedianMs:0.00} | {r.MeshMeanMs:0.00} | " +
                    $"{r.UploadMedianMs:0.00} | {Bytes(r.AllocatedBytesPerBuild)} | {Bytes(r.RetainedBytes)} | {(r.MatchesCore ? "yes" : "**no**")} |");
            }
            return text.ToString();
        }

        // Rolling hills filling the middle of the chunk, the same shape at every size.
        private static Chunk TerrainChunk(int size)
        {
            var chunk = new Chunk(new Vector3Int(size, size, size));
            var generator = new FractalPerlin2DGenerator(1, size * 0.5f, size * 0.25f, 1.2f / size, octaves: 4);
            ChunkGenerator.Fill(chunk, generator, Vector3.zero, 1f);
            return chunk;
        }

        private static MeshStorageResult MeasureCore(
            Chunk chunk, ChunkMeshSettings settings, Mesh mesh, int warmup, int runs,
            List<Vector3> vertices, List<int> triangles)
        {
            Timings timings = Time(
                () => ChunkMesher.Build(chunk, settings, vertices, triangles),
                () =>
                {
                    mesh.Clear();
                    mesh.indexFormat = vertices.Count > ushort.MaxValue
                        ? UnityEngine.Rendering.IndexFormat.UInt32
                        : UnityEngine.Rendering.IndexFormat.UInt16;
                    mesh.SetVertices(vertices);
                    mesh.SetTriangles(triangles, 0);
                },
                warmup, runs);

            long retained = vertices.Capacity * 12L + triangles.Capacity * 4L;
            return timings.ToResult("Core `ChunkMesher` (reference)", chunk, triangles.Count / 3, retained, matchesCore: true);
        }

        private static MeshStorageResult Measure<TStorage>(string variant, TStorage storage, Context context)
            where TStorage : struct, IMeshStorage
        {
            Timings timings = Time(
                () => StorageMesher.BuildFlat(context.Chunk, context.Settings, ref storage),
                () => storage.Upload(context.Mesh),
                context.Warmup, context.Runs);

            var vertices = new List<Vector3>();
            var indices = new List<int>();
            storage.CopyTo(vertices, indices);
            bool matches = Same(vertices, context.ExpectedVertices) && Same(indices, context.ExpectedIndices);
            return timings.ToResult(variant, context.Chunk, storage.IndexCount / 3, storage.RetainedBytes, matches);
        }

        private static Timings Time(Action build, Action upload, int warmup, int runs)
        {
            for (int i = 0; i < warmup; i++)
            {
                build();
                upload();
            }

            // Start each variant from a clean heap, so one variant's garbage isn't collected on another's time.
            GC.Collect();
            GC.WaitForPendingFinalizers();

            var timings = new Timings(runs);
            var stopwatch = new Stopwatch();
            for (int i = 0; i < runs; i++)
            {
                // GC.GetAllocatedBytesForCurrentThread always reads 0 on Unity's Mono, so this
                // measures heap growth instead. A collection mid-build shrinks the heap: skip that run.
                long heapBefore = GC.GetTotalMemory(false);
                stopwatch.Restart();
                build();
                timings.MeshMs[i] = stopwatch.Elapsed.TotalMilliseconds;
                long growth = GC.GetTotalMemory(false) - heapBefore;
                if (growth >= 0)
                {
                    timings.AllocatedBytes += growth;
                    timings.AllocationSamples++;
                }

                stopwatch.Restart();
                upload();
                timings.UploadMs[i] = stopwatch.Elapsed.TotalMilliseconds;
            }
            return timings;
        }

        private static bool Same<T>(List<T> a, List<T> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }
            var comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < a.Count; i++)
            {
                if (!comparer.Equals(a[i], b[i]))
                {
                    return false;
                }
            }
            return true;
        }

        private static string Bytes(double bytes)
        {
            if (double.IsNaN(bytes))
            {
                return "n/a";
            }
            if (bytes < 1024)
            {
                return $"{bytes:0} B";
            }
            return bytes < 1024 * 1024 ? $"{bytes / 1024:0.0} KB" : $"{bytes / (1024 * 1024):0.0} MB";
        }

        private static double Median(double[] values)
        {
            var sorted = (double[])values.Clone();
            Array.Sort(sorted);
            int middle = sorted.Length / 2;
            return sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
        }

        private static double Mean(double[] values)
        {
            double sum = 0;
            foreach (double value in values)
            {
                sum += value;
            }
            return sum / values.Length;
        }

        /// <summary>What every variant at one chunk size is measured against.</summary>
        private sealed class Context
        {
            public Context(
                Chunk chunk, ChunkMeshSettings settings, Mesh mesh, int warmup, int runs,
                List<Vector3> expectedVertices, List<int> expectedIndices)
            {
                Chunk = chunk;
                Settings = settings;
                Mesh = mesh;
                Warmup = warmup;
                Runs = runs;
                ExpectedVertices = expectedVertices;
                ExpectedIndices = expectedIndices;
            }

            public Chunk Chunk { get; }
            public ChunkMeshSettings Settings { get; }
            public Mesh Mesh { get; }
            public int Warmup { get; }
            public int Runs { get; }
            public List<Vector3> ExpectedVertices { get; }
            public List<int> ExpectedIndices { get; }
        }

        private sealed class Timings
        {
            public Timings(int runs)
            {
                MeshMs = new double[runs];
                UploadMs = new double[runs];
            }

            public double[] MeshMs { get; }
            public double[] UploadMs { get; }
            public long AllocatedBytes { get; set; }
            public int AllocationSamples { get; set; }

            public MeshStorageResult ToResult(string variant, Chunk chunk, int triangles, long retainedBytes, bool matchesCore)
            {
                return new MeshStorageResult(
                    variant, chunk.VoxelCount.x, triangles, Median(MeshMs), Mean(MeshMs), Median(UploadMs),
                    AllocationSamples > 0 ? (double)AllocatedBytes / AllocationSamples : double.NaN, retainedBytes, matchesCore);
            }
        }
    }
}
