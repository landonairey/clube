using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Core.Tests
{
    /// <summary>
    /// The plan-of-record chunk (16³, M17) must mesh with 16-bit indices whatever its terrain,
    /// since not every GPU takes 32-bit index buffers.
    /// </summary>
    public class ChunkIndexBudgetTests
    {
        private const int Side = 16;

        [Test]
        public void Budget_16CubedFits_17CubedDoesNot()
        {
            Assert.That(ChunkIndexBudget.MaxVertices(new Vector3Int(Side, Side, Side)), Is.EqualTo(61440));
            Assert.That(ChunkIndexBudget.Fits16Bit(new Vector3Int(Side, Side, Side)), Is.True);
            Assert.That(ChunkIndexBudget.Fits16Bit(new Vector3Int(17, 17, 17)), Is.False);
            Assert.That(ChunkIndexBudget.Fits16Bit(new Vector3Int(16, 48, 16)), Is.False);
        }

        [Test]
        public void Checkerboard16Cubed_MixedMaterials_Uses16BitIndices(
            [Values(Shading.Flat, Shading.Smooth)] Shading shading,
            [Values(MaterialDisplay.None, MaterialDisplay.HardSeams, MaterialDisplay.Blended)] MaterialDisplay display,
            [Values(MesherBackend.Managed, MesherBackend.Burst)] MesherBackend backend)
        {
            // Every edge crosses the surface and neighbouring samples differ in material: as many
            // triangles as marching cubes makes over a whole chunk, split by every material seam.
            var chunk = new Chunk(new Vector3Int(Side, Side, Side));
            var random = new System.Random(5);
            for (int z = 0; z <= Side; z++)
            {
                for (int y = 0; y <= Side; y++)
                {
                    for (int x = 0; x <= Side; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        chunk.SetDensity(sample, ((x + y + z) & 1) == 0 ? 1f : 0f);
                        chunk.SetMaterial(sample, (byte)random.Next(0, 6));
                    }
                }
            }

            var builder = new ChunkMeshBuilder("Checkerboard");
            try
            {
                builder.Build(chunk, new ChunkMeshSettings(0.5f, 1f, shading: shading, materialDisplay: display, backend: backend));

                Assert.That(builder.Mesh.GetIndexCount(0), Is.EqualTo(Side * Side * Side * 4 * 3), "4 triangles per voxel");
                Assert.That(builder.Mesh.vertexCount, Is.LessThanOrEqualTo(ChunkIndexBudget.MaxVertices(chunk.VoxelCount)));
                Assert.That(builder.Mesh.indexFormat, Is.EqualTo(IndexFormat.UInt16));
            }
            finally
            {
                Object.DestroyImmediate(builder.Mesh);
            }
        }
    }
}
