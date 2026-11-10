using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>The opening tutorial's job rules (GL479): the wage, where the quarry goes, and what counts as stealing.</summary>
    public class CompanyWorkTests
    {
        [Test]
        public void PaysTenCoinsForEveryHundred()
        {
            Assert.AreEqual(10f, CompanyWork.Wage(100f), 1e-5f);
            Assert.AreEqual(0.25f, CompanyWork.Wage(2.5f), 1e-5f);
            Assert.AreEqual(0f, CompanyWork.Wage(-5f));
        }

        [Test]
        public void QuarryIsOutOfTheGateAndBesideTheRoad()
        {
            var gate = new Vector2(100f, 50f);
            Vector2 centre = CompanyWork.QuarryCentre(gate, Vector2.down * 3f);
            Vector2 offset = centre - gate;
            Assert.AreEqual(-CompanyWork.OutFromGate, offset.y, 1e-4f, "out along the road");
            Assert.AreEqual(CompanyWork.BesideRoad, Mathf.Abs(offset.x), 1e-4f, "to the side of it");
        }

        [Test]
        public void StealingIsLeavingWithStoneOrHoldingAnythingElse()
        {
            const float rim = 4.5f;
            Assert.IsFalse(CompanyWork.IsStealing(3f, rim, true, false), "stone in the pit");
            Assert.IsFalse(CompanyWork.IsStealing(rim + CompanyWork.Margin - 0.1f, rim, true, false), "stone by the rim");
            Assert.IsTrue(CompanyWork.IsStealing(rim + CompanyWork.Margin + 0.1f, rim, true, false), "stone out of the quarry");
            Assert.IsFalse(CompanyWork.IsStealing(100f, rim, false, false), "empty-handed anywhere");
            Assert.IsTrue(CompanyWork.IsStealing(1f, rim, false, true), "anything that isn't the company's");
        }
    }
}
