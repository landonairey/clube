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

        [Test]
        public void StampFields_CombineSkipsMissing()
        {
            Assert.IsNull(StampFields.Combine(null, null));
        }
    }
}
