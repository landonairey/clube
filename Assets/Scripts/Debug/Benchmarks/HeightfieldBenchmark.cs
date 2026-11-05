using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Clube.Core;
using Unity.Collections;
using Unity.Mathematics;

namespace Clube.Debug
{
    /// <summary>One row of the heightfield benchmark: one generator over one kind of land.</summary>
    public readonly struct HeightfieldResult
    {
        public HeightfieldResult(string generator, string land, double columnMs, double farMs)
        {
            Generator = generator;
            Land = land;
            ColumnMs = columnMs;
            FarMs = farMs;
        }

        public string Generator { get; }

        public string Land { get; }

        /// <summary>Median time for one chunk column's heights (17 x 17 columns at 0.25 m), on this thread.</summary>
        public double ColumnMs { get; }

        /// <summary>Median time for a 256 x 256 block of heights spread over the workers (the far terrain's kind of batch).</summary>
        public double FarMs { get; }
    }

    /// <summary>
    /// P12: what the eroded landscape costs to generate against Landscape. Times the Burst column-heights jobs (one chunk
    /// column on this thread, as streaming runs them, and one big block over the workers, as the far terrain does) on the
    /// plains and in the mountains. Warmup runs first (Burst compiles), then the median of timed runs (Stopwatch).
    /// </summary>
    public static class HeightfieldBenchmark
    {
        private const int Seed = 1;

        public static IReadOnlyList<HeightfieldResult> Run(int warmup = 5, int runs = 40)
        {
            var settings = new ErosionSettings();
            var smoothLand = new LandscapeHeight(Seed, 20f, 110f, 250f, 0.35f, settings.BaseOctaves).WithSmoothMassifs();
            var generators = new (string, HeightfieldGenerator)[]
            {
                ("Landscape", new LandscapeGenerator(Seed, 20f, 110f, 250f, 0.35f)),
                ("Eroded landscape", new ErodedLandscapeGenerator(new ErodedLandscapeHeight(smoothLand, settings.ToFilter(Seed), 20f, 110f))),
            };
            // Plains and mountains for seed 1: the land north of the origin is plains, south of it ranges.
            var places = new (string, int2)[] { ("plains", new int2(0, 600)), ("mountains", new int2(-400, -900)) };

            var results = new List<HeightfieldResult>();
            foreach ((string name, HeightfieldGenerator generator) in generators)
            {
                foreach ((string where, int2 first) in places)
                {
                    results.Add(new HeightfieldResult(name, where, TimeColumn(generator, first, warmup, runs), TimeFar(generator, first, warmup, runs / 4)));
                }
            }
            return results;
        }

        private static double TimeColumn(HeightfieldGenerator generator, int2 first, int warmup, int runs)
        {
            var block = new ColumnBlock(first, new int2(17, 17), 0.25f);
            var heights = new NativeArray<float>(block.Length, Allocator.Persistent);
            var range = new NativeArray<float2>(1, Allocator.Persistent);
            var times = new double[runs];
            var timer = new Stopwatch();
            try
            {
                for (int i = 0; i < warmup + runs; i++)
                {
                    // A new place each run, so nothing is cached between them.
                    block.FirstColumn = first + new int2(i * 17, 0);
                    timer.Restart();
                    generator.ScheduleColumnHeights(block, heights, range).Complete();
                    timer.Stop();
                    if (i >= warmup)
                    {
                        times[i - warmup] = timer.Elapsed.TotalMilliseconds;
                    }
                }
            }
            finally
            {
                heights.Dispose();
                range.Dispose();
            }
            return BenchmarkStats.Median(times);
        }

        private static double TimeFar(HeightfieldGenerator generator, int2 first, int warmup, int runs)
        {
            // 256 x 256 heights 2 m apart, as a far terrain level's batch.
            var block = new ColumnBlock(first / 8, new int2(256, 256), 2f);
            var heights = new NativeArray<float>(block.Length, Allocator.Persistent);
            var times = new double[runs];
            var timer = new Stopwatch();
            try
            {
                for (int i = 0; i < warmup + runs; i++)
                {
                    timer.Restart();
                    generator.ScheduleColumnHeightsParallel(block, heights).Complete();
                    timer.Stop();
                    if (i >= warmup)
                    {
                        times[i - warmup] = timer.Elapsed.TotalMilliseconds;
                    }
                }
            }
            finally
            {
                heights.Dispose();
            }
            return BenchmarkStats.Median(times);
        }

        public static string ToMarkdown(IReadOnlyList<HeightfieldResult> results)
        {
            var text = new StringBuilder();
            text.AppendLine("| Generator | Land | Chunk column (17² heights, 1 thread) | Far block (256² heights, workers) |");
            text.AppendLine("|---|---|---|---|");
            foreach (HeightfieldResult r in results)
            {
                text.AppendLine($"| {r.Generator} | {r.Land} | {r.ColumnMs:0.000} ms | {r.FarMs:0.00} ms |");
            }
            return text.ToString();
        }
    }
}
