using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class ChunkVolumeTests
    {
        private const float Tolerance = 1e-4f;
        private const float Iso = 0.5f;

        [Test]
        public void EmptyChunk_HasNoVolume()
        {
            var chunk = new Chunk(new Vector3Int(3, 2, 4));

            Assert.That(ChunkVolume.Approximate(chunk, 1f), Is.EqualTo(0f));
            Assert.That(ChunkVolume.Exact(chunk, Iso, InterpolatedEdgePlacer.Instance, 1f), Is.EqualTo(0f));
        }

        [Test]
        public void SolidChunk_FillsItsWholeVolume()
        {
            var chunk = Filled(new Vector3Int(3, 2, 4), 1f);
            const float voxelSize = 0.5f;
            float expected = 3 * 2 * 4 * voxelSize * voxelSize * voxelSize;

            Assert.That(ChunkVolume.Approximate(chunk, voxelSize), Is.EqualTo(expected).Within(Tolerance));
            Assert.That(ChunkVolume.Exact(chunk, Iso, InterpolatedEdgePlacer.Instance, voxelSize), Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void SingleVoxelChunk_MatchesVoxelVolume()
        {
            float[] corners = { 1f, 0.2f, 0.9f, 0f, 0.6f, 0f, 0.3f, 1f };
            var chunk = new Chunk(Vector3Int.one);
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                chunk.SetDensity(MarchingCubes.CornerOffset(corner), corners[corner]);
            }

            Assert.That(ChunkVolume.Approximate(chunk, 1f), Is.EqualTo(VoxelVolume.Approximate(corners)).Within(Tolerance));
            Assert.That(
                ChunkVolume.Exact(chunk, Iso, InterpolatedEdgePlacer.Instance, 1f),
                Is.EqualTo(VoxelVolume.Exact(corners, Iso, InterpolatedEdgePlacer.Instance)).Within(Tolerance));
        }

        [Test]
        public void HalfFilledChunk_ExactVolumeIsHalf()
        {
            // Bottom two sample layers solid, top layer empty: the surface sits halfway
            // up the top row of voxels, so the solid is 1.5 of 2 voxel layers.
            var chunk = new Chunk(new Vector3Int(2, 2, 2));
            for (int z = 0; z < 3; z++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int x = 0; x < 3; x++)
                    {
                        chunk.SetDensity(new Vector3Int(x, y, z), 1f);
                    }
                }
            }

            Assert.That(ChunkVolume.Exact(chunk, Iso, InterpolatedEdgePlacer.Instance, 1f), Is.EqualTo(2f * 2f * 1.5f).Within(Tolerance));
        }

        private static Chunk Filled(Vector3Int voxelCount, float density)
        {
            var chunk = new Chunk(voxelCount);
            Vector3Int samples = chunk.SampleCount;
            for (int z = 0; z < samples.z; z++)
            {
                for (int y = 0; y < samples.y; y++)
                {
                    for (int x = 0; x < samples.x; x++)
                    {
                        chunk.SetDensity(new Vector3Int(x, y, z), density);
                    }
                }
            }
            return chunk;
        }
    }
}
