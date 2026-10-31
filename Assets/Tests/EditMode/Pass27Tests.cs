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
        public void Sunsets_VaryFromPlainToVivid_InManyColours()
        {
            int plain = 0, vivid = 0;
            var hues = new System.Collections.Generic.HashSet<int>();
            for (int day = 0; day < 400; day++)
            {
                SunsetLook look = SkyWeather.Sunset(11, day, day % 2 == 0, 0.45f);
                Assert.AreEqual(look.Vividness, SkyWeather.Sunset(11, day, day % 2 == 0, 0.45f).Vividness, "The same day looks the same.");
                plain += look.Vividness == 0f ? 1 : 0;
                vivid += look.Vividness >= 0.8f ? 1 : 0;
                // Which palette colour it's nearest.
                int nearest = 0;
                float best = float.MaxValue;
                for (int i = 0; i < SkyWeather.Palette.Length; i++)
                {
                    Color c = SkyWeather.Palette[i];
                    float d = Mathf.Abs(c.r - look.Glow.r) + Mathf.Abs(c.g - look.Glow.g) + Mathf.Abs(c.b - look.Glow.b);
                    if (d < best)
                    {
                        best = d;
                        nearest = i;
                    }
                }
                hues.Add(nearest);
            }
            Assert.Greater(plain, 40, "Some sunsets aren't coloured at all.");
            Assert.Greater(vivid, 30, "Some are vivid.");
            Assert.AreEqual(SkyWeather.Palette.Length, hues.Count, "Every colour comes up.");
            Assert.Less(SkyWeather.Sunset(11, 3, true, 0.05f).Vividness, SkyWeather.Sunset(11, 3, true, 0.45f).Vividness + 1e-4f,
                "A clear sky doesn't make it more vivid than broken cloud.");
        }

        [Test]
        public void CloudCover_ChangesFromDayToDay_Smoothly()
        {
            int clear = 0, heavy = 0;
            for (int day = 0; day < 300; day++)
            {
                float cover = SkyWeather.Cover(5, day + 0.5d);
                Assert.AreEqual(SkyWeather.DayCover(5, day), cover, 1e-4f, "A day's cover holds at its middle.");
                clear += cover < 0.16f ? 1 : 0;
                heavy += cover > 0.54f ? 1 : 0;
            }
            Assert.Greater(clear, 45, "Some days are nearly clear.");
            Assert.Greater(heavy, 60, "Some are heavily clouded.");
            for (double days = 0d; days < 20d; days += 0.01d)
            {
                Assert.Less(Mathf.Abs(SkyWeather.Cover(5, days + 0.01d) - SkyWeather.Cover(5, days)), 0.05f, "It eases, never jumps.");
            }
        }

        [Test]
        public void Dahlias_GrowOverGoldNodes_ThinningAway()
        {
            var growth = new DahliaGrowth { PeakShare = 0.04f, Spread = 2.5f, Deepest = 30f };
            var nodes = new System.Collections.Generic.List<OreNode>
            {
                new OreNode(0, 5, new Vector3(10f, 20f, 10f), Vector3.one, 0.45f, 2),
            };
            Assert.AreEqual(0.04f, growth.ShareAt(nodes, new Vector3(10f, 35f, 10f), out int node), 1e-5f, "Straight over the node, the peak.");
            Assert.AreEqual(0, node);
            float aside = growth.ShareAt(nodes, new Vector3(13f, 35f, 10f), out _);
            Assert.IsTrue(aside > 0f && aside < 0.04f, "Fewer a few metres to the side.");
            Assert.Less(growth.ShareAt(nodes, new Vector3(10f + growth.Reach, 35f, 10f), out _), 0.001f, "Next to none at the patch's edge.");
            Assert.AreEqual(0f, growth.ShareAt(nodes, new Vector3(10f, 55f, 10f), out node), "Nothing over a node deeper than the deepest.");
            Assert.AreEqual(-1, node);
            Assert.AreEqual(0f, growth.ShareAt(nodes, new Vector3(10f, 15f, 10f), out _), "Nothing under a node.");

            int colour = DahliaGrowth.ColourOf(nodes[0], 0.9f, 0.3f);
            Assert.IsTrue(colour >= 0 && colour < DahliaGrowth.Colours);
            Assert.AreEqual(colour, DahliaGrowth.ColourOf(nodes[0], 0.6f, 0.9f), "A patch keeps its colour.");
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
