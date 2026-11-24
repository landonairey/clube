using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// GL862: round the capital on seed 391376 the trees stood in stretched horizontal stripes. The capital's ground was
    /// cut out of a wood whose crowns all touch, and the cut (a flood through plant samples) gave out part way: it ran depth
    /// first off a stack and stopped with the samples still on the stack standing between the lines it had cleared.
    /// </summary>
    public class WoodCutTests
    {
        private const float Iso = 0.5f;
        private readonly List<Object> made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object thing in made)
            {
                Object.DestroyImmediate(thing);
            }
            made.Clear();
        }

        private static bool Solid(World world, int x, int y, int z)
        {
            return (world.GetDensity(new Vector3Int(x, y, z)) ?? 0f) >= Iso;
        }

        [Test]
        public void ACutThatGivesOutInAWood_LeavesNoSheetsOfLeaves()
        {
            VoxelMaterial dirt = VoxelMaterial.Create(1, "Dirt"), leaves = VoxelMaterial.Create(2, "Leaves", VoxelMaterialCategory.Plant);
            made.Add(dirt);
            made.Add(leaves);
            MaterialRegistry registry = MaterialRegistry.Create(dirt, leaves);
            made.Add(registry);

            // 1 m voxels, ground up to 2.2 m; a canopy 11 samples deep over 93 x 93 m, far more than one cut takes, on two
            // trunks: one to cut at (20, 20), one that stands at (76, 76) and holds the rest of the wood up.
            var world = new World(new Vector3Int(16, 16, 16), 1f, samples => new ByteVoxelStorage(samples));
            var generator = new FlatGenerator(2.2f);
            for (int x = 0; x < 6; x++)
            {
                for (int z = 0; z < 6; z++)
                {
                    world.Load(new Vector3Int(x, 0, z), generator);
                    world.Load(new Vector3Int(x, 1, z), generator);
                }
            }
            void Leaf(int x, int y, int z)
            {
                var sample = new Vector3Int(x, y, z);
                world.SetDensity(sample, 1f);
                world.SetMaterial(sample, leaves.Id);
            }
            for (int y = 3; y < 12; y++)
            {
                Leaf(20, y, 20);
                Leaf(76, y, 76);
            }
            for (int z = 2; z <= 94; z++)
            {
                for (int y = 12; y <= 22; y++)
                {
                    for (int x = 2; x <= 94; x++)
                    {
                        Leaf(x, y, z);
                    }
                }
            }

            GroundLeveling.ClearTrees(world, registry, new Vector2(20f, 20f), new Vector2(1f, 1f), 2f, 6f, Iso);

            Assert.IsFalse(Solid(world, 20, 6, 20), "the cut tree's trunk goes");
            Assert.IsFalse(Solid(world, 20, 16, 20), "and its crown");
            Assert.IsTrue(Solid(world, 76, 6, 76), "the other tree stands");
            Assert.IsTrue(Solid(world, 76, 16, 76), "with the wood it holds up");
            int sheets = 0, left = 0;
            for (int z = 2; z <= 94; z++)
            {
                for (int y = 12; y <= 22; y++)
                {
                    for (int x = 2; x <= 94; x++)
                    {
                        if (!Solid(world, x, y, z))
                        {
                            continue;
                        }
                        left++;
                        // A leaf with air over and under it: a one-sample sheet, drawn as a stripe.
                        if (!Solid(world, x, y + 1, z) && !Solid(world, x, y - 1, z))
                        {
                            sheets++;
                        }
                    }
                }
            }
            Assert.That(left, Is.GreaterThan(0), "most of the wood is left");
            Assert.That(sheets, Is.Zero, $"one-sample sheets of leaves left standing (of {left} leaves)");
        }
    }
}
