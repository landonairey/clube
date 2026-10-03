using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class HeightmapTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void FromRaw_ReadsEightBitPixels()
        {
            Heightmap map = Heightmap.FromRaw(new byte[] { 0, 255, 51, 102 }, sixteenBit: false);

            Assert.That(map.Width, Is.EqualTo(2));
            Assert.That(map[0, 0], Is.EqualTo(0f));
            Assert.That(map[1, 0], Is.EqualTo(1f));
            Assert.That(map[0, 1], Is.EqualTo(0.2f).Within(Tolerance));
        }

        [Test]
        public void FromRaw_ReadsLittleEndianSixteenBitPixels()
        {
            // 0xFFFF = 1, 0x8000 = about 0.5 (low byte first).
            Heightmap map = Heightmap.FromRaw(new byte[] { 0x00, 0x00, 0xFF, 0xFF, 0x00, 0x80, 0x00, 0x00 }, sixteenBit: true);

            Assert.That(map[1, 0], Is.EqualTo(1f));
            Assert.That(map[0, 1], Is.EqualTo(32768f / 65535f).Within(Tolerance));
        }

        [Test]
        public void FromRaw_RejectsNonSquareData()
        {
            Assert.Throws<System.ArgumentException>(() => Heightmap.FromRaw(new byte[6], sixteenBit: false));
        }

        [Test]
        public void Sample_BlendsBetweenPixels_AndClampsAtTheEdges()
        {
            var map = new Heightmap(2, 2, new[] { 0f, 1f, 0f, 1f });

            Assert.That(map.Sample(0.25f, 0.5f), Is.EqualTo(0.25f).Within(Tolerance));
            Assert.That(map.Sample(-3f, 0f), Is.EqualTo(0f));
            Assert.That(map.Sample(9f, 0f), Is.EqualTo(1f));
        }

        [Test]
        public void HeightmapGenerator_MapsBlackAndWhiteToLevelMinusAndPlusAmplitude()
        {
            var map = new Heightmap(2, 1, new[] { 0f, 1f });
            var generator = new HeightmapGenerator(map, level: 4f, amplitude: 2f, unitsPerPixel: 3f);

            Assert.That(generator.Height(0f, 0f), Is.EqualTo(2f).Within(Tolerance));
            Assert.That(generator.Height(3f, 0f), Is.EqualTo(6f).Within(Tolerance));
            Assert.That(generator.Height(1.5f, 0f), Is.EqualTo(4f).Within(Tolerance));
        }

        [Test]
        public void Spline_StraightCurve_SpansTheSameRangeAsTheNoise()
        {
            var straight = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            var spline = new SplineGenerator(straight, seed: 3, level: 4f, amplitude: 2f, frequency: 0.2f);
            var noise = new FractalPerlin2DGenerator(3, 4f, 2f, 0.2f);

            for (int i = 0; i < 50; i++)
            {
                Assert.That(spline.Height(i * 0.7f, i * 0.3f), Is.EqualTo(noise.Height(i * 0.7f, i * 0.3f)).Within(Tolerance));
            }
        }

        [Test]
        public void Spline_FlatCurve_GivesAPlateau()
        {
            var plateau = AnimationCurve.Constant(0f, 1f, 0.75f);
            var spline = new SplineGenerator(plateau, seed: 3, level: 4f, amplitude: 2f, frequency: 0.2f);

            for (int i = 0; i < 20; i++)
            {
                Assert.That(spline.Height(i * 0.7f, i * 0.3f), Is.EqualTo(5f).Within(Tolerance));
            }
        }

        [Test]
        public void Export_FlatTerrain_ReadsBackTheLevel()
        {
            var chunk = new Chunk(new Vector3Int(3, 6, 3));
            ChunkGenerator.Fill(chunk, new FlatGenerator(2.5f), Vector3.zero, 1f);

            float[] heights = HeightmapExport.ColumnSurfaceHeights(chunk, 0.5f, 1f);

            Assert.That(heights.Length, Is.EqualTo(4 * 4));
            foreach (float height in heights)
            {
                Assert.That(height, Is.EqualTo(2.5f).Within(Tolerance));
            }
        }

        [Test]
        public void Export_ThenImport_GivesBackTheSurface()
        {
            // Export a hilly chunk to bytes, read them back as a heightmap, regenerate, compare.
            var source = new Chunk(new Vector3Int(8, 8, 8));
            ChunkGenerator.Fill(source, new SineGenerator(4f, 1.5f, 0.1f), Vector3.zero, 1f);
            float[] heights = HeightmapExport.ColumnSurfaceHeights(source, 0.5f, 1f);
            byte[] grey = HeightmapExport.ToGrayscale(heights, chunkHeight: 8f);

            // Level = amplitude = half the chunk height maps 0-255 back onto 0-8.
            var imported = new HeightmapGenerator(Heightmap.FromRaw(grey, sixteenBit: false), 4f, 4f, 1f);
            var copy = new Chunk(new Vector3Int(8, 8, 8));
            ChunkGenerator.Fill(copy, imported, Vector3.zero, 1f);

            float[] roundTrip = HeightmapExport.ColumnSurfaceHeights(copy, 0.5f, 1f);
            for (int i = 0; i < heights.Length; i++)
            {
                // 8-bit steps of 8/255 world units are the only loss.
                Assert.That(roundTrip[i], Is.EqualTo(heights[i]).Within(8f / 255f + Tolerance), $"Column {i}");
            }
        }
    }
}
