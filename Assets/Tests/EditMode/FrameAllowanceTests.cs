using NUnit.Framework;

namespace Clube.Core.Tests
{
    /// <summary>Lumpy work kept to an average share of the frame (GL770).</summary>
    public class FrameAllowanceTests
    {
        [Test]
        public void MayWork_NothingSpent_Works()
        {
            var allowance = new FrameAllowance();
            Assert.That(allowance.MayWork(3f, 1), Is.True);
            Assert.That(allowance.MayWork(3f, 2), Is.True);
        }

        [Test]
        public void MayWork_AfterALongPiece_WaitsUntilItIsPaidBack()
        {
            var allowance = new FrameAllowance();
            allowance.MayWork(3f, 1);
            allowance.Spend(9f);
            Assert.That(allowance.MayWork(3f, 2), Is.False, "6 ms still owed");
            Assert.That(allowance.MayWork(3f, 3), Is.False, "3 ms still owed");
            Assert.That(allowance.MayWork(3f, 4), Is.True, "paid back");
        }

        [Test]
        public void MayWork_SameFrameTwice_PaysBackOnce()
        {
            var allowance = new FrameAllowance();
            allowance.MayWork(3f, 1);
            allowance.Spend(6f);
            allowance.MayWork(3f, 2);
            Assert.That(allowance.MayWork(3f, 2), Is.False, "3 ms still owed");
        }

        [Test]
        public void MayWork_FramesSkipped_PaysBackForEach()
        {
            var allowance = new FrameAllowance();
            allowance.MayWork(3f, 1);
            allowance.Spend(9f);
            Assert.That(allowance.MayWork(3f, 4), Is.True);
        }
    }
}
