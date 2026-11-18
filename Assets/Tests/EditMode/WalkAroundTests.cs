using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>Figures walk round someone in their way (GL740).</summary>
    public class WalkAroundTests
    {
        private const float Clearance = 1.1f;
        private const float Stride = 0.05f;

        private static Vector2 Walk(Vector2 from, Vector2 to, Vector2 other)
        {
            Vector2 at = from;
            for (int i = 0; i < 2000 && (to - at).magnitude > 0.1f; i++)
            {
                at = WalkAround.Step(at, to, other, Clearance, Stride);
                Assert.GreaterOrEqual((at - other).magnitude, Clearance - 1e-3f, "never inside the clearance");
            }
            return at;
        }

        [TestCase(0f)]
        [TestCase(0.3f)]
        [TestCase(-0.6f)]
        public void AFigure_GoesRoundSomeoneOnItsLine_AndStillArrives(float offset)
        {
            var to = new Vector2(0f, 8f);
            Assert.Less((Walk(new Vector2(0f, -8f), to, new Vector2(offset, 0f)) - to).magnitude, 0.11f);
        }

        [Test]
        public void AFigure_NotHeadingForThem_KeepsItsLine()
        {
            Vector2 next = WalkAround.Step(Vector2.zero, new Vector2(0f, 5f), new Vector2(4f, 0f), Clearance, Stride);
            Assert.AreEqual(new Vector2(0f, Stride), next);
        }

        [Test]
        public void AFigureInsideTheClearance_IsMovedOutToItsEdge()
        {
            Vector2 next = WalkAround.Step(new Vector2(0.2f, 0f), new Vector2(5f, 0f), Vector2.zero, Clearance, Stride);
            Assert.AreEqual(Clearance, next.magnitude, 1e-3f);
        }
    }
}
