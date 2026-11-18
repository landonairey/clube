using Clube.Core;
using NUnit.Framework;

namespace Clube.Core.Tests
{
    public class FrameBudgetTests
    {
        [Test]
        public void AnEarlyFrame_KeepsASystemsOwnBudget()
        {
            Assert.AreEqual(2f, FrameBudget.Limited(2f, 1f), 1e-4f);
        }

        [Test]
        public void ALateFrame_CutsTheBudgetToWhatIsLeft()
        {
            float spent = FrameBudget.ScriptMilliseconds - 0.5f;
            Assert.AreEqual(0.5f, FrameBudget.Limited(2f, spent), 1e-4f);
        }

        [Test]
        public void AnOverrunFrame_LeavesNothing_NeverLessThanNothing()
        {
            Assert.AreEqual(0f, FrameBudget.Limited(2f, FrameBudget.ScriptMilliseconds + 9f), 1e-4f);
        }
    }
}
