using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    // GL252-GL254: the clouds' repeating noise, the stars turning with the sky, and the moon looking bigger low down.
    public class Pass27Tests
    {
        private const float Latitude = 28f;

        [Test]
        public void CloudNoise_RepeatsSeamlessly()
        {
            var point = new Vector3(1.37f, 2.81f, 0.53f);
            foreach (Vector3 axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
            {
                Assert.AreEqual(CloudNoise.Worley(point, 4, 9), CloudNoise.Worley(point + axis * 4f, 4, 9), 1e-4f, "Worley repeats every tile.");
                Assert.AreEqual(CloudNoise.Perlin(point, 8, 9), CloudNoise.Perlin(point + axis * 8f, 8, 9), 1e-4f, "Perlin repeats every tile.");
            }
        }

        [Test]
        public void CloudNoise_SpansTheFullRange()
        {
            const int size = 8;
            byte[] bytes = CloudNoise.Build(size, 3);
            Assert.AreEqual(size * size * size * 4, bytes.Length);
            for (int channel = 0; channel < 4; channel++)
            {
                byte low = 255;
                byte high = 0;
                for (int i = channel; i < bytes.Length; i += 4)
                {
                    low = (byte)Mathf.Min(low, bytes[i]);
                    high = (byte)Mathf.Max(high, bytes[i]);
                }
                Assert.AreEqual(0, low, $"Channel {channel} reaches 0.");
                Assert.AreEqual(255, high, $"Channel {channel} reaches 255.");
            }
        }

        [Test]
        public void TheCelestialPole_StandsAtTheLatitude_InTheNorth()
        {
            foreach (float hour in new[] { 0f, 7f, 21f })
            {
                Vector3 pole = SkyPositions.Star(0f, 90f, 3.5d, hour, Latitude);
                Assert.AreEqual(Mathf.Sin(Latitude * Mathf.Deg2Rad), pole.y, 1e-4f, "As high as the latitude.");
                Assert.AreEqual(0f, pole.x, 1e-4f);
                Assert.Greater(pole.z, 0f, "Due north.");
            }
        }

        [Test]
        public void Stars_TurnOnceASiderealDay_AndRiseEarlierEachNight()
        {
            // A star comes back to the same place after a sidereal day, not a solar one.
            double days = 2.25d;
            float hour = 6f;
            Vector3 now = SkyPositions.Star(40f, 20f, days, hour, Latitude);
            double later = days + SkyPositions.SiderealDayShare;
            float laterHour = Mathf.Repeat(hour + (float)(SkyPositions.SiderealDayShare * 24d), 24f);
            Vector3 then = SkyPositions.Star(40f, 20f, later, laterHour, Latitude);
            Assert.Less(Vector3.Angle(now, then), 0.01f);

            // At the same hour a day later the sky has turned about a degree further: four minutes earlier.
            float gain = SkyPositions.SiderealAngle(days + 1d, hour) - SkyPositions.SiderealAngle(days, hour);
            Assert.AreEqual(360f / 365.24219f, gain, 1e-3f);
            Assert.AreEqual(3.94f, gain / 15f * 60f, 0.02f, "Minutes earlier each night.");
        }

        [Test]
        public void TheMoon_LooksAQuarterBiggerOnTheHorizon()
        {
            Assert.AreEqual(1.25f, SkyPositions.MoonIllusion(0f, 0.25f, 0.4f), 1e-4f);
            Assert.AreEqual(1f, SkyPositions.MoonIllusion(0.4f, 0.25f, 0.4f), 1e-4f);
            Assert.AreEqual(1f, SkyPositions.MoonIllusion(0.9f, 0.25f, 0.4f), 1e-4f);
            float half = SkyPositions.MoonIllusion(0.2f, 0.25f, 0.4f);
            Assert.IsTrue(half > 1f && half < 1.25f, "It grows smoothly on the way down.");
        }
    }
}
