using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// Ore generation (3D, O9): deterministic hashing and centroids, the Gaussian falloff,
    /// depth ranges, hosts, the overlap rule, and chunk borders agreeing whatever the load order.
    /// </summary>
    public class OreTests
    {
        private const byte Stone = 0;
        private const byte Dirt = 1;
        private const byte Copper = 3;
        private const byte Gold = 5;
        private const float Surface = 40f;

        private VoxelMaterial stone;
        private VoxelMaterial dirt;
        private TerrainLayers layers;

        [SetUp]
        public void MakeMaterials()
        {
            stone = VoxelMaterial.Create(Stone, "Stone");
            dirt = VoxelMaterial.Create(Dirt, "Dirt");
            layers = new TerrainLayers { Band = dirt, TopDepth = 0f, BandDepth = 3f, Base = stone };
        }

        [Test]
        public void VoxelHash_IsRepeatable_InRange_AndVaries()
        {
            float a = VoxelHash.Uniform(7, 1, 2, 3, 4);
            Assert.That(VoxelHash.Uniform(7, 1, 2, 3, 4), Is.EqualTo(a));
            Assert.That(a, Is.InRange(0f, 1f));

            var values = new HashSet<float>();
            for (int i = 0; i < 100; i++)
            {
                values.Add(VoxelHash.Uniform(7, i, 2, 3, 4));
            }
            Assert.That(values.Count, Is.GreaterThan(95), "neighbouring inputs give different values");
        }

        [Test]
        public void SameSeed_GivesTheSameCentroids_OtherSeedsDont()
        {
            OreField first = Field(seed: 3);
            OreField again = Field(seed: 3);
            OreField other = Field(seed: 4);
            var cell = new Vector3Int(1, 1, -2);

            Assert.That(first.NodesInCell(cell), Is.Not.Empty);
            Assert.That(Centres(again.NodesInCell(cell)), Is.EqualTo(Centres(first.NodesInCell(cell))));
            Assert.That(Centres(other.NodesInCell(cell)), Is.Not.EqualTo(Centres(first.NodesInCell(cell))));
        }

        [Test]
        public void Centroids_StayInTheirCell_AndDepthRange()
        {
            OreField field = Field(seed: 1);
            for (int x = -3; x <= 3; x++)
            {
                var cell = new Vector3Int(x, 1, 0);
                foreach (OreNode node in field.NodesInCell(cell))
                {
                    float depth = Surface - node.Centre.y;
                    Assert.That(depth, Is.InRange(6f, 30f));
                    Assert.That(Vector3Int.FloorToInt(node.Centre / field.CellSize), Is.EqualTo(cell));
                }
            }
        }

        [Test]
        public void Probability_IsThePeakAtTheCentre_AndFallsOffAsAGaussian()
        {
            var node = new OreNode(0, Copper, Vector3.zero, new Vector3(2f, 1f, 2f), 0.8f, 0);

            Assert.That(node.Probability(Vector3.zero), Is.EqualTo(0.8f).Within(1e-6f));
            Assert.That(node.Probability(new Vector3(2f, 0f, 0f)), Is.EqualTo(0.8f * Mathf.Exp(-0.5f)).Within(1e-6f), "1σ along x");
            Assert.That(node.Probability(new Vector3(0f, 1f, 0f)), Is.EqualTo(0.8f * Mathf.Exp(-0.5f)).Within(1e-6f), "1σ along y");
            Assert.That(node.Probability(new Vector3(0f, 3f, 0f)), Is.EqualTo(0.8f * Mathf.Exp(-4.5f)).Within(1e-6f), "3σ");
            Assert.That(node.Probability(new Vector3(1f, 0f, 0f)), Is.GreaterThan(node.Probability(new Vector3(1.5f, 0f, 0f))));
        }

        [Test]
        public void Ore_OnlyReplacesItsHosts()
        {
            OreField field = Field(seed: 2, hosts: new[] { stone });
            var nodes = new List<OreNode> { new OreNode(0, Copper, Vector3.zero, Vector3.one, 1f, 0) };

            Assert.That(field.Pick(Stone, Vector3Int.zero, Vector3.zero, nodes), Is.EqualTo(Copper));
            Assert.That(field.Pick(Dirt, Vector3Int.zero, Vector3.zero, nodes), Is.EqualTo(Dirt));
        }

        [Test]
        public void Overlap_HigherPriorityWins()
        {
            OreField field = Field(seed: 2, secondOre: true);
            var nodes = new List<OreNode>
            {
                new OreNode(0, Copper, Vector3.zero, Vector3.one, 1f, 0),
                new OreNode(1, Gold, Vector3.zero, Vector3.one, 1f, 5),
            };

            Assert.That(field.Pick(Stone, Vector3Int.zero, Vector3.zero, nodes), Is.EqualTo(Gold));
            nodes.Reverse();
            Assert.That(field.Pick(Stone, Vector3Int.zero, Vector3.zero, nodes), Is.EqualTo(Gold), "order doesn't matter");
        }

        [Test]
        public void CollectNodes_IncludesNodesSpillingInFromNeighbouringCells()
        {
            OreField field = Field(seed: 5);
            var all = new List<OreNode>();
            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    all.AddRange(field.NodesInCell(new Vector3Int(x, 1, z)));
                }
            }

            // A thin box on the cell (0, 1, 0)'s face: nodes from the next cell reach across it.
            var box = new Bounds(new Vector3(16f, 24f, 8f), new Vector3(0.1f, 16f, 16f));
            var found = new List<OreNode>();
            field.CollectNodes(box, found);

            foreach (OreNode node in all)
            {
                bool reaches = new Bounds(node.Centre, node.Reach * 2f).Intersects(box);
                Assert.That(found.Contains(node), Is.EqualTo(reaches), $"node at {node.Centre}");
            }
        }

        [Test]
        public void BorderSamples_Agree_WhateverTheLoadOrder()
        {
            var generator = new FlatGenerator(Surface);
            OreField ores = Field(seed: 9, nodes: 4f, peak: 0.9f);

            var forward = new World(new Vector3Int(8, 8, 8), 1f);
            var backward = new World(new Vector3Int(8, 8, 8), 1f);
            var coords = new List<Vector3Int>();
            for (int x = 0; x < 3; x++)
            {
                for (int y = 3; y < 5; y++)
                {
                    coords.Add(new Vector3Int(x, y, 0));
                }
            }
            foreach (Vector3Int coord in coords)
            {
                forward.Load(coord, generator, layers, ores);
            }
            OreField fresh = Field(seed: 9, nodes: 4f, peak: 0.9f);
            for (int i = coords.Count - 1; i >= 0; i--)
            {
                backward.Load(coords[i], generator, layers, fresh);
            }

            int oreSamples = 0;
            foreach (Vector3Int coord in coords)
            {
                Chunk a = forward.Chunks[coord];
                Chunk b = backward.Chunks[coord];
                for (int z = 0; z <= 8; z++)
                {
                    for (int y = 0; y <= 8; y++)
                    {
                        for (int x = 0; x <= 8; x++)
                        {
                            var sample = new Vector3Int(x, y, z);
                            Assert.That(b.GetMaterial(sample), Is.EqualTo(a.GetMaterial(sample)), $"{coord} {sample}");
                            if (a.GetMaterial(sample) == Copper)
                            {
                                oreSamples++;
                            }
                        }
                    }
                }

                // The shared face with the next chunk along x holds the same materials in both.
                if (forward.Chunks.TryGetValue(coord + Vector3Int.right, out Chunk next))
                {
                    for (int z = 0; z <= 8; z++)
                    {
                        for (int y = 0; y <= 8; y++)
                        {
                            Assert.That(next.GetMaterial(new Vector3Int(0, y, z)), Is.EqualTo(a.GetMaterial(new Vector3Int(8, y, z))));
                        }
                    }
                }
            }
            Assert.That(oreSamples, Is.GreaterThan(0), "the test world has some ore");
        }

        [Test]
        public void ContentsAt_IsTheNearestNodesGrade_ForTheOreAndItsBrokenStages()
        {
            VoxelMaterial stone = VoxelMaterial.Create(Stone, "Stone");
            VoxelMaterial loose = VoxelMaterial.Create(9, "Loose copper");
            VoxelMaterial copper = VoxelMaterial.Create(Copper, "Copper", VoxelMaterialCategory.Ore).WithBreaking(3f, null, loose);
            var settings = new OreGeneration { CellSize = 16f };
            settings.Ores.Add(new OreSpec
            {
                Ore = copper, NodesPerCell = 2f, MinDepth = 6f, MaxDepth = 30f, Spread = new Vector3(2f, 1.5f, 2f),
                Grade = new Vector2(0.1f, 0.4f), Gangue = stone,
            });
            var field = new OreField(settings, 11, new FlatGenerator(Surface));

            var grades = new HashSet<float>();
            for (int x = 0; x < 6; x++)
            {
                foreach (OreNode node in field.NodesInCell(new Vector3Int(x, 1, 0)))
                {
                    Assert.That(node.Contents.FractionOf(copper), Is.InRange(0.1f, 0.4f));
                    Assert.AreEqual(1f - node.Contents.FractionOf(copper), node.Contents.FractionOf(stone), 1e-5f);
                    Assert.AreSame(node.Contents, field.ContentsAt(loose, node.Centre), "Loose copper from a node has its grade.");
                    grades.Add(node.Contents.FractionOf(copper));
                }
            }
            Assert.That(grades.Count, Is.GreaterThan(1), "Nodes differ in grade.");
            Assert.AreEqual(0.25f, field.ContentsAt(copper, new Vector3(0f, 1000f, 0f)).FractionOf(copper), 1e-5f,
                "Far from every node: the spec's typical grade.");
            Assert.IsNull(field.ContentsAt(stone, Vector3.zero), "Stone isn't ore.");
        }

        private OreField Field(int seed, VoxelMaterial[] hosts = null, bool secondOre = false, float nodes = 2f, float peak = 0.7f)
        {
            var settings = new OreGeneration { CellSize = 16f };
            var copper = new OreSpec
            {
                Ore = VoxelMaterial.Create(Copper, "Copper", VoxelMaterialCategory.Ore),
                NodesPerCell = nodes,
                MinDepth = 6f,
                MaxDepth = 30f,
                PeakProbability = peak,
                Spread = new Vector3(2f, 1.5f, 2f),
            };
            if (hosts != null)
            {
                copper.Hosts.AddRange(hosts);
            }
            settings.Ores.Add(copper);
            if (secondOre)
            {
                settings.Ores.Add(new OreSpec { Ore = VoxelMaterial.Create(Gold, "Gold", VoxelMaterialCategory.Ore), Priority = 5 });
            }
            return new OreField(settings, seed, new FlatGenerator(Surface));
        }

        private static List<Vector3> Centres(IReadOnlyList<OreNode> nodes)
        {
            var centres = new List<Vector3>();
            foreach (OreNode node in nodes)
            {
                centres.Add(node.Centre);
            }
            return centres;
        }
    }
}
