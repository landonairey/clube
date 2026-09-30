using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class ChunkTests
    {
        [Test]
        public void SingleVoxelChunk_HasEightSamples()
        {
            var chunk = new Chunk(Vector3Int.one);

            Assert.That(chunk.VoxelCount, Is.EqualTo(Vector3Int.one));
            Assert.That(chunk.SampleCount, Is.EqualTo(new Vector3Int(2, 2, 2)));
        }

        [Test]
        public void NewChunk_IsDirty()
        {
            Assert.That(new Chunk(Vector3Int.one).IsDirty, Is.True);
        }

        [Test]
        public void SetDensity_WritesSampleAndMarksDirty()
        {
            var chunk = new Chunk(Vector3Int.one);
            chunk.MarkClean();

            chunk.SetDensity(new Vector3Int(1, 1, 1), 0.7f);

            Assert.That(chunk.GetDensity(new Vector3Int(1, 1, 1)), Is.EqualTo(0.7f));
            Assert.That(chunk.IsDirty, Is.True);
        }

        [Test]
        public void SetDensity_SameValue_LeavesChunkClean()
        {
            var chunk = new Chunk(Vector3Int.one);
            chunk.SetDensity(Vector3Int.zero, 0.7f);
            chunk.MarkClean();

            chunk.SetDensity(Vector3Int.zero, 0.7f);

            Assert.That(chunk.IsDirty, Is.False);
        }

        [Test]
        public void MarkDirty_WithoutEdit_MarksDirty()
        {
            var chunk = new Chunk(Vector3Int.one);
            chunk.MarkClean();

            chunk.MarkDirty();

            Assert.That(chunk.IsDirty, Is.True);
        }
    }
}
