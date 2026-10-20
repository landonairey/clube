using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class CarvedVoxelTests
    {
        private const float Iso = 0.5f;

        // GL92: a carved voxel loses its own piece of marching cubes surface, and its solid neighbours wall
        // it off: a full square floor below, half-height walls on the four sides where the ground's
        // surface crosses them halfway up, nothing above (air).
        [Test]
        public void CarvedVoxel_IsEmpty_AndItsNeighboursShowFlatWalls()
        {
            World world = SlabWorld(0);
            world.TryGetChunk(Vector3Int.zero, out Chunk chunk);
            var mesh = new UnityEngine.Mesh();
            try
            {
                ChunkMeshJobs.BuildNow(chunk, Settings(), mesh);
                Assert.AreEqual(32, mesh.triangles.Length / 3, "A flat surface: 2 triangles in each of 16 voxels.");
                Assert.AreEqual(1.5f, GeometryBounds(mesh).min.y, 1e-5f);

                world.SetCarved(new Vector3Int(1, 1, 1), true);
                ChunkMeshJobs.BuildNow(chunk, Settings(), mesh);
                Assert.AreEqual(30 + 10, mesh.triangles.Length / 3, "15 surface voxels, and 5 walls of 2 triangles.");
                Assert.AreEqual(1f, GeometryBounds(mesh).min.y, 1e-5f, "The floor lies on the voxel grid.");

                // The walls face into the carved voxel: the floor's 6 vertices point up, and nothing faces down.
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                int floorUp = 0;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Assert.Greater(normals[i].y, -0.1f, "No face points down into the ground.");
                    if (Mathf.Abs(normals[i].x) > 0.9f)
                    {
                        // A side wall: at x = 1 it faces +x, at x = 2 it faces -x, into the voxel between.
                        Assert.AreEqual(vertices[i].x < 1.5f ? 1f : -1f, Mathf.Sign(normals[i].x));
                    }
                    if (Mathf.Abs(vertices[i].y - 1f) < 1e-5f && normals[i].y > 0.9f)
                    {
                        floorUp++;
                    }
                }
                Assert.AreEqual(6, floorUp);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        // Each chunk knows the carved voxels just past its border, so no wall stands between two
        // carved voxels in different chunks.
        [Test]
        public void CarvedVoxels_AreKnownAcrossChunkBorders()
        {
            var world = new World(new Vector3Int(4, 4, 4), 1f);
            Chunk left = world.Load(Vector3Int.zero, null);
            Chunk right = world.Load(Vector3Int.right, null);
            world.SetCarved(new Vector3Int(3, 1, 1), true);
            world.SetCarved(new Vector3Int(4, 1, 1), true);
            Assert.IsTrue(world.IsCarved(new Vector3Int(3, 1, 1)));
            Assert.IsTrue(world.IsCarved(new Vector3Int(4, 1, 1)));
            Assert.IsTrue(left.IsCarved(new Vector3Int(4, 1, 1)), "The left chunk sees the right one's voxel.");
            Assert.IsTrue(right.IsCarved(new Vector3Int(-1, 1, 1)), "The right chunk sees the left one's voxel.");
            Assert.IsFalse(right.IsCarved(new Vector3Int(-1, 2, 1)));
        }

        // GL87, GL92: a hit carves the aimed voxel and collects one drop; it never adds ground.
        [Test]
        public void Chisel_CarvesOneVoxel_CollectsIt_AndAddsNothing()
        {
            ItemDefinition rockItem = ItemDefinition.Create("rock", "Rock");
            VoxelMaterial rock = VoxelMaterial.Create(10, "Rock").WithBreaking(0.5f, rockItem);
            MaterialRegistry registry = MaterialRegistry.Create(rock);
            try
            {
                World world = SlabWorld(rock.Id);
                float before = TotalDensity(world);
                var voxel = new Vector3Int(2, 1, 2);
                var collected = new List<ItemDefinition>();

                ChiselResult first = ChiselStrike.Hit(world, VoxelReach.Around(voxel, 1, false), registry, collected, Iso);
                Assert.AreEqual(1, first.Carved);
                Assert.IsTrue(world.IsCarved(voxel));
                CollectionAssert.AreEqual(new[] { rockItem }, collected);
                Assert.LessOrEqual(TotalDensity(world), before, "The chisel never adds ground.");

                ChiselResult again = ChiselStrike.Hit(world, VoxelReach.Around(voxel, 1, false), registry, collected, Iso);
                Assert.AreEqual(0, again.Carved, "Already carved: nothing left there.");
                Assert.AreEqual(0, ChiselStrike.Hit(world, VoxelReach.Around(new Vector3Int(2, 3, 2), 1, false), registry, collected, Iso).Carved, "Air: nothing to carve.");
            }
            finally
            {
                Object.DestroyImmediate(registry);
                Object.DestroyImmediate(rock);
                Object.DestroyImmediate(rockItem);
            }
        }

        // Ground put back (a pile, a brush) fills a carved voxel in again.
        [Test]
        public void GroundPutBack_FillsACarvedVoxelIn()
        {
            World world = SlabWorld(0);
            var voxel = new Vector3Int(2, 1, 2);
            world.SetCarved(voxel, true);
            world.SetDensity(new Vector3Int(2, 1, 2), 0.2f);
            Assert.IsTrue(world.IsCarved(voxel), "Taking ground away leaves it carved.");
            world.SetDensity(new Vector3Int(2, 2, 2), 1f);
            Assert.IsFalse(world.IsCarved(voxel));
        }

        // A voxel carved all round keeps no ground in its middle: the data agrees with what's drawn.
        [Test]
        public void SamplesEnclosedByCarvedVoxels_BecomeAir()
        {
            World world = SlabWorld(0, 3);
            for (int z = 1; z <= 2; z++)
            {
                for (int y = 1; y <= 2; y++)
                {
                    for (int x = 1; x <= 2; x++)
                    {
                        world.SetCarved(new Vector3Int(x, y, z), true);
                    }
                }
            }
            Assert.AreEqual(0f, world.GetDensity(new Vector3Int(2, 2, 2)));
            Assert.AreEqual(1f, world.GetDensity(new Vector3Int(1, 1, 1)), "A corner of the hole keeps its ground.");
        }

        // GL88, GL92: a 3 x 3 level patch of carved voxels floored with rock is a flat top; 2 x 3 isn't.
        [Test]
        public void FlatTop_NeedsAThreeByThreePatchOfFlooredCarvedVoxels()
        {
            World world = SlabWorld(10, 2);
            var top = new List<Vector3Int>();
            for (int z = 0; z <= 2; z++)
            {
                for (int x = 0; x <= 1; x++)
                {
                    world.SetCarved(new Vector3Int(x, 2, z), true);
                }
            }
            Assert.IsFalse(FlatTop.Find(world, new Vector3Int(0, 2, 0), new Vector3Int(1, 2, 1), 2, Iso, id => id == 10, top));

            for (int z = 0; z <= 2; z++)
            {
                world.SetCarved(new Vector3Int(2, 2, z), true);
            }
            Assert.IsTrue(FlatTop.Find(world, new Vector3Int(0, 2, 0), new Vector3Int(1, 2, 1), 2, Iso, id => id == 10, top));
            Assert.AreEqual(16, top.Count, "The floor of 3 x 3 voxels: 4 x 4 samples.");
            Assert.IsFalse(FlatTop.Find(world, new Vector3Int(0, 2, 0), new Vector3Int(1, 2, 1), 2, Iso, id => id == 0, top), "Only host materials count.");
        }

        private static ChunkMeshSettings Settings()
        {
            return new ChunkMeshSettings(Iso, 1f, backend: MesherBackend.Burst);
        }

        // One 4-voxel chunk: ground up to y = top, air above.
        private static World SlabWorld(byte material, int top = 1)
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
                        world.SetDensity(sample, y <= top ? 1f : 0f);
                        world.SetMaterial(sample, material);
                    }
                }
            }
            return world;
        }

        private static float TotalDensity(World world)
        {
            float total = 0f;
            for (int z = 0; z <= 4; z++)
            {
                for (int y = 0; y <= 4; y++)
                {
                    for (int x = 0; x <= 4; x++)
                    {
                        total += world.GetDensity(new Vector3Int(x, y, z)) ?? 0f;
                    }
                }
            }
            return total;
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
