using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class SquareVoxelTests
    {
        private const float Iso = 0.5f;

        // GL86: square samples make a cube where all eight corners of a voxel are square, filling that
        // voxel on the marching grid (6 faces, 4 vertices and 2 triangles each); a lone square sample is
        // no cube, and marching cubes meshes it as air: chiselling digs in.
        [Test]
        public void SquareSamples_MakeACubeFillingTheirVoxel_AndALoneOneDigsOut()
        {
            var chunk = new Chunk(new Vector3Int(4, 4, 4));
            var lone = new Vector3Int(1, 1, 1);
            chunk.SetDensity(lone, 1f);
            var mesh = new UnityEngine.Mesh();
            try
            {
                ChunkMeshJobs.BuildNow(chunk, new ChunkMeshSettings(Iso, 1f, backend: MesherBackend.Burst), mesh);
                Assert.Greater(mesh.vertexCount, 0, "A lone solid sample is a marching cubes blob.");

                chunk.SetSquare(lone, true);
                ChunkMeshJobs.BuildNow(chunk, new ChunkMeshSettings(Iso, 1f, backend: MesherBackend.Burst), mesh);
                Assert.AreEqual(0, mesh.vertexCount, "A lone square sample is no cube: it's dug out.");

                SquareBlock(chunk, new Vector3Int(1, 1, 1), new Vector3Int(2, 2, 2));
                ChunkMeshJobs.BuildNow(chunk, new ChunkMeshSettings(Iso, 1f, backend: MesherBackend.Burst), mesh);
                Assert.AreEqual(24, mesh.vertexCount);
                Assert.AreEqual(36, mesh.triangles.Length);
                Bounds bounds = GeometryBounds(mesh);
                Assert.AreEqual(new Vector3(1f, 1f, 1f), bounds.min, "The cube fills the voxel between its corner samples.");
                Assert.AreEqual(new Vector3(2f, 2f, 2f), bounds.max);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        // Two cubes side by side share no face: 10 faces, not 12.
        [Test]
        public void SquareNeighbours_HideTheFaceBetweenThem()
        {
            var chunk = new Chunk(new Vector3Int(4, 4, 4));
            SquareBlock(chunk, new Vector3Int(1, 1, 1), new Vector3Int(3, 2, 2));
            var mesh = new UnityEngine.Mesh();
            try
            {
                ChunkMeshJobs.BuildNow(chunk, new ChunkMeshSettings(Iso, 1f, backend: MesherBackend.Burst), mesh);
                Assert.AreEqual(40, mesh.vertexCount);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void DiggingASampleOut_ForgetsItWasSquare()
        {
            var chunk = new Chunk(new Vector3Int(4, 4, 4));
            var sample = new Vector3Int(1, 1, 1);
            chunk.SetDensity(sample, 1f);
            chunk.SetSquare(sample, true);
            chunk.SetDensity(sample, 0f);
            Assert.IsFalse(chunk.IsSquare(sample));
        }

        // GL87: the first chisel hit squares what it reaches; the next cuts it away and collects it.
        [Test]
        public void Chisel_SquaresFirst_ThenCutsAway()
        {
            ItemDefinition rockItem = ItemDefinition.Create("rock", "Rock");
            VoxelMaterial rock = VoxelMaterial.Create(10, "Rock").WithBreaking(0.5f, rockItem);
            MaterialRegistry registry = MaterialRegistry.Create(rock);
            try
            {
                World world = SlabWorld(rock.Id);
                var target = new Vector3Int(2, 1, 2);
                var targets = new List<Vector3Int> { target };
                var collected = new List<ItemDefinition>();

                ChiselResult first = ChiselStrike.Hit(world, targets, registry, collected, Iso);
                Assert.AreEqual(1, first.Squared);
                Assert.IsTrue(world.IsSquare(target));
                Assert.AreEqual(1f, world.GetDensity(target));
                Assert.IsEmpty(collected);

                ChiselResult second = ChiselStrike.Hit(world, targets, registry, collected, Iso);
                Assert.AreEqual(1, second.Cut);
                Assert.AreEqual(0f, world.GetDensity(target));
                Assert.IsFalse(world.IsSquare(target), "Cut away, it isn't square any more.");
                CollectionAssert.AreEqual(new[] { rockItem }, collected);
            }
            finally
            {
                Object.DestroyImmediate(registry);
                Object.DestroyImmediate(rock);
                Object.DestroyImmediate(rockItem);
            }
        }

        // GL88: a 3 x 3 level patch of square rock open to the sky is a flat top; 2 x 3 isn't.
        [Test]
        public void FlatTop_NeedsAThreeByThreeLevelSquarePatch()
        {
            World world = SlabWorld(10);
            var top = new List<Vector3Int>();
            for (int z = 1; z <= 3; z++)
            {
                for (int x = 1; x <= 2; x++)
                {
                    world.SetSquare(new Vector3Int(x, 1, z), true);
                }
            }
            Assert.IsFalse(FlatTop.Find(world, new Vector3Int(1, 1, 1), new Vector3Int(2, 1, 2), 2, Iso, id => id == 10, top));

            for (int z = 1; z <= 3; z++)
            {
                world.SetSquare(new Vector3Int(3, 1, z), true);
            }
            Assert.IsTrue(FlatTop.Find(world, new Vector3Int(1, 1, 1), new Vector3Int(2, 1, 2), 2, Iso, id => id == 10, top));
            Assert.AreEqual(9, top.Count);
            Assert.IsFalse(FlatTop.Find(world, new Vector3Int(1, 1, 1), new Vector3Int(2, 1, 2), 2, Iso, id => id == 0, top), "Only host materials count.");
        }

        // One 4-voxel chunk: rock up to y = 1, air above.
        private static World SlabWorld(byte material)
        {
            var world = new World(new Vector3Int(4, 4, 4), 1f);
            world.Load(Vector3Int.zero, null);
            for (int z = 0; z <= 4; z++)
            {
                for (int y = 0; y <= 4; y++)
                {
                    for (int x = 0; x <= 4; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        world.SetDensity(sample, y <= 1 ? 1f : 0f);
                        world.SetMaterial(sample, material);
                    }
                }
            }
            return world;
        }

        // Every sample from min to max, solid and square.
        private static void SquareBlock(Chunk chunk, Vector3Int min, Vector3Int max)
        {
            for (int z = min.z; z <= max.z; z++)
            {
                for (int y = min.y; y <= max.y; y++)
                {
                    for (int x = min.x; x <= max.x; x++)
                    {
                        chunk.SetDensity(new Vector3Int(x, y, z), 1f);
                        chunk.SetSquare(new Vector3Int(x, y, z), true);
                    }
                }
            }
        }

        private static Bounds GeometryBounds(UnityEngine.Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            var bounds = new Bounds(vertices[0], Vector3.zero);
            foreach (Vector3 vertex in vertices)
            {
                bounds.Encapsulate(vertex);
            }
            return bounds;
        }
    }
}
