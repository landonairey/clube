using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// Terrain materials (3C): per-sample ids beside densities (M10), depth layers, keeping
    /// border copies in step, the brush's material, and the mesh's material pass (M13, M15).
    /// </summary>
    public class MaterialTests
    {
        private const byte Stone = 0;
        private const byte Dirt = 1;
        private const byte Grass = 2;

        private TerrainLayers layers;

        [SetUp]
        public void MakeLayers()
        {
            layers = new TerrainLayers
            {
                Top = VoxelMaterial.Create(Grass, "Grass"),
                TopDepth = 1f,
                Band = VoxelMaterial.Create(Dirt, "Dirt"),
                BandDepth = 3f,
                Base = VoxelMaterial.Create(Stone, "Stone"),
            };
        }

        [Test]
        public void VoxelMaterials_StayUnallocated_UntilAnotherIdIsSet()
        {
            var materials = new VoxelMaterials(new Vector3Int(3, 3, 3));
            materials.Set(1, 1, 1, 0);
            Assert.That(materials.MemoryBytes, Is.Zero);

            materials.Set(1, 2, 0, 5);
            Assert.That(materials.MemoryBytes, Is.EqualTo(27));
            Assert.That(materials.Get(1, 2, 0), Is.EqualTo(5));
            Assert.That(materials.Get(0, 0, 0), Is.Zero);
        }

        [Test]
        public void Chunk_SetMaterial_MarksDirtyOnlyOnChange()
        {
            var chunk = new Chunk(Vector3Int.one);
            chunk.MarkClean();
            chunk.SetMaterial(Vector3Int.zero, 0);
            Assert.That(chunk.IsDirty, Is.False);

            chunk.SetMaterial(Vector3Int.zero, Dirt);
            Assert.That(chunk.IsDirty, Is.True);
            Assert.That(chunk.GetMaterial(Vector3Int.zero), Is.EqualTo(Dirt));
        }

        [TestCase(-2f, Grass)]
        [TestCase(0.5f, Grass)]
        [TestCase(1f, Dirt)]
        [TestCase(2.9f, Dirt)]
        [TestCase(3f, Stone)]
        [TestCase(40f, Stone)]
        public void TerrainLayers_PickByDepth(float depth, byte expected)
        {
            Assert.That(layers.MaterialAt(depth), Is.EqualTo(expected));
        }

        [Test]
        public void ChunkGenerator_GivesEachSampleItsLayer()
        {
            // Ground at y = 6: depth 0 at y 6 is grass, depths 1-2 (y 5, 4) dirt, 3 and more stone.
            var chunk = new Chunk(new Vector3Int(1, 8, 1));
            ChunkGenerator.Fill(chunk, new FlatGenerator(6f), Vector3.zero, 1f, layers);

            Assert.That(chunk.GetMaterial(new Vector3Int(0, 6, 0)), Is.EqualTo(Grass));
            Assert.That(chunk.GetMaterial(new Vector3Int(0, 5, 0)), Is.EqualTo(Dirt));
            Assert.That(chunk.GetMaterial(new Vector3Int(0, 4, 0)), Is.EqualTo(Dirt));
            Assert.That(chunk.GetMaterial(new Vector3Int(0, 3, 0)), Is.EqualTo(Stone));
            Assert.That(chunk.GetMaterial(new Vector3Int(0, 0, 0)), Is.EqualTo(Stone));
        }

        [Test]
        public void World_SetMaterial_WritesEveryBorderCopy()
        {
            var world = new World(new Vector3Int(4, 4, 4), 1f);
            world.Load(Vector3Int.zero, null);
            world.Load(Vector3Int.right, null);

            // Global sample x = 4 is the last of chunk 0 and the first of chunk 1.
            world.SetMaterial(new Vector3Int(4, 1, 1), Dirt);

            Assert.That(world.Chunks[Vector3Int.zero].GetMaterial(new Vector3Int(4, 1, 1)), Is.EqualTo(Dirt));
            Assert.That(world.Chunks[Vector3Int.right].GetMaterial(new Vector3Int(0, 1, 1)), Is.EqualTo(Dirt));
            Assert.That(world.IsEdited(Vector3Int.zero) && world.IsEdited(Vector3Int.right), Is.True);
        }

        [Test]
        public void World_NewChunk_TakesBorderMaterialsFromAnEditedNeighbour()
        {
            var world = new World(new Vector3Int(4, 4, 4), 1f);
            world.Load(Vector3Int.zero, null);
            world.SetMaterial(new Vector3Int(4, 2, 2), Grass);

            Chunk loaded = world.Load(Vector3Int.right, null);

            Assert.That(loaded.GetMaterial(new Vector3Int(0, 2, 2)), Is.EqualTo(Grass));
        }

        [Test]
        public void Brush_Add_GivesNewGroundItsMaterial_AndKeepsOldGround()
        {
            var chunk = new Chunk(new Vector3Int(4, 4, 4));
            chunk.SetDensity(new Vector3Int(2, 2, 2), 1f);
            chunk.SetMaterial(new Vector3Int(2, 2, 2), Grass);

            TerrainBrush.Apply(chunk, new Vector3(2f, 2f, 2f), 1f, new BrushSettings(1f, material: Dirt), BrushOperation.Add);

            Assert.That(chunk.GetMaterial(new Vector3Int(2, 2, 2)), Is.EqualTo(Grass), "was already ground");
            Assert.That(chunk.GetMaterial(new Vector3Int(3, 2, 2)), Is.EqualTo(Dirt), "new ground");
            Assert.That(chunk.GetDensity(new Vector3Int(3, 2, 2)), Is.EqualTo(1f));
        }

        [Test]
        public void VertexMaterialSampler_TakesTheSolidEndOfEachEdge()
        {
            // Solid floor at y = 0 (grass) under air; every vertex sits on a vertical edge.
            var chunk = new Chunk(new Vector3Int(2, 1, 2));
            for (int z = 0; z < 3; z++)
            {
                for (int x = 0; x < 3; x++)
                {
                    chunk.SetDensity(new Vector3Int(x, 0, z), 1f);
                    chunk.SetMaterial(new Vector3Int(x, 0, z), x == 0 ? Grass : Dirt);
                    chunk.SetMaterial(new Vector3Int(x, 1, z), Stone);
                }
            }
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            ChunkMesher.Build(chunk, new ChunkMeshSettings(0.5f, 2f, shading: Shading.Smooth), vertices, triangles);

            var materials = new List<byte>();
            VertexMaterialSampler.Assign(chunk, 0.5f, 2f, vertices, materials);

            Assert.That(materials.Count, Is.EqualTo(vertices.Count));
            for (int i = 0; i < vertices.Count; i++)
            {
                byte expected = Mathf.Approximately(vertices[i].x, 0f) ? Grass : Dirt;
                Assert.That(materials[i], Is.EqualTo(expected), $"vertex at {vertices[i]}");
            }
        }

        [Test]
        public void HardSeams_OneMaterialPerTriangle_SplittingSharedVertices()
        {
            BuildTwoMaterialFloor(out List<Vector3> vertices, out List<int> triangles, out List<byte> materials, out List<Vector3> normals);
            var output = new MaterialMesh();

            MaterialSplitters.For(MaterialDisplay.HardSeams).Split(vertices, normals, triangles, materials, output);

            Assert.That(output.Triangles.Count, Is.EqualTo(triangles.Count));
            Assert.That(output.Vertices.Count, Is.GreaterThan(vertices.Count), "the seam's vertices are split");
            for (int i = 0; i < output.Triangles.Count; i += 3)
            {
                Vector3 ids = output.MaterialIds[output.Triangles[i]];
                Assert.That(ids.x, Is.EqualTo(ids.y).And.EqualTo(ids.z));
                for (int corner = 0; corner < 3; corner++)
                {
                    int index = output.Triangles[i + corner];
                    Assert.That(output.MaterialIds[index], Is.EqualTo(ids), "every corner shows the triangle's material");
                    Assert.That(output.MaterialWeights[index], Is.EqualTo(new Vector3(1f, 0f, 0f)));
                }
            }
            AssertSamePositions(vertices, triangles, output);
        }

        [Test]
        public void Blended_KeepsEachVertexsMaterial_WithOneWeightOnIt()
        {
            BuildTwoMaterialFloor(out List<Vector3> vertices, out List<int> triangles, out List<byte> materials, out List<Vector3> normals);
            var output = new MaterialMesh();

            MaterialSplitters.For(MaterialDisplay.Blended).Split(vertices, normals, triangles, materials, output);

            for (int i = 0; i < output.Triangles.Count; i++)
            {
                int index = output.Triangles[i];
                Vector3 ids = output.MaterialIds[index];
                Vector3 weights = output.MaterialWeights[index];
                Assert.That(weights.x + weights.y + weights.z, Is.EqualTo(1f));
                float shown = weights.x > 0f ? ids.x : weights.y > 0f ? ids.y : ids.z;
                Assert.That(shown, Is.EqualTo(materials[triangles[i]]), "the vertex shows its own material");
                Assert.That(ids.x <= ids.y && ids.y <= ids.z, Is.True, "ids are sorted");
            }
            AssertSamePositions(vertices, triangles, output);
        }

        [Test]
        public void Blended_UniformGround_IsNotSplit()
        {
            BuildTwoMaterialFloor(out List<Vector3> vertices, out List<int> triangles, out List<byte> materials, out List<Vector3> normals);
            for (int i = 0; i < materials.Count; i++)
            {
                materials[i] = Stone;
            }
            var output = new MaterialMesh();

            MaterialSplitters.For(MaterialDisplay.Blended).Split(vertices, normals, triangles, materials, output);

            Assert.That(output.Vertices.Count, Is.EqualTo(vertices.Count));
        }

        [Test]
        public void HardSeams_Dominant_IsTheMajority_OrTheFirstCorner()
        {
            Assert.That(HardSeamSplitter.Dominant(1, 2, 2), Is.EqualTo(2));
            Assert.That(HardSeamSplitter.Dominant(3, 1, 3), Is.EqualTo(3));
            Assert.That(HardSeamSplitter.Dominant(4, 5, 6), Is.EqualTo(4));
        }

        [Test]
        public void MeshNormals_MatchUnitysRecalculateNormals()
        {
            BuildTwoMaterialFloor(out List<Vector3> vertices, out List<int> triangles, out _, out List<Vector3> normals);
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            var expected = new List<Vector3>();
            mesh.GetNormals(expected);
            Object.DestroyImmediate(mesh);

            for (int i = 0; i < normals.Count; i++)
            {
                Assert.That((normals[i] - expected[i]).sqrMagnitude, Is.LessThan(1e-8f), $"normal {i}");
            }
        }

        // A smooth-shaded bumpy floor, grass on the left half and dirt on the right.
        private static void BuildTwoMaterialFloor(
            out List<Vector3> vertices, out List<int> triangles, out List<byte> materials, out List<Vector3> normals)
        {
            var chunk = new Chunk(new Vector3Int(4, 2, 4));
            for (int z = 0; z < 5; z++)
            {
                for (int x = 0; x < 5; x++)
                {
                    chunk.SetDensity(new Vector3Int(x, 0, z), 1f);
                    chunk.SetDensity(new Vector3Int(x, 1, z), (x + z) % 3 == 0 ? 0.8f : 0.2f);
                    for (int y = 0; y < 2; y++)
                    {
                        chunk.SetMaterial(new Vector3Int(x, y, z), x < 2 ? Grass : Dirt);
                    }
                }
            }
            vertices = new List<Vector3>();
            triangles = new List<int>();
            ChunkMesher.Build(chunk, new ChunkMeshSettings(0.5f, 1f, shading: Shading.Smooth), vertices, triangles);
            materials = new List<byte>();
            VertexMaterialSampler.Assign(chunk, 0.5f, 1f, vertices, materials);
            normals = new List<Vector3>();
            MeshNormals.Compute(vertices, triangles, normals);
        }

        private static void AssertSamePositions(List<Vector3> vertices, List<int> triangles, MaterialMesh output)
        {
            for (int i = 0; i < triangles.Count; i++)
            {
                Assert.That(output.Vertices[output.Triangles[i]], Is.EqualTo(vertices[triangles[i]]), $"corner {i} moved");
            }
        }
    }
}
