using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class MeshSealTests
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();

        private static Chunk Solid(int side)
        {
            var chunk = new Chunk(new Vector3Int(side, side, side));
            for (int z = 0; z <= side; z++)
            {
                for (int y = 0; y <= side; y++)
                {
                    for (int x = 0; x <= side; x++)
                    {
                        chunk.SetDensity(new Vector3Int(x, y, z), 1f);
                    }
                }
            }
            return chunk;
        }

        [Test]
        public void SealedSolidChunk_MeshesAsAClosedBlock_WithoutChangingItsData()
        {
            Chunk chunk = Solid(4);
            ChunkMesher.Build(chunk, new ChunkMeshSettings(0.5f, 1f), vertices, triangles);
            Assert.AreEqual(0, triangles.Count, "Unsealed, solid ground has no surface.");

            ChunkMesher.Build(chunk, new ChunkMeshSettings(0.5f, 1f, seal: MeshSeal.All), vertices, triangles);

            Assert.That(triangles.Count, Is.GreaterThan(0));
            var bounds = new Bounds(vertices[0], Vector3.zero);
            foreach (Vector3 vertex in vertices)
            {
                bounds.Encapsulate(vertex);
            }
            // Walls half a voxel in from every face: the surface between the air curtain and the solid inside.
            Assert.That(bounds.min.x, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(bounds.max.y, Is.EqualTo(3.5f).Within(1e-4f));
            Assert.That(bounds.min.z, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.AreEqual(1f, chunk.GetDensity(Vector3Int.zero), "The chunk's own samples stay solid.");
        }

        [Test]
        public void OneSealedFace_OnlyClosesThatSide()
        {
            Chunk chunk = Solid(4);
            ChunkMesher.Build(chunk, new ChunkMeshSettings(0.5f, 1f, seal: MeshSeal.MaxX), vertices, triangles);

            Assert.That(triangles.Count, Is.GreaterThan(0));
            foreach (Vector3 vertex in vertices)
            {
                Assert.That(vertex.x, Is.EqualTo(3.5f).Within(1e-4f), "Only a wall on the +X side.");
            }
        }

        [Test]
        public void FixedArea_SealsOnlyItsOutsideFacesAndTheBottom()
        {
            var columns = new RectInt(-2, -2, 4, 4);

            Assert.AreEqual(MeshSeal.MinX | MeshSeal.MinZ | MeshSeal.MinY, StreamingArea.FixedSeal(columns, new Vector3Int(-2, 0, -2)));
            Assert.AreEqual(MeshSeal.MaxX, StreamingArea.FixedSeal(columns, new Vector3Int(1, 3, 0)));
            Assert.AreEqual(MeshSeal.MaxZ, StreamingArea.FixedSeal(columns, new Vector3Int(0, 1, 1)));
            Assert.AreEqual(MeshSeal.None, StreamingArea.FixedSeal(columns, new Vector3Int(0, 2, -1)), "An inside chunk keeps open faces.");
        }

        [Test]
        public void FixedArea_WantsEveryChunkOfTheSquare_NearestFirst()
        {
            var coords = new List<Vector3Int>();
            StreamingArea.CollectFixed(new RectInt(-2, -2, 4, 4), new Vector3Int(1, 0, 1), 3, coords);

            Assert.AreEqual(4 * 4 * 3, coords.Count);
            Assert.AreEqual(new Vector3Int(1, 0, 1), coords[0], "The focus's own column first, bottom layer first.");
            Assert.AreEqual(new Vector3Int(-2, 2, -2), coords[coords.Count - 1], "The far corner last.");
        }
    }
}
