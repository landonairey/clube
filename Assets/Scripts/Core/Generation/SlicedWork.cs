using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace Clube.Core
{
    /// <summary>
    /// Long work cut into steps and run a few milliseconds a frame (GL762), so a load screen keeps drawing. The work is an
    /// iterator: each <c>yield return</c> ends a step; yielding a float says how far it has got (0 to 1), and yielding a
    /// <see cref="Part"/> runs another iterator first, its progress counted within the part's share. It stays on the main
    /// thread, where the work's jobs must be scheduled and the fields it reads (ores, trees, biomes) are shared with the
    /// streaming chunks; run to the end it gives what one call would.
    /// </summary>
    public sealed class SlicedWork : IDisposable
    {
        /// <summary>A piece of a larger work, and the share of the larger work's progress it runs over.</summary>
        public readonly struct Part
        {
            public readonly IEnumerator Work;
            public readonly float From;
            public readonly float To;

            public Part(IEnumerator work, float from, float to)
            {
                Work = work;
                From = from;
                To = to;
            }
        }

        private readonly Stack<Part> steps = new Stack<Part>();

        public SlicedWork(IEnumerator work)
        {
            steps.Push(new Part(work ?? throw new ArgumentNullException(nameof(work)), 0f, 1f));
        }

        /// <summary>True once every step has run.</summary>
        public bool IsDone => steps.Count == 0;

        /// <summary>How far the work says it has got, 0 to 1.</summary>
        public float Progress { get; private set; }

        /// <summary>The longest single step so far: the shortest stall a frame can have while this runs.</summary>
        public float LongestStepMilliseconds { get; private set; }

        /// <summary>Runs steps until the time is up (always at least one); true once the work is done.</summary>
        public bool Run(float milliseconds)
        {
            long began = Stopwatch.GetTimestamp();
            double budget = milliseconds * 0.001 * Stopwatch.Frequency;
            while (steps.Count > 0)
            {
                long stepBegan = Stopwatch.GetTimestamp();
                Step();
                long now = Stopwatch.GetTimestamp();
                LongestStepMilliseconds = Math.Max(LongestStepMilliseconds, (float)((now - stepBegan) * 1000.0 / Stopwatch.Frequency));
                if (now - began >= budget)
                {
                    break;
                }
            }
            return IsDone;
        }

        /// <summary>Runs the whole of a piece of work now.</summary>
        public static void RunToEnd(IEnumerator work)
        {
            using var sliced = new SlicedWork(work);
            while (!sliced.IsDone)
            {
                sliced.Step();
            }
        }

        /// <summary>A step that waits for a job without holding up the frames, then finishes it.</summary>
        public static IEnumerator WaitFor(JobHandle job)
        {
            JobHandle.ScheduleBatchedJobs();
            // With no worker threads the job only runs when it is completed.
            while (JobsUtility.JobWorkerCount > 0 && !job.IsCompleted)
            {
                yield return null;
            }
            job.Complete();
        }

        /// <summary>Drops what is left of the work, freeing what its iterators hold.</summary>
        public void Dispose()
        {
            while (steps.Count > 0)
            {
                (steps.Pop().Work as IDisposable)?.Dispose();
            }
        }

        private void Step()
        {
            Part top = steps.Peek();
            if (!top.Work.MoveNext())
            {
                steps.Pop();
                (top.Work as IDisposable)?.Dispose();
                Progress = top.To;
                return;
            }
            float span = top.To - top.From;
            if (top.Work.Current is Part part)
            {
                steps.Push(new Part(part.Work, top.From + part.From * span, top.From + part.To * span));
            }
            else if (top.Work.Current is IEnumerator inner)
            {
                steps.Push(new Part(inner, top.From, top.To));
            }
            else if (top.Work.Current is float progress)
            {
                Progress = top.From + progress * span;
            }
        }
    }
}
