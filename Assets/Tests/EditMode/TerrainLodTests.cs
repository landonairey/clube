using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// The coarse voxel terrain (P7c): each ring always has the next coarser one under its edge,
    /// a coarse chunk's surface lies where the generator puts it and is still the top layer's
    /// material, and the builder shows every level and keeps up with a moving focus.
    /// </summary>
    public class TerrainLodTests
    {
        private const float ChunkWidth = 4f;
        private const int HalfChunks = 8;

        private VoxelMaterial stone;
        private VoxelMaterial dirt;
        private VoxelMaterial grass;

        [SetUp]
        public void MakeMaterials()
        {
            stone = VoxelMaterial.Create(0, "Stone");
            dirt = VoxelMaterial.Create(1, "Dirt");
            grass = VoxelMaterial.Create(2, "Grass");
        }

        [TearDown]
        public void DestroyMaterials()
        {
            Object.DestroyImmediate(stone);
            Object.DestroyImmediate(dirt);
            Object.DestroyImmediate(grass);
        }

        [Test]
        public void EachRing_LiesOnTheNextCoarserRingsChunks_WhereverTheFocusIs()
        {
            var random = new System.Random(5);
            for (int i = 0; i < 2000; i++)
            {
                var focus = new Vector2((float)(random.NextDouble() * 40000 - 20000), (float)(random.NextDouble() * 40000 - 20000));
                for (int level = 1; level < 9; level++)
                {
                    float fineWidth = ChunkWidth * TerrainLodRing.ScaleOf(level);
                    TerrainLodRing fine = TerrainLodRing.Around(focus, level, HalfChunks, fineWidth);
                    TerrainLodRing coarse = TerrainLodRing.Around(focus, level + 1, HalfChunks, fineWidth * 2f);
                    float fineVoxel = fineWidth / 16f;
                    Assert.That(coarse.Covers(fine, fineVoxel), $"level {level + 1} under level {level} at {focus}");
                    Assert.That(fine.Area.Contains(focus), "the focus is inside every ring");
                }
            }
        }

        [Test]
        public void Builds_TheSquare_LessItsFourMiddleColumns()
        {
            TerrainLodRing ring = TerrainLodRing.Around(new Vector2(13f, -27f), 2, HalfChunks, 16f);
            int built = 0;
            for (int z = ring.First.y - 2; z < ring.First.y + ring.Width + 2; z++)
            {
                for (int x = ring.First.x - 2; x < ring.First.x + ring.Width + 2; x++)
                {
                    if (ring.Builds(new Vector2Int(x, z)))
                    {
                        built++;
                    }
                }
            }
            Assert.That(built, Is.EqualTo(ring.Width * ring.Width - 4));
            Assert.That(ring.Builds(ring.Centre), Is.False);
            Assert.That(ring.Builds(ring.First), Is.True);
        }

        [Test]
        public void ACoarseChunk_PutsTheSurfaceWhereTheGroundIs_AndGrassOnTop()
        {
            // 16 voxels of 4 m (level 4 of a 0.25 m world): the ramp widens with them, so the
            // interpolated surface still lands on the height; unscaled it would be off by metres.
            const float height = 37.3f;
            const float scale = 16f;
            var grid = new WorldGrid(new Vector3Int(16, 16, 16), 0.25f * scale);
            var layers = new TerrainLayers { Top = grass, TopDepth = 0.25f, Band = dirt, BandDepth = 3f, Base = stone };
            ChunkFillSettings settings = ChunkGenerator.SettingsFor(layers, null, DensityFormat.Float).ForLod(scale);
            var chunkGrid = ChunkSampleGrid.ForChunk(grid, Vector3Int.zero);
            var output = ChunkFillOutput.Allocate(chunkGrid.Length, DensityFormat.Float, Allocator.TempJob);
            var ores = new NativeArray<OreNodeData>(0, Allocator.TempJob);
            var trees = new NativeArray<TreePart>(0, Allocator.TempJob);
            try
            {
                ChunkGenerator.Schedule(new FlatGenerator(height), chunkGrid, settings, ores, trees, output).Complete();
                MiddleColumn(chunkGrid, out int x, out int z);
                float below = 0f;
                float above = 0f;
                int belowY = -1;
                for (int y = 0; y < chunkGrid.SampleCount.y - 1; y++)
                {
                    float a = output.Densities[chunkGrid.Index(x, y, z)];
                    float b = output.Densities[chunkGrid.Index(x, y + 1, z)];
                    if (a >= 0.5f && b < 0.5f)
                    {
                        below = a;
                        above = b;
                        belowY = y;
                    }
                }
                Assert.That(belowY, Is.GreaterThanOrEqualTo(0), "the surface crosses the chunk");
                float surface = (belowY + (below - 0.5f) / (below - above)) * grid.VoxelSize;
                Assert.That(surface, Is.EqualTo(height).Within(0.01f));
                Assert.That(output.Materials[chunkGrid.Index(x, belowY, z)], Is.EqualTo(grass.Id), "the solid sample under the surface is the top layer");
            }
            finally
            {
                output.Dispose();
                ores.Dispose();
                trees.Dispose();
            }
        }

        [Test]
        public void Builder_ShowsEveryLevel_AndFollowsTheFocus()
        {
            var sink = new RecordingSink();
            var world = new World(new Vector3Int(16, 16, 16), 0.25f, size => new ByteVoxelStorage(size));
            var layers = new TerrainLayers { Top = grass, TopDepth = 0.25f, Band = dirt, BandDepth = 3f, Base = stone };
            ChunkFillSettings fill = ChunkGenerator.SettingsFor(layers, null, DensityFormat.Byte);
            var mesh = new ChunkMeshSettings(0.5f, 0.25f, backend: MesherBackend.Burst, materialDisplay: MaterialDisplay.Blended);
            using (var builder = new TerrainLodBuilder(
                       new FlatGenerator(10.3f), world.Grid, fill, mesh, world.StorageFactory, 1, 3, 4, sink))
            {
                Settle(builder, Vector3.zero);
                for (int i = 0; i < builder.LevelCount; i++)
                {
                    Assert.That(builder.TryGetShown(i, out _), Is.True, $"level {i} shows");
                    builder.CountOf(i, out int drawn, out _);
                    Assert.That(drawn, Is.GreaterThan(0), $"level {i} draws chunks");
                }
                AssertShownInsideRings(builder, sink);

                // Far enough that every level moves.
                Settle(builder, new Vector3(301f, 0f, -177f));
                AssertShownInsideRings(builder, sink);
                Assert.That(sink.Shown.Count, Is.EqualTo(Drawn(builder)), "hidden chunks were handed back");
            }
        }

        private static void Settle(TerrainLodBuilder builder, Vector3 focus)
        {
            for (int frame = 0; frame < 5000; frame++)
            {
                builder.Update(focus);
                if (builder.IsSettled)
                {
                    return;
                }
                System.Threading.Thread.Sleep(1);
            }
            Assert.Fail("The builder never settled.");
        }

        private static int Drawn(TerrainLodBuilder builder)
        {
            int total = 0;
            for (int i = 0; i < builder.LevelCount; i++)
            {
                builder.CountOf(i, out int drawn, out _);
                total += drawn;
            }
            return total;
        }

        private static void AssertShownInsideRings(TerrainLodBuilder builder, RecordingSink sink)
        {
            foreach ((int level, Vector3Int coord) in sink.Shown)
            {
                int i = level - builder.FirstLevel;
                Assert.That(builder.TryGetShown(i, out TerrainLodRing ring), Is.True);
                Assert.That(ring.Builds(new Vector2Int(coord.x, coord.z)), Is.True, $"level {level} chunk {coord} lies in its ring");
            }
        }

        // The middle column of a chunk's samples.
        private static void MiddleColumn(ChunkSampleGrid grid, out int x, out int z)
        {
            x = grid.SampleCount.x / 2;
            z = grid.SampleCount.z / 2;
        }

        private sealed class RecordingSink : TerrainLodBuilder.ISink
        {
            public readonly HashSet<(int, Vector3Int)> Shown = new HashSet<(int, Vector3Int)>();

            public void Show(int level, Vector3Int coord, Mesh mesh)
            {
                Assert.That(Shown.Add((level, coord)), Is.True, $"{coord} shown twice");
                Assert.That(mesh, Is.Not.Null);
            }

            public void Hide(int level, Vector3Int coord)
            {
                Assert.That(Shown.Remove((level, coord)), Is.True, $"{coord} hidden without being shown");
            }
        }

        // P19: the first coarse level leaves out its middle 2 x 2 chunks for the world's to cover; a level may come
        // first only where that middle, wherever the focus puts it, lies within the world's reach. A fixed area
        // (reach 0) starts at level 1; render distance 64 at level 3, as before (P17).
        [Test]
        public void FirstLevel_LeavesNoGapBetweenTheWorldsEdgeAndItsMiddle()
        {
            Assert.AreEqual(1, TerrainLodView.FirstLevelFor(0, 8, 8));
            Assert.AreEqual(3, TerrainLodView.FirstLevelFor(64, 8, 8));
            for (int reach = 0; reach <= 64; reach++)
            {
                int first = TerrainLodView.FirstLevelFor(reach, 8, 8);
                if (first > 1)
                {
                    float middle = 2f * TerrainLodRing.ScaleOf(first) * Mathf.Sqrt(2f);
                    Assert.That(middle, Is.LessThan(reach), $"reach {reach}: level {first}'s middle reaches {middle} chunks");
                }
            }
        }
    }
}
