using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class BurstChunkMesherTests
    {
        // Burst may fuse multiply-adds, so positions can differ from the managed mesher by rounding.
        private const float Tolerance = 1e-5f;

        [Test]
        public void RandomChunk_MatchesManagedMesher(
            [Values(EdgePlacement.Interpolated, EdgePlacement.Midpoint)] EdgePlacement placement,
            [Values(Shading.Flat, Shading.Smooth)] Shading shading)
        {
            Chunk chunk = RandomChunk(new Vector3Int(6, 5, 7), seed: 31);
            var settings = new ChunkMeshSettings(0.5f, 1.5f, placement, shading);
            var expectedVertices = new List<Vector3>();
            var expectedTriangles = new List<int>();
            ChunkMesher.Build(chunk, settings, expectedVertices, expectedTriangles);

            var vertices = new NativeList<float3>(Allocator.TempJob);
            var triangles = new NativeList<int>(Allocator.TempJob);
            try
            {
                BurstChunkMesher.Build(chunk, settings, vertices, triangles);

                Assert.That(vertices.Length, Is.EqualTo(expectedVertices.Count), "vertex count");
                Assert.That(triangles.Length, Is.EqualTo(expectedTriangles.Count), "index count");
                for (int i = 0; i < triangles.Length; i++)
                {
                    Assert.That(triangles[i], Is.EqualTo(expectedTriangles[i]), $"index {i}");
                    Vector3 actual = vertices[triangles[i]];
                    Assert.That((actual - expectedVertices[expectedTriangles[i]]).sqrMagnitude, Is.LessThan(Tolerance), $"corner {i}");
                }
            }
            finally
            {
                vertices.Dispose();
                triangles.Dispose();
            }
        }

        [Test]
        public void EmptyChunk_ProducesNoGeometry()
        {
            var vertices = new NativeList<float3>(Allocator.TempJob);
            var triangles = new NativeList<int>(Allocator.TempJob);
            try
            {
                vertices.Add(new float3(1f, 2f, 3f));
                BurstChunkMesher.Build(new Chunk(new Vector3Int(3, 3, 3)), new ChunkMeshSettings(0.5f, 1f), vertices, triangles);

                Assert.That(vertices.Length, Is.Zero);
                Assert.That(triangles.Length, Is.Zero);
            }
            finally
            {
                vertices.Dispose();
                triangles.Dispose();
            }
        }

        private static Chunk RandomChunk(Vector3Int voxelCount, int seed)
        {
            var random = new System.Random(seed);
            var chunk = new Chunk(voxelCount);
            Vector3Int samples = chunk.SampleCount;
            for (int z = 0; z < samples.z; z++)
            {
                for (int y = 0; y < samples.y; y++)
                {
                    for (int x = 0; x < samples.x; x++)
                    {
                        double roll = random.NextDouble();
                        float density = roll < 0.2 ? 0f : roll > 0.8 ? 1f : (float)random.NextDouble();
                        chunk.SetDensity(new Vector3Int(x, y, z), density);
                    }
                }
            }
            return chunk;
        }
    }
}
