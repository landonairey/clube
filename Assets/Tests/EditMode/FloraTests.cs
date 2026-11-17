using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    // GL234-GL238: how tall grass grows where, wheat fields, bushes' sizes and drops, and the save keeping picked bushes.
    public class FloraTests
    {
        [Test]
        public void Grass_GrowsUpToTwiceAsTall_InLowMeadows()
        {
            var growth = new GrassGrowth { ShorterWithAltitude = new Vector2(30f, 90f), MeadowGrowth = 1f };
            Assert.AreEqual(2f, growth.HeightScale(0f, 10f), 1e-5f, "Open meadow, low down: double.");
            Assert.AreEqual(1f, growth.HeightScale(1f, 10f), 1e-5f, "Deep forest: the base height.");
            Assert.AreEqual(1.5f, growth.HeightScale(0.5f, 10f), 1e-5f, "Between.");
            Assert.AreEqual(1f, growth.HeightScale(0f, 120f), 1e-5f, "High up: back to the base height.");
            Assert.AreEqual(1.5f, growth.HeightScale(0f, 60f), 1e-5f, "Gradually shorter on the way up.");
        }

        [Test]
        public void Wheat_GrowsOnlyInOpenLand_AndInPatches()
        {
            var growth = new GrassGrowth { WheatShare = 0.3f, WheatScale = 40f, WheatMaxForest = 0.2f };
            int open = 0, forest = 0, samples = 0;
            for (int x = 0; x < 100; x++)
            {
                for (int z = 0; z < 100; z++)
                {
                    float px = x * 4f, pz = z * 4f;
                    samples++;
                    if (growth.WheatAt(5, px, pz, 0f) > 0.5f)
                    {
                        open++;
                    }
                    if (growth.WheatAt(5, px, pz, 0.6f) > 0f)
                    {
                        forest++;
                    }
                }
            }
            Assert.AreEqual(0, forest, "No wheat among the trees.");
            Assert.Greater(open, samples / 20, "Some of the open land is wheat.");
            Assert.Less(open, samples * 0.6f, "But not all of it.");
            Assert.AreEqual(0f, new GrassGrowth { WheatShare = 0f }.WheatAt(5, 10f, 10f, 0f), "None with no share.");
        }

        [Test]
        public void Bushes_ComeInThreeSizes_MoreInForests()
        {
            var growth = new BushGrowth { FieldChance = 0.05f, ForestChance = 0.3f };
            int inField = 0, inForest = 0;
            var sizes = new HashSet<BushSize>();
            for (int x = 0; x < 60; x++)
            {
                for (int z = 0; z < 60; z++)
                {
                    var cell = new Vector2Int(x, z);
                    if (growth.TryGrow(3, cell, 0f, out _, out _))
                    {
                        inField++;
                    }
                    if (growth.TryGrow(3, cell, 1f, out BushSize size, out float height))
                    {
                        inForest++;
                        sizes.Add(size);
                        Vector2 range = growth.HeightRange(size);
                        Assert.That(height, Is.InRange(range.x, range.y));
                    }
                }
            }
            Assert.Greater(inForest, inField * 3, "Forests are bushier than fields.");
            Assert.AreEqual(3, sizes.Count, "Small, medium and large all grow.");
            Assert.Less(growth.HeightRange(BushSize.Small).y, growth.HeightRange(BushSize.Medium).x);
            Assert.Less(growth.HeightRange(BushSize.Medium).y, growth.HeightRange(BushSize.Large).x);
            Assert.GreaterOrEqual(growth.HeightRange(BushSize.Large).y, 1.8f, "The large ones stand as tall as the player.");
        }

        // A 60 x 20 m water map of 1 m cells (GL723): a fresh lake over x 0-19 (deepening 0.15 m a metre from its shore at x = 19),
        // dry land over x 20-39, and the sea over x 40-59 (its surface at the sea level, under the lake's).
        private static WaterMap LakeAndSea()
        {
            const int width = 60, depthCells = 20;
            int count = width * depthCells;
            var map = new WaterMap
            {
                Origin = Unity.Mathematics.float2.zero, CellSize = 1f, Size = new Unity.Mathematics.int2(width, depthCells),
                Carve = new Unity.Collections.NativeArray<float>(count, Unity.Collections.Allocator.Persistent),
                Surface = new Unity.Collections.NativeArray<float>(count, Unity.Collections.Allocator.Persistent),
                Depth = new Unity.Collections.NativeArray<float>(count, Unity.Collections.Allocator.Persistent),
                Kind = new Unity.Collections.NativeArray<byte>(count, Unity.Collections.Allocator.Persistent),
                Flow = new Unity.Collections.NativeArray<Unity.Mathematics.float2>(count, Unity.Collections.Allocator.Persistent),
                LakeDistance = new Unity.Collections.NativeArray<byte>(count, Unity.Collections.Allocator.Persistent),
                RiverDistance = new Unity.Collections.NativeArray<byte>(count, Unity.Collections.Allocator.Persistent),
                HasSea = true, SeaLevel = 0f,
            };
            for (int z = 0; z < depthCells; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = x + z * width;
                    map.RiverDistance[i] = WaterMap.MaxDistance;
                    if (x < 20)
                    {
                        map.Kind[i] = (byte)WaterKind.Lake;
                        map.Surface[i] = 5f;
                        map.Depth[i] = (20 - x) * 0.15f;
                    }
                    else if (x >= 40)
                    {
                        map.Kind[i] = (byte)WaterKind.Lake;
                        map.Surface[i] = 0f;
                        map.Depth[i] = 2f;
                    }
                    else
                    {
                        map.LakeDistance[i] = (byte)Mathf.Min(WaterMap.MaxDistance, Mathf.Min(x - 19, 40 - x));
                    }
                }
            }
            return map;
        }

        [Test]
        public void Reeds_GrowByFreshWaterAndInItsShallows_NeverByTheSea_InBedsAlongTheBank()
        {
            WaterMap water = LakeAndSea();
            try
            {
                var reeds = new ReedGrowth();
                Assert.That(reeds.ShoreAt(water, 18.5f, 10.5f), Is.GreaterThan(0.5f), "In the lake's shallows (0.3 m).");
                Assert.That(reeds.ShoreAt(water, 20.5f, 10.5f), Is.GreaterThan(0.5f), "On its bank.");
                Assert.That(reeds.ShoreAt(water, 10.5f, 10.5f), Is.EqualTo(0f), "Not out in deep water (1.5 m).");
                Assert.That(reeds.ShoreAt(water, 26.5f, 10.5f), Is.EqualTo(0f), "Not far up the bank.");
                Assert.That(reeds.ShoreAt(water, 39.5f, 10.5f), Is.EqualTo(0f), "Not on the sea's shore.");
                Assert.That(reeds.ShoreAt(water, 40.5f, 10.5f), Is.EqualTo(0f), "Not in the sea.");
                Assert.That(reeds.ShoreAt(WaterMap.None, 20.5f, 10.5f), Is.EqualTo(0f), "Nowhere without water.");

                // Thick beds where the bank is all reed bed, a few clumps where none is.
                int Clumps(float cover)
                {
                    reeds.BedCover = cover;
                    int grown = 0;
                    for (int x = 16; x < 23; x++)
                    {
                        for (int z = 0; z < 20; z++)
                        {
                            if (reeds.TryGrow(5, new Vector2Int(x, z), water, x + 0.5f, z + 0.5f, out float tall))
                            {
                                grown++;
                                Assert.That(tall, Is.InRange(reeds.Height.x, reeds.Height.y));
                            }
                        }
                    }
                    return grown;
                }
                int beds = Clumps(1f), bare = Clumps(0f);
                Assert.That(beds, Is.GreaterThan(40), "A reed bed along the shore.");
                Assert.That(bare, Is.LessThan(beds / 5), "Few where the bank has no bed.");
                int picked = reeds.ReedsOf(5, new Vector2Int(3, 4));
                Assert.That(picked, Is.InRange(3, 6));
            }
            finally
            {
                water.Dispose();
            }
        }

        [Test]
        public void Bushes_GatherIntoGroups_AndTheTallOnesTopThePlayer()
        {
            // GL722: groups of bushes, with open ground between, about as many on the whole; large ones up to 2.6 m.
            var grouped = new BushGrowth { FieldChance = 0.1f, ForestChance = 0.1f };
            var even = new BushGrowth { FieldChance = 0.1f, ForestChance = 0.1f, Grouping = 0f };
            const int size = 200;
            var grid = new bool[size, size];
            int groupedCount = 0, evenCount = 0;
            for (int x = 0; x < size; x++)
            {
                for (int z = 0; z < size; z++)
                {
                    grid[x, z] = grouped.TryGrow(3, new Vector2Int(x, z), 0f, out _, out _);
                    groupedCount += grid[x, z] ? 1 : 0;
                    evenCount += even.TryGrow(3, new Vector2Int(x, z), 0f, out _, out _) ? 1 : 0;
                    Assert.That(grouped.GroupFactor(3, new Vector2Int(x, z)), Is.InRange(0.1f, 4.5f));
                }
            }
            Assert.That(groupedCount, Is.InRange(evenCount * 0.6f, evenCount * 1.6f), "About as many bushes on the whole.");

            // A bush's neighbours are bushes far more often than any cell is.
            int pairs = 0, neighbours = 0;
            for (int x = 1; x < size - 1; x++)
            {
                for (int z = 1; z < size - 1; z++)
                {
                    if (!grid[x, z])
                    {
                        continue;
                    }
                    pairs += 4;
                    neighbours += (grid[x + 1, z] ? 1 : 0) + (grid[x - 1, z] ? 1 : 0) + (grid[x, z + 1] ? 1 : 0) + (grid[x, z - 1] ? 1 : 0);
                }
            }
            float everywhere = groupedCount / (float)(size * size);
            Assert.That(neighbours / (float)pairs, Is.GreaterThan(everywhere * 1.6f), "Bushes stand in groups.");
            Assert.That(new BushGrowth().HeightRange(BushSize.Large).y, Is.GreaterThanOrEqualTo(2.5f), "The large ones stand taller than the player.");
        }

        [Test]
        public void Bushes_DropLeavesAndAStickOrTwo_MoreWhenBigger()
        {
            var growth = new BushGrowth();
            int lastLeaves = 0;
            foreach (BushSize size in new[] { BushSize.Small, BushSize.Medium, BushSize.Large })
            {
                for (int x = 0; x < 20; x++)
                {
                    growth.DropsOf(1, new Vector2Int(x, 0), size, out int leaves, out int sticks);
                    Assert.That(sticks, Is.InRange(1, 2));
                    Assert.Greater(leaves, lastLeaves);
                }
                growth.DropsOf(1, Vector2Int.zero, size, out lastLeaves, out _);
            }
        }

        [Test]
        public void BushMeshes_StandOnTheirFoot_OneUnitTall()
        {
            foreach (BushSize size in new[] { BushSize.Small, BushSize.Medium, BushSize.Large })
            {
                Mesh mesh = BushMeshes.Build(size);
                try
                {
                    Bounds bounds = mesh.bounds;
                    Assert.AreEqual(0f, bounds.min.y, 0.05f, $"{size}: on its foot.");
                    Assert.That(bounds.max.y, Is.InRange(0.85f, 1.15f), $"{size}: about one unit tall.");
                    Assert.AreEqual(mesh.vertexCount, mesh.colors.Length);
                }
                finally
                {
                    Object.DestroyImmediate(mesh);
                }
            }
        }

        [Test]
        public void PickedBushes_KeptInTheSave()
        {
            var save = new GameSave();
            save.RemovedBushes.Add(new Vector2Int(3, -7));
            save.RemovedBushes.Add(new Vector2Int(-120, 44));
            using var stream = new MemoryStream();
            GameSaveFile.Write(stream, save);
            stream.Position = 0;
            GameSave read = GameSaveFile.Read(stream);
            CollectionAssert.AreEqual(save.RemovedBushes, read.RemovedBushes);
        }
    }
}
