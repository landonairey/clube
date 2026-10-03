using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class TerrainBrushTests
    {
        private const float Tolerance = 1e-3f;

        [Test]
        public void HardAdd_InAir_BuildsASphereWithItsSurfaceOnTheRadius()
        {
            var chunk = new Chunk(new Vector3Int(10, 10, 10));
            var centre = new Vector3(5f, 5f, 5f);

            TerrainBrush.Apply(chunk, centre, 1f, new BrushSettings(3f), BrushOperation.Add);

            Assert.That(chunk.GetDensity(new Vector3Int(5, 5, 5)), Is.EqualTo(1f));
            Assert.That(chunk.GetDensity(new Vector3Int(5, 5, 9)), Is.EqualTo(0f));
            // The sample on the radius sits exactly at the iso level, so the surface passes through it.
            Assert.That(chunk.GetDensity(new Vector3Int(5, 8, 5)), Is.EqualTo(0.5f).Within(Tolerance));
        }

        [Test]
        public void HardRemove_InSolid_CarvesAHoleWithItsSurfaceOnTheRadius()
        {
            var chunk = SolidChunk(10);
            var centre = new Vector3(5f, 5f, 5f);

            TerrainBrush.Apply(chunk, centre, 1f, new BrushSettings(3f), BrushOperation.Remove);

            Assert.That(chunk.GetDensity(new Vector3Int(5, 5, 5)), Is.EqualTo(0f));
            Assert.That(chunk.GetDensity(new Vector3Int(5, 5, 9)), Is.EqualTo(1f));

            Assert.That(chunk.GetDensity(new Vector3Int(5, 2, 5)), Is.EqualTo(0.5f).Within(Tolerance));
        }

        [Test]
        public void Strength_ScalesEachApplication_AndApplicationsAddUp()
        {
            var chunk = new Chunk(new Vector3Int(6, 6, 6));
            var brush = new BrushSettings(2f, strength: 0.25f);
            var centre = new Vector3(3f, 3f, 3f);
            var sample = new Vector3Int(3, 3, 3);

            TerrainBrush.Apply(chunk, centre, 1f, brush, BrushOperation.Add);
            Assert.That(chunk.GetDensity(sample), Is.EqualTo(0.25f).Within(Tolerance));

            TerrainBrush.Apply(chunk, centre, 1f, brush, BrushOperation.Add);
            Assert.That(chunk.GetDensity(sample), Is.EqualTo(0.5f).Within(Tolerance));
        }

        [Test]
        public void SmoothFalloff_IsFullAtTheCentre_AndZeroAtTheRadius()
        {
            var brush = new BrushSettings(4f, falloff: BrushFalloff.Smooth);

            Assert.That(TerrainBrush.Weight(0f, brush), Is.EqualTo(1f));
            Assert.That(TerrainBrush.Weight(2f, brush), Is.EqualTo(0.5625f).Within(Tolerance));
            Assert.That(TerrainBrush.Weight(4f, brush), Is.EqualTo(0f));
            Assert.That(TerrainBrush.Weight(2f, brush), Is.LessThan(TerrainBrush.Weight(2f, new BrushSettings(4f))));
        }

        [Test]
        public void BrushPartlyOutsideTheChunk_EditsOnlyTheInsideSamples()
        {
            var chunk = new Chunk(new Vector3Int(4, 4, 4));

            int changed = TerrainBrush.Apply(chunk, new Vector3(-1f, 0f, 0f), 1f, new BrushSettings(2f), BrushOperation.Add);

            Assert.That(changed, Is.GreaterThan(0));
            Assert.That(chunk.GetDensity(Vector3Int.zero), Is.EqualTo(1f));
        }

        [Test]
        public void Apply_GoesThroughTheEditPath_SoTheChunkIsDirty()
        {
            var chunk = new Chunk(new Vector3Int(4, 4, 4));
            chunk.MarkClean();

            TerrainBrush.Apply(chunk, new Vector3(2f, 2f, 2f), 1f, new BrushSettings(1f), BrushOperation.Add);

            Assert.That(chunk.IsDirty, Is.True);
        }

        [Test]
        public void RemovingFromAir_ChangesNothing()
        {
            var chunk = new Chunk(new Vector3Int(4, 4, 4));
            chunk.MarkClean();

            int changed = TerrainBrush.Apply(chunk, new Vector3(2f, 2f, 2f), 1f, new BrushSettings(2f), BrushOperation.Remove);

            Assert.That(changed, Is.EqualTo(0));
            Assert.That(chunk.IsDirty, Is.False);
        }

        [Test]
        public void SmallerVoxels_KeepTheSphereTheSameWorldSize()
        {
            var chunk = new Chunk(new Vector3Int(20, 20, 20));
            var centre = new Vector3(5f, 5f, 5f);

            TerrainBrush.Apply(chunk, centre, 0.5f, new BrushSettings(3f), BrushOperation.Add);

            // Sample (10, 16, 10) is world (5, 8, 5): on the radius.
            Assert.That(chunk.GetDensity(new Vector3Int(10, 16, 10)), Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(chunk.GetDensity(new Vector3Int(10, 18, 10)), Is.EqualTo(0f));
        }

        private static Chunk SolidChunk(int size)
        {
            var chunk = new Chunk(new Vector3Int(size, size, size));
            for (int z = 0; z <= size; z++)
            {
                for (int y = 0; y <= size; y++)
                {
                    for (int x = 0; x <= size; x++)
                    {
                        chunk.SetDensity(new Vector3Int(x, y, z), 1f);
                    }
                }
            }
            return chunk;
        }
    }
}
