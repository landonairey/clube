using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// Generation as Burst jobs (K35): the job paths agree with the managed generators, uniform
    /// chunks are found and kept without storage, and the shared kernel rolls ore as
    /// <see cref="OreField.Pick"/> does.
    /// </summary>
    public class GenerationJobTests
    {
        // One density step of byte storage, plus room for Burst rounding (it may fuse multiply-adds).
        private const float OneStep = 1f / 255f + 1e-4f;

        private VoxelMaterial stone;
        private VoxelMaterial dirt;
        private VoxelMaterial grass;
        private VoxelMaterial copper;

        [SetUp]
        public void MakeMaterials()
        {
            stone = VoxelMaterial.Create(0, "Stone");
            dirt = VoxelMaterial.Create(1, "Dirt");
            grass = VoxelMaterial.Create(2, "Grass");
            copper = VoxelMaterial.Create(3, "Copper", VoxelMaterialCategory.Ore);
        }

        [TearDown]
        public void DestroyMaterials()
        {
            Object.DestroyImmediate(stone);
            Object.DestroyImmediate(dirt);
            Object.DestroyImmediate(grass);
            Object.DestroyImmediate(copper);
        }

        [Test]
        public void ColumnHeightsJob_MatchesTheManagedHeights()
        {
            var generator = new FractalPerlin2DGenerator(11, 3f, 2f, 0.2f, octaves: 4);
            var block = new ColumnBlock(new int2(-5, 7), new int2(9, 6), 0.5f);
            var heights = new NativeArray<float>(block.Length, Allocator.TempJob);
            var range = new NativeArray<float2>(1, Allocator.TempJob);
            try
            {
                generator.ScheduleColumnHeights(block, heights, range).Complete();
                float low = float.MaxValue;
                float high = float.MinValue;
                for (int z = 0; z < block.Count.y; z++)
                {
                    for (int x = 0; x < block.Count.x; x++)
                    {
                        float2 position = block.Position(x, z);
                        float expected = generator.Height(position.x, position.y);
                        Assert.That(heights[block.Index(x, z)], Is.EqualTo(expected).Within(1e-4f), $"column {x}, {z}");
                        low = Mathf.Min(low, expected);
                        high = Mathf.Max(high, expected);
                    }
                }
                Assert.That(range[0].x, Is.EqualTo(low).Within(1e-4f));
                Assert.That(range[0].y, Is.EqualTo(high).Within(1e-4f));
            }
            finally
            {
                heights.Dispose();
                range.Dispose();
            }
        }

        [Test]
        public void HeightfieldJob_MatchesSamplingTheGeneratorDirectly()
        {
            var generator = new FractalPerlin2DGenerator(11, 3f, 2f, 0.2f, octaves: 3);
            var chunk = new Chunk(new Vector3Int(6, 6, 6));
            var origin = new Vector3(2f, 0f, -3f);
            ChunkGenerator.Fill(chunk, generator, origin, 1f);

            for (int z = 0; z <= 6; z++)
            {
                for (int y = 0; y <= 6; y++)
                {
                    for (int x = 0; x <= 6; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        float expected = generator.Density(origin + (Vector3)sample);
                        Assert.That(chunk.GetDensity(sample), Is.EqualTo(expected).Within(1e-4f), sample.ToString());
                    }
                }
            }
        }

        [Test]
        public void ManagedGenerator_FillsLikeItsJobForm()
        {
            var generator = new SineGenerator(4f, 1.5f, 0.1f);
            var viaJob = new Chunk(new Vector3Int(5, 8, 5));
            var viaDepths = new Chunk(new Vector3Int(5, 8, 5));
            ChunkGenerator.Fill(viaJob, generator, Vector3.zero, 1f);
            ChunkGenerator.Fill(viaDepths, new Wrapped(generator), Vector3.zero, 1f);

            AssertSameDensities(viaJob, viaDepths, 1e-4f);
        }

        [Test]
        public void ByteFormat_RoundsToTheNearestStep()
        {
            var generator = new FractalPerlin2DGenerator(5, 4f, 2f, 0.15f, octaves: 2);
            var floats = new Chunk(new Vector3Int(6, 8, 6));
            var bytes = new Chunk(new ByteVoxelStorage(new Vector3Int(7, 9, 7)));
            ChunkGenerator.Fill(floats, generator, Vector3.zero, 1f);
            ChunkGenerator.Fill(bytes, generator, Vector3.zero, 1f);

            AssertSameDensities(floats, bytes, OneStep);
        }

        [Test]
        public void ChunkAboveTheSurface_IsUniformAir_WithNoStorage()
        {
            var world = new World(new Vector3Int(8, 8, 8), 1f, samples => new ByteVoxelStorage(samples));
            Chunk sky = world.Load(new Vector3Int(0, 3, 0), new FlatGenerator(4f), Layers());

            Assert.That(sky.IsUniform, Is.True);
            Assert.That(sky.UniformDensity, Is.EqualTo(0f));
            Assert.That(sky.DensityMemoryBytes, Is.Zero);
            Assert.That(sky.Materials.IsUniform, Is.True);
        }

        [Test]
        public void ChunkBelowTheSurface_IsUniformSolid_ButKeepsItsLayers()
        {
            var world = new World(new Vector3Int(8, 8, 8), 1f, samples => new ByteVoxelStorage(samples));
            Chunk deep = world.Load(new Vector3Int(0, 0, 0), new FlatGenerator(20f), Layers());

            Assert.That(deep.IsUniform, Is.True);
            Assert.That(deep.UniformDensity, Is.EqualTo(1f));
            Assert.That(deep.Materials.IsUniform, Is.False, "dirt over stone");
            Assert.That(deep.GetMaterial(new Vector3Int(0, 8, 0)), Is.EqualTo(dirt.Id), "12 m below the surface: band");
            Assert.That(deep.GetMaterial(new Vector3Int(0, 0, 0)), Is.EqualTo(stone.Id), "20 m below: base");
        }

        [Test]
        public void SurfaceChunk_IsStored()
        {
            var world = new World(new Vector3Int(8, 8, 8), 1f, samples => new ByteVoxelStorage(samples));
            Chunk surface = world.Load(Vector3Int.zero, new FlatGenerator(4.5f), Layers());

            Assert.That(surface.IsUniform, Is.False);
            Assert.That(surface.DensityMemoryBytes, Is.EqualTo(9 * 9 * 9));
            Assert.That(surface.GetMaterial(new Vector3Int(2, 4, 2)), Is.EqualTo(grass.Id));
        }

        [Test]
        public void UniformChunk_GetsStorageOnItsFirstEdit_KeepingItsDensity()
        {
            var world = new World(new Vector3Int(4, 4, 4), 1f, samples => new ByteVoxelStorage(samples));
            Chunk deep = world.Load(Vector3Int.zero, new FlatGenerator(20f));
            Assert.That(deep.IsUniform, Is.True);

            world.SetDensity(new Vector3Int(1, 1, 1), 0f);

            Assert.That(deep.IsUniform, Is.False);
            Assert.That(deep.GetDensity(new Vector3Int(1, 1, 1)), Is.EqualTo(0f));
            Assert.That(deep.GetDensity(new Vector3Int(2, 2, 2)), Is.EqualTo(1f), "the rest stays solid");
            Assert.That(deep.IsDirty, Is.True);
        }

        [Test]
        public void VolumeGenerator_ChunkAboveItsHighestSurface_IsAirWithoutSampling()
        {
            var generator = new Perlin3DGenerator(9, 4f, 2f, 0.2f);
            Assert.That(generator.SurfaceBounds.y, Is.LessThan(8f));

            var world = new World(new Vector3Int(8, 8, 8), 1f);
            Chunk sky = world.Load(new Vector3Int(0, 1, 0), generator);

            Assert.That(sky.IsUniform, Is.True);
            Assert.That(sky.UniformDensity, Is.EqualTo(0f));
        }

        [Test]
        public void CurveTable_FollowsTheCurve()
        {
            var curve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.4f, 0.15f), new Keyframe(0.55f, 0.75f), new Keyframe(1f, 1f));
            CurveTable table = CurveTable.Bake(curve);
            for (int i = 0; i <= 100; i++)
            {
                float t = i / 100f;
                Assert.That(table.Evaluate(t), Is.EqualTo(curve.Evaluate(t)).Within(1e-3f), $"t = {t}");
            }
            Assert.That(table.Evaluate(-1f), Is.EqualTo(curve.Evaluate(0f)).Within(1e-5f));
            Assert.That(table.Evaluate(2f), Is.EqualTo(curve.Evaluate(1f)).Within(1e-5f));
        }

        [Test]
        public void KernelOre_MatchesPick_ForEverySampleUnderground()
        {
            var settings = new OreGeneration { CellSize = 8f };
            settings.Ores.Add(new OreSpec { Ore = copper, NodesPerCell = 3f, MinDepth = 1f, MaxDepth = 6f, PeakProbability = 0.9f, Spread = new Vector3(1.5f, 1f, 1.5f) });
            settings.Ores[0].Hosts.Add(stone);
            var generator = new FlatGenerator(7f);
            var ores = new OreField(settings, 3, generator);
            TerrainLayers layers = Layers(bandDepth: 2f);

            var chunk = new Chunk(new Vector3Int(8, 8, 8));
            ChunkGenerator.Fill(chunk, generator, Vector3.zero, 1f, layers, ores);

            var nodes = new List<OreNode>();
            ores.CollectNodes(new Bounds(Vector3.one * 4f, Vector3.one * 8f), nodes);
            int oreSamples = 0;
            for (int z = 0; z <= 8; z++)
            {
                for (int y = 0; y <= 8; y++)
                {
                    for (int x = 0; x <= 8; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        float depth = generator.Depth(sample);
                        if (depth <= 0f)
                        {
                            continue;
                        }
                        byte expected = ores.Pick(layers.MaterialAt(depth), sample, sample, nodes);
                        Assert.That(chunk.GetMaterial(sample), Is.EqualTo(expected), sample.ToString());
                        oreSamples += expected == copper.Id ? 1 : 0;
                    }
                }
            }
            Assert.That(oreSamples, Is.GreaterThan(0), "the test should place some ore");
        }

        [Test]
        public void Release_GivesTheArraysBackToThePool()
        {
            VoxelArrayPool.Clear();
            var chunk = new Chunk(new ByteVoxelStorage(new Vector3Int(5, 5, 5)));
            chunk.SetMaterial(Vector3Int.one, 3);

            chunk.Release();

            Assert.That(VoxelArrayPool.FreeCount, Is.EqualTo(2), "densities and materials");
            byte[] reused = VoxelArrayPool.Rent(125);
            Assert.That(reused, Is.All.EqualTo((byte)0), "rented arrays come back cleared");
            VoxelArrayPool.Clear();
        }

        private TerrainLayers Layers(float bandDepth = 15f)
        {
            return new TerrainLayers { Top = grass, TopDepth = 1f, Band = dirt, BandDepth = bandDepth, Base = stone };
        }

        private static void AssertSameDensities(Chunk expected, Chunk actual, float tolerance)
        {
            Vector3Int samples = expected.SampleCount;
            for (int z = 0; z < samples.z; z++)
            {
                for (int y = 0; y < samples.y; y++)
                {
                    for (int x = 0; x < samples.x; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        Assert.That(actual.GetDensity(sample), Is.EqualTo(expected.GetDensity(sample)).Within(tolerance), sample.ToString());
                    }
                }
            }
        }

        // A generator with no job form, so generation samples it on the main thread (DepthFillJob).
        private sealed class Wrapped : ITerrainGenerator
        {
            private readonly ITerrainGenerator inner;

            public Wrapped(ITerrainGenerator inner)
            {
                this.inner = inner;
            }

            public float Depth(Vector3 position)
            {
                return inner.Depth(position);
            }
        }
    }
}
