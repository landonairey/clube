using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// GL775: the Burst flora jobs against their managed references. <see cref="GroundSpotSearch"/> against
    /// <see cref="GrassyGround"/>, <see cref="TreeCoverGrid"/> against <see cref="TreeCover"/>, and <see cref="GrassScatterJob"/>
    /// against <see cref="GrassTuftsReference"/>.
    /// </summary>
    public class FloraJobTests
    {
        private const float GrassId = 3f;
        private const float SandId = 4f;
        private const float Tolerance = 1e-4f;

        private VoxelMaterial stone;
        private VoxelMaterial dirt;
        private VoxelMaterial grass;
        private VoxelMaterial wood;
        private VoxelMaterial leaves;
        private Mesh mesh;

        // The chunk's corner in the world: a few trees stand round it in the test terrain.
        private static readonly Vector3 ChunkCorner = new Vector3(37.5f, 18f, -12.25f);

        [OneTimeSetUp]
        public void SetUp()
        {
            stone = VoxelMaterial.Create(1, "Stone");
            dirt = VoxelMaterial.Create(2, "Dirt");
            grass = VoxelMaterial.Create(3, "Grass");
            wood = VoxelMaterial.Create(6, "Wood");
            leaves = VoxelMaterial.Create(7, "Leaves");
            mesh = BumpyMesh();
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            Hydrology.DisposeAll();
            foreach (Object o in new Object[] { stone, dirt, grass, wood, leaves, mesh })
            {
                Object.DestroyImmediate(o);
            }
        }

        // 4 m of rolling ground in 0.25 m quads, mostly grass, some sand, one steep step; normals from the mesh.
        private static Mesh BumpyMesh()
        {
            const int n = 16;
            var vertices = new List<Vector3>();
            var ids = new List<Vector3>();
            var weights = new List<Vector3>();
            for (int z = 0; z <= n; z++)
            {
                for (int x = 0; x <= n; x++)
                {
                    float h = 0.4f * Mathf.Sin(x * 0.7f) + 0.3f * Mathf.Cos(z * 0.9f) + (x >= 12 ? 0.9f : 0f);
                    vertices.Add(new Vector3(x * 0.25f, h, z * 0.25f));
                    float sand = Mathf.Clamp01((z - 9) / 4f);
                    ids.Add(new Vector3(GrassId, SandId, 0f));
                    weights.Add(new Vector3(1f - sand, sand, 0f));
                }
            }
            var triangles = new List<int>();
            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    int i = x + z * (n + 1);
                    triangles.AddRange(new[] { i, i + n + 1, i + 1, i + 1, i + n + 1, i + n + 2 });
                }
            }
            var result = new Mesh();
            result.SetVertices(vertices);
            result.SetUVs(2, ids);
            result.SetUVs(3, weights);
            result.SetTriangles(triangles, 0);
            result.RecalculateNormals();
            return result;
        }

        private TreeField Trees()
        {
            var settings = new TerrainSettings { Seed = 11, SurfaceLevel = 20f, Amplitude = 3f };
            settings.Layers.Top = grass;
            settings.Layers.Band = dirt;
            settings.Layers.Base = stone;
            settings.Trees.Chance = 0.6f;
            settings.Trees.Wood = wood;
            settings.Trees.Leaves = leaves;
            return TreeField.Create(settings, TerrainGenerators.Create(settings));
        }

        private static List<GroundSpot> JobSpots(Mesh source, float4x4 toWorld, float[] grounds, float minUp, float cell, float margin)
        {
            var query = new GroundSpotQuery { MinUp = minUp, Cell = cell, Seed = 4, SaltX = 1, SaltZ = 2, Margin = margin };
            foreach (float id in grounds)
            {
                query.Grounds.Add(id);
            }
            Mesh.MeshDataArray data = Mesh.AcquireReadOnlyMeshData(source);
            var first = new NativeList<GroundSpot>(Allocator.TempJob);
            var second = new NativeList<GroundSpot>(Allocator.TempJob);
            try
            {
                new GroundSpotsJob { Meshes = data, MeshIndex = 0, ToWorld = toWorld, First = query, Second = default, FirstSpots = first, SecondSpots = second }
                    .Schedule().Complete();
                Assert.That(second.Length, Is.Zero, "an empty query finds nothing");
                var spots = new List<GroundSpot>();
                foreach (GroundSpot spot in first)
                {
                    spots.Add(spot);
                }
                return spots;
            }
            finally
            {
                first.Dispose();
                second.Dispose();
                data.Dispose();
            }
        }

        [TestCase(0.7f, 0.4f, 0f, false)]
        [TestCase(0.6f, 1.7f, 0.15f, false)]
        [TestCase(0.6f, 1.7f, 0.15f, true)]
        public void GroundSpots_JobFindsWhatTheManagedSearchFinds(float minUp, float cell, float margin, bool withSand)
        {
            Matrix4x4 toWorld = Matrix4x4.Translate(ChunkCorner);
            float[] grounds = withSand ? new[] { GrassId, SandId } : new[] { GrassId };
            var expected = new List<GroundSpot>();
            new GrassyGround().Find(mesh, toWorld, grounds, minUp, cell, 4, 1, 2, margin, expected);
            List<GroundSpot> actual = JobSpots(mesh, toWorld, grounds, minUp, cell, margin);

            Assert.That(expected.Count, Is.GreaterThan(0), "the test ground grows something");
            Assert.That(actual.Count, Is.EqualTo(expected.Count));
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.That(actual[i].Cell, Is.EqualTo(expected[i].Cell), $"spot {i}");
                Assert.That(Vector3.Distance(actual[i].Position, expected[i].Position), Is.LessThan(Tolerance), $"spot {i}");
                Assert.That(Vector3.Distance(actual[i].Normal, expected[i].Normal), Is.LessThan(Tolerance), $"spot {i}");
            }
        }

        [Test]
        public void GroundSpots_SandIsLeftOutUnlessAsked()
        {
            Matrix4x4 toWorld = Matrix4x4.Translate(ChunkCorner);
            int grassOnly = JobSpots(mesh, toWorld, new[] { GrassId }, 0.6f, 0.4f, 0f).Count;
            int both = JobSpots(mesh, toWorld, new[] { GrassId, SandId }, 0.6f, 0.4f, 0f).Count;
            Assert.That(both, Is.GreaterThan(grassOnly));
        }

        [Test]
        public void TreeCoverGrid_MatchesTheTreeCover()
        {
            TreeField trees = Trees();
            Assert.That(trees, Is.Not.Null);
            var cover = new TreeCover(trees);
            var roots = new NativeList<float3>(Allocator.Temp);
            try
            {
                TreeCoverGrid grid = cover.Fill(ChunkCorner.x - 1f, ChunkCorner.z - 1f, ChunkCorner.x + 5f, ChunkCorner.z + 5f, roots);
                NativeArray<float3> array = roots.AsArray();
                float most = 0f;
                for (int i = 0; i <= 20; i++)
                {
                    for (int j = 0; j <= 20; j++)
                    {
                        float x = ChunkCorner.x - 1f + i * 0.3f, z = ChunkCorner.z - 1f + j * 0.3f;
                        float expected = cover.At(x, z);
                        Assert.That(grid.At(array, x, z), Is.EqualTo(expected).Within(Tolerance), $"({x}, {z})");
                        most = Mathf.Max(most, expected);
                    }
                }
                Assert.That(most, Is.GreaterThan(0f), "some trees stand near the test chunk");
            }
            finally
            {
                roots.Dispose();
            }
        }

        [Test]
        public void GrassScatterJob_GrowsWhatTheReferenceGrows()
        {
            TreeField trees = Trees();
            var cover = new TreeCover(trees);
            var growth = new GrassGrowth { WheatShare = 0.9f, WheatMaxForest = 1f };
            var dahlias = new DahliaGrowth { PeakShare = 0.3f, Spread = 1.5f };
            var rules = new GrassTuftRules
            {
                GroundId = GrassId, Cell = 1f / Mathf.Sqrt(6f), MinUp = 0.7f, Height = new Vector2(0.25f, 0.55f), Seed = 11,
                Growth = growth.Rules, Dahlias = dahlias.Rules,
            };
            Matrix4x4 chunkToWorld = Matrix4x4.Translate(ChunkCorner);
            Matrix4x4 worldToLocal = Matrix4x4.identity;
            var nodes = new List<Vector3> { ChunkCorner + new Vector3(1f, -4f, 1f), ChunkCorner + new Vector3(3.5f, -8f, 2f) };
            var patches = new List<int> { 1, 3 };

            var grass = new List<Matrix4x4>();
            var wheat = new List<Matrix4x4>();
            var flowers = new List<DahliaTuft>();
            GrassTuftsReference.Scatter(mesh, chunkToWorld, worldToLocal, rules, cover, nodes, patches, grass, wheat, flowers);

            var roots = new NativeList<float3>(Allocator.TempJob);
            var nodeArray = new NativeArray<Vector3>(nodes.ToArray(), Allocator.TempJob);
            var patchArray = new NativeArray<int>(patches.ToArray(), Allocator.TempJob);
            var jobGrass = new NativeList<float4x4>(Allocator.TempJob);
            var jobWheat = new NativeList<float4x4>(Allocator.TempJob);
            var jobFlowers = new NativeList<DahliaTuft>(Allocator.TempJob);
            Mesh.MeshDataArray data = Mesh.AcquireReadOnlyMeshData(mesh);
            try
            {
                TreeCoverGrid grid = cover.Fill(ChunkCorner.x - 1f, ChunkCorner.z - 1f, ChunkCorner.x + 5f, ChunkCorner.z + 5f, roots);
                new GrassScatterJob
                {
                    Meshes = data, MeshIndex = 0, ChunkToWorld = chunkToWorld, WorldToLocal = worldToLocal, Rules = rules, Cover = grid,
                    Roots = roots.AsArray(), DahliaNodes = nodeArray, DahliaPatches = patchArray, Grass = jobGrass, Wheat = jobWheat, Dahlias = jobFlowers,
                }.Schedule().Complete();

                Assert.That(grass.Count, Is.GreaterThan(10), "grass");
                Assert.That(wheat.Count, Is.GreaterThan(0), "wheat");
                Assert.That(flowers.Count, Is.GreaterThan(0), "dahlias");
                SameMatrices(jobGrass, grass, "grass");
                SameMatrices(jobWheat, wheat, "wheat");
                Assert.That(jobFlowers.Length, Is.EqualTo(flowers.Count), "dahlias");
                for (int i = 0; i < flowers.Count; i++)
                {
                    Assert.That(jobFlowers[i].Colour, Is.EqualTo(flowers[i].Colour), $"dahlia {i}");
                    Same(jobFlowers[i].Matrix, flowers[i].Matrix, $"dahlia {i}");
                }
            }
            finally
            {
                data.Dispose();
                roots.Dispose();
                nodeArray.Dispose();
                patchArray.Dispose();
                jobGrass.Dispose();
                jobWheat.Dispose();
                jobFlowers.Dispose();
            }
        }

        [Test]
        public void FromTo_MatchesQuaternionFromToRotation()
        {
            var directions = new[]
            {
                new Vector3(0f, 1f, 0f), new Vector3(0.3f, 0.9f, -0.1f), new Vector3(-0.5f, 0.6f, 0.4f), new Vector3(1f, 0.2f, 0f), new Vector3(0.01f, -1f, 0f),
            };
            foreach (Vector3 to in directions)
            {
                Quaternion expected = Quaternion.FromToRotation(Vector3.up, to);
                quaternion actual = GrassScatterJob.FromTo(new float3(0f, 1f, 0f), to);
                // Either sign is the same turn; the rotated directions must agree.
                Vector3 a = expected * new Vector3(0.2f, 0.7f, -0.4f);
                Vector3 b = math.rotate(actual, new float3(0.2f, 0.7f, -0.4f));
                Assert.That(Vector3.Distance(a, b), Is.LessThan(Tolerance), to.ToString());
            }
        }

        private static void SameMatrices(NativeList<float4x4> actual, List<Matrix4x4> expected, string what)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Count), what);
            for (int i = 0; i < expected.Count; i++)
            {
                Same(actual[i], expected[i], $"{what} {i}");
            }
        }

        private static void Same(Matrix4x4 actual, Matrix4x4 expected, string what)
        {
            for (int k = 0; k < 16; k++)
            {
                Assert.That(actual[k], Is.EqualTo(expected[k]).Within(Tolerance), what);
            }
        }
    }
}
