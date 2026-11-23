using NUnit.Framework;

namespace Clube.Core.Tests
{
    /// <summary>GL802: the tutorial's survey and plan are handed to the game scene of the same map, once.</summary>
    public class MapPlanHandoverTests
    {
        [TearDown]
        public void ClearHandover()
        {
            MapPlanHandover.Clear();
        }

        [Test]
        public void TheSameMapTakesThePlanOnce()
        {
            var plan = new WorldPlan();
            var survey = (ResourceSurvey)null;
            MapPlanHandover.Hold("map a", survey, plan);
            Assert.IsTrue(MapPlanHandover.TryTake("map a", out ResourceSurvey taken, out WorldPlan takenPlan));
            Assert.AreSame(survey, taken);
            Assert.AreSame(plan, takenPlan);
            Assert.IsFalse(MapPlanHandover.TryTake("map a", out _, out _), "taken once");
        }

        [Test]
        public void AnotherMapPlansAfresh_AndDropsTheHeldPlan()
        {
            MapPlanHandover.Hold("map a", (ResourceSurvey)null, new WorldPlan());
            Assert.IsFalse(MapPlanHandover.TryTake("map b", out ResourceSurvey survey, out WorldPlan plan));
            Assert.IsNull(survey);
            Assert.IsNull(plan);
            Assert.IsFalse(MapPlanHandover.IsHolding);
        }
    }
}
