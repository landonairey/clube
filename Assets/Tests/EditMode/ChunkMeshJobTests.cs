using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// The Burst mesh job (<see cref="ChunkMeshJob"/>) against the managed path (<see cref="ChunkMesher"/>
    /// then the managed material pass), through <see cref="ChunkMeshBuilder"/>'s two backends:
    /// the same vertices, triangles, normals and material channels for every variant.
    /// </summary>
    public class ChunkMeshJobTests
    {
        // Burst may fuse multiply-adds, so positions can differ from the managed mesher by rounding.
        private const float Tolerance = 1e-5f;

        private ChunkMeshBuilder managed;
        private ChunkMeshBuilder burst;

        [SetUp]
        public void MakeBuilders()
        {
            managed = new ChunkMeshBuilder("Managed");
            burst = new ChunkMeshBuilder("Burst");
        }

        [TearDown]
        public void DestroyMeshes()
        {
            Object.DestroyImmediate(managed.Mesh);
            Object.DestroyImmediate(burst.Mesh);
        }

        [Test]
        public void RandomChunk_MatchesManagedPath(
            [Values(EdgePlacement.Interpolated, EdgePlacement.Midpoint)] EdgePlacement placement,
            [Values(Shading.Flat, Shading.Smooth)] Shading shading,
            [Values(MaterialDisplay.None, MaterialDisplay.HardSeams, MaterialDisplay.Blended)] MaterialDisplay display,
            [Values(VoxelStorageType.Flat, VoxelStorageType.FlatByte)] VoxelStorageType storage)
        {
            Chunk chunk = RandomChunk(new Vector3Int(6, 5, 7), storage, seed: 31);
            BuildBoth(chunk, new ChunkMeshSettings(0.5f, 1.5f, placement, shading, materialDisplay: display));
            AssertSameMesh(managed.Mesh, burst.Mesh, display != MaterialDisplay.None);
        }

        [Test]
        public void UniformMaterials_MatchManagedPath()
        {
            Chunk chunk = RandomChunk(new Vector3Int(5, 5, 5), VoxelStorageType.FlatByte, seed: 7, withMaterials: false);
            BuildBoth(chunk, new ChunkMeshSettings(0.5f, 1f, shading: Shading.Smooth, materialDisplay: MaterialDisplay.Blended));
            AssertSameMesh(managed.Mesh, burst.Mesh, true);
        }

        [Test]
        public void EmptyChunk_ProducesNoGeometry()
        {
            burst.Build(new Chunk(new Vector3Int(3, 3, 3)), new ChunkMeshSettings(0.5f, 1f, backend: MesherBackend.Burst));

            Assert.That(burst.Mesh.vertexCount, Is.Zero);
            Assert.That(burst.Mesh.GetIndexCount(0), Is.Zero);
        }

        [Test]
        public void Build_MarksTheChunkClean()
        {
            Chunk chunk = RandomChunk(new Vector3Int(3, 3, 3), VoxelStorageType.Flat, seed: 3);
            burst.Build(chunk, new ChunkMeshSettings(0.5f, 1f, backend: MesherBackend.Burst));

            Assert.That(chunk.IsDirty, Is.False);
        }

        private void BuildBoth(Chunk chunk, ChunkMeshSettings settings)
        {
            managed.Build(chunk, With(settings, MesherBackend.Managed));
            burst.Build(chunk, With(settings, MesherBackend.Burst));
        }

        private static ChunkMeshSettings With(ChunkMeshSettings settings, MesherBackend backend)
        {
            return new ChunkMeshSettings(settings.IsoLevel, settings.VoxelSize, settings.EdgePlacement, settings.Shading, backend, settings.MaterialDisplay);
        }

        private static void AssertSameMesh(Mesh expected, Mesh actual, bool withMaterials)
        {
            Assert.That(actual.vertexCount, Is.EqualTo(expected.vertexCount), "vertex count");
            Assert.That(actual.triangles, Is.EqualTo(expected.triangles), "indices");
            Vector3[] expectedVertices = expected.vertices;
            Vector3[] actualVertices = actual.vertices;
            Vector3[] expectedNormals = expected.normals;
            Vector3[] actualNormals = actual.normals;
            for (int i = 0; i < expectedVertices.Length; i++)
            {
                Assert.That((actualVertices[i] - expectedVertices[i]).sqrMagnitude, Is.LessThan(Tolerance), $"vertex {i}");
                Assert.That((actualNormals[i] - expectedNormals[i]).sqrMagnitude, Is.LessThan(1e-4f), $"normal {i}");
            }
            if (!withMaterials)
            {
                return;
            }

            var expectedIds = new System.Collections.Generic.List<Vector3>();
            var actualIds = new System.Collections.Generic.List<Vector3>();
            expected.GetUVs(2, expectedIds);
            actual.GetUVs(2, actualIds);
            Assert.That(actualIds, Is.EqualTo(expectedIds), "material ids (UV2)");
            expected.GetUVs(3, expectedIds);
            actual.GetUVs(3, actualIds);
            Assert.That(actualIds, Is.EqualTo(expectedIds), "material weights (UV3)");
        }

        private static Chunk RandomChunk(Vector3Int voxelCount, VoxelStorageType storage, int seed, bool withMaterials = true)
        {
            var random = new System.Random(seed);
            var chunk = new Chunk(VoxelStorages.Create(storage, voxelCount + Vector3Int.one));
            Vector3Int samples = chunk.SampleCount;
            for (int z = 0; z < samples.z; z++)
            {
                for (int y = 0; y < samples.y; y++)
                {
                    for (int x = 0; x < samples.x; x++)
                    {
                        double roll = random.NextDouble();
                        float density = roll < 0.2 ? 0f : roll > 0.8 ? 1f : (float)random.NextDouble();
                        var sample = new Vector3Int(x, y, z);
                        chunk.SetDensity(sample, density);
                        if (withMaterials)
                        {
                            chunk.SetMaterial(sample, (byte)random.Next(0, 4));
                        }
                    }
                }
            }
            return chunk;
        }
    }
}
