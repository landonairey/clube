using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>GL775: <see cref="WaterMeshJob"/> builds the same water surface as <see cref="WaterMesher"/>.</summary>
    public class WaterMeshJobTests
    {
        private const float Voxel = 0.25f;
        private const float Iso = 0.5f;
        private const int Chunks = 3;

        // A basin across the middle of a 3 x 2 x 3 chunk world, its floor sloping, partly filled: still water, a bank, a thin
        // stream down a step and a few lone samples.
        private static World Basin(bool bytes)
        {
            var world = bytes
                ? new World(new Vector3Int(16, 16, 16), Voxel, samples => new ByteVoxelStorage(samples))
                : new World(new Vector3Int(16, 16, 16), Voxel);
            for (int cz = 0; cz < Chunks; cz++)
            {
                for (int cy = 0; cy < 2; cy++)
                {
                    for (int cx = 0; cx < Chunks; cx++)
                    {
                        world.Load(new Vector3Int(cx, cy, cz), null);
                    }
                }
            }
            int size = 16 * Chunks;
            for (int z = 0; z <= size; z++)
            {
                for (int x = 0; x <= size; x++)
                {
                    float rim = Mathf.Max(Mathf.Abs(x - 24f), Mathf.Abs(z - 24f));
                    int top = rim > 14f ? 24 : 10 + x / 8 + (x > 30 ? 4 : 0);
                    for (int y = 0; y <= top; y++)
                    {
                        world.SetDensity(new Vector3Int(x, y, z), 1f);
                    }
                    if (rim <= 14f)
                    {
                        int surface = x < 26 ? 18 : top + 1;
                        for (int y = top + 1; y <= surface; y++)
                        {
                            world.SetWater(new Vector3Int(x, y, z), y == surface && x >= 26 ? 0.25f : 1f);
                        }
                    }
                }
            }
            world.SetWater(new Vector3Int(5, 30, 40), 0.3f);
            return world;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Job_BuildsWhatTheManagedMesherBuilds(bool bytes)
        {
            World world = Basin(bytes);
            var mesher = new WaterMesher();
            var input = new WaterMeshInput();
            var output = new WaterMeshOutput();
            int built = 0;
            try
            {
                foreach (Vector3Int coord in new[] { new Vector3Int(1, 0, 1), new Vector3Int(1, 1, 1), new Vector3Int(0, 1, 1), new Vector3Int(2, 0, 2), new Vector3Int(0, 1, 2) })
                {
                    bool managed = mesher.Build(world, coord, Iso, WaterMap.None);
                    bool gathered = input.Gather(world, coord);
                    if (gathered)
                    {
                        new WaterMeshJob
                        {
                            Size = new Unity.Mathematics.int3(16, 16, 16), VoxelSize = Voxel, Origin = world.Grid.ChunkOrigin(coord), GroundIso = Iso,
                            DensityBytes = input.DensityBytes, DensityFloats = input.DensityFloats, WaterSamples = input.Water, Modes = input.Modes,
                            Uniform = input.Uniform, HasWater = input.HasWater, Map = WaterMap.None,
                            Vertices = output.Vertices, Normals = output.Normals, Flows = output.Flows, Looks = output.Looks, Ripples = output.Ripples,
                            Banks = output.Banks, Triangles = output.Triangles,
                        }.Schedule().Complete();
                    }
                    int jobTriangles = gathered ? output.Triangles.Length : 0;
                    Assert.That(jobTriangles > 0, Is.EqualTo(managed), $"{coord}: something to draw");
                    if (!managed)
                    {
                        continue;
                    }
                    built++;
                    Same(output.Vertices, mesher.Vertices, $"{coord} vertices");
                    Same(output.Flows, mesher.Flows, $"{coord} flows");
                    Same(output.Looks, mesher.Looks, $"{coord} looks");
                    Same(output.Ripples, mesher.Ripples, $"{coord} ripples");
                    Same(output.Banks, mesher.Banks, $"{coord} banks");
                    Assert.That(output.Triangles.AsArray().ToArray(), Is.EqualTo(mesher.Triangles.ToArray()), $"{coord} triangles");

                    // The job's normals are what RecalculateNormals gave the managed mesh.
                    var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                    mesh.SetVertices(mesher.Vertices);
                    mesh.SetTriangles(mesher.Triangles, 0);
                    mesh.RecalculateNormals();
                    var normals = new List<Vector3>();
                    mesh.GetNormals(normals);
                    Object.DestroyImmediate(mesh);
                    Same(output.Normals, normals, $"{coord} normals");
                }
                Assert.That(built, Is.GreaterThanOrEqualTo(3), "chunks with water drawn");
            }
            finally
            {
                input.Dispose();
                output.Dispose();
            }
        }

        private static void Same(NativeList<Vector3> actual, List<Vector3> expected, string what)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Count), what);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.That(Vector3.Distance(actual[i], expected[i]), Is.LessThan(1e-4f), $"{what} {i}");
            }
        }

        private static void Same(NativeList<Vector2> actual, List<Vector2> expected, string what)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Count), what);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.That(Vector2.Distance(actual[i], expected[i]), Is.LessThan(1e-4f), $"{what} {i}");
            }
        }

        private static void Same(NativeList<Vector4> actual, List<Vector4> expected, string what)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Count), what);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.That(Vector4.Distance(actual[i], expected[i]), Is.LessThan(1e-4f), $"{what} {i}");
            }
        }
    }
}
