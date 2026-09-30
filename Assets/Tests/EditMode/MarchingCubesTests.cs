using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class MarchingCubesTests
    {
        private const float Tolerance = 1e-5f;

        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();

        // NUnit reuses one fixture instance for every test in the class.
        [SetUp]
        public void ClearBuffers()
        {
            vertices.Clear();
            triangles.Clear();
        }

        [Test]
        public void AllEmpty_ProducesNoTriangles()
        {
            Polygonise(new float[8], isoLevel: 0.5f);

            Assert.That(triangles, Is.Empty);
        }

        [Test]
        public void AllSolid_ProducesNoTriangles()
        {
            Polygonise(new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f }, isoLevel: 0.5f);

            Assert.That(triangles, Is.Empty);
        }

        [Test]
        public void SingleSolidCorner_ProducesOneTriangle()
        {
            Polygonise(new[] { 1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f }, isoLevel: 0.5f);

            Assert.That(triangles.Count, Is.EqualTo(3));
            Assert.That(vertices.Count, Is.EqualTo(3));
        }

        [Test]
        public void SingleSolidCorner_VerticesAreInterpolatedAlongCornerEdges()
        {
            // Corner 0 = 1, its neighbours = 0, iso 0.25: the surface crosses each of
            // corner 0's three edges 0.75 of the way from corner 0.
            Polygonise(new[] { 1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f }, isoLevel: 0.25f);

            var expected = new[] { new Vector3(0.75f, 0f, 0f), new Vector3(0f, 0.75f, 0f), new Vector3(0f, 0f, 0.75f) };
            foreach (Vector3 point in expected)
            {
                Assert.That(vertices.Exists(v => (v - point).sqrMagnitude < Tolerance), $"No vertex at {point}.");
            }
        }

        [Test]
        public void SingleSolidCorner_TriangleFacesAwayFromSolid()
        {
            Polygonise(new[] { 1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f }, isoLevel: 0.5f);

            // Same normal Unity computes for a clockwise front face.
            Vector3 normal = Vector3.Cross(vertices[1] - vertices[0], vertices[2] - vertices[0]);
            Vector3 awayFromCorner0 = Vector3.one;
            Assert.That(Vector3.Dot(normal, awayFromCorner0), Is.GreaterThan(0f));
        }

        [Test]
        public void OriginAndSize_TransformVertices()
        {
            var origin = new Vector3(10f, 20f, 30f);
            MarchingCubes.Polygonise(
                new[] { 1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f }, 0.5f, origin, 2f, vertices, triangles);

            foreach (Vector3 v in vertices)
            {
                Vector3 local = (v - origin) / 2f;
                Assert.That(local.x + local.y + local.z, Is.EqualTo(0.5f).Within(Tolerance));
            }
        }

        private void Polygonise(float[] corners, float isoLevel)
        {
            MarchingCubes.Polygonise(corners, isoLevel, Vector3.zero, 1f, vertices, triangles);
        }
    }
}
