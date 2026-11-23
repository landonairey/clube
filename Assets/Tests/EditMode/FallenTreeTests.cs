using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>Fallen trees (GL505) and river debris (GL504).</summary>
    public class FallenTreeTests
    {
        [Test]
        public void IsFallen_IsDeterministic()
        {
            var settings = new TreeGeneration();
            for (int i = 0; i < 50; i++)
            {
                var cell = new Vector2Int(i, -i);
                Assert.AreEqual(TreeForests.IsFallen(7, cell, settings), TreeForests.IsFallen(7, cell, settings));
            }
        }

        [Test]
        public void RiverDebris_OnADryWorld_HasNothing()
        {
            var debris = new RiverDebris(3, new FlatGenerator(10f), 1, 2);
            var parts = new List<TreePart>();
            debris.CollectParts(new Bounds(Vector3.zero, new Vector3(200f, 100f, 200f)), parts, 1f);
            Assert.AreEqual(0, parts.Count);
        }

        // GL823: on bumpy sloping land the trunk lies straight (every segment along one line), down the slope.
        [Test]
        public void AFallenTrunk_OnBumpyLand_LiesStraight_DownTheSlope()
        {
            VoxelMaterial wood = VoxelMaterial.Create(11, "Wood", VoxelMaterialCategory.Plant);
            VoxelMaterial leaves = VoxelMaterial.Create(12, "Leaves", VoxelMaterialCategory.Plant);
            var settings = new TreeGeneration { Wood = wood, Leaves = leaves };
            // Falls towards +x at 0.3 m a metre, with 0.6 m bumps.
            System.Func<float, float, float> ground = (x, z) => 20f - 0.3f * x + 0.6f * Mathf.Sin(x * 1.7f) * Mathf.Cos(z * 1.3f);
            for (int i = 0; i < 12; i++)
            {
                var parts = new List<TreePart>();
                var cell = new Vector2Int(i, 3 * i);
                TreeShape.BuildFallen(7, cell, new float3(0f, ground(0f, 0f), 0f), settings, ground, parts);
                var trunk = parts.GetRange(1, parts.Count - 1);
                Assert.That(trunk.Count, Is.GreaterThanOrEqualTo(2));
                float3 along = math.normalize(trunk[trunk.Count - 1].To - trunk[0].From);
                foreach (TreePart part in trunk)
                {
                    float3 step = math.normalize(part.To - part.From);
                    Assert.That(math.dot(step, along), Is.GreaterThan(0.9999f), "every segment on one line");
                }
                Assert.That(along.x, Is.GreaterThan(0.9f), "down the slope");
                Assert.That(along.y, Is.LessThan(0f), "it falls with the land");
            }
            Object.DestroyImmediate(wood);
            Object.DestroyImmediate(leaves);
        }

        [Test]
        public void FitLine_OnAPlane_FindsItsRise()
        {
            System.Func<float, float, float> ground = (x, z) => 5f + 0.5f * z;
            TreeShape.FitLine(new float3(0f, 0f, 0f), new float3(0f, 0f, 1f), 2f, 10f, ground, out float start, out float rise);
            Assert.That(start, Is.EqualTo(6f).Within(1e-3f));
            Assert.That(rise, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void StampFields_CombineSkipsMissing()
        {
            Assert.IsNull(StampFields.Combine(null, null));
        }
    }
}
