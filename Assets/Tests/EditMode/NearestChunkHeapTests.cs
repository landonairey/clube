using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>The streamer's nearest-first order for starting mesh jobs (GL759).</summary>
    public class NearestChunkHeapTests
    {
        private static int DistanceSquared(Vector3Int coord, Vector3Int centre)
        {
            Vector3Int d = coord - centre;
            return d.x * d.x + d.y * d.y + d.z * d.z;
        }

        [Test]
        public void TryTake_GivesEveryChunkOnce_NearestFirst()
        {
            var centre = new Vector3Int(3, 1, -2);
            var random = new System.Random(5);
            var coords = new List<Vector3Int>();
            for (int i = 0; i < 500; i++)
            {
                coords.Add(new Vector3Int(random.Next(-40, 40), random.Next(0, 12), random.Next(-40, 40)));
            }
            var expected = coords.Select(c => DistanceSquared(c, centre)).OrderBy(d => d).ToList();
            var all = new List<Vector3Int>(coords);

            var heap = new NearestChunkHeap(coords, centre);
            var taken = new List<Vector3Int>();
            while (heap.TryTake(out Vector3Int coord))
            {
                taken.Add(coord);
            }

            Assert.That(taken.Select(c => DistanceSquared(c, centre)), Is.EqualTo(expected), "nearest first");
            Assert.That(taken, Is.EquivalentTo(all), "each chunk once");
        }

        [Test]
        public void TryTake_EmptyList_TakesNothing()
        {
            var heap = new NearestChunkHeap(new List<Vector3Int>(), Vector3Int.zero);
            Assert.That(heap.TryTake(out _), Is.False);
        }

        [Test]
        public void TryTake_OneChunk_TakesItThenStops()
        {
            var only = new Vector3Int(4, 5, 6);
            var heap = new NearestChunkHeap(new List<Vector3Int> { only }, Vector3Int.zero);
            Assert.That(heap.TryTake(out Vector3Int coord), Is.True);
            Assert.That(coord, Is.EqualTo(only));
            Assert.That(heap.TryTake(out _), Is.False);
        }
    }
}
