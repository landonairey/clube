using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    // GL464-GL466: 16 hours of daylight, fireflies at dusk, jumping trout.
    public class SkyNatureTests
    {
        private const float Latitude = 28f;

        [Test]
        public void Sun_IsUpSixteenHoursAndDownEight()
        {
            int up = 0;
            for (int minute = 0; minute < 24 * 60; minute++)
            {
                if (SkyPositions.Sun(minute / 60f, Latitude).y > 0f)
                {
                    up++;
                }
            }
            Assert.AreEqual(16 * 60, up, 3, "Sixteen hours with the sun over the horizon.");
            Assert.Greater(SkyPositions.Sun(7f, Latitude).y, 0.1f, "A new game's 07:00 is daylight.");
            Assert.Less(SkyPositions.Sun(21f, Latitude).y, 0f);
            Assert.Less(SkyPositions.Sun(3f, Latitude).y, 0f);
        }

        [Test]
        public void SunHourAngle_IsContinuousAcrossTheDay()
        {
            float last = SkyPositions.SunHourAngle(0f);
            for (int minute = 1; minute <= 24 * 60; minute++)
            {
                float angle = SkyPositions.SunHourAngle(minute / 60f);
                float step = Mathf.DeltaAngle(last, angle);
                Assert.Greater(step, 0f, "Always turns forward at " + minute);
                Assert.Less(step, 0.5f, "No jumps at " + minute);
                last = angle;
            }
        }

        [Test]
        public void DuskGlow_OnlyInTheEveningAroundSunset()
        {
            Assert.AreEqual(0f, DuskLight.Glow(0.8f, true), 1e-5f, "Not at noon.");
            Assert.AreEqual(0f, DuskLight.Glow(0f, false), 1e-5f, "Not at dawn.");
            Assert.Greater(DuskLight.Glow(-0.05f, true), 0.9f, "Full just after sunset.");
            Assert.AreEqual(0f, DuskLight.Glow(-0.5f, true), 1e-5f, "Gone in deep night.");
        }

        [Test]
        public void Trout_JumpMostInTheMorning_LeastInTheAfternoon_NeverAtNight()
        {
            Assert.AreEqual(1f, TroutJumps.Activity(7f, 1f), 1e-5f);
            Assert.Less(TroutJumps.Activity(11f, 1f), TroutJumps.Activity(7f, 1f));
            Assert.Less(TroutJumps.Activity(16f, 1f), TroutJumps.Activity(11f, 1f));
            Assert.Greater(TroutJumps.Activity(16f, 1f), 0f, "A few still jump in the afternoon.");
            Assert.AreEqual(0f, TroutJumps.Activity(23f, 0f), 1e-5f, "None at night.");
            Assert.AreEqual(0f, TroutJumps.ChanceIn(0.1f, 2f, 0.1f), 1e-5f);
        }
    }
}
