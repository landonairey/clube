using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// Sparse chunks (GL773): layers proven all air or all rock count as loaded and answer reads without a chunk, get a real
    /// chunk when something changes them, and are exactly what generating them would have given.
    /// </summary>
    public class SparseChunkTests
    {
        private const int Size = 8;
        private const int Layers = 12;
        private const byte AirId = 2;
        private const byte RockId = 5;

        // ---- The world's bookkeeping --------------------------------------------------------

        [Test]
        public void UniformLayers_KeepTheirLayersApart_AcrossTheWordBoundary()
        {
            var layers = new UniformLayers();
            layers.SetAir(3);
            layers.SetRock(63);
            layers.SetAir(64);
            layers.SetRock(127);
            layers.SetAir(UniformLayers.MaxLayers);

            Assert.That(layers.Count, Is.EqualTo(4));
            Assert.That(layers.IsAir(3) && layers.IsRock(63) && layers.IsAir(64) && layers.IsRock(127), Is.True);
            Assert.That(layers.IsRock(3) || layers.IsAir(63) || layers.IsUniform(4) || layers.IsUniform(UniformLayers.MaxLayers), Is.False);

            layers.SetRock(3);
            Assert.That(layers.IsRock(3) && !layers.IsAir(3), Is.True, "a layer is one or the other");
            layers.Clear(64);
            Assert.That(layers.IsUniform(64), Is.False);
            Assert.That(layers.Count, Is.EqualTo(3));
        }

        [Test]
        public void ProvenLayers_CountAsLoaded_AndAnswerReads_WithoutAChunk()
        {
            World world = NewWorld();
            Assert.That(world.ProveColumn(Vector2Int.zero, RockAndAir(), Layers), Is.EqualTo(2));

            Assert.That(world.LoadedCount, Is.Zero, "no chunk is held");
            Assert.That(world.LoadedLayersIn(Vector2Int.zero), Is.EqualTo(2));
            Assert.That(world.IsLoaded(new Vector3Int(0, 0, 0)) && world.IsLoaded(new Vector3Int(0, 3, 0)), Is.True);
            Assert.That(world.IsLoaded(new Vector3Int(0, 1, 0)), Is.False, "a layer not proven still has to load");
            Assert.That(world.GetDensity(new Vector3Int(1, 1, 1)), Is.EqualTo(1f));
            Assert.That(world.GetMaterial(new Vector3Int(1, 1, 1)), Is.EqualTo(RockId));
            Assert.That(world.GetDensity(new Vector3Int(1, 3 * Size + 1, 1)), Is.EqualTo(0f));
            Assert.That(world.GetMaterial(new Vector3Int(1, 3 * Size + 1, 1)), Is.EqualTo(AirId));
            Assert.That(world.TryPeekChunk(Vector3Int.zero, out _), Is.False);
            Assert.That(world.LoadedCount, Is.Zero, "reading made no chunk");
        }

        [Test]
        public void AskingForAProvenChunk_MakesIt_AsGeneratedAndUnchanged()
        {
            World world = NewWorld();
            world.ProveColumn(Vector2Int.zero, RockAndAir(), Layers);
            var made = new List<Vector3Int>();
            world.ChunkMaterialised += made.Add;

            Assert.That(world.TryGetChunk(Vector3Int.zero, out Chunk chunk), Is.True);

            Assert.That(made, Is.EqualTo(new[] { Vector3Int.zero }));
            Assert.That(chunk.IsUniform, Is.True);
            Assert.That(chunk.UniformDensity, Is.EqualTo(1f));
            Assert.That(chunk.GetMaterial(Vector3Int.one), Is.EqualTo(RockId));
            Assert.That(world.IsEdited(Vector3Int.zero), Is.False);
            Assert.That(world.LoadedLayersIn(Vector2Int.zero), Is.EqualTo(2), "it was counted already");
            Assert.That(world.ProvenLayersIn(Vector2Int.zero), Is.EqualTo(1));

            world.Unload(Vector3Int.zero);
            Assert.That(world.LoadedCount, Is.Zero);
            Assert.That(world.IsLoaded(Vector3Int.zero), Is.True, "unchanged, it goes back to being proven");
            Assert.That(world.LoadedLayersIn(Vector2Int.zero), Is.EqualTo(2));
            Assert.That(world.ProvenLayersIn(Vector2Int.zero), Is.EqualTo(2));
        }

        [Test]
        public void WritingWhatAProvenLayerHolds_MakesNoChunk()
        {
            World world = NewWorld();
            world.ProveColumn(Vector2Int.zero, RockAndAir(), Layers);

            Assert.That(world.SetDensity(new Vector3Int(1, 1, 1), 1f), Is.Zero);
            world.SetMaterial(new Vector3Int(1, 1, 1), RockId);
            world.SetWater(new Vector3Int(1, 3 * Size + 1, 1), 0f);
            world.SetCarved(new Vector3Int(1, 1, 1), false);

            Assert.That(world.LoadedCount, Is.Zero);
            Assert.That(world.EditedCount, Is.Zero);
        }

        [Test]
        public void AnEdit_ReachesEveryCopyOfABorderSample_AndStaysEdited()
        {
            World world = NewWorld();
            world.ProveColumn(Vector2Int.zero, RockAndAir(), Layers);
            world.ProveColumn(Vector2Int.right, RockAndAir(), Layers);

            // On the face the two columns share.
            var sample = new Vector3Int(Size, 2, 2);
            world.SetDensity(sample, 0f);

            Assert.That(world.TryPeekChunk(Vector3Int.zero, out Chunk left) && world.TryPeekChunk(Vector3Int.right, out _), Is.True);
            world.TryPeekChunk(Vector3Int.right, out Chunk right);
            Assert.That(left.GetDensity(new Vector3Int(Size, 2, 2)), Is.EqualTo(0f));
            Assert.That(right.GetDensity(new Vector3Int(0, 2, 2)), Is.EqualTo(0f));
            Assert.That(world.IsEdited(Vector3Int.zero) && world.IsEdited(Vector3Int.right), Is.True);
            Assert.That(world.IsProvenUniform(Vector3Int.zero), Is.False, "an edited chunk is no longer what was proven");
            Assert.That(world.LoadedLayersIn(Vector2Int.zero), Is.EqualTo(2));

            world.ForgetColumnProof(Vector2Int.zero);
            world.Unload(Vector3Int.zero);
            Assert.That(world.KeptChunks.ContainsKey(Vector3Int.zero), Is.True, "edits are kept when it unloads");
            Assert.That(world.IsLoaded(Vector3Int.zero), Is.False);
            Assert.That(world.TryRestoreKept(Vector3Int.zero, out Chunk back), Is.True);
            Assert.That(back.GetDensity(new Vector3Int(Size, 2, 2)), Is.EqualTo(0f));
        }

        [Test]
        public void ALayerBesideAnEdit_IsNotProven_SoItTakesTheEditedBorder()
        {
            World world = NewWorld();
            world.Load(Vector3Int.zero, null);
            world.SetDensity(new Vector3Int(Size, 2, 2), 1f);

            Assert.That(world.ProveColumn(Vector2Int.right, RockAndAir(), Layers), Is.EqualTo(1));

            Assert.That(world.IsProvenUniform(Vector3Int.right), Is.False, "layer 0 touches the edited chunk");
            Assert.That(world.IsProvenUniform(new Vector3Int(1, 3, 0)), Is.True, "layer 3 doesn't");
        }

        [Test]
        public void ForgettingAProof_TakesItsLayersOffTheCount()
        {
            World world = NewWorld();
            world.ProveColumn(Vector2Int.zero, RockAndAir(), Layers);
            world.TryGetChunk(Vector3Int.zero, out _);

            world.ForgetColumnProof(Vector2Int.zero);

            Assert.That(world.IsColumnProven(Vector2Int.zero), Is.False);
            Assert.That(world.IsLoaded(new Vector3Int(0, 3, 0)), Is.False);
            Assert.That(world.LoadedLayersIn(Vector2Int.zero), Is.EqualTo(1), "the chunk that was made is still there");
            world.Unload(Vector3Int.zero);
            Assert.That(world.LoadedColumns.ContainsKey(Vector2Int.zero), Is.False);
        }

        // ---- The proof ----------------------------------------------------------------------

        [Test]
        public void EveryProvenLayer_IsWhatGeneratingItGives()
        {
            VoxelMaterial stone = VoxelMaterial.Create(RockId, "Stone");
            VoxelMaterial dirt = VoxelMaterial.Create(1, "Dirt");
            VoxelMaterial copper = VoxelMaterial.Create(3, "Copper", VoxelMaterialCategory.Ore);
            VoxelMaterial wood = VoxelMaterial.Create(11, "Wood", VoxelMaterialCategory.Plant);
            VoxelMaterial leaves = VoxelMaterial.Create(12, "Leaves", VoxelMaterialCategory.Plant);
            try
            {
                var layers = new TerrainLayers { Band = dirt, TopDepth = 0f, BandDepth = 3f, Base = stone };
                var generator = new FractalPerlin2DGenerator(3, 40f, 6f, 0.05f, octaves: 3);
                var oreSettings = new OreGeneration { CellSize = 16f };
                oreSettings.Ores.Add(new OreSpec { Ore = copper, NodesPerCell = 0.6f, MinDepth = 3f, MaxDepth = 14f, Spread = new Vector3(2f, 1.5f, 2f) });
                var ores = new OreField(oreSettings, 7, generator);
                var trees = new TreeField(new TreeGeneration { Chance = 0.6f, CellSize = 10f, Wood = wood, Leaves = leaves }, 7, generator);

                World world = NewWorld();
                var pipeline = new ChunkPipeline(world.Grid, generator, layers, ores, world.PreferredFormat, world.StorageFactory, trees);
                int air = 0, rock = 0, left = 0;
                try
                {
                    for (int z = -2; z <= 2; z++)
                    {
                        for (int x = -2; x <= 2; x++)
                        {
                            var column = new Vector2Int(x, z);
                            UniformLayers proof = Prove(pipeline, column);
                            for (int y = 0; y < Layers; y++)
                            {
                                if (!proof.IsUniform(y))
                                {
                                    left++;
                                    continue;
                                }
                                var coord = new Vector3Int(x, y, z);
                                Chunk chunk = ChunkGenerator.Generate(
                                    generator, ChunkSampleGrid.ForChunk(world.Grid, coord), world.StorageFactory, layers, ores, world.PreferredFormat, trees);
                                AssertAllSamples(chunk, proof.IsRock(y), proof.RockMaterial, coord);
                                chunk.Release();
                                air += proof.IsAir(y) ? 1 : 0;
                                rock += proof.IsRock(y) ? 1 : 0;
                            }
                        }
                    }
                }
                finally
                {
                    pipeline.Dispose();
                }
                Assert.That(air, Is.GreaterThan(0), "some sky was proven");
                Assert.That(rock, Is.GreaterThan(0), "some deep rock was proven");
                Assert.That(left, Is.GreaterThan(0), "the ground itself is not");
            }
            finally
            {
                Object.DestroyImmediate(stone);
                Object.DestroyImmediate(dirt);
                Object.DestroyImmediate(copper);
                Object.DestroyImmediate(wood);
                Object.DestroyImmediate(leaves);
            }
        }

        // ---- Streaming, end to end ----------------------------------------------------------

        [Test]
        public void TheStreamer_SkipsProvenLayers_AndStillSettles()
        {
            using (var sparse = new StreamedWorld(sparseChunks: true))
            {
                sparse.Settle(Vector3.zero);

                StreamingStats stats = sparse.Streamer.Stats;
                Assert.That(stats.Pending, Is.Zero);
                Assert.That(sparse.World.LoadedCount, Is.LessThan(stats.Wanted / 2), "most layers hold no chunk");
                Assert.That(sparse.World.CountProvenLayers(), Is.EqualTo(stats.Wanted - sparse.World.LoadedCount));
                Assert.That(sparse.Streamer.IsColumnSettled(Vector2Int.zero), Is.True);
                Assert.That(sparse.Pool.Active.Count, Is.GreaterThan(0));
            }
        }

        [Test]
        public void TheSurface_IsTheSame_WithAndWithoutSparseChunks()
        {
            using (var sparse = new StreamedWorld(sparseChunks: true))
            using (var full = new StreamedWorld(sparseChunks: false))
            {
                sparse.Settle(Vector3.zero);
                full.Settle(Vector3.zero);

                Assert.That(sparse.Pool.Active.Count, Is.EqualTo(full.Pool.Active.Count));
                foreach (KeyValuePair<Vector3Int, ChunkRenderer> entry in full.Pool.Active)
                {
                    Assert.That(sparse.Pool.TryGet(entry.Key, out ChunkRenderer other), Is.True, entry.Key.ToString());
                    Assert.That(other.Mesh.vertexCount, Is.EqualTo(entry.Value.Mesh.vertexCount), entry.Key.ToString());
                }
            }
        }

        [Test]
        public void DiggingIntoProvenRock_ShowsInTheSameFrame_AndMatchesAFullyLoadedWorld()
        {
            using (var sparse = new StreamedWorld(sparseChunks: true))
            using (var full = new StreamedWorld(sparseChunks: false))
            {
                sparse.Settle(Vector3.zero);
                full.Settle(Vector3.zero);
                var deep = new Vector3Int(0, 1, 0);
                Assert.That(sparse.World.IsProvenUniform(deep), Is.True, "the test digs where nothing is held");
                Assert.That(sparse.World.TryPeekChunk(deep, out _), Is.False);

                Vector3 centre = sparse.World.Grid.ChunkOrigin(deep) + Vector3.one * (Size * 0.5f);
                var brush = new BrushSettings(3f);
                sparse.World.ApplyBrush(centre, brush, BrushOperation.Remove);
                full.World.ApplyBrush(centre, brush, BrushOperation.Remove);
                sparse.Streamer.RebuildEdited(Vector3.zero);
                full.Streamer.RebuildEdited(Vector3.zero);

                Assert.That(sparse.Pool.TryGet(deep, out ChunkRenderer dug), Is.True, "the hole shows in the frame of the edit");
                Assert.That(full.Pool.TryGet(deep, out ChunkRenderer expected), Is.True);
                Assert.That(dug.Mesh.vertexCount, Is.EqualTo(expected.Mesh.vertexCount));
                Assert.That(sparse.World.IsEdited(deep), Is.True);

                for (int z = -Size; z <= 2 * Size; z += 2)
                {
                    for (int y = 0; y <= 3 * Size; y += 2)
                    {
                        for (int x = -Size; x <= 2 * Size; x += 2)
                        {
                            var sample = new Vector3Int(x, y, z);
                            Assert.That(sparse.World.GetDensity(sample), Is.EqualTo(full.World.GetDensity(sample)), sample.ToString());
                            Assert.That(sparse.World.GetMaterial(sample), Is.EqualTo(full.World.GetMaterial(sample)), sample.ToString());
                        }
                    }
                }
            }
        }

        [Test]
        public void MovingAway_DropsTheProofs_KeepingEdits_AndComingBackFindsThem()
        {
            using (var sparse = new StreamedWorld(sparseChunks: true))
            {
                sparse.Settle(Vector3.zero);
                var deep = new Vector3Int(0, 1, 0);
                var sample = new Vector3Int(3, Size + 3, 3);
                sparse.World.SetDensity(sample, 0f);

                sparse.Settle(new Vector3(10 * Size, 0f, 0f));

                Assert.That(sparse.World.IsColumnProven(Vector2Int.zero), Is.False);
                Assert.That(sparse.World.LoadedLayersIn(Vector2Int.zero), Is.Zero);
                Assert.That(sparse.World.KeptChunks.ContainsKey(deep), Is.True, "the dug chunk is kept");

                sparse.Settle(Vector3.zero);

                Assert.That(sparse.World.TryPeekChunk(deep, out Chunk back), Is.True, "and restored rather than proven again");
                Assert.That(back.GetDensity(new Vector3Int(3, 3, 3)), Is.EqualTo(0f));
                Assert.That(sparse.Streamer.Stats.Pending, Is.Zero);
            }
        }

        private static World NewWorld()
        {
            return new World(new Vector3Int(Size, Size, Size), 1f, samples => new ByteVoxelStorage(samples));
        }

        // Rock in layer 0, air in layer 3.
        private static UniformLayers RockAndAir()
        {
            var layers = new UniformLayers { AirMaterial = AirId, RockMaterial = RockId };
            layers.SetRock(0);
            layers.SetAir(3);
            return layers;
        }

        private static UniformLayers Prove(ChunkPipeline pipeline, Vector2Int column)
        {
            UniformLayers proof = default;
            for (int frame = 0; frame < 5000 && !pipeline.TryProveColumn(column, Layers, out proof); frame++)
            {
                pipeline.Flush();
                System.Threading.Thread.Sleep(1);
            }
            return proof;
        }

        private static void AssertAllSamples(Chunk chunk, bool rock, byte rockMaterial, Vector3Int coord)
        {
            Vector3Int count = chunk.SampleCount;
            for (int z = 0; z < count.z; z++)
            {
                for (int y = 0; y < count.y; y++)
                {
                    for (int x = 0; x < count.x; x++)
                    {
                        var local = new Vector3Int(x, y, z);
                        Assert.That(chunk.GetDensity(local), Is.EqualTo(rock ? 1f : 0f).Within(0.001f), $"{coord} sample {local}");
                        if (rock)
                        {
                            Assert.That(chunk.GetMaterial(local), Is.EqualTo(rockMaterial), $"{coord} sample {local}");
                        }
                    }
                }
            }
        }

        // A streamed world whose ground lies around layer 5 of 12, so there is deep rock under it and sky over it.
        private sealed class StreamedWorld : System.IDisposable
        {
            private readonly GameObject root;

            public StreamedWorld(bool sparseChunks)
            {
                root = new GameObject("Sparse Chunk Test");
                World = new World(new Vector3Int(Size, Size, Size), 1f, samples => new ByteVoxelStorage(samples));
                ITerrainGenerator generator = new FractalPerlin2DGenerator(3, 44f, 3f, 0.05f, octaves: 3);
                var pipeline = new ChunkPipeline(World.Grid, generator, null, null, World.PreferredFormat, World.StorageFactory);
                Pool = new ChunkRendererPool(root.transform, true, new Material[0]);
                Streamer = new WorldStreamer(World, pipeline, Pool, () => new ChunkMeshSettings(0.5f, 1f), new StreamingSettings
                {
                    RenderDistance = 2,
                    HeightInChunks = Layers,
                    FrameBudgetMilliseconds = 3f,
                    Colliders = true,
                    ColliderRadius = 10f,
                    MaxEditsPerFrame = 32,
                    SparseChunks = sparseChunks,
                });
            }

            public World World { get; }

            public ChunkRendererPool Pool { get; }

            public WorldStreamer Streamer { get; }

            public void Settle(Vector3 focus)
            {
                for (int frame = 0; frame < 5000; frame++)
                {
                    Streamer.Update(focus);
                    Streamer.RebuildEdited(focus);
                    StreamingStats stats = Streamer.Stats;
                    if (frame > 0 && stats.Pending == 0 && stats.Generating == 0 && stats.Meshing == 0 && stats.Baking == 0)
                    {
                        return;
                    }
                    System.Threading.Thread.Sleep(1);
                }
                Assert.Fail("The world never settled.");
            }

            public void Dispose()
            {
                Streamer.UnloadAll();
                Streamer.Dispose();
                Object.DestroyImmediate(root);
            }
        }
    }
}
