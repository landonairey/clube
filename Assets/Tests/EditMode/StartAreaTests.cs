using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>The start area (2026-10-13): generation tops it up with trees and clay when the seed gave none.</summary>
    public class StartAreaTests
    {
        private static StartArea Area(int minTrees, bool clay)
        {
            var area = new StartArea { Enabled = true, Area = new Rect(-32f, 0f, 32f, 32f), MinTrees = minTrees, Clay = clay };
            return area;
        }

        [Test]
        public void TreeField_GrowsTheMinimumOfTrees_WhenTheSeedGaveNone()
        {
            VoxelMaterial wood = VoxelMaterial.Create(11, "Wood", VoxelMaterialCategory.Plant);
            VoxelMaterial leaves = VoxelMaterial.Create(12, "Leaves", VoxelMaterialCategory.Plant);
            try
            {
                // No chance of a tree anywhere, flat ground.
                var settings = new TreeGeneration { Chance = 0f, CellSize = 10f, Wood = wood, Leaves = leaves };
                var generator = new FlatGenerator(20f);
                StartArea start = Area(2, false);

                var without = new TreeField(settings, 7, generator);
                var with = new TreeField(settings, 7, generator, start);
                Assert.AreEqual(0, RootedIn(without, start.Area));
                Assert.AreEqual(2, RootedIn(with, start.Area));
                Assert.AreEqual(2, RootedIn(new TreeField(settings, 7, generator, start), start.Area), "The same trees every time.");
            }
            finally
            {
                Object.DestroyImmediate(wood);
                Object.DestroyImmediate(leaves);
            }
        }

        [Test]
        public void LayerTable_AddsAClayDeposit_OnlyWhenNoneReachesTheArea()
        {
            VoxelMaterial clay = VoxelMaterial.Create(13, "Clay");
            VoxelMaterial stone = VoxelMaterial.Create(0, "Stone");
            try
            {
                var layers = new TerrainLayers { Base = stone, Clay = clay, ClayChance = 0f, ClayRadius = 3f, ClayDepth = 0.75f, ClayCellSize = 24f };
                StartArea start = Area(0, true);

                LayerTable table = LayerTable.From(layers, start, 7);
                Assert.Greater(table.StartClayRadius, 0f, "No clay from the seed: one is added.");
                Assert.Greater(ChunkFillKernel.ClayDepthAt(7, new float2(start.Area.center.x, start.Area.center.y), table), 0f);

                layers.ClayChance = 1f;
                Assert.AreEqual(0f, LayerTable.From(layers, start, 7).StartClayRadius, "Clay everywhere: nothing added.");
                Assert.AreEqual(0f, LayerTable.From(layers, null, 7).StartClayRadius);
            }
            finally
            {
                Object.DestroyImmediate(clay);
                Object.DestroyImmediate(stone);
            }
        }

        private static int RootedIn(TreeField trees, Rect area)
        {
            int count = 0;
            float size = trees.CellSize;
            for (int z = Mathf.FloorToInt(area.yMin / size); z <= Mathf.FloorToInt((area.yMax - 0.01f) / size); z++)
            {
                for (int x = Mathf.FloorToInt(area.xMin / size); x <= Mathf.FloorToInt((area.xMax - 0.01f) / size); x++)
                {
                    if (trees.TryGetTree(new Vector2Int(x, z), out Vector3 root, out _) && area.Contains(new Vector2(root.x, root.z)))
                    {
                        count++;
                    }
                }
            }
            return count;
        }
    }
}
