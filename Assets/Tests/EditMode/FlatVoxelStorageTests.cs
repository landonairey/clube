using System;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class FlatVoxelStorageTests
    {
        [Test]
        public void Constructor_RejectsFewerThanTwoSamplesPerAxis()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FlatVoxelStorage(new Vector3Int(1, 2, 2)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FlatVoxelStorage(new Vector3Int(2, 2, 0)));
        }

        [Test]
        public void NewStorage_IsAllZero()
        {
            var storage = new FlatVoxelStorage(new Vector3Int(2, 3, 4));

            ForEachSample(storage, (x, y, z) => Assert.That(storage.GetDensity(x, y, z), Is.EqualTo(0f)));
        }

        [Test]
        public void EverySample_StoresItsOwnValue()
        {
            // Non-cubic so a swapped axis in the index maths would collide.
            var storage = new FlatVoxelStorage(new Vector3Int(3, 4, 5));

            ForEachSample(storage, (x, y, z) => storage.SetDensity(x, y, z, Encode(x, y, z)));

            ForEachSample(storage, (x, y, z) => Assert.That(storage.GetDensity(x, y, z), Is.EqualTo(Encode(x, y, z))));
        }

        [TestCase(-1, 0, 0)]
        [TestCase(3, 0, 0)]
        [TestCase(0, 4, 0)]
        [TestCase(0, 0, 5)]
        public void OutOfRangeSample_Throws(int x, int y, int z)
        {
            var storage = new FlatVoxelStorage(new Vector3Int(3, 4, 5));

            Assert.Throws<ArgumentOutOfRangeException>(() => storage.GetDensity(x, y, z));
            Assert.Throws<ArgumentOutOfRangeException>(() => storage.SetDensity(x, y, z, 1f));
        }

        [Test]
        public void ReadLayer_CopiesOneZLayer_XFastestThenY()
        {
            var storage = new FlatVoxelStorage(new Vector3Int(3, 4, 5));
            ForEachSample(storage, (x, y, z) => storage.SetDensity(x, y, z, Encode(x, y, z)));
            var layer = new float[3 * 4];

            storage.ReadLayer(2, layer);

            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    Assert.That(layer[x + 3 * y], Is.EqualTo(Encode(x, y, 2)));
                }
            }
        }

        [TestCase(-1)]
        [TestCase(5)]
        public void ReadLayer_OutOfRange_Throws(int z)
        {
            var storage = new FlatVoxelStorage(new Vector3Int(3, 4, 5));

            Assert.Throws<ArgumentOutOfRangeException>(() => storage.ReadLayer(z, new float[3 * 4]));
        }

        private static float Encode(int x, int y, int z)
        {
            return x + 10 * y + 100 * z;
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
