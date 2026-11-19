using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class FadingWallTests
    {
        [Test]
        public void EachStep_RisesTheHeight_FromTheGroundLine_AndFadesToNothingAtTheTop()
        {
            var line = new List<Vector3> { new Vector3(0f, 2f, 0f), new Vector3(1f, 3f, 0f), new Vector3(2f, 2.5f, 0f) };
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            var yellow = new Color(1f, 0.8f, 0.1f, 0.45f);

            FadingWall.Add(line, 15f, yellow, vertices, colors, triangles);

            Assert.AreEqual(8, vertices.Count, "four vertices a step");
            Assert.AreEqual(24, triangles.Count, "two triangles a side, both sides");
            Assert.AreEqual(new Vector3(1f, 18f, 0f), vertices[2], "the top stands 15 m over its ground point");
            Assert.AreEqual(0.45f, colors[0].a, 1e-6f, "the bottom keeps the colour");
            Assert.AreEqual(0f, colors[2].a, 1e-6f, "the top fades to nothing");
            Assert.AreEqual(1f, colors[2].r, 1e-6f, "still yellow as it fades");
        }

        [Test]
        public void EveryQuad_IsWoundBothWays_SoItShowsFromEitherSide()
        {
            var line = new List<Vector3> { Vector3.zero, Vector3.right };
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            FadingWall.Add(line, 1f, Color.yellow, vertices, new List<Color>(), triangles);

            Vector3 Normal(int t) => Vector3.Cross(vertices[triangles[t + 1]] - vertices[triangles[t]], vertices[triangles[t + 2]] - vertices[triangles[t]]).normalized;
            Assert.AreEqual(-1f, Vector3.Dot(Normal(0), Normal(6)), 1e-5f);
        }
    }
}
