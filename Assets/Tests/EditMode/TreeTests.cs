using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>Trees (GL30): their shapes, where they grow, and how chunks receive them.</summary>
    public class TreeTests
    {
        private const float Ground = 2f;
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

        private TreeGeneration Settings(float chance = 1f)
        {
            return new TreeGeneration { Chance = chance, CellSize = 10f, Wood = wood, Leaves = leaves };
        }

        [Test]
        public void Shape_IsTheSameForACell_AndVariesBetweenCells()
        {
            TreeGeneration settings = Settings();
            var first = new List<TreePart>();
            var again = new List<TreePart>();
            var other = new List<TreePart>();
            TreeShape.Build(Seed, new Vector2Int(3, 4), new float3(0f, Ground, 0f), settings, first);
            TreeShape.Build(Seed, new Vector2Int(3, 4), new float3(0f, Ground, 0f), settings, again);
            TreeShape.Build(Seed, new Vector2Int(4, 4), new float3(0f, Ground, 0f), settings, other);

            CollectionAssert.AreEqual(Positions(first), Positions(again), "One cell, one tree.");
            CollectionAssert.AreNotEqual(Positions(first), Positions(other), "Neighbouring cells grow different trees.");
        }

        [Test]
        public void Shape_HasATrunkBranchesAndLeaves_WithinItsSpread([Values(0, 1, 2, 3, 4, 5, 6, 7)] int cell)
        {
            TreeGeneration settings = Settings();
            var parts = new List<TreePart>();
            var root = new float3(0f, Ground, 0f);
            TreeShape.Build(Seed, new Vector2Int(cell, -cell), root, settings, parts);

            int woodParts = 0;
            int leafBalls = 0;
            foreach (TreePart part in parts)
            {
                bool ball = math.all(part.From == part.To);
                if (part.Material == wood.Id)
                {
                    woodParts++;
                    Assert.That(ball, Is.False, "Wood is trunk and branch segments.");
                    Assert.That(math.min(part.FromRadius, part.ToRadius), Is.GreaterThanOrEqualTo(settings.MinBranchRadius - 1e-5f));
                }
                else
                {
                    Assert.That(part.Material, Is.EqualTo(leaves.Id));
                    Assert.That(ball, Is.True, "Leaves are balls.");
                    leafBalls++;
                }
                Bounds bounds = part.Bounds(0f);
                float spread = math.max(
                    math.max(math.abs(bounds.min.x - root.x), math.abs(bounds.max.x - root.x)),
                    math.max(math.abs(bounds.min.z - root.z), math.abs(bounds.max.z - root.z)));
                Assert.That(spread, Is.LessThanOrEqualTo(settings.MaxSpread), "Every part stays within the spread chunks gather trees by.");
            }

            // Foot and trunk (5), then two segments and at least two twigs per large branch.
            int fewestBranches = settings.Branches.x;
            Assert.That(woodParts, Is.GreaterThanOrEqualTo(5 + fewestBranches * 4));
            // A clump on every twig and branch end, and the crown.
            Assert.That(leafBalls, Is.GreaterThanOrEqualTo((fewestBranches * 3 + 1) * 3));
        }

        [Test]
        public void Field_GrowsTreesOnFlatGround_NotOnSlopes()
        {
            var flat = new TreeField(Settings(), Seed, new FlatGenerator(Ground));
            var slope = new TreeField(Settings(), Seed, new Ramp(0.5f));

            for (int x = 0; x < 4; x++)
            {
                Assert.That(flat.TryGetTree(new Vector2Int(x, 0), out Vector3 root, out _), Is.True, "Every cell grows one at chance 1.");
                Assert.That(root.y, Is.EqualTo(Ground).Within(1e-3f), "Rooted on the surface.");
                Assert.That(slope.TryGetTree(new Vector2Int(x, 0), out _, out _), Is.False, "A 27° slope is too steep.");
            }
        }

        [Test]
        public void Stamp_DoesNotDependOnPartOrder()
        {
            var grid = new ChunkSampleGrid(new int3(-8, 0, -8), new int3(17, 25, 17), 0.5f);
            var parts = new List<TreePart>();
            TreeShape.Build(Seed, new Vector2Int(1, 1), new float3(0f, Ground, 0f), Settings(), parts);
            var reversed = new List<TreePart>(parts);
            reversed.Reverse();

            using (ChunkFillOutput forward = Stamp(grid, parts))
            using (ChunkFillOutput backward = Stamp(grid, reversed))
            {
                CollectionAssert.AreEqual(forward.DensityBytes.ToArray(), backward.DensityBytes.ToArray());
                CollectionAssert.AreEqual(forward.Materials.ToArray(), backward.Materials.ToArray());
                CollectionAssert.Contains(forward.Materials.ToArray(), wood.Id);
            }
        }

        [Test]
        public void World_ChunksAgreeOnBorders_AndChunksAboveTheGroundGetTheCrowns()
        {
            // 4 m chunks of 0.25 m voxels, flat ground at 2 m: every tree rises through chunks
            // that are wholly above the ground, and crosses chunk borders.
            var settings = Settings();
            var generator = new FlatGenerator(Ground);
            var trees = new TreeField(settings, Seed, generator);
            var world = new World(new Vector3Int(16, 16, 16), 0.25f);
            for (int y = 0; y < 4; y++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        world.Load(new Vector3Int(x, y, z), generator, null, null, trees);
                    }
                }
            }

            int leafSamples = 0;
            foreach (KeyValuePair<Vector3Int, Chunk> entry in world.Chunks)
            {
                if (entry.Key.y >= 1)
                {
                    leafSamples += CountSolid(entry.Value, leaves.Id);
                }
            }
            Assert.That(leafSamples, Is.GreaterThan(0), "Leaves reach chunks above the ground.");

            // Each shared face holds the same samples in both chunks.
            foreach (KeyValuePair<Vector3Int, Chunk> entry in world.Chunks)
            {
                if (!world.Chunks.TryGetValue(entry.Key + Vector3Int.right, out Chunk right))
                {
                    continue;
                }
                for (int z = 0; z <= 16; z++)
                {
                    for (int y = 0; y <= 16; y++)
                    {
                        var face = new Vector3Int(16, y, z);
                        var other = new Vector3Int(0, y, z);
                        Assert.That(right.GetDensity(other), Is.EqualTo(entry.Value.GetDensity(face)), $"density {entry.Key} {face}");
                        Assert.That(right.GetMaterial(other), Is.EqualTo(entry.Value.GetMaterial(face)), $"material {entry.Key} {face}");
                    }
                }
            }
        }

        private static ChunkFillOutput Stamp(ChunkSampleGrid grid, List<TreePart> parts)
        {
            // All air before the trees, as above the ground.
            var output = ChunkFillOutput.Allocate(grid.Length, DensityFormat.Byte, Allocator.Persistent);
            for (int i = 0; i < grid.Length; i++)
            {
                output.DensityBytes[i] = 0;
                output.Materials[i] = 0;
            }
            using (var data = new NativeArray<TreePart>(parts.ToArray(), Allocator.Persistent))
            {
                TreeStamp.Run(data, grid, DensityFormat.Byte, ref output);
            }
            return output;
        }

        private static int CountSolid(Chunk chunk, byte material)
        {
            int count = 0;
            Vector3Int samples = chunk.SampleCount;
            for (int z = 0; z < samples.z; z++)
            {
                for (int y = 0; y < samples.y; y++)
                {
                    for (int x = 0; x < samples.x; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        if (chunk.GetDensity(sample) >= 0.5f && chunk.GetMaterial(sample) == material)
                        {
                            count++;
                        }
                    }
                }
            }
            return count;
        }

        private static List<Vector3> Positions(List<TreePart> parts)
        {
            var positions = new List<Vector3>();
            foreach (TreePart part in parts)
            {
                positions.Add(part.From);
                positions.Add(part.To);
            }
            return positions;
        }

        // Ground rising along x: a slope of rise per metre.
        private sealed class Ramp : ITerrainGenerator
        {
            private readonly float rise;

            public Ramp(float rise)
            {
                this.rise = rise;
            }

            public float Depth(Vector3 position)
            {
                return Ground + position.x * rise - position.y;
            }
        }
    }
}
