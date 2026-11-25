using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>The hand axe's model (GL875) and the iron bloom's (GL876).</summary>
    public class HandAxeBloomMeshTests
    {
        private const float Length = 0.2f;

        private static Vector3[] HandAxe(Color stone)
        {
            var model = new LowPolyMeshBuilder();
            HandAxeMesh.Add(model, Length, stone);
            Mesh mesh = model.Make("Hand axe");
            Vector3[] vertices = mesh.vertices;
            Object.DestroyImmediate(mesh);
            return vertices;
        }

        // The widest and thickest the model is within a band of heights (fractions of the length).
        private static Vector2 Extent(Vector3[] vertices, float from, float to)
        {
            Vector3[] band = vertices.Where(v => v.y >= from * Length && v.y <= to * Length).ToArray();
            return new Vector2(band.Max(v => Mathf.Abs(v.x)), band.Max(v => Mathf.Abs(v.z)));
        }

        [Test]
        public void HandAxeRunsFromButtToWorkingEnd()
        {
            Vector3[] vertices = HandAxe(Color.grey);
            Assert.That(vertices.Min(v => v.y), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(vertices.Max(v => v.y), Is.EqualTo(Length).Within(1e-4f));
        }

        [Test]
        public void HandAxeIsThickAndRoundAtTheButtAndBluntAtTheWorkingEnd()
        {
            Vector3[] vertices = HandAxe(Color.grey);
            Vector2 butt = Extent(vertices, 0.1f, 0.35f), point = Extent(vertices, 0.8f, 1f);
            Assert.That(point.x, Is.GreaterThan(butt.x * 0.65f), "GL884: still broad near the tip, a rounded arc and no spike");
            Assert.That(point.y, Is.GreaterThan(butt.y * 0.25f), "and not a needle edge-on");
            Assert.That(HandAxeMesh.HalfWidth(0.6f), Is.GreaterThan(0.85f), "most of its width well up the working end");
            Assert.That(butt.y, Is.GreaterThan(butt.x * 0.3f), "a thick butt to hold");
            // The butt's end is blunt: the rim there has thickness, where the sides meet in an edge.
            Assert.That(Extent(vertices, 0f, 0.02f).y, Is.GreaterThan(Length * 0.05f));
        }

        [Test]
        public void HandAxeTakesItsColourFromTheOneGiven()
        {
            var model = new LowPolyMeshBuilder();
            var bone = new Color(0.85f, 0.8f, 0.68f);
            HandAxeMesh.Add(model, Length, bone);
            Mesh mesh = model.Make("Bone hand axe");
            Color[] colours = mesh.colors;
            Object.DestroyImmediate(mesh);
            Assert.That(colours.Select(c => c.ToString()).Distinct().Count(), Is.GreaterThan(4), "flake scars shaded apart");
            float mean = colours.Average(c => c.r);
            Assert.That(mean, Is.EqualTo(bone.r).Within(0.2f));
        }

        [Test]
        public void BloomIsALumpyMassWithDarkPores()
        {
            var iron = new Color(0.39f, 0.4f, 0.43f);
            Mesh mesh = ItemChunkMeshes.Build(ItemIconShape.Bloom, iron);
            Assert.That(mesh, Is.Not.Null);
            Color[] colours = mesh.colors;
            Bounds bounds = mesh.bounds;
            Assert.That(mesh.triangles.Length / 3, Is.LessThanOrEqualTo(800), "low-poly, as every item model");
            Object.DestroyImmediate(mesh);
            Assert.That(bounds.size.x, Is.InRange(0.6f, 1.2f), "about a unit across, like the other item models");
            Assert.That(bounds.min.y, Is.GreaterThan(-0.15f), "stands on the ground");
            Assert.That(colours.Any(c => c.r < iron.r * 0.3f), "near-black pores");
            Assert.That(colours.Any(c => c.r > c.b * 1.3f), "rusty slag");
        }
    }
}
