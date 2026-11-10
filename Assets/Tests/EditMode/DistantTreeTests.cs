using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// Trees far off (GL162): the builder places exactly the trees the voxel world grows, at the
    /// same roots, and their meshes face outwards.
    /// </summary>
    public class DistantTreeTests
    {
        private const int Seed = 7;

        private VoxelMaterial wood;
        private VoxelMaterial leaves;

        [SetUp]
        public void MakeMaterials()
        {
            wood = VoxelMaterial.Create(11, "Wood", VoxelMaterialCategory.Plant);
            leaves = VoxelMaterial.Create(12, "Leaves", VoxelMaterialCategory.Plant);
        }

        [TearDown]
        public void DestroyMaterials()
        {
            Object.DestroyImmediate(wood);
            Object.DestroyImmediate(leaves);
        }

        [Test]
        public void Builder_PlacesTheTreesTheFieldGrows()
        {
            // Hilly enough that some spots are too steep for a tree.
            var generator = new FractalPerlin2DGenerator(11, 30f, 12f, 0.015f, octaves: 3);
            // Forests and ages on (GL164, GL165): both sides must read them the same way.
            var settings = new TreeGeneration
            {
                Chance = 0.6f, CellSize = 10f, Wood = wood, Leaves = leaves,
                ForestCover = 0.5f, ForestScale = 80f, FieldChance = 0.05f, YoungestSize = 0.5f,
            };
            var field = new TreeField(settings, Seed, generator);
            using (var builder = new DistantTreeBuilder(generator, field, 300f))
            {
                for (int frame = 0; frame < 2000 && (frame == 0 || builder.Pending > 0); frame++)
                {
                    builder.Update(Vector3.zero);
                    System.Threading.Thread.Sleep(1);
                }
                Assert.That(builder.Pending, Is.EqualTo(0), "every tile placed");

                var placed = new Dictionary<Vector2Int, DistantTree>();
                foreach (DistantTreeBuilder.Tile tile in builder.Tiles)
                {
                    foreach (DistantTree tree in tile.Trees)
                    {
                        placed.Add(new Vector2Int(Mathf.FloorToInt(tree.Root.x / 10f), Mathf.FloorToInt(tree.Root.z / 10f)), tree);
                    }
                }

                // Every cell of the tiles round the origin: a tree in one exactly where the field grows one.
                int grown = 0;
                int refused = 0;
                for (int z = -12; z < 12; z++)
                {
                    for (int x = -12; x < 12; x++)
                    {
                        var cell = new Vector2Int(x, z);
                        bool hasTree = field.TryGetTree(cell, out Vector3 root, out _) && !TreeForests.IsFallen(Seed, cell, settings);
                        Assert.That(placed.TryGetValue(cell, out DistantTree tree), Is.EqualTo(hasTree), $"cell {cell}");
                        if (hasTree)
                        {
                            grown++;
                            Assert.That(Vector3.Distance(tree.Root, root), Is.LessThan(0.05f), $"cell {cell} root");
                            Assert.That(tree.Height, Is.EqualTo(TreeShape.HeightOf(Seed, cell, settings)));
                        }
                        else if (field.TryGetSpot(cell, out _))
                        {
                            refused++;
                        }
                    }
                }
                Assert.That(grown, Is.GreaterThan(40));
                Assert.That(refused, Is.GreaterThan(0), "some spots are too steep, and both agree on them");
            }
        }

        [Test]
        public void Meshes_FaceOutwards()
        {
            // A small tree as TreeShape lays one out (GL258): a stump, a trunk of two segments, a branch, and two clusters
            // of leaf balls, the last round the trunk's top; 10 m tall, rooted away from the origin.
            var root = new Unity.Mathematics.float3(30f, 5f, -12f);
            var up = new Unity.Mathematics.float3(0f, 1f, 0f);
            var side = new Unity.Mathematics.float3(1f, 0f, 0f);
            var parts = new System.Collections.Generic.List<TreePart>
            {
                TreePart.Segment(root - up * 0.3f, root + up * 0.4f, 0.4f, 0.35f, 1),
                TreePart.Segment(root, root + up * 4f, 0.3f, 0.2f, 1),
                TreePart.Segment(root + up * 4f, root + up * 8f, 0.2f, 0.1f, 1),
                TreePart.Segment(root + up * 5f, root + up * 6.5f + side * 2f, 0.12f, 0.06f, 1),
                TreePart.Ball(root + up * 6.8f + side * 2.2f, 0.8f, 2),
                TreePart.Ball(root + up * 7.2f + side * 1.6f, 0.7f, 2),
                TreePart.Ball(root + up * 8.4f, 1.1f, 2),
                TreePart.Ball(root + up * 8.9f + side * 0.6f, 0.9f, 2),
            };
            Mesh[] levels = DistantTreeMeshes.BuildLevels(parts, (Vector3)root, 10f, Color.white, Color.green, "Test");
            Assert.AreEqual(DistantTreeMeshes.Levels, levels.Length);
            int previous = int.MaxValue;
            foreach (Mesh mesh in levels)
            {
                try
                {
                    Assert.That(mesh.triangles.Length, Is.LessThanOrEqualTo(previous), "each level has no more triangles than the one before");
                    previous = mesh.triangles.Length;
                    Vector3[] vertices = mesh.vertices;
                    Vector3[] normals = mesh.normals;
                    int[] triangles = mesh.triangles;
                    for (int t = 0; t < triangles.Length; t += 3)
                    {
                        Vector3 a = vertices[triangles[t]];
                        Vector3 b = vertices[triangles[t + 1]];
                        Vector3 c = vertices[triangles[t + 2]];
                        Assert.That(Vector3.Dot(Vector3.Cross(b - a, c - a), normals[triangles[t]]), Is.GreaterThan(0f), "winding matches the normal");
                    }
                    Assert.That(mesh.bounds.max.y, Is.LessThanOrEqualTo(1.2f), "the crown reaches a little over the unit height, as the voxel trees' do");
                    Assert.That(mesh.bounds.min.y, Is.LessThan(0f), "the trunk runs on below the ground");
                }
                finally
                {
                    Object.DestroyImmediate(mesh);
                }
            }
        }
    }
}
