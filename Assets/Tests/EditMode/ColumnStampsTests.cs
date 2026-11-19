using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// A column's trees, rocks and ores gathered once (GL774, <see cref="ColumnStamps"/>): each chunk's share, picked in a
    /// Burst job, is exactly what the chunk gathers itself, in the same order, and the pipeline's chunks are what generating
    /// them directly gives.
    /// </summary>
    public class ColumnStampsTests
    {
        private const int Size = 8;
        private const int Layers = 12;
        private const byte RockId = 5;

        private VoxelMaterial stone, dirt, copper, wood, leaves;
        private TerrainLayers layers;
        private FractalPerlin2DGenerator generator;
        private OreField ores;
        private TreeField trees;

        [SetUp]
        public void SetUp()
        {
            stone = VoxelMaterial.Create(RockId, "Stone");
            dirt = VoxelMaterial.Create(1, "Dirt");
            copper = VoxelMaterial.Create(3, "Copper", VoxelMaterialCategory.Ore);
            wood = VoxelMaterial.Create(11, "Wood", VoxelMaterialCategory.Plant);
            leaves = VoxelMaterial.Create(12, "Leaves", VoxelMaterialCategory.Plant);
            layers = new TerrainLayers { Band = dirt, TopDepth = 0f, BandDepth = 3f, Base = stone };
            generator = new FractalPerlin2DGenerator(3, 40f, 6f, 0.05f, octaves: 3);
            var oreSettings = new OreGeneration { CellSize = 16f };
            oreSettings.Ores.Add(new OreSpec { Ore = copper, NodesPerCell = 0.8f, MinDepth = 1f, MaxDepth = 30f, Spread = new Vector3(2f, 1.5f, 2f) });
            ores = new OreField(oreSettings, 7, generator);
            trees = new TreeField(new TreeGeneration { Chance = 0.7f, CellSize = 10f, Wood = wood, Leaves = leaves }, 7, generator);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(stone);
            Object.DestroyImmediate(dirt);
            Object.DestroyImmediate(copper);
            Object.DestroyImmediate(wood);
            Object.DestroyImmediate(leaves);
        }

        [Test]
        public void EachLayer_PicksWhatItsChunkGathersItself_InTheSameOrder()
        {
            World world = NewWorld();
            var boulders = new BoulderField(5, 0.6f, world.Grid.VoxelSize, generator, RockId);
            var pipeline = new ChunkPipeline(world.Grid, generator, layers, ores, world.PreferredFormat, world.StorageFactory, trees, null, boulders);
            int withParts = 0, withNodes = 0, covered = 0;
            try
            {
                for (int z = -3; z <= 3; z++)
                {
                    for (int x = -3; x <= 3; x++)
                    {
                        var column = new Vector2Int(x, z);
                        float highest = Highest(pipeline, column);
                        float rampReach = ChunkGenerator.SettingsFor(layers, ores, world.PreferredFormat, null, generator).RampReach;
                        using (ColumnStamps stamps = ColumnStamps.Gather(world.Grid, column, highest, rampReach, trees, boulders, ores))
                        {
                            for (int y = 0; y < Layers; y++)
                            {
                                var grid = ChunkSampleGrid.ForChunk(world.Grid, new Vector3Int(x, y, z));
                                using (NativeArray<TreePart> expectedParts = ChunkGenerator.CollectTrees(trees, boulders, grid, Allocator.Temp))
                                using (NativeArray<OreNodeData> expectedNodes = ChunkGenerator.CollectOres(ores, grid, Allocator.Temp))
                                {
                                    string where = $"chunk ({x}, {y}, {z})";
                                    Assert.That(stamps.Knows(y), Is.True, where);
                                    Assert.That(stamps.TreesReach(y), Is.EqualTo(expectedParts.Length > 0), where);
                                    if (!stamps.Covers(y))
                                    {
                                        continue;
                                    }
                                    covered++;
                                    Assert.That(stamps.OresReach(y), Is.EqualTo(expectedNodes.Length > 0), where);
                                    var parts = new NativeList<TreePart>(Allocator.TempJob);
                                    var nodes = new NativeList<OreNodeData>(Allocator.TempJob);
                                    stamps.ScheduleSelect(y, parts, nodes).Complete();
                                    try
                                    {
                                        Assert.That(parts.Length, Is.EqualTo(expectedParts.Length), where + " parts");
                                        for (int i = 0; i < parts.Length; i++)
                                        {
                                            Assert.That(parts[i], Is.EqualTo(expectedParts[i]), $"{where} part {i}");
                                        }
                                        Assert.That(nodes.Length, Is.EqualTo(expectedNodes.Length), where + " nodes");
                                        for (int i = 0; i < nodes.Length; i++)
                                        {
                                            Assert.That(nodes[i], Is.EqualTo(expectedNodes[i]), $"{where} node {i}");
                                        }
                                        withParts += parts.Length > 0 ? 1 : 0;
                                        withNodes += nodes.Length > 0 ? 1 : 0;
                                    }
                                    finally
                                    {
                                        parts.Dispose();
                                        nodes.Dispose();
                                    }
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                pipeline.Dispose();
            }
            Assert.That(covered, Is.GreaterThan(7 * 7 * 3), "the ground and the trees are covered");
            Assert.That(withParts, Is.GreaterThan(0), "some chunks have trees or rocks");
            Assert.That(withNodes, Is.GreaterThan(0), "some chunks have ore");
        }

        [Test]
        public void ThePipelinesChunks_AreWhatGeneratingThemDirectlyGives()
        {
            World world = NewWorld();
            var pipeline = new ChunkPipeline(world.Grid, generator, layers, ores, world.PreferredFormat, world.StorageFactory, trees);
            var pending = new HashSet<Vector3Int>();
            var made = new Dictionary<Vector3Int, Chunk>();
            try
            {
                for (int z = -2; z <= 2; z++)
                {
                    for (int x = -2; x <= 2; x++)
                    {
                        for (int y = 0; y < Layers; y++)
                        {
                            pending.Add(new Vector3Int(x, y, z));
                        }
                    }
                }
                var waiting = new List<Vector3Int>(pending);
                for (int frame = 0; frame < 20000 && made.Count < pending.Count; frame++)
                {
                    waiting.RemoveAll(coord => pipeline.StartGeneration(coord));
                    pipeline.Flush();
                    pipeline.Poll();
                    while (pipeline.TryTakeGenerated(out Vector3Int coord, out Chunk chunk))
                    {
                        made.Add(coord, chunk);
                    }
                    System.Threading.Thread.Sleep(waiting.Count > 0 || made.Count < pending.Count ? 1 : 0);
                }
                Assert.That(made.Count, Is.EqualTo(pending.Count));
                foreach (KeyValuePair<Vector3Int, Chunk> entry in made)
                {
                    Chunk expected = ChunkGenerator.Generate(
                        generator, ChunkSampleGrid.ForChunk(world.Grid, entry.Key), world.StorageFactory, layers, ores, world.PreferredFormat, trees);
                    AssertSame(expected, entry.Value, entry.Key);
                    expected.Release();
                }
            }
            finally
            {
                foreach (Chunk chunk in made.Values)
                {
                    chunk.Release();
                }
                pipeline.Dispose();
            }
        }

        private static World NewWorld()
        {
            return new World(new Vector3Int(Size, Size, Size), 1f, samples => new ByteVoxelStorage(samples));
        }

        private static float Highest(ChunkPipeline pipeline, Vector2Int column)
        {
            float highest = 0f;
            for (int frame = 0; frame < 5000 && !pipeline.TryGetColumnRange(column, out _, out highest); frame++)
            {
                pipeline.Flush();
                System.Threading.Thread.Sleep(1);
            }
            return highest;
        }

        private static void AssertSame(Chunk expected, Chunk actual, Vector3Int coord)
        {
            Vector3Int count = expected.SampleCount;
            for (int z = 0; z < count.z; z++)
            {
                for (int y = 0; y < count.y; y++)
                {
                    for (int x = 0; x < count.x; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        Assert.That(actual.GetDensity(sample), Is.EqualTo(expected.GetDensity(sample)), $"{coord} {sample} density");
                        Assert.That(actual.GetMaterial(sample), Is.EqualTo(expected.GetMaterial(sample)), $"{coord} {sample} material");
                    }
                }
            }
        }
    }
}
