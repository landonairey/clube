using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>The dirty chunks waiting for a mesh (GL774): taken as they become dirty, held ones parked until let go.</summary>
    public class DirtyChunksTests
    {
        [Test]
        public void ChunksWait_UntilCleaned_AndComeBackWhenDirtiedAgain()
        {
            World world = NewWorld();
            var dirty = new DirtyChunks();
            var list = new List<Vector3Int>();
            Chunk chunk = Add(world, Vector3Int.zero);

            dirty.Take(world);
            dirty.CollectWaiting(world, list);
            Assert.That(list, Is.EquivalentTo(new[] { Vector3Int.zero }));

            chunk.MarkClean();
            dirty.CollectWaiting(world, list);
            Assert.That(list, Is.Empty, "a cleaned chunk is forgotten");

            chunk.MarkDirty();
            dirty.Take(world);
            dirty.CollectWaiting(world, list);
            Assert.That(list, Is.EquivalentTo(new[] { Vector3Int.zero }), "and noted again when dirtied");
        }

        [Test]
        public void ParkedChunks_StayOutOfTheLook_UntilTheirColumnIsLetGo()
        {
            World world = NewWorld();
            var dirty = new DirtyChunks();
            var list = new List<Vector3Int>();
            var held = new HashSet<Vector2Int> { new Vector2Int(1, 0) };
            Add(world, new Vector3Int(1, 0, 0));
            Add(world, new Vector3Int(1, 1, 0));
            Add(world, new Vector3Int(2, 0, 0));
            dirty.Take(world);
            dirty.Park(new Vector3Int(1, 0, 0));
            dirty.Park(new Vector3Int(1, 1, 0));

            dirty.LookAtParked(world, held.Contains);
            dirty.CollectWaiting(world, list);
            Assert.That(list, Is.EquivalentTo(new[] { new Vector3Int(2, 0, 0) }));
            Assert.That(dirty.ParkedCount, Is.EqualTo(2));

            world.Chunks[new Vector3Int(1, 1, 0)].MarkDirty();
            dirty.Take(world);
            dirty.CollectWaiting(world, list);
            Assert.That(list, Has.No.Member(new Vector3Int(1, 1, 0)), "an edit to a parked chunk leaves it parked");

            held.Clear();
            dirty.LookAtParked(world, held.Contains);
            dirty.CollectWaiting(world, list);
            Assert.That(list, Is.EquivalentTo(new[] { new Vector3Int(1, 0, 0), new Vector3Int(1, 1, 0), new Vector3Int(2, 0, 0) }));
            Assert.That(dirty.ParkedCount, Is.Zero);
        }

        [Test]
        public void UnloadedParkedChunks_AreForgotten()
        {
            World world = NewWorld();
            var dirty = new DirtyChunks();
            Add(world, Vector3Int.zero);
            dirty.Take(world);
            dirty.Park(Vector3Int.zero);

            world.Unload(Vector3Int.zero);
            dirty.LookAtParked(world, _ => true);

            Assert.That(dirty.ParkedCount, Is.Zero);
        }

        private static World NewWorld()
        {
            return new World(new Vector3Int(8, 8, 8), 1f, samples => new ByteVoxelStorage(samples));
        }

        private static Chunk Add(World world, Vector3Int coord)
        {
            var chunk = new Chunk(new Vector3Int(9, 9, 9));
            world.Add(coord, chunk);
            return chunk;
        }
    }
}
