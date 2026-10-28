using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// Forests and open fields (GL164) and tree ages (GL165): <see cref="TreeForests"/> splits the land
    /// near the share asked for, cells take the forest's or the field's chance, and every tree's size
    /// is in range, with deep forest older on average. With the defaults, nothing changes.
    /// </summary>
    public class TreeForestTests
    {
        private const int Seed = 5;

        private static TreeGeneration Forested(float cover)
        {
            return new TreeGeneration
            {
                Chance = 0.8f, CellSize = 10f, ForestCover = cover, ForestScale = 200f, FieldChance = 0.02f, YoungestSize = 0.4f,
            };
        }

        [Test]
        public void Defaults_KeepForestEverywhere_AndTreesFullGrown()
        {
            var settings = new TreeGeneration { Chance = 0.4f };
            for (int i = 0; i < 50; i++)
            {
                var cell = new Vector2Int(i * 7 - 100, i * 13 - 300);
                Assert.That(TreeForests.ChanceIn(Seed, cell, settings), Is.EqualTo(0.4f));
                Assert.That(TreeForests.SizeOf(Seed, cell, settings), Is.EqualTo(1f));
            }
        }

        [Test]
        public void Cover_IsRoughlyTheShareOfForest([Values(0.25f, 0.5f, 0.75f)] float cover)
        {
            TreeGeneration settings = Forested(cover);
            int forest = 0;
            int samples = 0;
            for (int z = 0; z < 120; z++)
            {
                for (int x = 0; x < 120; x++)
                {
                    samples++;
                    forest += TreeForests.ForestAt(Seed, settings, x * 50f, z * 50f) > 0.5f ? 1 : 0;
                }
            }
            Assert.That(forest / (float)samples, Is.EqualTo(cover).Within(0.12f));
        }

        [Test]
        public void Cells_TakeTheForestsChance_OrTheFields()
        {
            TreeGeneration settings = Forested(0.5f);
            var chances = new List<float>();
            for (int z = 0; z < 80; z++)
            {
                for (int x = 0; x < 80; x++)
                {
                    float chance = TreeForests.ChanceIn(Seed, new Vector2Int(x, z), settings);
                    Assert.That(chance, Is.InRange(settings.FieldChance, settings.Chance));
                    chances.Add(chance);
                }
            }
            Assert.That(chances.Exists(c => Mathf.Approximately(c, settings.Chance)), Is.True, "some deep forest");
            Assert.That(chances.Exists(c => Mathf.Approximately(c, settings.FieldChance)), Is.True, "some open field");
        }

        [Test]
        public void Sizes_RunFromTheYoungestToFullGrown_OlderInTheForest()
        {
            TreeGeneration settings = Forested(0.5f);
            float forestSum = 0f, fieldSum = 0f;
            int forestCount = 0, fieldCount = 0;
            for (int z = 0; z < 150; z++)
            {
                for (int x = 0; x < 150; x++)
                {
                    var cell = new Vector2Int(x, z);
                    float size = TreeForests.SizeOf(Seed, cell, settings);
                    Assert.That(size, Is.InRange(settings.YoungestSize, 1f));
                    float forest = TreeForests.ForestAt(Seed, settings, (x + 0.5f) * 10f, (z + 0.5f) * 10f);
                    if (forest > 0.99f)
                    {
                        forestSum += size;
                        forestCount++;
                    }
                    else if (forest < 0.01f)
                    {
                        fieldSum += size;
                        fieldCount++;
                    }
                }
            }
            Assert.That(forestCount, Is.GreaterThan(100));
            Assert.That(fieldCount, Is.GreaterThan(100));
            Assert.That(forestSum / forestCount, Is.GreaterThan(fieldSum / fieldCount + 0.05f), "deep forest leans old");
        }

        [Test]
        public void AYoungTree_IsSmallerAllOver()
        {
            var wood = VoxelMaterial.Create(11, "Wood", VoxelMaterialCategory.Plant);
            var leaves = VoxelMaterial.Create(12, "Leaves", VoxelMaterialCategory.Plant);
            try
            {
                TreeGeneration settings = Forested(1f);
                settings.Wood = wood;
                settings.Leaves = leaves;
                settings.MinBranchRadius = 0.05f;
                // The youngest and oldest trees among some cells: the young one's parts reach less high.
                Vector2Int youngest = default, oldest = default;
                float least = 2f, most = 0f;
                for (int i = 0; i < 400; i++)
                {
                    var cell = new Vector2Int(i, 3);
                    float size = TreeForests.SizeOf(Seed, cell, settings);
                    if (size < least) { least = size; youngest = cell; }
                    if (size > most) { most = size; oldest = cell; }
                }
                Assert.That(least, Is.LessThan(0.6f));
                Assert.That(TopOf(youngest, settings), Is.LessThan(TopOf(oldest, settings) * 0.75f));
                Assert.That(TreeShape.HeightOf(Seed, youngest, settings), Is.LessThan(settings.Height.y * 0.6f));
            }
            finally
            {
                Object.DestroyImmediate(wood);
                Object.DestroyImmediate(leaves);
            }
        }

        private static float TopOf(Vector2Int cell, TreeGeneration settings)
        {
            var parts = new List<TreePart>();
            TreeShape.Build(Seed, cell, Unity.Mathematics.float3.zero, settings, parts);
            float top = 0f;
            foreach (TreePart part in parts)
            {
                top = Mathf.Max(top, part.Bounds(0f).max.y);
            }
            return top;
        }
    }
}
