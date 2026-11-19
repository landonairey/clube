using Clube.Game;
using NUnit.Framework;

namespace Clube.Game.Tests
{
    public class LoadProgressTests
    {
        [Test]
        public void StagesFollowOneAnotherWithoutGapsOrOverlap()
        {
            Assert.AreEqual(0f, LoadProgress.Charting(0f), 1e-5f);
            Assert.AreEqual(LoadProgress.ChartingEnd, LoadProgress.Charting(1f), 1e-5f);
            Assert.AreEqual(LoadProgress.ChartingEnd, LoadProgress.SceneLoad(0f), 1e-5f);
            Assert.AreEqual(LoadProgress.SceneEnd, LoadProgress.SceneLoad(1f), 1e-5f);
            Assert.AreEqual(LoadProgress.SceneEnd, LoadProgress.InScene(true, false, 0f, 0f), 1e-5f);
            Assert.AreEqual(LoadProgress.SurveyEnd, LoadProgress.InScene(true, false, 1f, 0f), 1e-5f);
            Assert.AreEqual(LoadProgress.SurveyEnd, LoadProgress.InScene(true, true, 1f, 0f), 1e-5f);
            Assert.AreEqual(1f, LoadProgress.InScene(true, true, 1f, 1f), 1e-5f);
        }

        [Test]
        public void ProgressRisesAcrossTheWholeLoad()
        {
            float last = -1f;
            for (int i = 0; i <= 10; i++)
            {
                last = Rises(LoadProgress.Charting(i / 10f), last);
            }
            for (int i = 0; i <= 10; i++)
            {
                last = Rises(LoadProgress.SceneLoad(i / 10f), last);
            }
            for (int i = 0; i <= 10; i++)
            {
                last = Rises(LoadProgress.InScene(true, false, i / 10f, 0f), last);
            }
            for (int i = 0; i <= 10; i++)
            {
                last = Rises(LoadProgress.InScene(true, true, 1f, i / 10f), last);
            }
        }

        private static float Rises(float now, float last)
        {
            Assert.GreaterOrEqual(now, last);
            return now;
        }

        [Test]
        public void ASceneWithoutASurveyGivesTheGroundTheRest()
        {
            Assert.AreEqual(LoadProgress.SceneEnd, LoadProgress.InScene(false, false, 0f, 0f), 1e-5f);
            Assert.AreEqual(1f, LoadProgress.InScene(false, false, 0f, 1f), 1e-5f);
        }

        [Test]
        public void OutOfRangeProgressIsClamped()
        {
            Assert.AreEqual(LoadProgress.ChartingEnd, LoadProgress.Charting(5f), 1e-5f);
            Assert.AreEqual(0f, LoadProgress.Charting(-1f), 1e-5f);
        }

        [Test]
        public void ThePercentIsOnlyHundredWhenFull()
        {
            Assert.AreEqual(0, LoadCurtainView.Percent(0f));
            Assert.AreEqual(99, LoadCurtainView.Percent(0.999f));
            Assert.AreEqual(100, LoadCurtainView.Percent(1f));
            Assert.AreEqual(100, LoadCurtainView.Percent(2f));
            Assert.AreEqual(0, LoadCurtainView.Percent(-1f));
        }
    }
}
