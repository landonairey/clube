using NUnit.Framework;

namespace Clube.Core.Tests
{
    public class ChunkSizingTests
    {
        [Test]
        public void FinerVoxels_KeepTheWorldHeightInMetres(
            [Values(1, 2, 4, 8, 16)] int voxelsPerMetre)
        {
            // WorldLab's shape at 1 voxel per metre: 16 m chunks, 2 layers, 32 m high.
            ChunkSizing.Shape shape = ChunkSizing.KeepWorldSize(16, 1f, 2, 1f / voxelsPerMetre, 16, 32, 32);

            float chunkMetres = shape.Side / (float)voxelsPerMetre;
            Assert.That(chunkMetres * shape.Layers, Is.GreaterThanOrEqualTo(32f));
            Assert.That(chunkMetres * (shape.Layers - 1), Is.LessThan(32f), "no more layers than needed");
        }

        [Test]
        public void ChunkWidthInMetres_IsKeptWhileTheSideFits()
        {
            ChunkSizing.Shape shape = ChunkSizing.KeepWorldSize(16, 1f, 2, 0.5f, 16, 32, 32);

            Assert.That(shape.Side, Is.EqualTo(32));
            Assert.That(shape.Layers, Is.EqualTo(2));
        }

        [Test]
        public void SideLimits_AreRespected()
        {
            Assert.That(ChunkSizing.KeepWorldSize(16, 1f, 2, 1f / 16f, 16, 32, 32).Side, Is.EqualTo(32));
            Assert.That(ChunkSizing.KeepWorldSize(32, 1f / 16f, 16, 1f, 16, 32, 32).Side, Is.EqualTo(16));
        }

        [Test]
        public void Layers_AreCapped()
        {
            Assert.That(ChunkSizing.KeepWorldSize(16, 1f, 8, 1f / 16f, 16, 32, 10).Layers, Is.EqualTo(10));
        }

        [Test]
        public void GoingFineAndBack_ReturnsToTheStartingShape()
        {
            ChunkSizing.Shape fine = ChunkSizing.KeepWorldSize(16, 1f, 2, 1f / 8f, 16, 32, 32);
            ChunkSizing.Shape back = ChunkSizing.KeepWorldSize(fine.Side, 1f / 8f, fine.Layers, 1f, 16, 32, 32);

            Assert.That(back.Side, Is.EqualTo(16));
            Assert.That(back.Layers, Is.EqualTo(2));
        }
    }
}
