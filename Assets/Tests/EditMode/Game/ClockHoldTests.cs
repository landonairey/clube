using Clube.Game;
using NUnit.Framework;

namespace Clube.Game.Tests
{
    /// <summary>GL785: the game clock's one switch.</summary>
    public class ClockHoldTests
    {
        [SetUp]
        public void SetUp()
        {
            ClockHold.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            ClockHold.Clear();
        }

        [Test]
        public void NothingHoldingLetsTheClockRun()
        {
            Assert.IsFalse(ClockHold.IsHeld);
        }

        [Test]
        public void HeldUntilEveryHolderLetsGo()
        {
            object curtain = new object();
            object cutscene = new object();
            ClockHold.Hold(curtain);
            ClockHold.Hold(cutscene);
            ClockHold.Release(curtain);
            Assert.IsTrue(ClockHold.IsHeld, "the cart ride still holds it");
            ClockHold.Release(cutscene);
            Assert.IsFalse(ClockHold.IsHeld);
        }

        [Test]
        public void HoldingTwiceNeedsOneRelease()
        {
            object curtain = new object();
            ClockHold.Hold(curtain);
            ClockHold.Set(curtain, true);
            ClockHold.Release(curtain);
            Assert.IsFalse(ClockHold.IsHeld);
        }

        [Test]
        public void ReleasingWithoutAHoldChangesNothing()
        {
            object curtain = new object();
            ClockHold.Hold(curtain);
            ClockHold.Release(new object());
            ClockHold.Release(null);
            Assert.IsTrue(ClockHold.IsHeld);
        }
    }
}
