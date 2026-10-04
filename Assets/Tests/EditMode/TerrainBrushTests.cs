using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class TerrainBrushTests
    {
        private const float Tolerance = 1e-3f;

        [Test]
        public void HardAdd_FillsEverySampleWithinTheRadius()
        {
            var chunk = new Chunk(new Vector3Int(10, 10, 10));
            var centre = new Vector3(5f, 5f, 5f);

            TerrainBrush.Apply(chunk, centre, 1f, new BrushSettings(3f), BrushOperation.Add);

            Assert.That(chunk.GetDensity(new Vector3Int(5, 5, 5)), Is.EqualTo(1f));
            Assert.That(chunk.GetDensity(new Vector3Int(5, 8, 5)), Is.EqualTo(1f), "On the radius");
            Assert.That(chunk.GetDensity(new Vector3Int(7, 7, 5)), Is.EqualTo(1f), "Inside, off-axis");
            Assert.That(chunk.GetDensity(new Vector3Int(5, 9, 5)), Is.EqualTo(0f), "Outside");
            Assert.That(chunk.GetDensity(new Vector3Int(7, 7, 7)), Is.EqualTo(0f), "Outside, off-axis");
        }

        [Test]
        public void HardRemove_EmptiesEverySampleWithinTheRadius()
        {
            var chunk = SolidBelow(10, 10);

            TerrainBrush.Apply(chunk, new Vector3(5f, 5f, 5f), 1f, new BrushSettings(3f), BrushOperation.Remove);

            Assert.That(chunk.GetDensity(new Vector3Int(5, 5, 5)), Is.EqualTo(0f));
            Assert.That(chunk.GetDensity(new Vector3Int(5, 2, 5)), Is.EqualTo(0f), "On the radius");
            Assert.That(chunk.GetDensity(new Vector3Int(5, 1, 5)), Is.EqualTo(1f), "Outside");
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
        public void SoftAdd_PilesUpOneLayerPerApplication()
        {
            // Solid ground up to y = 2, air above.
            var chunk = SolidBelow(10, 2);
            var brush = new BrushSettings(3f, falloff: BrushFalloff.Soft);
            var centre = new Vector3(5f, 3f, 5f);

            TerrainBrush.Apply(chunk, centre, 1f, brush, BrushOperation.Add);
            Assert.That(chunk.GetDensity(new Vector3Int(5, 3, 5)), Is.EqualTo(1f), "The layer on the ground fills");
            Assert.That(chunk.GetDensity(new Vector3Int(5, 4, 5)), Is.EqualTo(0f), "The next layer waits");

            TerrainBrush.Apply(chunk, centre, 1f, brush, BrushOperation.Add);
            Assert.That(chunk.GetDensity(new Vector3Int(5, 4, 5)), Is.EqualTo(1f), "Then the next one up");
            Assert.That(chunk.GetDensity(new Vector3Int(5, 5, 5)), Is.EqualTo(0f));
        }

        [Test]
        public void SoftAdd_InOpenAir_DoesNothing()
        {
            var chunk = new Chunk(new Vector3Int(8, 8, 8));

            BrushResult result = TerrainBrush.Apply(
                chunk, new Vector3(4f, 4f, 4f), 1f, new BrushSettings(3f, falloff: BrushFalloff.Soft), BrushOperation.Add);

            Assert.That(result.ChangedSamples, Is.EqualTo(0));
        }

        [Test]
        public void SoftRemove_DigsTheSurfaceLayerFirst()
        {
            // Solid up to y = 4, air above.
            var chunk = SolidBelow(10, 4);
            var brush = new BrushSettings(3f, falloff: BrushFalloff.Soft);
            var centre = new Vector3(5f, 4f, 5f);

            TerrainBrush.Apply(chunk, centre, 1f, brush, BrushOperation.Remove);
            Assert.That(chunk.GetDensity(new Vector3Int(5, 4, 5)), Is.EqualTo(0f), "The surface layer goes first");
            Assert.That(chunk.GetDensity(new Vector3Int(5, 3, 5)), Is.EqualTo(1f), "The layer below waits");

            TerrainBrush.Apply(chunk, centre, 1f, brush, BrushOperation.Remove);
            Assert.That(chunk.GetDensity(new Vector3Int(5, 3, 5)), Is.EqualTo(0f), "Then the layer below");
        }

        [Test]
        public void SoftRemove_FinishesALayerBeforeStartingTheNext()
        {
            var chunk = SolidBelow(10, 4);
            var brush = new BrushSettings(3f, strength: 0.5f, falloff: BrushFalloff.Soft);
            var centre = new Vector3(5f, 4f, 5f);

            TerrainBrush.Apply(chunk, centre, 1f, brush, BrushOperation.Remove);
            Assert.That(chunk.GetDensity(new Vector3Int(5, 4, 5)), Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(chunk.GetDensity(new Vector3Int(5, 3, 5)), Is.EqualTo(1f), "Half-dug neighbours don't expose it yet");

            TerrainBrush.Apply(chunk, centre, 1f, brush, BrushOperation.Remove);
            TerrainBrush.Apply(chunk, centre, 1f, brush, BrushOperation.Remove);
            Assert.That(chunk.GetDensity(new Vector3Int(5, 4, 5)), Is.EqualTo(0f));
            Assert.That(chunk.GetDensity(new Vector3Int(5, 3, 5)), Is.EqualTo(0.5f).Within(Tolerance));
        }

        [Test]
        public void Result_ReportsTheDensityAddedAndRemoved()
        {
            var chunk = new Chunk(new Vector3Int(6, 6, 6));
            var centre = new Vector3(3f, 3f, 3f);

            // Radius 1 reaches the centre sample and its six neighbours.
            BrushResult added = TerrainBrush.Apply(chunk, centre, 1f, new BrushSettings(1f), BrushOperation.Add);
            Assert.That(added.ChangedSamples, Is.EqualTo(7));
            Assert.That(added.DensityAdded, Is.EqualTo(7f).Within(Tolerance));
            Assert.That(added.DensityRemoved, Is.EqualTo(0f));
            Assert.That(added.VolumeAdded(0.5f), Is.EqualTo(7f * 0.125f).Within(Tolerance));

            BrushResult removed = TerrainBrush.Apply(chunk, centre, 1f, new BrushSettings(1f, 0.25f), BrushOperation.Remove);
            Assert.That(removed.DensityRemoved, Is.EqualTo(7f * 0.25f).Within(Tolerance));
        }

        [Test]
        public void BrushPartlyOutsideTheChunk_EditsOnlyTheInsideSamples()
        {
            var chunk = new Chunk(new Vector3Int(4, 4, 4));

            BrushResult result = TerrainBrush.Apply(chunk, new Vector3(-1f, 0f, 0f), 1f, new BrushSettings(2f), BrushOperation.Add);

            Assert.That(result.ChangedSamples, Is.GreaterThan(0));
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

            BrushResult result = TerrainBrush.Apply(chunk, new Vector3(2f, 2f, 2f), 1f, new BrushSettings(2f), BrushOperation.Remove);

            Assert.That(result.ChangedSamples, Is.EqualTo(0));
            Assert.That(chunk.IsDirty, Is.False);
        }

        [Test]
        public void SmallerVoxels_KeepTheSphereTheSameWorldSize()
        {
            var chunk = new Chunk(new Vector3Int(20, 20, 20));

            TerrainBrush.Apply(chunk, new Vector3(5f, 5f, 5f), 0.5f, new BrushSettings(3f), BrushOperation.Add);

            // Sample (10, 16, 10) is world (5, 8, 5): on the radius. (10, 18, 10) is outside.
            Assert.That(chunk.GetDensity(new Vector3Int(10, 16, 10)), Is.EqualTo(1f));
            Assert.That(chunk.GetDensity(new Vector3Int(10, 18, 10)), Is.EqualTo(0f));
        }

        /// <summary>A chunk whose samples are solid (1) up to and including <paramref name="topY"/>, air above.</summary>
        private static Chunk SolidBelow(int size, int topY)
        {
            var chunk = new Chunk(new Vector3Int(size, size, size));
            for (int z = 0; z <= size; z++)
            {
                for (int y = 0; y <= Mathf.Min(topY, size); y++)
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
