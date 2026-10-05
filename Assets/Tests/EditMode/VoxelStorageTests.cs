using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// The contract every <see cref="IVoxelStorage"/> keeps (A12, 2G), run against each scheme.
    /// The random edit sequences are what catch run splitting and merging (RLE) and node
    /// division and collapse (octree) going wrong.
    /// </summary>
    [TestFixture(VoxelStorageType.Flat)]
    [TestFixture(VoxelStorageType.FlatByte)]
    [TestFixture(VoxelStorageType.RunLengthX)]
    [TestFixture(VoxelStorageType.RunLengthY)]
    [TestFixture(VoxelStorageType.RunLengthZ)]
    public class VoxelStorageTests
    {
        private readonly VoxelStorageType type;

        public VoxelStorageTests(VoxelStorageType type)
        {
            this.type = type;
        }

        // Single bytes round to the nearest 1/255; every other scheme stores exactly.
        private float Tolerance => type == VoxelStorageType.FlatByte ? 0.5f / 255f + 1e-6f : 0f;

        [Test]
        public void Create_RejectsFewerThanTwoSamplesPerAxis()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => VoxelStorages.Create(type, new Vector3Int(1, 2, 2)));
            Assert.Throws<ArgumentOutOfRangeException>(() => VoxelStorages.Create(type, new Vector3Int(2, 2, 0)));
        }

        [Test]
        public void NewStorage_IsAllZero()
        {
            IVoxelStorage storage = Create(new Vector3Int(2, 3, 4));

            ForEachSample(storage, (x, y, z) => Assert.That(storage.GetDensity(x, y, z), Is.EqualTo(0f)));
        }

        [Test]
        public void EverySample_StoresItsOwnValue()
        {
            // Non-cubic so a swapped axis in the index maths would collide.
            IVoxelStorage storage = Create(new Vector3Int(3, 4, 5));

            ForEachSample(storage, (x, y, z) => storage.SetDensity(x, y, z, Encode(x, y, z)));

            ForEachSample(storage, (x, y, z) =>
                Assert.That(storage.GetDensity(x, y, z), Is.EqualTo(Encode(x, y, z)).Within(Tolerance), $"({x}, {y}, {z})"));
        }

        [Test]
        public void RandomEdits_MatchAPlainArray([Values(1, 2, 3)] int seed)
        {
            var sampleCount = new Vector3Int(9, 7, 10);
            IVoxelStorage storage = Create(sampleCount);
            var expected = new float[sampleCount.x, sampleCount.y, sampleCount.z];
            var random = new System.Random(seed);

            // Mostly 0 and 1 in blocks (what solid and air look like), some in-between values.
            for (int i = 0; i < 4000; i++)
            {
                int x = random.Next(sampleCount.x);
                int y = random.Next(sampleCount.y);
                int z = random.Next(sampleCount.z);
                double roll = random.NextDouble();
                float value = roll < 0.4 ? 0f : roll < 0.8 ? 1f : (float)random.NextDouble();
                int size = random.Next(1, 4);
                for (int dz = 0; dz < size && z + dz < sampleCount.z; dz++)
                {
                    for (int dy = 0; dy < size && y + dy < sampleCount.y; dy++)
                    {
                        for (int dx = 0; dx < size && x + dx < sampleCount.x; dx++)
                        {
                            storage.SetDensity(x + dx, y + dy, z + dz, value);
                            expected[x + dx, y + dy, z + dz] = value;
                        }
                    }
                }
            }

            ForEachSample(storage, (x, y, z) =>
                Assert.That(storage.GetDensity(x, y, z), Is.EqualTo(expected[x, y, z]).Within(Tolerance), $"({x}, {y}, {z})"));
        }

        [Test]
        public void ReadLayer_MatchesGetDensity()
        {
            IVoxelStorage storage = Filled(new Vector3Int(5, 4, 6), seed: 7);
            Vector3Int count = storage.SampleCount;
            var layer = new float[count.x * count.y];
            for (int z = 0; z < count.z; z++)
            {
                storage.ReadLayer(z, layer);
                for (int y = 0; y < count.y; y++)
                {
                    for (int x = 0; x < count.x; x++)
                    {
                        Assert.That(layer[x + count.x * y], Is.EqualTo(storage.GetDensity(x, y, z)), $"({x}, {y}, {z})");
                    }
                }
            }
        }

        [TestCase(-1, 0, 0)]
        [TestCase(3, 0, 0)]
        [TestCase(0, 4, 0)]
        [TestCase(0, 0, 5)]
        public void OutOfRangeSample_Throws(int x, int y, int z)
        {
            IVoxelStorage storage = Create(new Vector3Int(3, 4, 5));

            Assert.Throws<ArgumentOutOfRangeException>(() => storage.GetDensity(x, y, z));
            Assert.Throws<ArgumentOutOfRangeException>(() => storage.SetDensity(x, y, z, 1f));
        }

        [TestCase(-1)]
        [TestCase(5)]
        public void ReadLayer_OutOfRange_Throws(int z)
        {
            IVoxelStorage storage = Create(new Vector3Int(3, 4, 5));

            Assert.Throws<ArgumentOutOfRangeException>(() => storage.ReadLayer(z, new float[3 * 4]));
        }

        [Test]
        public void WriteThenRead_GivesTheSameStorage()
        {
            IVoxelStorage storage = Filled(new Vector3Int(6, 5, 7), seed: 11);
            IVoxelStorage copy;
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    VoxelStorages.Write(storage, writer);
                }
                stream.Position = 0;
                using (var reader = new BinaryReader(stream))
                {
                    copy = VoxelStorages.Read(reader);
                }
            }

            Assert.That(copy.Type, Is.EqualTo(storage.Type));
            Assert.That(copy.SampleCount, Is.EqualTo(storage.SampleCount));
            ForEachSample(storage, (x, y, z) =>
                Assert.That(copy.GetDensity(x, y, z), Is.EqualTo(storage.GetDensity(x, y, z)), $"({x}, {y}, {z})"));
        }

        [Test]
        public void Chunk_MeshesLikeFlatStorageWithTheSameValues()
        {
            IVoxelStorage storage = Filled(new Vector3Int(7, 6, 8), seed: 5);
            var flat = new FlatVoxelStorage(storage.SampleCount);
            ForEachSample(storage, (x, y, z) => flat.SetDensity(x, y, z, storage.GetDensity(x, y, z)));

            var settings = new ChunkMeshSettings(0.5f, 1f);
            var expectedVertices = new List<Vector3>();
            var expectedTriangles = new List<int>();
            ChunkMesher.Build(new Chunk(flat), settings, expectedVertices, expectedTriangles);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            ChunkMesher.Build(new Chunk(storage), settings, vertices, triangles);

            Assert.That(vertices, Is.EqualTo(expectedVertices));
            Assert.That(triangles, Is.EqualTo(expectedTriangles));
        }

        [Test]
        public void MemoryBytes_IsPositive()
        {
            Assert.That(Filled(new Vector3Int(4, 4, 4), seed: 3).MemoryBytes, Is.GreaterThan(0));
        }

        private IVoxelStorage Create(Vector3Int sampleCount)
        {
            return VoxelStorages.Create(type, sampleCount, octreeMaxDepth: 3);
        }

        // Solid below a bumpy surface, air above, in-between values near it: like terrain.
        private IVoxelStorage Filled(Vector3Int sampleCount, int seed)
        {
            IVoxelStorage storage = Create(sampleCount);
            var random = new System.Random(seed);
            ForEachSample(storage, (x, y, z) =>
            {
                float surface = sampleCount.y * 0.5f + (float)random.NextDouble() - 0.5f;
                float density = Mathf.Clamp01(0.5f + (surface - y) * 0.5f);
                storage.SetDensity(x, y, z, density);
            });
            return storage;
        }

        private static float Encode(int x, int y, int z)
        {
            return (x + 10 * y + 100 * z) / 1000f;
        }

        private static void ForEachSample(IVoxelStorage storage, Action<int, int, int> action)
        {
            Vector3Int count = storage.SampleCount;
            for (int z = 0; z < count.z; z++)
            {
                for (int y = 0; y < count.y; y++)
                {
                    for (int x = 0; x < count.x; x++)
                    {
                        action(x, y, z);
                    }
                }
            }
        }
    }
}
