using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Clube.Core
{
    /// <summary>
    /// What each system's own update took this frame (GL770), timed in the built game too, where the engine's profiler
    /// markers are switched off. A system opens one line at the top of its update:
    /// <c>using FrameCost.Scope cost = FrameCost.Of("Grass");</c>. It costs one check until a benchmark sets
    /// <see cref="Measuring"/>. Main thread only.
    /// </summary>
    public sealed class FrameCost
    {
        private static readonly Dictionary<string, FrameCost> ByName = new Dictionary<string, FrameCost>();
        private static readonly List<FrameCost> Costs = new List<FrameCost>();
        private static readonly double MillisecondsPerTick = 1000.0 / Stopwatch.Frequency;

        private long ticks;
        private long bytes;

        private FrameCost(string name)
        {
            Name = name;
        }

        /// <summary>Whether updates are being timed.</summary>
        public static bool Measuring { get; set; }

        /// <summary>Every system timed so far.</summary>
        public static IReadOnlyList<FrameCost> All => Costs;

        /// <summary>The system's name.</summary>
        public string Name { get; }

        /// <summary>Starts timing the named system until the scope ends.</summary>
        public static Scope Of(string name)
        {
            if (!Measuring)
            {
                return default;
            }
            if (!ByName.TryGetValue(name, out FrameCost cost))
            {
                cost = new FrameCost(name);
                ByName.Add(name, cost);
                Costs.Add(cost);
            }
            return new Scope(cost, Stopwatch.GetTimestamp(), GC.GetAllocatedBytesForCurrentThread());
        }

        /// <summary>The bytes the main thread allocated inside the system's scopes since the last call, and starts again from nothing (GL778).</summary>
        public long TakeBytes()
        {
            long taken = bytes;
            bytes = 0L;
            return taken;
        }

        /// <summary>The milliseconds added up since the last call, and starts again from nothing.</summary>
        public double TakeMilliseconds()
        {
            double milliseconds = ticks * MillisecondsPerTick;
            ticks = 0L;
            return milliseconds;
        }

        /// <summary>One timed stretch of a system's update.</summary>
        public readonly struct Scope : IDisposable
        {
            private readonly FrameCost cost;
            private readonly long started;
            private readonly long startedBytes;

            internal Scope(FrameCost cost, long started, long startedBytes)
            {
                this.cost = cost;
                this.started = started;
                this.startedBytes = startedBytes;
            }

            /// <summary>Adds the stretch to the system's time.</summary>
            public void Dispose()
            {
                if (cost != null)
                {
                    cost.ticks += Stopwatch.GetTimestamp() - started;
                    cost.bytes += GC.GetAllocatedBytesForCurrentThread() - startedBytes;
                }
            }
        }
    }
}
