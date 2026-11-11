using Clube.Core;
using NUnit.Framework;

namespace Clube.Core.Tests
{
    public class StallTimerTests
    {
        [Test]
        public void Tick_ReportsOnceAfterTheLimitThenCountsAgain()
        {
            var timer = new StallTimer();
            Assert.IsFalse(timer.Tick(4f, 6f));
            Assert.IsTrue(timer.Tick(2f, 6f));
            Assert.IsFalse(timer.Tick(5f, 6f));
            Assert.IsTrue(timer.Tick(1f, 6f));
        }

        [Test]
        public void Reset_RestartsTheCount()
        {
            var timer = new StallTimer();
            timer.Tick(5f, 6f);
            timer.Reset();
            Assert.AreEqual(0f, timer.Idle);
            Assert.IsFalse(timer.Tick(5f, 6f));
        }
    }
}
