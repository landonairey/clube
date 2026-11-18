using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;

namespace Clube.Core.Tests
{
    /// <summary>Long work run a few milliseconds a frame (GL762).</summary>
    public class SlicedWorkTests
    {
        private static IEnumerator Count(List<int> into, int from, int to)
        {
            for (int i = from; i < to; i++)
            {
                into.Add(i);
                yield return null;
            }
        }

        private static IEnumerator Nested(List<int> into)
        {
            yield return new SlicedWork.Part(Count(into, 0, 2), 0f, 0.5f);
            yield return new SlicedWork.Part(Count(into, 2, 4), 0.5f, 1f);
        }

        private static IEnumerator Halfway()
        {
            yield return 0.5f;
            yield return null;
        }

        private static IEnumerator SecondHalf()
        {
            yield return new SlicedWork.Part(Halfway(), 0.5f, 1f);
        }

        [Test]
        public void Run_PartsProgress_CountsWithinItsShare()
        {
            using var work = new SlicedWork(SecondHalf());
            work.Run(0f);
            work.Run(0f);
            Assert.That(work.Progress, Is.EqualTo(0.75f));
        }

        [Test]
        public void Run_NoTime_TakesOneStepAtATime()
        {
            var seen = new List<int>();
            using var work = new SlicedWork(Count(seen, 0, 3));
            Assert.That(work.Run(0f), Is.False);
            Assert.That(seen, Has.Count.EqualTo(1));
            Assert.That(work.Run(0f), Is.False);
            Assert.That(seen, Has.Count.EqualTo(2));
        }

        [Test]
        public void Run_NestedWork_KeepsItsOrderAndProgress()
        {
            var seen = new List<int>();
            using var work = new SlicedWork(Nested(seen));
            while (!work.Run(0f))
            {
            }
            Assert.That(seen, Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(work.Progress, Is.EqualTo(1f));
            Assert.That(work.IsDone, Is.True);
        }

        [Test]
        public void RunToEnd_DoesAllOfIt()
        {
            var seen = new List<int>();
            SlicedWork.RunToEnd(Nested(seen));
            Assert.That(seen, Is.EqualTo(new[] { 0, 1, 2, 3 }));
        }

        [Test]
        public void Dispose_RunsTheWorksFinallyBlocks()
        {
            bool freed = false;
            IEnumerator Holding()
            {
                try
                {
                    yield return null;
                    yield return null;
                }
                finally
                {
                    freed = true;
                }
            }

            var work = new SlicedWork(Holding());
            work.Run(0f);
            work.Dispose();
            Assert.That(freed, Is.True);
            Assert.That(work.IsDone, Is.True);
        }
    }
}
