using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>Clay deposits and clay drying into brick (GL34).</summary>
    public class ClayTests
    {
        private static LayerTable Layers(float chance = 1f)
        {
            return new LayerTable
            {
                Clay = 13,
                ClayChance = chance,
                ClayCellSize = 24f,
                ClayRadius = 3f,
                ClayDepth = 0.75f,
            };
        }

        [Test]
        public void ClayDepth_IsALens_DeepestInTheMiddle_AndZeroOutside()
        {
            LayerTable layers = Layers();
            int found = 0;
            for (float x = 0f; x < 96f; x += 0.25f)
            {
                for (float z = 0f; z < 96f; z += 0.25f)
                {
                    float depth = ChunkFillKernel.ClayDepthAt(7, new float2(x, z), layers);
                    Assert.That(depth, Is.InRange(0f, layers.ClayDepth));
                    if (depth > 0f)
                    {
                        found++;
                    }
                }
            }
            Assert.That(found, Is.GreaterThan(0), "every cell has a deposit at chance 1");
            Assert.AreEqual(0f, ChunkFillKernel.ClayDepthAt(7, new float2(10f, 10f), Layers(0f)), "no clay at chance 0");
        }

        [Test]
        public void ClayDepth_IsTheSameEveryTime_ForTheSameSeed()
        {
            LayerTable layers = Layers(0.5f);
            for (int i = 0; i < 200; i++)
            {
                var at = new float2(i * 1.37f, i * 0.91f);
                Assert.AreEqual(ChunkFillKernel.ClayDepthAt(3, at, layers), ChunkFillKernel.ClayDepthAt(3, at, layers));
            }
        }

        [Test]
        public void Drying_TurnsDroppedClayIntoBrick_OnlyOnceItsDaysHavePassed()
        {
            VoxelMaterial brick = VoxelMaterial.Create(14, "Brick");
            VoxelMaterial clay = VoxelMaterial.Create(13, "Clay").WithDrying(brick, 2f);
            var world = new World(new Vector3Int(8, 8, 8), 1f);
            world.Load(Vector3Int.zero, null);
            try
            {
                var kept = new Vector3Int(2, 1, 2);
                var dug = new Vector3Int(3, 1, 2);
                foreach (Vector3Int sample in new[] { kept, dug })
                {
                    world.SetMaterial(sample, clay.Id);
                    world.SetDensity(sample, 1f);
                }
                var drying = new GroundDrying();
                Assert.AreEqual(2, drying.Add(new List<Vector3Int> { kept, dug }, clay, now: 10.0));

                world.SetDensity(dug, 0f);
                Assert.AreEqual(0, drying.Update(world, 11.9));
                Assert.AreEqual(clay.Id, world.GetMaterial(kept));

                Assert.AreEqual(1, drying.Update(world, 12.0), "Only the clay still lying there dries.");
                Assert.AreEqual(brick.Id, world.GetMaterial(kept));
                Assert.AreEqual(0, drying.Count);
            }
            finally
            {
                Object.DestroyImmediate(clay);
                Object.DestroyImmediate(brick);
            }
        }

        [Test]
        public void Drying_IgnoresMaterialsThatDontDry()
        {
            VoxelMaterial stone = VoxelMaterial.Create(0, "Stone");
            try
            {
                Assert.AreEqual(0, new GroundDrying().Add(new[] { Vector3Int.one }, stone, 0.0));
            }
            finally
            {
                Object.DestroyImmediate(stone);
            }
        }
    }
}
