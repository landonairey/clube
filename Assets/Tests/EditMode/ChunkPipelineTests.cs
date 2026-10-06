using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// The world's job runner (<see cref="ChunkPipeline"/>, K35, P3): it generates the same chunks
    /// as loading them one by one, drops what was cancelled, builds the same meshes as the
    /// one-chunk path, and can be disposed with jobs still running.
    /// </summary>
    public class ChunkPipelineTests
    {
        private const int Size = 8;

        private static readonly Vector3Int ChunkSize = new Vector3Int(Size, Size, Size);

        private static ITerrainGenerator Terrain()
        {
            return new FractalPerlin2DGenerator(3, 9f, 4f, 0.08f, octaves: 3);
        }

        private static World NewWorld()
        {
            return new World(ChunkSize, 1f, samples => new ByteVoxelStorage(samples));
        }

        private static ChunkPipeline NewPipeline(World world, ITerrainGenerator generator)
        {
            return new ChunkPipeline(world.Grid, generator, null, null, world.PreferredFormat, world.StorageFactory);
        }

        [Test]
        public void GeneratesTheSameChunks_AsLoadingThemOneByOne()
        {
            ITerrainGenerator generator = Terrain();
            World expected = NewWorld();
            World actual = NewWorld();
            var coords = new List<Vector3Int>();
            StreamingArea.Collect(Vector3Int.zero, 2, 3, coords);

            using (ChunkPipeline pipeline = NewPipeline(actual, generator))
            {
                foreach (Vector3Int coord in coords)
                {
                    expected.Load(coord, generator);
                    pipeline.StartGeneration(coord);
                }
                TakeAll(pipeline, actual, coords.Count);
            }

            int uniform = 0;
            foreach (Vector3Int coord in coords)
            {
                Assert.That(actual.TryGetChunk(coord, out Chunk chunk), Is.True, coord.ToString());
                Chunk reference = expected.Chunks[coord];
                Assert.That(chunk.IsUniform, Is.EqualTo(reference.IsUniform), coord.ToString());
                uniform += chunk.IsUniform ? 1 : 0;
                for (int z = 0; z <= Size; z += 2)
                {
                    for (int y = 0; y <= Size; y++)
                    {
                        for (int x = 0; x <= Size; x += 2)
                        {
                            var sample = new Vector3Int(x, y, z);
                            Assert.That(chunk.GetDensity(sample), Is.EqualTo(reference.GetDensity(sample)), $"{coord} {sample}");
                        }
                    }
                }
            }
            Assert.That(uniform, Is.GreaterThan(0), "the area should hold some all-air or all-solid chunks");
            Assert.That(uniform, Is.LessThan(coords.Count), "and some surface");
        }

        [Test]
        public void CancelledGeneration_IsNeverTaken()
        {
            World world = NewWorld();
            using (ChunkPipeline pipeline = NewPipeline(world, Terrain()))
            {
                pipeline.StartGeneration(Vector3Int.zero);
                pipeline.StartGeneration(Vector3Int.right);
                pipeline.CancelGeneration(Vector3Int.zero);
                Assert.That(pipeline.IsGenerating(Vector3Int.zero), Is.False, "cancelled: its job may still run, but nothing waits for it");
                Assert.That(pipeline.GeneratingCount, Is.EqualTo(2), "it still counts against the job limit until it finishes");

                TakeAll(pipeline, world, 1);
                Assert.That(world.IsLoaded(Vector3Int.right), Is.True);
                Assert.That(world.IsLoaded(Vector3Int.zero), Is.False);
                Assert.That(pipeline.IsGenerating(Vector3Int.zero), Is.False);
            }
        }

        [Test]
        public void CancelledThenWantedAgain_IsGeneratedAfterAll()
        {
            World world = NewWorld();
            using (ChunkPipeline pipeline = NewPipeline(world, Terrain()))
            {
                pipeline.StartGeneration(Vector3Int.zero);
                pipeline.CancelGeneration(Vector3Int.zero);
                Assert.That(pipeline.IsGenerating(Vector3Int.zero), Is.False, "cancelled");

                // The focus came back before the job finished.
                pipeline.StartGeneration(Vector3Int.zero);
                Assert.That(pipeline.IsGenerating(Vector3Int.zero), Is.True);
                TakeAll(pipeline, world, 1);
                Assert.That(world.IsLoaded(Vector3Int.zero), Is.True);
            }
        }

        [Test]
        public void CancelledMesh_DoesNotBlockTheNextOne()
        {
            World world = NewWorld();
            Chunk chunk = world.Load(Vector3Int.zero, new FlatGenerator(4.5f));
            var settings = new ChunkMeshSettings(0.5f, 1f);
            using (ChunkPipeline pipeline = NewPipeline(world, null))
            {
                // Streamed out (mesh cancelled) and straight back in, before and after the old job finishes.
                pipeline.StartMesh(Vector3Int.zero, chunk, settings);
                pipeline.CancelMesh(Vector3Int.zero);
                Assert.That(pipeline.IsMeshing(Vector3Int.zero), Is.False);
                Assert.DoesNotThrow(() => pipeline.StartMesh(Vector3Int.zero, chunk, settings));
                pipeline.CancelMesh(Vector3Int.zero);

                for (int attempt = 0; attempt < 1000 && pipeline.MeshingCount > 0; attempt++)
                {
                    pipeline.Poll();
                    Assert.That(pipeline.TryTakeMeshed(out _), Is.False, "a cancelled mesh is never handed out");
                    System.Threading.Thread.Sleep(1);
                }
                Assert.That(pipeline.IsMeshing(Vector3Int.zero), Is.False);

                pipeline.StartMesh(Vector3Int.zero, chunk, settings);
                Assert.That(pipeline.TryTakeMeshNow(Vector3Int.zero, out ChunkPipeline.MeshResult result), Is.True);
                result.Discard();
            }
        }

        [Test]
        public void ChunkAboveTheColumn_IsAirWithoutAJob()
        {
            World world = NewWorld();
            using (ChunkPipeline pipeline = NewPipeline(world, new FlatGenerator(4f)))
            {
                // The first chunk of the column computes its heights; once they're known, the
                // chunk high above needs no job.
                pipeline.StartGeneration(Vector3Int.zero);
                TakeAll(pipeline, world, 1);
                pipeline.StartGeneration(new Vector3Int(0, 5, 0));

                Assert.That(pipeline.TryTakeGenerated(out Vector3Int coord, out Chunk chunk), Is.True, "ready at once");
                Assert.That(coord, Is.EqualTo(new Vector3Int(0, 5, 0)));
                Assert.That(chunk.IsUniform && chunk.UniformDensity == 0f, Is.True);
            }
        }

        [Test]
        public void MeshJob_GivesTheSameMeshAsTheOneChunkPath()
        {
            World world = NewWorld();
            Chunk chunk = world.Load(Vector3Int.zero, new SineGenerator(4f, 2f, 0.1f));
            var settings = new ChunkMeshSettings(0.5f, 1f, shading: Shading.Smooth);
            var expected = new Mesh();
            var actual = new Mesh();
            try
            {
                ChunkMeshJobs.BuildNow(chunk, settings, expected);
                chunk.MarkDirty();
                using (ChunkPipeline pipeline = NewPipeline(world, null))
                {
                    pipeline.StartMesh(Vector3Int.zero, chunk, settings);
                    Assert.That(chunk.IsDirty, Is.False, "marked clean when the job starts");
                    Assert.That(pipeline.IsMeshing(Vector3Int.zero), Is.True);
                    Assert.That(pipeline.TryTakeMeshNow(Vector3Int.zero, out ChunkPipeline.MeshResult result), Is.True);
                    Assert.That(result.VertexCount, Is.EqualTo(expected.vertexCount));
                    result.ApplyTo(actual);
                    Assert.That(pipeline.IsMeshing(Vector3Int.zero), Is.False);
                }

                Assert.That(actual.vertices, Is.EqualTo(expected.vertices));
                Assert.That(actual.triangles, Is.EqualTo(expected.triangles));
            }
            finally
            {
                Object.DestroyImmediate(expected);
                Object.DestroyImmediate(actual);
            }
        }

        [Test]
        public void EditDuringAMeshJob_LeavesTheChunkDirty()
        {
            World world = NewWorld();
            Chunk chunk = world.Load(Vector3Int.zero, new FlatGenerator(4.5f));
            using (ChunkPipeline pipeline = NewPipeline(world, null))
            {
                pipeline.StartMesh(Vector3Int.zero, chunk, new ChunkMeshSettings(0.5f, 1f));
                world.SetDensity(new Vector3Int(2, 2, 2), 0f);

                Assert.That(chunk.IsDirty, Is.True, "the job shows the chunk before the edit, so it must be rebuilt");
                Assert.That(pipeline.TryTakeMeshNow(Vector3Int.zero, out ChunkPipeline.MeshResult result), Is.True);
                result.Discard();
            }
        }

        [Test]
        public void Dispose_WithJobsStillRunning_FreesEverything()
        {
            World world = NewWorld();
            Chunk chunk = world.Load(Vector3Int.zero, new FlatGenerator(4.5f));
            var pipeline = NewPipeline(world, Terrain());
            for (int x = 1; x < 20; x++)
            {
                pipeline.StartGeneration(new Vector3Int(x, 0, 0));
            }
            pipeline.StartMesh(Vector3Int.zero, chunk, new ChunkMeshSettings(0.5f, 1f));

            Assert.DoesNotThrow(() => pipeline.Dispose());
            Assert.That(pipeline.GeneratingCount + pipeline.MeshingCount + pipeline.BakingCount, Is.Zero);
        }

        // Polls until the given number of chunks were generated and added.
        private static void TakeAll(ChunkPipeline pipeline, World world, int count)
        {
            pipeline.Flush();
            for (int attempt = 0; attempt < 10000 && world.LoadedCount < count; attempt++)
            {
                pipeline.Poll();
                while (pipeline.TryTakeGenerated(out Vector3Int coord, out Chunk chunk))
                {
                    world.Add(coord, chunk);
                }
                System.Threading.Thread.Sleep(1);
            }
            Assert.That(world.LoadedCount, Is.EqualTo(count), "generation finished");
        }
    }
}
