using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Clube.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>One row of the mesher benchmark: one mesher at one chunk size and shading.</summary>
    public readonly struct MesherSpeedResult
    {
        public MesherSpeedResult(
            int chunkSize, MesherBackend backend, Shading shading, int vertices, int triangles, double medianMs, double meanMs)
        {
            ChunkSize = chunkSize;
            Backend = backend;
            Shading = shading;
            Vertices = vertices;
            Triangles = triangles;
            MedianMs = medianMs;
            MeanMs = meanMs;
        }

        /// <summary>Voxels along each axis of the cubic chunk.</summary>
        public int ChunkSize { get; }

        public MesherBackend Backend { get; }

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
    /// K32 and K12: times the managed <see cref="ChunkMesher"/> and the Burst job
    /// (<see cref="ChunkMeshJob"/>, built and completed on this thread) on the K11 terrain,
    /// flat and smooth, so each mesher change can be measured against the last.
    /// Warmup builds first (which also compiles the Burst job), then timed builds (Stopwatch);
    /// the median is the headline.
    /// </summary>
    public static class MesherSpeedBenchmark
    {
        public static IReadOnlyList<MesherSpeedResult> Run(IReadOnlyList<int> sizes, int warmup = 5, int runs = 30)
        {
            var results = new List<MesherSpeedResult>();
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var mesh = new Mesh();
            try
            {
                foreach (int size in sizes)
                {
                    Chunk chunk = BenchmarkTerrain.Hills(size);
                    foreach (Shading shading in new[] { Shading.Flat, Shading.Smooth })
                    {
                        var settings = new ChunkMeshSettings(0.5f, 1f, EdgePlacement.Interpolated, shading);

                        (double median, double mean) = Time(() => ChunkMesher.Build(chunk, settings, vertices, triangles), warmup, runs);
                        results.Add(new MesherSpeedResult(
                            size, MesherBackend.Managed, shading, vertices.Count, triangles.Count / 3, median, mean));

                        // The job alone (snapshot, meshing, normals, buffers): handing the result to a mesh is upload, not meshing.
                        (median, mean) = Time(() => BuildWithJob(chunk, settings), warmup, runs);
                        ChunkMeshJobs.BuildNow(chunk, settings, mesh);
                        results.Add(new MesherSpeedResult(
                            size, MesherBackend.Burst, shading, mesh.vertexCount, (int)mesh.GetIndexCount(0) / 3, median, mean));
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
            return results;
        }

        /// <summary>The results as a Markdown table, one row per chunk size, shading and mesher.</summary>
        public static string ToMarkdown(IReadOnlyList<MesherSpeedResult> results)
        {
            var text = new StringBuilder();
            text.AppendLine("| Chunk | Shading | Mesher | Vertices | Triangles | Mesh ms (median) | Mesh ms (mean) | ns per voxel |");
            text.AppendLine("|---|---|---|--:|--:|--:|--:|--:|");
            foreach (MesherSpeedResult r in results)
            {
                text.AppendLine(
                    $"| {r.ChunkSize}³ | {r.Shading} | {r.Backend} | {r.Vertices:N0} | {r.Triangles:N0} | " +
                    $"{r.MedianMs:0.00} | {r.MeanMs:0.00} | {r.NanosecondsPerVoxel:0} |");
            }
            return text.ToString();
        }

        private static void BuildWithJob(Chunk chunk, ChunkMeshSettings settings)
        {
            Mesh.MeshDataArray data = Mesh.AllocateWritableMeshData(1);
            ChunkMeshJobs.Schedule(ChunkMeshInput.Snapshot(chunk, Allocator.TempJob), settings, data).Complete();
            data.Dispose();
        }

        private static (double Median, double Mean) Time(Action build, int warmup, int runs)
        {
            for (int i = 0; i < warmup; i++)
            {
                build();
            }

            var timesMs = new double[runs];
            var stopwatch = new Stopwatch();
            for (int i = 0; i < runs; i++)
            {
                stopwatch.Restart();
                build();
                timesMs[i] = stopwatch.Elapsed.TotalMilliseconds;
            }
            return (BenchmarkStats.Median(timesMs), BenchmarkStats.Mean(timesMs));
        }
    }
}
