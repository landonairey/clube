using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class ChunkMesherTests
    {
        private const float Tolerance = 1e-5f;

        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();

        // NUnit reuses one fixture instance for every test in the class.
        [SetUp]
        public void ClearBuffers()
        {
            vertices.Clear();
            triangles.Clear();
        }

        [Test]
        public void SingleVoxelChunk_MatchesDirectPolygonise()
        {
            float[] corners = { 1f, 0.2f, 0.9f, 0f, 0.6f, 0f, 0.3f, 1f };
            var chunk = new Chunk(Vector3Int.one);
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                chunk.SetDensity(MarchingCubes.CornerOffset(corner), corners[corner]);
            }

            var expectedVertices = new List<Vector3>();
            var expectedTriangles = new List<int>();
            MarchingCubes.Polygonise(corners, 0.5f, Vector3.zero, 1f, expectedVertices, expectedTriangles);

            ChunkMesher.Build(chunk, new ChunkMeshSettings(0.5f, 1f), vertices, triangles);

            Assert.That(vertices, Is.EqualTo(expectedVertices));
            Assert.That(triangles, Is.EqualTo(expectedTriangles));
        }

        [Test]
        public void EmptyChunk_ProducesNoGeometry()
        {
            ChunkMesher.Build(new Chunk(new Vector3Int(3, 3, 3)), new ChunkMeshSettings(0.5f, 1f), vertices, triangles);

            Assert.That(vertices, Is.Empty);
            Assert.That(triangles, Is.Empty);
        }

        [Test]
        public void Build_ReplacesPreviousContents()
        {
            vertices.Add(Vector3.one);
            triangles.Add(0);

            ChunkMesher.Build(new Chunk(Vector3Int.one), new ChunkMeshSettings(0.5f, 1f), vertices, triangles);

            Assert.That(vertices, Is.Empty);
            Assert.That(triangles, Is.Empty);
        }

        [Test]
        public void SolidBottomLayer_ProducesFlatFloorAcrossVoxels()
        {
            // 2x2x2 voxels, solid samples on y = 0 only. The four bottom voxels each
            // emit a two-triangle quad at the iso crossing; the top voxels are empty.
            var chunk = new Chunk(new Vector3Int(2, 2, 2));
            for (int z = 0; z < 3; z++)
            {
                for (int x = 0; x < 3; x++)
                {
                    chunk.SetDensity(new Vector3Int(x, 0, z), 1f);
                }
            }

            const float voxelSize = 2f;
            ChunkMesher.Build(chunk, new ChunkMeshSettings(0.5f, voxelSize), vertices, triangles);

            Assert.That(triangles.Count / 3, Is.EqualTo(8));
            foreach (Vector3 v in vertices)
            {
                Assert.That(v.y, Is.EqualTo(0.5f * voxelSize).Within(Tolerance));
                Assert.That(v.x, Is.InRange(0f, 2 * voxelSize));
                Assert.That(v.z, Is.InRange(0f, 2 * voxelSize));
            }
        }

        [Test]
        public void MidpointPlacement_IgnoresDensities()
        {
            var chunk = new Chunk(Vector3Int.one);
            chunk.SetDensity(Vector3Int.zero, 1f);

            // With interpolation, iso 0.25 would put the vertices 0.75 along each edge.
            ChunkMesher.Build(chunk, new ChunkMeshSettings(0.25f, 1f, EdgePlacement.Midpoint), vertices, triangles);

            foreach (Vector3 v in vertices)
            {
                Assert.That(v.x + v.y + v.z, Is.EqualTo(0.5f).Within(Tolerance));
            }
        }

        [Test]
        public void FlatShading_NeverSharesVertices()
        {
            ChunkMesher.Build(FloorChunk(), new ChunkMeshSettings(0.5f, 1f, shading: Shading.Flat), vertices, triangles);

            Assert.That(vertices.Count, Is.EqualTo(triangles.Count));
        }

        [Test]
        public void SmoothShading_SharesOneVertexPerCrossedEdge()
        {
            // A 2x1x1 floor crosses the 6 vertical edges (3 x 2 samples) once each.
            ChunkMesher.Build(FloorChunk(), new ChunkMeshSettings(0.5f, 1f, shading: Shading.Smooth), vertices, triangles);

            Assert.That(vertices.Count, Is.EqualTo(6));
            Assert.That(triangles.Count, Is.EqualTo(12));
        }

        [Test]
        public void SmoothShading_ProducesSameTrianglesAsFlat()
        {
            var flatVertices = new List<Vector3>();
            var flatTriangles = new List<int>();
            ChunkMesher.Build(FloorChunk(), new ChunkMeshSettings(0.5f, 1f, shading: Shading.Flat), flatVertices, flatTriangles);

            ChunkMesher.Build(FloorChunk(), new ChunkMeshSettings(0.5f, 1f, shading: Shading.Smooth), vertices, triangles);

            Assert.That(triangles.Count, Is.EqualTo(flatTriangles.Count));
            for (int i = 0; i < triangles.Count; i++)
            {
                Vector3 smooth = vertices[triangles[i]];
                Vector3 flat = flatVertices[flatTriangles[i]];
                Assert.That((smooth - flat).sqrMagnitude, Is.LessThan(Tolerance), $"Triangle corner {i} moved.");
            }
        }

        // Guards the K32 speed-ups: the chunk loop must give exactly what running
        // Polygonise voxel by voxel gives, in the same order, for every variant.
        // Random densities reach every kind of case; the odd sizes catch mixed-up axes.
        [Test]
        public void RandomChunk_MatchesPolygonisePerVoxel(
            [Values(EdgePlacement.Interpolated, EdgePlacement.Midpoint)] EdgePlacement placement,
            [Values(Shading.Flat, Shading.Smooth)] Shading shading)
        {
            Chunk chunk = RandomChunk(new Vector3Int(6, 5, 7), seed: 12);
            var settings = new ChunkMeshSettings(0.5f, 1.5f, placement, shading);
            List<Vector3> expected = PolygonisePerVoxel(chunk, settings);

            ChunkMesher.Build(chunk, settings, vertices, triangles);

            Assert.That(triangles.Count, Is.EqualTo(expected.Count));
            for (int i = 0; i < triangles.Count; i++)
            {
                // A shared edge vertex is placed from whichever voxel reached it first, so
                // smooth shading may differ from the per-voxel position by rounding.
                Vector3 actual = vertices[triangles[i]];
                Assert.That((actual - expected[i]).sqrMagnitude, Is.LessThan(Tolerance), $"Triangle corner {i} moved.");
            }
        }

        [Test]
        public void SolidChunk_ProducesNoGeometry()
        {
            var chunk = new Chunk(new Vector3Int(3, 3, 3));
            for (int z = 0; z < 4; z++)
            {
                for (int y = 0; y < 4; y++)
                {
                    for (int x = 0; x < 4; x++)
                    {
                        chunk.SetDensity(new Vector3Int(x, y, z), 1f);
                    }
                }
            }

            ChunkMesher.Build(chunk, new ChunkMeshSettings(0.5f, 1f), vertices, triangles);

            Assert.That(vertices, Is.Empty);
            Assert.That(triangles, Is.Empty);
        }

        /// <summary>Every triangle corner, built one voxel at a time in z, y, x order with flat shading.</summary>
        private static List<Vector3> PolygonisePerVoxel(Chunk chunk, ChunkMeshSettings settings)
        {
            var flatVertices = new List<Vector3>();
            var flatTriangles = new List<int>();
            var writer = new FlatVertexWriter(flatVertices);
            IEdgeVertexPlacer placer = EdgeVertexPlacers.For(settings.EdgePlacement);
            var corners = new float[MarchingCubes.CornerCount];
            Vector3Int count = chunk.VoxelCount;
            for (int z = 0; z < count.z; z++)
            {
                for (int y = 0; y < count.y; y++)
                {
                    for (int x = 0; x < count.x; x++)
                    {
                        var voxel = new Vector3Int(x, y, z);
                        for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
                        {
                            corners[corner] = chunk.GetDensity(voxel + MarchingCubes.CornerOffset(corner));
                        }

                        MarchingCubes.Polygonise(
                            corners, settings.IsoLevel, (Vector3)voxel * settings.VoxelSize, settings.VoxelSize,
                            placer, writer, flatTriangles);
                    }
                }
            }

            var positions = new List<Vector3>(flatTriangles.Count);
            foreach (int index in flatTriangles)
            {
                positions.Add(flatVertices[index]);
            }
            return positions;
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
                        // Some exact 0s and 1s too, like generated terrain away from the surface.
                        double roll = random.NextDouble();
                        float density = roll < 0.2 ? 0f : roll > 0.8 ? 1f : (float)random.NextDouble();
                        chunk.SetDensity(new Vector3Int(x, y, z), density);
                    }
                }
            }
            return chunk;
        }

        /// <summary>2x1x1 voxels with the bottom samples solid: a flat floor at mid-height.</summary>
        private static Chunk FloorChunk()
        {
            var chunk = new Chunk(new Vector3Int(2, 1, 1));
            for (int z = 0; z < 2; z++)
            {
                for (int x = 0; x < 3; x++)
                {
                    chunk.SetDensity(new Vector3Int(x, 0, z), 1f);
                }
            }

            return chunk;
        }
    }
}
