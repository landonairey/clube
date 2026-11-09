using Clube.Core;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>The travellers (GL476-GL477): the adventurer's peak and arrival, the trade cart's schedule and route.</summary>
    public class TravellerTests
    {
        // A hill at (40, -20), 30 m high, on flat ground at 10 m.
        private static float Hill(float x, float z)
        {
            float d = new Vector2(x - 40f, z + 20f).magnitude;
            return 10f + 30f * Mathf.Exp(-d * d / 400f);
        }

        [Test]
        public void PeakSearch_FindsTheHilltop()
        {
            Vector2 peak = PeakSearch.Highest(Hill, Vector2.zero, 100f, 8f, null, out float height);
            Assert.AreEqual(40f, peak.x, 1f);
            Assert.AreEqual(-20f, peak.y, 1f);
            Assert.AreEqual(40f, height, 0.2f);
        }

        [Test]
        public void PeakSearch_StaysWithinTheRadius()
        {
            // The hilltop is 45 m away; with 20 m the best is on the circle's edge towards it.
            Vector2 peak = PeakSearch.Highest(Hill, Vector2.zero, 20f, 4f, null, out _);
            Assert.LessOrEqual(peak.magnitude, 20.01f);
            Assert.Greater(peak.x, 10f);
        }

        [Test]
        public void PeakSearch_SummitClimbsPastTheRadiusToTheTop()
        {
            Vector2 edge = PeakSearch.Highest(Hill, Vector2.zero, 20f, 4f, null, out float edgeHeight);
            Vector2 top = PeakSearch.Summit(Hill, edge, 4f, null, out float height);
            Assert.AreEqual(40f, top.x, 0.6f);
            Assert.AreEqual(-20f, top.y, 0.6f);
            Assert.Greater(height, edgeHeight);
        }

        [Test]
        public void PeakSearch_StaysInsideTheArea()
        {
            var area = Rect.MinMaxRect(-50f, -10f, 50f, 50f);
            Vector2 peak = PeakSearch.Highest(Hill, Vector2.zero, 100f, 8f, area, out _);
            Assert.IsTrue(area.Contains(peak), peak.ToString());
        }

        [Test]
        public void AdventurerVisit_ArrivesAtTheDawnAfterTheFifthNight()
        {
            const float Sunrise = 4f;
            Assert.IsFalse(AdventurerVisit.HasArrived(1, 7f, Sunrise));
            Assert.IsFalse(AdventurerVisit.HasArrived(5, 23f, Sunrise));
            Assert.IsFalse(AdventurerVisit.HasArrived(6, 3.9f, Sunrise));
            Assert.IsTrue(AdventurerVisit.HasArrived(6, 4f, Sunrise));
            Assert.IsTrue(AdventurerVisit.HasArrived(9, 1f, Sunrise));
        }
    }
}
