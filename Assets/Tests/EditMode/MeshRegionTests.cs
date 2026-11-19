using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Core.Tests
{
    /// <summary>GL776: chunks drawn merged by region (<see cref="MeshMerge"/>, <see cref="MeshRegionSet"/>).</summary>
    public class MeshRegionTests
    {
        private readonly List<Object> made = new List<Object>();

        [TearDown]
        public void DestroyMade()
        {
            foreach (Object item in made)
            {
                Object.DestroyImmediate(item);
            }
            made.Clear();
        }

        [Test]
        public void Merge_CopiesEveryVertexMovedByItsOffset_AndRaisesTheIndices()
        {
            Mesh a = Quad(0f, materials: false);
            Mesh b = Quad(5f, materials: false);
            Mesh target = Make(new Mesh());
            var merge = new MeshMerge();
            Assert.That(merge.Add(target, new[] { a, b }, new[] { Vector3.zero, new Vector3(10f, 0f, -2f) }), Is.True);
            merge.Run();

            Assert.That(target.vertexCount, Is.EqualTo(8));
            Assert.That(target.indexFormat, Is.EqualTo(IndexFormat.UInt16));
            Vector3[] positions = target.vertices;
            Vector3[] normals = target.normals;
            Vector3[] expectA = a.vertices;
            Vector3[] expectB = b.vertices;
            for (int i = 0; i < 4; i++)
            {
                Assert.That(positions[i], Is.EqualTo(expectA[i]));
                Assert.That(positions[4 + i], Is.EqualTo(expectB[i] + new Vector3(10f, 0f, -2f)));
                Assert.That(normals[4 + i], Is.EqualTo(b.normals[i]));
            }
            int[] indices = target.GetIndices(0);
            int[] fromB = b.GetIndices(0);
            Assert.That(indices.Length, Is.EqualTo(12));
            for (int i = 0; i < 6; i++)
            {
                Assert.That(indices[6 + i], Is.EqualTo(fromB[i] + 4));
            }
            Assert.That(target.bounds.max.x, Is.EqualTo(11f).Within(1e-4f), "bounds take in the moved source");
            Assert.That(target.bounds.min.z, Is.EqualTo(-2f).Within(1e-4f));
        }

        [Test]
        public void Merge_KeepsTheMaterialAttributes_AndSkipsEmptySources()
        {
            Mesh a = Quad(0f, materials: true);
            Mesh empty = Make(new Mesh());
            Mesh target = Make(new Mesh());
            var merge = new MeshMerge();
            Assert.That(merge.Add(target, new[] { empty, a }, new[] { Vector3.one, Vector3.up }), Is.True);
            merge.Run();

            Assert.That(target.vertexCount, Is.EqualTo(4));
            var ids = new List<Vector3>();
            target.GetUVs(2, ids);
            var expected = new List<Vector3>();
            a.GetUVs(2, expected);
            Assert.That(ids, Is.EqualTo(expected));
            Assert.That(target.vertices[0], Is.EqualTo(a.vertices[0] + Vector3.up));
        }

        [Test]
        public void Merge_RefusesMixedLayouts_AndUsesWideIndicesPastSixtyFiveThousandVertices()
        {
            var merge = new MeshMerge();
            Assert.That(merge.Add(Make(new Mesh()), new[] { Quad(0f, false), Quad(0f, true) }, new[] { Vector3.zero, Vector3.zero }), Is.False);
            Assert.That(merge.Count, Is.Zero);

            Mesh big = Grid(200);
            Mesh target = Make(new Mesh());
            merge.Add(target, new[] { big, big }, new[] { Vector3.zero, Vector3.right * 300f });
            merge.Run();
            Assert.That(target.vertexCount, Is.EqualTo(2 * big.vertexCount));
            Assert.That(target.indexFormat, Is.EqualTo(IndexFormat.UInt32));
            int[] indices = target.GetIndices(0);
            Assert.That(indices[indices.Length - 1], Is.EqualTo(big.GetIndices(0)[indices.Length / 2 - 1] + big.vertexCount));
        }

        [Test]
        public void RegionSet_MergesEachBlockOfCells_DrawsByTheRule_AndLetsEmptyRegionsGo()
        {
            var parent = Make(new GameObject("Regions"));
            var regions = new MeshRegionSet(parent.transform, "Region", new Vector3Int(2, 2, 2), Vector3.one * 4f, cell => (Vector3)cell * 4f);
            Mesh quad = Quad(0f, false);
            regions.Set(new Vector3Int(0, 0, 0), quad);
            regions.Set(new Vector3Int(1, 0, 1), quad);
            regions.Set(new Vector3Int(2, 0, 0), quad);
            regions.Set(new Vector3Int(-1, 0, 0), quad);
            Assert.That(regions.RegionCount, Is.EqualTo(3), "(0..1), (2..3) and (-2..-1) along x");
            Assert.That(regions.RegionOf(new Vector3Int(-1, 0, 0)), Is.EqualTo(new Vector3Int(-1, 0, 0)));
            Assert.That(regions.DrawnCount, Is.Zero, "nothing draws before it's merged");

            Assert.That(regions.Build(Vector3.zero, 0f, float.PositiveInfinity, int.MaxValue), Is.EqualTo(3));
            Assert.That(regions.DrawnCount, Is.EqualTo(3));
            MeshFilter first = FilterOf(parent, "Region 0, 0, 0");
            Assert.That(first.sharedMesh.vertexCount, Is.EqualTo(8));
            Assert.That(first.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(first.sharedMesh.vertices[4], Is.EqualTo(quad.vertices[0] + new Vector3(4f, 0f, 4f)), "the second cell moved to its place");
            Assert.That(FilterOf(parent, "Region -1, 0, 0").transform.localPosition, Is.EqualTo(new Vector3(-8f, 0f, 0f)));

            regions.SetShownRule(region => region.x >= 0);
            Assert.That(regions.DrawnCount, Is.EqualTo(2));
            regions.SetVisible(new Vector3Int(2, 0, 0), false);
            Assert.That(regions.DrawnCount, Is.EqualTo(1), "a region with no visible cell is switched off");

            regions.Remove(new Vector3Int(1, 0, 1));
            Assert.That(regions.DirtyCount, Is.EqualTo(1));
            regions.Build(Vector3.zero, 0f, float.PositiveInfinity, int.MaxValue);
            Assert.That(first.sharedMesh.vertexCount, Is.EqualTo(4));
            regions.Remove(new Vector3Int(0, 0, 0));
            Assert.That(regions.RegionCount, Is.EqualTo(2));
            Assert.That(first.GetComponent<MeshRenderer>().enabled, Is.False, "an empty region stops drawing at once");
            regions.Dispose();
        }

        [Test]
        public void RegionSet_MergesNearRegionsAlways_AndFarOnesToTheBudget()
        {
            var parent = Make(new GameObject("Regions"));
            var regions = new MeshRegionSet(parent.transform, "Region", Vector3Int.one, Vector3.one * 4f, cell => (Vector3)cell * 4f);
            Mesh quad = Quad(0f, false);
            for (int x = 0; x < 10; x++)
            {
                regions.Set(new Vector3Int(x, 0, 0), quad);
            }
            // Budget of one quad: the two within 5 m, then the nearest one past them.
            Assert.That(regions.Build(Vector3.zero, 5f, float.PositiveInfinity, 4), Is.EqualTo(3));
            Assert.That(regions.DirtyCount, Is.EqualTo(7));
            // Hidden and beyond the build distance: left for later.
            regions.SetShownRule(region => false);
            Assert.That(regions.Build(Vector3.zero, 0f, 15f, int.MaxValue), Is.EqualTo(1), "only x = 3 lies within 15 m");
            regions.Dispose();
        }

        private T Make<T>(T item) where T : Object
        {
            made.Add(item);
            return item;
        }

        private static MeshFilter FilterOf(GameObject parent, string name)
        {
            Transform child = parent.transform.Find(name);
            Assert.That(child, Is.Not.Null, name);
            return child.GetComponent<MeshFilter>();
        }

        // A quad of 4 vertices and 2 triangles at height y; with material ids and weights (TexCoord2, 3) like a chunk's.
        private Mesh Quad(float y, bool materials)
        {
            var mesh = Make(new Mesh());
            mesh.vertices = new[] { new Vector3(0f, y, 0f), new Vector3(1f, y, 0f), new Vector3(0f, y, 1f), new Vector3(1f, y + 1f, 1f) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.forward, Vector3.right };
            if (materials)
            {
                mesh.SetUVs(2, new List<Vector3> { new Vector3(1, 2, 3), new Vector3(4, 5, 6), new Vector3(7, 8, 9), new Vector3(1, 1, 1) });
                mesh.SetUVs(3, new List<Vector3> { Vector3.right, Vector3.up, Vector3.forward, Vector3.one / 3f });
            }
            mesh.SetIndices(new[] { 0, 2, 1, 1, 2, 3 }, MeshTopology.Triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // A flat grid of size x size quads, past 16-bit indices when merged twice.
        private Mesh Grid(int size)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var indices = new List<int>();
            for (int z = 0; z <= size; z++)
            {
                for (int x = 0; x <= size; x++)
                {
                    vertices.Add(new Vector3(x, 0f, z));
                    normals.Add(Vector3.up);
                }
            }
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = z * (size + 1) + x;
                    indices.AddRange(new[] { i, i + size + 1, i + 1, i + 1, i + size + 1, i + size + 2 });
                }
            }
            var mesh = Make(new Mesh());
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
