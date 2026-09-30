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
