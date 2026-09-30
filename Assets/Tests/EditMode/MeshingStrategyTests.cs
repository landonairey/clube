using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class MeshingStrategyTests
    {
        private const float Tolerance = 1e-5f;

        [Test]
        public void InterpolatedPlacer_FindsIsoCrossing()
        {
            Vector3 point = InterpolatedEdgePlacer.Instance.Place(Vector3.zero, Vector3.right, 0f, 1f, 0.25f);

            Assert.That((point - new Vector3(0.25f, 0f, 0f)).sqrMagnitude, Is.LessThan(Tolerance));
        }

        [Test]
        public void InterpolatedPlacer_EqualValues_FallsBackToFirstCorner()
        {
            Vector3 point = InterpolatedEdgePlacer.Instance.Place(Vector3.zero, Vector3.right, 0.5f, 0.5f, 0.5f);

            Assert.That(point, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void MidpointPlacer_IgnoresValues()
        {
            Vector3 point = MidpointEdgePlacer.Instance.Place(Vector3.zero, Vector3.up, 0.9f, 0f, 0.1f);

            Assert.That((point - new Vector3(0f, 0.5f, 0f)).sqrMagnitude, Is.LessThan(Tolerance));
        }

        [Test]
        public void FlatWriter_AlwaysAddsVertex()
        {
            var vertices = new List<Vector3>();
            var writer = new FlatVertexWriter(vertices);

            int first = writer.Write(0, Vector3.zero);
            int second = writer.Write(0, Vector3.zero);

            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(vertices.Count, Is.EqualTo(2));
        }

        [Test]
        public void SharedWriter_ReusesVertexForSameEdgeInNeighbourVoxel()
        {
            var vertices = new List<Vector3>();
            var writer = new SharedVertexWriter(vertices, new Vector3Int(3, 2, 2));

            // Edge 1 of voxel (0,0,0) runs (1,0,0)-(1,0,1); edge 3 of voxel (1,0,0)
            // runs (1,0,1)-(1,0,0). Same grid edge, opposite direction.
            writer.BeginVoxel(Vector3Int.zero);
            int fromFirstVoxel = writer.Write(1, Vector3.zero);
            writer.BeginVoxel(new Vector3Int(1, 0, 0));
            int fromSecondVoxel = writer.Write(3, Vector3.zero);

            Assert.That(fromSecondVoxel, Is.EqualTo(fromFirstVoxel));
            Assert.That(vertices.Count, Is.EqualTo(1));
        }

        [Test]
        public void SharedWriter_KeepsDistinctEdgesApart()
        {
            var vertices = new List<Vector3>();
            var writer = new SharedVertexWriter(vertices, new Vector3Int(2, 2, 2));
            writer.BeginVoxel(Vector3Int.zero);

            var indices = new HashSet<int>();
            for (int edge = 0; edge < 12; edge++)
            {
                indices.Add(writer.Write(edge, Vector3.zero));
            }

            Assert.That(indices.Count, Is.EqualTo(12));
        }
    }
}
