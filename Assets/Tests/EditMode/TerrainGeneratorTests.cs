using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class TerrainGeneratorTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void Noise_SameSeed_GivesSameValues()
        {
            var a = new PerlinNoise(42);
            var b = new PerlinNoise(42);

            for (int i = 0; i < 50; i++)
            {
                float x = i * 0.37f, y = i * 0.91f, z = i * 0.13f;
                Assert.That(a.Sample(x, y, z), Is.EqualTo(b.Sample(x, y, z)));
            }
        }

        [Test]
        public void Noise_DifferentSeeds_GiveDifferentFields()
        {
            var a = new PerlinNoise(1);
            var b = new PerlinNoise(2);

            int differing = 0;
            for (int i = 0; i < 50; i++)
            {
                if (Mathf.Abs(a.Sample(i * 0.37f + 0.5f, i * 0.91f + 0.5f) - b.Sample(i * 0.37f + 0.5f, i * 0.91f + 0.5f)) > 1e-3f)
                {
                    differing++;
                }
            }
            Assert.That(differing, Is.GreaterThan(40));
        }

        [Test]
        public void Noise_StaysWithinMinusOneToOne()
        {
            var noise = new PerlinNoise(7);
            for (int i = 0; i < 2000; i++)
            {
                float value = noise.Sample(i * 0.173f, i * 0.291f, i * 0.057f);
                Assert.That(value, Is.InRange(-1f, 1f));
            }
        }

        [Test]
        public void Noise_IsZeroOnLatticePoints()
        {
            var noise = new PerlinNoise(3);
            Assert.That(noise.Sample(2f, 5f, 1f), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Flat_DensityIsHalfAtTheLevel_SolidBelow_EmptyAbove()
        {
            var flat = new FlatGenerator(3f);

            Assert.That(flat.Density(new Vector3(1f, 3f, 2f)), Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(flat.Density(new Vector3(1f, 1f, 2f)), Is.EqualTo(1f));
            Assert.That(flat.Density(new Vector3(1f, 5f, 2f)), Is.EqualTo(0f));
        }

        [Test]
        public void Sine_StaysWithinItsAmplitude()
        {
            var sine = new SineGenerator(4f, 1.5f, 0.2f);
            for (int i = 0; i < 200; i++)
            {
                Assert.That(sine.Height(i * 0.31f, i * 0.17f), Is.InRange(4f - 1.5f - Tolerance, 4f + 1.5f + Tolerance));
            }
        }

        [Test]
        public void FractalPerlin_StaysWithinItsAmplitude_AndRepeatsForASeed()
        {
            var a = new FractalPerlin2DGenerator(5, 4f, 2f, 0.15f, octaves: 5);
            var b = new FractalPerlin2DGenerator(5, 4f, 2f, 0.15f, octaves: 5);
            for (int i = 0; i < 200; i++)
            {
                float height = a.Height(i * 0.7f, i * 0.4f);
                Assert.That(height, Is.InRange(2f, 6f));
                Assert.That(height, Is.EqualTo(b.Height(i * 0.7f, i * 0.4f)));
            }
        }

        [Test]
        public void Perlin3D_IsSolidDeepDown_AndEmptyHighUp()
        {
            var generator = new Perlin3DGenerator(9, 4f, 2f, 0.2f);
            for (int i = 0; i < 50; i++)
            {
                Assert.That(generator.Density(new Vector3(i * 0.5f, -2f, i * 0.3f)), Is.EqualTo(1f));
                Assert.That(generator.Density(new Vector3(i * 0.5f, 10f, i * 0.3f)), Is.EqualTo(0f));
            }
        }

        [Test]
        public void Fill_FlatTerrain_PutsTheSurfaceAtTheLevel()
        {
            var chunk = new Chunk(new Vector3Int(4, 6, 4));
            ChunkGenerator.Fill(chunk, new FlatGenerator(2.5f), Vector3.zero, 1f);

            // Surface halfway between sample rows 2 (density 1) and 3 (density 0).
            var settings = new ChunkMeshSettings(0.5f, 1f);
            bool hit = SurfaceRaycast.Cast(new Ray(new Vector3(1.5f, 10f, 1.5f), Vector3.down), chunk, settings, out _, out Vector3 point);
            Assert.That(hit, Is.True);
            Assert.That(point.y, Is.EqualTo(2.5f).Within(Tolerance));
        }

        [Test]
        public void Fill_SamplesWorldPositions_SoNeighbouringChunksMatchAtTheirBorder()
        {
            var generator = new FractalPerlin2DGenerator(11, 3f, 2f, 0.2f, octaves: 3);
            var left = new Chunk(new Vector3Int(4, 6, 4));
            var right = new Chunk(new Vector3Int(4, 6, 4));
            ChunkGenerator.Fill(left, generator, Vector3.zero, 1f);
            ChunkGenerator.Fill(right, generator, new Vector3(4f, 0f, 0f), 1f);

            for (int y = 0; y <= 6; y++)
            {
                for (int z = 0; z <= 4; z++)
                {
                    Assert.That(right.GetDensity(new Vector3Int(0, y, z)), Is.EqualTo(left.GetDensity(new Vector3Int(4, y, z))));
                }
            }
        }

        [Test]
        public void Create_BuildsEachGeneratorType()
        {
            foreach (TerrainGeneratorType type in System.Enum.GetValues(typeof(TerrainGeneratorType)))
            {
                var settings = new TerrainSettings();
                typeof(TerrainSettings)
                    .GetField("generator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .SetValue(settings, type);

                ITerrainGenerator generator = TerrainGenerators.Create(settings);
                Assert.That(generator, Is.Not.Null, type.ToString());
                Assert.That(generator.Density(new Vector3(0.5f, -10f, 0.5f)), Is.EqualTo(1f), type.ToString());
            }
        }
    }
}
