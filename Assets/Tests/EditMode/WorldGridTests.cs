using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class WorldGridTests
    {
        private static readonly WorldGrid Grid = new WorldGrid(new Vector3Int(8, 8, 8), 0.5f);

        [TestCase(0, 8, 0)]
        [TestCase(7, 8, 0)]
        [TestCase(8, 8, 1)]
        [TestCase(-1, 8, -1)]
        [TestCase(-8, 8, -1)]
        [TestCase(-9, 8, -2)]
        public void FloorDiv_RoundsTowardsNegativeInfinity(int value, int divisor, int expected)
        {
            Assert.That(WorldGrid.FloorDiv(value, divisor), Is.EqualTo(expected));
        }

        [Test]
        public void WorldToChunk_FloorsNegativePositions()
        {
            // A chunk is 8 voxels × 0.5 = 4 units.
            Assert.That(Grid.WorldToChunk(new Vector3(0.1f, 3.9f, 4.1f)), Is.EqualTo(new Vector3Int(0, 0, 1)));
            Assert.That(Grid.WorldToChunk(new Vector3(-0.1f, -4.1f, -3.9f)), Is.EqualTo(new Vector3Int(-1, -2, -1)));
        }

        [Test]
        public void ChunkOrigin_And_GlobalToLocal_RoundTrip()
        {
            var chunk = new Vector3Int(-2, 1, 3);
            Assert.That(Grid.ChunkOrigin(chunk), Is.EqualTo(new Vector3(-8f, 4f, 12f)));

            var sample = new Vector3Int(-13, 9, 30);
            Vector3Int local = Grid.GlobalToLocal(sample, chunk);
            Assert.That(local, Is.EqualTo(new Vector3Int(3, 1, 6)));
            Assert.That(Grid.ChunkFirstSample(chunk) + local, Is.EqualTo(sample));
        }

        [Test]
        public void ChunksContainingSample_CountsShareBorders()
        {
            var result = new List<Vector3Int>();

            Grid.ChunksContainingSample(new Vector3Int(3, 3, 3), result);
            Assert.That(result, Is.EquivalentTo(new[] { Vector3Int.zero }), "Inside a chunk");

            Grid.ChunksContainingSample(new Vector3Int(8, 3, 3), result);
            Assert.That(result, Is.EquivalentTo(new[] { new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0) }), "On a face");

            Grid.ChunksContainingSample(new Vector3Int(8, 0, 3), result);
            Assert.That(result.Count, Is.EqualTo(4), "On an edge");

            Grid.ChunksContainingSample(new Vector3Int(-8, 0, 16), result);
            Assert.That(result.Count, Is.EqualTo(8), "On a corner");
            foreach (Vector3Int chunk in result)
            {
                Assert.That(Grid.ChunkContainsSample(chunk, new Vector3Int(-8, 0, 16)), Is.True, chunk.ToString());
            }
        }

        [Test]
        public void ChunksOverlapping_IncludesChunksThatOnlyShareABorder()
        {
            var result = new List<Vector3Int>();
            Grid.ChunksOverlapping(new Vector3Int(8, 1, 1), new Vector3Int(9, 2, 2), result);
            Assert.That(result, Is.EquivalentTo(new[] { new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0) }));

            Grid.ChunksOverlapping(new Vector3Int(2, 2, 2), new Vector3Int(3, 3, 3), result);
            Assert.That(result, Is.EquivalentTo(new[] { Vector3Int.zero }));
        }
    }
}
