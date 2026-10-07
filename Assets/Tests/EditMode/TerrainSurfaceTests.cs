using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>Bare rock on steep slopes (GL21) and the scattered surface rocks (GL22).</summary>
    public class TerrainSurfaceTests
    {
        private const int Size = 8;
        private const float Voxel = 0.5f;

        private VoxelMaterial stone;
        private VoxelMaterial dirt;
        private VoxelMaterial grass;
        private VoxelMaterial rock;
        private TerrainLayers layers;
        private FractalPerlin2DGenerator generator;

        [SetUp]
        public void SetUp()
        {
            stone = VoxelMaterial.Create(0, "Stone");
            dirt = VoxelMaterial.Create(1, "Dirt");
            grass = VoxelMaterial.Create(2, "Grass");
            rock = VoxelMaterial.Create(10, "Rock");
            layers = new TerrainLayers
            {
                Top = grass, TopDepth = 0.5f, Band = dirt, BandDepth = 2f, Base = stone,
                SteepAngle = 30f, SteepMinHeight = 0f, Rock = rock, RockChance = 0.05f,
            };
            // Hilly enough to have both steep and gentle columns in a couple of chunks.
            generator = new FractalPerlin2DGenerator(3, 2f, 2.5f, 0.25f, octaves: 2);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(stone);
            Object.DestroyImmediate(dirt);
            Object.DestroyImmediate(grass);
            Object.DestroyImmediate(rock);
        }

        private World NewWorld()
        {
            return new World(new Vector3Int(Size, Size, Size), Voxel);
        }

        [Test]
        public void Neighbours_AgreeOnTheirSharedBorder_WithSteepSlopesAndRocks()
        {
            World world = NewWorld();
            Chunk origin = world.Load(Vector3Int.zero, generator, layers);
            Chunk east = world.Load(Vector3Int.right, generator, layers);
            Chunk north = world.Load(new Vector3Int(0, 0, 1), generator, layers);

            for (int a = 0; a <= Size; a++)
            {
                for (int y = 0; y <= Size; y++)
                {
                    var shared = new Vector3Int(Size, y, a);
                    var theirs = new Vector3Int(0, y, a);
                    Assert.AreEqual(origin.GetDensity(shared), east.GetDensity(theirs), 1e-6f, $"density at x border {shared}");
                    Assert.AreEqual(origin.GetMaterial(shared), east.GetMaterial(theirs), $"material at x border {shared}");

                    shared = new Vector3Int(a, y, Size);
                    theirs = new Vector3Int(a, y, 0);
                    Assert.AreEqual(origin.GetDensity(shared), north.GetDensity(theirs), 1e-6f, $"density at z border {shared}");
                    Assert.AreEqual(origin.GetMaterial(shared), north.GetMaterial(theirs), $"material at z border {shared}");
                }
            }
        }

        [Test]
        public void SteepColumns_ShowTheBase_GentleOnesTheTopLayer()
        {
            World world = NewWorld();
            Chunk chunk = world.Load(Vector3Int.zero, generator, layers);
            LayerTable table = LayerTable.From(layers);
            int steep = 0;
            int gentle = 0;
            for (int z = 0; z <= Size; z++)
            {
                for (int x = 0; x <= Size; x++)
                {
                    float px = x * Voxel;
                    float pz = z * Voxel;
                    float h = generator.Height(px, pz);
                    float dx = (generator.Height(px + Voxel, pz) - generator.Height(px - Voxel, pz)) / (2f * Voxel);
                    float dz = (generator.Height(px, pz + Voxel) - generator.Height(px, pz - Voxel)) / (2f * Voxel);
                    bool isSteep = table.IsSteep(math.sqrt(dx * dx + dz * dz), h);

                    // The highest solid sample in the column: just under the surface.
                    int y = Mathf.FloorToInt(h / Voxel);
                    if (y < 0 || y > Size || h - y * Voxel < 0.1f || h - y * Voxel > TopDepthMargin)
                    {
                        continue;
                    }
                    byte material = chunk.GetMaterial(new Vector3Int(x, y, z));
                    if (material == rock.Id)
                    {
                        continue;
                    }
                    Assert.AreEqual(isSteep ? stone.Id : grass.Id, material, $"column {x}, {z}: steep {isSteep}");
                    if (isSteep)
                    {
                        steep++;
                    }
                    else
                    {
                        gentle++;
                    }
                }
            }
            Assert.Greater(steep, 0, "the terrain should have some steep columns");
            Assert.Greater(gentle, 0, "and some gentle ones");
        }

        // Samples this far under the surface are still in the top layer.
        private const float TopDepthMargin = 0.45f;

        [Test]
        public void Rocks_SitJustAboveTheSurface_InTheirColumns()
        {
            World world = NewWorld();
            Chunk chunk = world.Load(Vector3Int.zero, generator, layers);
            int rocks = 0;
            for (int z = 0; z <= Size; z++)
            {
                for (int y = 0; y <= Size; y++)
                {
                    for (int x = 0; x <= Size; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        if (chunk.GetMaterial(sample) != rock.Id)
                        {
                            continue;
                        }
                        rocks++;
                        float depth = generator.Height(x * Voxel, z * Voxel) - y * Voxel;
                        Assert.That(depth, Is.LessThan(0f).And.GreaterThanOrEqualTo(-Voxel - 1e-4f), $"{sample} sits just above the surface");
                        Assert.AreEqual(1f, chunk.GetDensity(sample), 1e-6f, $"{sample} is solid");
                        Assert.IsTrue(ChunkFillKernel.IsRockColumn(0, new int2(x, z), layers.RockChance), $"{sample}'s column has a rock");
                    }
                }
            }
            Assert.Greater(rocks, 0, "at 5% a column, an 8x8 chunk should have rocks");
        }
    }
}
