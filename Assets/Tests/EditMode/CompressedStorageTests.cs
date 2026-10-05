using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// What the compressed schemes promise beyond the shared contract (<see cref="VoxelStorageTests"/>):
    /// the octree collapses uniform regions (K25) and run-length storage merges equal runs (K24).
    /// </summary>
    public class CompressedStorageTests
    {
        [Test]
        public void Octree_UniformCube_CollapsesToOneNode()
        {
            // 8 samples a side is already a power of two, so there's no padding to block collapse.
            var octree = new OctreeVoxelStorage(new Vector3Int(8, 8, 8), maxDepth: 3);
            Fill(octree, (x, y, z) => 1f);

            Assert.That(octree.NodeCount, Is.EqualTo(1));
            Assert.That(octree.BrickCount, Is.Zero);
            Assert.That(octree.GetDensity(3, 4, 5), Is.EqualTo(1f));
        }

        [Test]
        public void Octree_FlatFloor_StoresDetailOnlyNearTheSurface()
        {
            // Solid below y = 3.5, with a smooth sample row at y = 3 and 4: only the slab
            // holding those rows needs bricks; the octants above and below stay single nodes.
            var octree = new OctreeVoxelStorage(new Vector3Int(16, 16, 16), maxDepth: 2);
            Fill(octree, (x, y, z) => y < 3 ? 1f : y > 4 ? 0f : y == 3 ? 0.75f : 0.25f);

            // Bricks are 4 samples a side; the slab y 0-3 and 4-7 is 4 × 4 bricks per layer.
            Assert.That(octree.BrickSize, Is.EqualTo(4));
            Assert.That(octree.BrickCount, Is.EqualTo(32));
            Assert.That(octree.GetDensity(5, 3, 9), Is.EqualTo(0.75f));
            Assert.That(octree.GetDensity(5, 12, 9), Is.EqualTo(0f));
        }

        [Test]
        public void Octree_RandomEdits_MatchAPlainArray_AtEveryDepth([Values(1, 2, 3, 4)] int depth)
        {
            var sampleCount = new Vector3Int(11, 9, 13);
            var octree = new OctreeVoxelStorage(sampleCount, depth);
            var expected = new float[sampleCount.x, sampleCount.y, sampleCount.z];
            var random = new System.Random(depth);
            for (int i = 0; i < 3000; i++)
            {
                int x = random.Next(sampleCount.x);
                int y = random.Next(sampleCount.y);
                int z = random.Next(sampleCount.z);
                float value = random.NextDouble() < 0.8 ? (float)random.Next(2) : (float)random.NextDouble();
                octree.SetDensity(x, y, z, value);
                expected[x, y, z] = value;
            }

            for (int z = 0; z < sampleCount.z; z++)
            {
                for (int y = 0; y < sampleCount.y; y++)
                {
                    for (int x = 0; x < sampleCount.x; x++)
                    {
                        Assert.That(octree.GetDensity(x, y, z), Is.EqualTo(expected[x, y, z]), $"({x}, {y}, {z})");
                    }
                }
            }
        }

        [Test]
        public void Octree_ClearingEverything_CollapsesBack()
        {
            var octree = new OctreeVoxelStorage(new Vector3Int(8, 8, 8), maxDepth: 3);
            Fill(octree, (x, y, z) => (x + y + z) % 3 == 0 ? 1f : 0.5f);
            Fill(octree, (x, y, z) => 0f);

            Assert.That(octree.NodeCount, Is.EqualTo(1));
            Assert.That(octree.BrickCount, Is.Zero);
        }

        [Test]
        public void RunLengthY_FlatFloor_IsTwoRunsPerColumn()
        {
            var sampleCount = new Vector3Int(5, 8, 6);
            var storage = new RunLengthVoxelStorage(sampleCount, runAxis: 1);
            Fill(storage, (x, y, z) => y < 3 ? 1f : 0f);

            Assert.That(storage.RunCount, Is.EqualTo(sampleCount.x * sampleCount.z * 2));
        }

        [Test]
        public void RunLength_EditsThatRestoreAValue_MergeRunsAgain()
        {
            var storage = new RunLengthVoxelStorage(new Vector3Int(10, 2, 2), runAxis: 0);
            storage.SetDensity(4, 0, 0, 1f);
            storage.SetDensity(5, 0, 0, 1f);
            storage.SetDensity(4, 0, 0, 0f);
            storage.SetDensity(5, 0, 0, 0f);

            Assert.That(storage.RunCount, Is.EqualTo(4), "one run per line again");
        }

        private static void Fill(IVoxelStorage storage, System.Func<int, int, int, float> value)
        {
            Vector3Int count = storage.SampleCount;
            for (int z = 0; z < count.z; z++)
            {
                for (int y = 0; y < count.y; y++)
                {
                    for (int x = 0; x < count.x; x++)
                    {
                        storage.SetDensity(x, y, z, value(x, y, z));
                    }
                }
            }
        }
    }
}
