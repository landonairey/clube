using System.Collections.Generic;
using Clube.Core;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// Water at the game's scale (GL332): a 16 x 16 m piece of ground in 0.25 m voxels, with no generated water, and the
    /// scenarios seen in play: a bucket poured on flat, near-flat and steep ground, a channel fed from one end, a hole dug
    /// under standing water, and a big flood for the cost of a step. Each logs what it measured.
    /// </summary>
    public class WaterScenarioTests
    {
        private const float Voxel = 0.25f;
        private const int Chunks = 4;
        private const int Size = Chunks * 16;
        private const int Height = 32;

        private World world;
        private LiveWater water;
        private readonly List<Vector3Int> poured = new List<Vector3Int>();

        /// <summary>What a scenario measured.</summary>
        public struct Result
        {
            public int Steps;
            public bool Settled;
            public float Volume;
            public int WetColumns;
            public float WetArea => WetColumns * Voxel * Voxel;
            public float MaxStepMs;
            public float MeanStepMs;
            public int MinX;
            public int MaxX;

            public override string ToString()
            {
                return $"{(Settled ? "settled" : "still moving")} after {Steps} steps ({Steps * 0.06f:0.0} s of game time), " +
                       $"volume {Volume:0.000}, wet area {WetArea:0.0} m² ({WetColumns} columns), x {MinX}-{MaxX}, " +
                       $"step {MeanStepMs:0.00} ms mean / {MaxStepMs:0.00} ms max";
            }
        }

        [TearDown]
        public void TearDown()
        {
            water?.Dispose();
            water = null;
        }

        // Ground solid up to the height the function gives for each column (in samples).
        private void Ground(System.Func<int, int, int> height)
        {
            world = new World(new Vector3Int(16, 16, 16), Voxel);
            for (int cz = 0; cz < Chunks; cz++)
            {
                for (int cy = 0; cy < Height / 16; cy++)
                {
                    for (int cx = 0; cx < Chunks; cx++)
                    {
                        world.Load(new Vector3Int(cx, cy, cz), null);
                    }
                }
            }
            for (int z = 0; z <= Size; z++)
            {
                for (int x = 0; x <= Size; x++)
                {
                    int top = height(x, z);
                    for (int y = 0; y <= top && y <= Height; y++)
                    {
                        world.SetDensity(new Vector3Int(x, y, z), 1f);
                    }
                }
            }
            water = new LiveWater(world, WaterMap.None, 0.5f);
        }

        private void Pour(Vector3 at, int units)
        {
            poured.Clear();
            Assert.That(WaterScoop.Pour(world, at, 3f, units, 0.5f, poured), Is.EqualTo(units), "the bucket empties");
            foreach (Vector3Int sample in poured)
            {
                water.Wake(sample);
            }
        }

        private Result Run(int maxSteps, float budgetMs = 0f)
        {
            var result = new Result { MinX = int.MaxValue, MaxX = int.MinValue };
            var timer = new System.Diagnostics.Stopwatch();
            double total = 0;
            for (; result.Steps < maxSteps && water.AwakeCount > 0; result.Steps++)
            {
                timer.Restart();
                water.Step(budgetMs: budgetMs);
                float ms = (float)timer.Elapsed.TotalMilliseconds;
                total += ms;
                result.MaxStepMs = Mathf.Max(result.MaxStepMs, ms);
            }
            result.Settled = water.AwakeCount == 0;
            result.MeanStepMs = result.Steps > 0 ? (float)(total / result.Steps) : 0f;
            for (int z = 0; z <= Size; z++)
            {
                for (int x = 0; x <= Size; x++)
                {
                    bool wet = false;
                    for (int y = 0; y <= Height; y++)
                    {
                        float a = world.GetWater(new Vector3Int(x, y, z));
                        result.Volume += a;
                        wet |= a > 0f;
                    }
                    if (wet)
                    {
                        result.WetColumns++;
                        result.MinX = Mathf.Min(result.MinX, x);
                        result.MaxX = Mathf.Max(result.MaxX, x);
                    }
                }
            }
            return result;
        }

        private static void Report(string scenario, Result result)
        {
            UnityEngine.Debug.Log($"[WaterScenario] {scenario}: {result}");
        }

        [Test]
        public void Bucket_OnFlatGround_SettlesIntoAPuddle()
        {
            Ground((x, z) => 4);
            Pour(new Vector3(8f, 2f, 8f), 100);
            Result result = Run(3000);
            Report("100 units on flat ground", result);
            Assert.That(result.Settled, "the puddle stops spreading");
            Assert.That(result.Volume, Is.EqualTo(100f).Within(0.001f), "no water made or lost");
            Assert.That(result.WetColumns, Is.LessThanOrEqualTo(800), "no thinner than two steps: at most 100 / (2/16) columns");
        }

        [Test]
        public void Bucket_OnGentleSlope_PoolsWhereItLands()
        {
            // Ground falling 1 sample in 8 along +x (about 7°), gentler than MinFlowSlope.
            Ground((x, z) => 20 - x / 8);
            Pour(new Vector3(4f, 6f, 8f), 100);
            Result result = Run(3000);
            Report("100 units on a 1-in-8 slope (7°)", result);
            Assert.That(result.Settled);
            Assert.That(result.Volume, Is.EqualTo(100f).Within(0.001f));
            Assert.That(result.MaxX, Is.LessThan(Size - 4), "it doesn't run all the way down");
        }

        [Test]
        public void Bucket_OnGentleSlope_RunsWithNoMinimumSlope()
        {
            // The same, with every drop in reach counted as downhill: the old behaviour, for comparison.
            Ground((x, z) => 20 - x / 8);
            water.MinFlowSlope = 0f;
            Pour(new Vector3(4f, 6f, 8f), 100);
            Result result = Run(3000);
            Report("100 units on a 1-in-8 slope, no minimum slope", result);
            Assert.That(result.Volume, Is.EqualTo(100f).Within(0.001f));
        }

        [Test]
        public void Bucket_OnSteepSlope_RunsToTheFoot()
        {
            // Ground falling 1 sample in 2 along +x (about 27°) down to a flat floor at y = 4.
            Ground((x, z) => Mathf.Max(4, 28 - x / 2));
            Pour(new Vector3(2f, 7.5f, 8f), 100);
            Result result = Run(4000);
            float foot = 0f;
            for (int z = 0; z <= Size; z++)
            {
                for (int x = 48; x <= Size; x++)
                {
                    for (int y = 5; y <= 8; y++)
                    {
                        foot += world.GetWater(new Vector3Int(x, y, z));
                    }
                }
            }
            Report($"100 units on a 1-in-2 slope (27°), {foot:0.0} units on the floor at its foot", result);
            Assert.That(result.Volume, Is.EqualTo(100f).Within(0.001f));
            Assert.That(foot, Is.GreaterThan(80f), "most of it ends up at the foot");
        }

        [Test]
        public void Bucket_OnAMeadowHillside_RunsOffAndSettles()
        {
            // Ground falling 3 samples in 8 along +x (about 21°, the meadow by the start) down to a flat floor at y = 4.
            Ground((x, z) => Mathf.Max(4, 26 - x * 3 / 8));
            Pour(new Vector3(1.5f, 7f, 8f), 100);
            Result result = Run(4000);
            float foot = 0f;
            int film = 0;
            for (int z = 0; z <= Size; z++)
            {
                for (int x = 0; x <= Size; x++)
                {
                    for (int y = 5; y <= Height; y++)
                    {
                        float a = world.GetWater(new Vector3Int(x, y, z));
                        if (x >= 52)
                        {
                            foot += a;
                        }
                        else if (a > 0f && y > 5)
                        {
                            film++;
                        }
                    }
                }
            }
            Report($"100 units on a 3-in-8 slope (21°), {foot:0.0} units in its last 2 m and at its foot, {film} wet samples higher up", result);
            Assert.That(result.Settled, "the water comes to rest");
            Assert.That(result.Volume, Is.EqualTo(100f).Within(0.001f));
            Assert.That(result.MinX, Is.GreaterThanOrEqualTo(52), "none is left on the slope above its last 2 m");
        }

        [Test]
        public void BucketDump_RunsDownAHillsideAsOneStream()
        {
            // A full bucket dumped at the top of a 21° slope (GL337): as it runs down, the wet stretch along the slope should
            // stay in one piece instead of breaking into separate clumps with dry ground between them.
            Ground((x, z) => Mathf.Max(4, 26 - x * 3 / 8));
            Pour(new Vector3(1.5f, 7f, 8f), 100);
            int worstGaps = 0, worstAt = 0;
            for (int step = 1; step <= 600 && water.AwakeCount > 0; step++)
            {
                water.Step();
                if (step % 10 != 0)
                {
                    continue;
                }
                // Columns along x with any water on the slope (x < 56, above the floor), and the dry ones between them.
                int first = -1, last = -1, wet = 0;
                for (int x = 0; x < 56; x++)
                {
                    bool any = false;
                    for (int z = 0; z <= Size && !any; z++)
                    {
                        for (int y = 5; y <= Height && !any; y++)
                        {
                            any = world.GetWater(new Vector3Int(x, y, z)) > 0f;
                        }
                    }
                    if (any)
                    {
                        first = first < 0 ? x : first;
                        last = x;
                        wet++;
                    }
                }
                int gaps = first < 0 ? 0 : last - first + 1 - wet;
                if (gaps > worstGaps)
                {
                    worstGaps = gaps;
                    worstAt = step;
                }
            }
            Result result = Run(4000);
            Report($"Bucket dump down a 21° slope: worst {worstGaps} dry columns inside the wet stretch (step {worstAt})", result);
            Assert.That(result.Volume, Is.EqualTo(100f).Within(0.001f));
            Assert.That(worstGaps, Is.LessThanOrEqualTo(3), "the stream stays in one piece down the slope");
        }

        [Test]
        public void FedChannel_FillsAlongItsLength()
        {
            // A channel 2 samples wide along x at z 31-32, its floor falling 1 sample in 6; a spring at its head.
            Ground((x, z) => z == 31 || z == 32 ? 18 - x / 6 : 24);
            var result = new Result();
            int reached = 0;
            int steps;
            for (steps = 0; steps < 1500; steps++)
            {
                for (int z = 31; z <= 32; z++)
                {
                    world.SetWater(new Vector3Int(0, 19, z), 1f);
                    water.Wake(new Vector3Int(0, 19, z));
                }
                water.Step();
                while (reached < Size && world.GetWater(new Vector3Int(reached + 1, 19 - (reached + 1) / 6, 31)) > 0f)
                {
                    reached++;
                }
                if (reached >= Size - 2)
                {
                    break;
                }
            }
            int gaps = 0;
            for (int x = 0; x <= reached; x++)
            {
                gaps += world.GetWater(new Vector3Int(x, 19 - x / 6, 31)) > 0f ? 0 : 1;
            }
            result = Run(0);
            UnityEngine.Debug.Log($"[WaterScenario] Fed channel (1 in 6): front at x = {reached} of {Size} after {steps} steps " +
                                  $"({steps * 0.06f:0.0} s), {gaps} dry samples behind the front, {result.Volume:0.0} units in it");
            Assert.That(reached, Is.GreaterThanOrEqualTo(Size - 2), "the stream reaches the end");
            string profile = "";
            for (int x = 0; x <= reached; x++)
            {
                int floor = 19 - x / 6;
                profile += $"{x}:{world.GetWater(new Vector3Int(x, floor, 31)) * 16:0}/{world.GetWater(new Vector3Int(x, floor + 1, 31)) * 16:0} ";
            }
            Assert.That(gaps, Is.EqualTo(0), "no dry floor behind the front: " + profile);
        }

        [Test]
        public void HoleDugUnderAPuddle_TakesItAtOnce()
        {
            Ground((x, z) => 12);
            Pour(new Vector3(8f, 4f, 8f), 20);
            Run(2000);
            // A 1 m shaft dug under the middle of the puddle.
            Vector3Int middle = world.Grid.WorldToNearestSample(new Vector3(8f, 3.25f, 8f));
            for (int y = middle.y; y > middle.y - 4; y--)
            {
                world.SetDensity(new Vector3Int(middle.x, y, middle.z), 0f);
            }
            water.Step();
            float inShaft = world.GetWater(new Vector3Int(middle.x, middle.y - 3, middle.z));
            Result result = Run(2000);
            Report($"20 units, then a shaft dug under them: {inShaft:0.00} at its bottom after one step", result);
            Assert.That(inShaft, Is.GreaterThan(0f), "water reaches the bottom in the first step");
            Assert.That(result.Volume, Is.EqualTo(20f).Within(0.001f));
        }

        [Test]
        public void BigFlood_StaysWithinTheFrameBudget()
        {
            // Ten buckets at once on flat ground, stepped with the game's 2.5 ms budget.
            Ground((x, z) => 4);
            for (int i = 0; i < 10; i++)
            {
                Pour(new Vector3(4f + i % 5 * 2f, 3f, 5f + i / 5 * 6f), 100);
            }
            Result result = Run(6000, 2.5f);
            Report("1000 units on flat ground, 2.5 ms budget", result);
            Assert.That(result.Volume, Is.EqualTo(1000f).Within(0.01f));
            Assert.That(result.MeanStepMs, Is.LessThan(4f), "a step keeps to about its budget");
        }
    }
}
