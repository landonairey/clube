using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>One row of the K32 benchmark: the core mesher at one chunk size and shading.</summary>
    public readonly struct MesherSpeedResult
    {
        public MesherSpeedResult(int chunkSize, Shading shading, int vertices, int triangles, double medianMs, double meanMs)
        {
            ChunkSize = chunkSize;
            Shading = shading;
            Vertices = vertices;
            Triangles = triangles;
            MedianMs = medianMs;
            MeanMs = meanMs;
        }

        /// <summary>Voxels along each axis of the cubic chunk.</summary>
        public int ChunkSize { get; }

        public Shading Shading { get; }

        public int Vertices { get; }

        public int Triangles { get; }

        public double MedianMs { get; }

        /// <summary>Includes any garbage collections the builds triggered, unlike the median.</summary>
        public double MeanMs { get; }

        /// <summary>Median build time spread over every voxel in the chunk.</summary>
        public double NanosecondsPerVoxel => MedianMs * 1e6 / ((double)ChunkSize * ChunkSize * ChunkSize);
    }

    /// <summary>
    /// K32: times <see cref="ChunkMesher.Build"/> on the K11 terrain, flat and smooth,
    /// so each speed-up to the per-voxel loop can be measured against the last.
    /// Warmup builds first, then timed builds (Stopwatch); the median is the headline.
    /// </summary>
    public static class MesherSpeedBenchmark
    {
        public static IReadOnlyList<MesherSpeedResult> Run(IReadOnlyList<int> sizes, int warmup = 5, int runs = 30)
        {
            var results = new List<MesherSpeedResult>();
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (int size in sizes)
            {
                Chunk chunk = BenchmarkTerrain.Hills(size);
                foreach (Shading shading in new[] { Shading.Flat, Shading.Smooth })
                {
                    var settings = new ChunkMeshSettings(0.5f, 1f, EdgePlacement.Interpolated, shading);
                    for (int i = 0; i < warmup; i++)
                    {
                        ChunkMesher.Build(chunk, settings, vertices, triangles);
                    }

                    var timesMs = new double[runs];
                    var stopwatch = new Stopwatch();
                    for (int i = 0; i < runs; i++)
                    {
                        stopwatch.Restart();
                        ChunkMesher.Build(chunk, settings, vertices, triangles);
                        timesMs[i] = stopwatch.Elapsed.TotalMilliseconds;
                    }

                    results.Add(new MesherSpeedResult(
                        size, shading, vertices.Count, triangles.Count / 3,
                        BenchmarkStats.Median(timesMs), BenchmarkStats.Mean(timesMs)));
                }
            }
            return results;
        }

        /// <summary>The results as a Markdown table, one row per chunk size and shading.</summary>
        public static string ToMarkdown(IReadOnlyList<MesherSpeedResult> results)
        {
            var text = new StringBuilder();
            text.AppendLine("| Chunk | Shading | Vertices | Triangles | Mesh ms (median) | Mesh ms (mean) | ns per voxel |");
            text.AppendLine("|---|---|--:|--:|--:|--:|--:|");
            foreach (MesherSpeedResult r in results)
            {
                text.AppendLine(
                    $"| {r.ChunkSize}³ | {r.Shading} | {r.Vertices:N0} | {r.Triangles:N0} | " +
                    $"{r.MedianMs:0.00} | {r.MeanMs:0.00} | {r.NanosecondsPerVoxel:0} |");
            }
            return text.ToString();
        }
    }
}
