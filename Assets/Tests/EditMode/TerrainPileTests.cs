using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class TerrainPileTests
    {
        private const int Size = 8;
        private const float Iso = 0.5f;
        private const byte Loose = 7;

        private World world;

        [SetUp]
        public void SetUp()
        {
            // One chunk; ground up to y = 1, air above.
            world = new World(new Vector3Int(Size, Size, Size), 1f);
            world.Load(Vector3Int.zero, null);
            for (int z = 0; z <= Size; z++)
            {
                for (int y = 0; y <= Size; y++)
                {
                    for (int x = 0; x <= Size; x++)
                    {
                        world.SetDensity(new Vector3Int(x, y, z), y <= 1 ? 1f : 0f);
                    }
                }
            }
        }

        private int Solid()
        {
            int count = 0;
            for (int z = 0; z <= Size; z++)
            {
                for (int y = 0; y <= Size; y++)
                {
                    for (int x = 0; x <= Size; x++)
                    {
                        if (world.GetDensity(new Vector3Int(x, y, z)) >= Iso)
                        {
                            count++;
                        }
                    }
                }
            }
            return count;
        }

        [Test]
        public void Place_FillsOneSampleRestingOnTheGround()
        {
            int before = Solid();

            Assert.AreEqual(1, TerrainPile.Place(world, new Vector3(4f, 2.2f, 4f), Loose, 1, Iso));

            Assert.AreEqual(before + 1, Solid());
            Assert.AreEqual(1f, world.GetDensity(new Vector3Int(4, 2, 4)));
            Assert.AreEqual(Loose, world.GetMaterial(new Vector3Int(4, 2, 4)));
        }

        [Test]
        public void Place_FromInsideTheGroundOrMidAir_SettlesOnTheSurface()
        {
            TerrainPile.Place(world, new Vector3(4f, 1f, 4f), Loose, 1, Iso);
            Assert.AreEqual(1f, world.GetDensity(new Vector3Int(4, 2, 4)), "From just inside the ground.");

            TerrainPile.Place(world, new Vector3(2f, 6f, 2f), Loose, 1, Iso);
            Assert.AreEqual(1f, world.GetDensity(new Vector3Int(2, 2, 2)), "Dropped from above.");
        }

        [Test]
        public void Place_SpreadsBeforeItClimbs()
        {
            TerrainPile.Place(world, new Vector3(4f, 2f, 4f), Loose, 5, Iso);

            // The first five go on the ground layer, around the drop point, not up a column.
            Assert.AreEqual(0f, world.GetDensity(new Vector3Int(4, 3, 4)));
            Assert.AreEqual(1f, world.GetDensity(new Vector3Int(5, 2, 4)));
            Assert.AreEqual(1f, world.GetDensity(new Vector3Int(3, 2, 4)));
        }

        [Test]
        public void Block_FillsItsBuildCellLevel_ThenTheNextCell()
        {
            // Cells of 2 m: 2 x 2 sample columns at 1 m voxels, one cell (2 samples) high.
            var settings = new PileSettings(PileShape.Block, 40f, new BuildGrid(2f));

            Assert.AreEqual(8, TerrainPile.Place(world, new Vector3(4.5f, 2f, 4.5f), Loose, 8, Iso, settings));
            for (int z = 4; z <= 5; z++)
            {
                for (int y = 2; y <= 3; y++)
                {
                    for (int x = 4; x <= 5; x++)
                    {
                        Assert.AreEqual(1f, world.GetDensity(new Vector3Int(x, y, z)), $"cell sample {x},{y},{z}");
                    }
                }
            }
            Assert.AreEqual(0f, world.GetDensity(new Vector3Int(4, 4, 4)), "no higher than one cell");
            Assert.AreEqual(0f, world.GetDensity(new Vector3Int(6, 2, 4)), "nothing outside the cell yet");

            int before = Solid();
            Assert.AreEqual(4, TerrainPile.Place(world, new Vector3(4.5f, 2f, 4.5f), Loose, 4, Iso, settings));
            Assert.AreEqual(before + 4, Solid());
            Assert.AreEqual(0f, world.GetDensity(new Vector3Int(4, 4, 4)), "the full cell doesn't grow taller");
            int besideOnTheGround = 0;
            for (int z = 0; z <= Size; z++)
            {
                for (int x = 0; x <= Size; x++)
                {
                    bool inFirstCell = x >= 4 && x <= 5 && z >= 4 && z <= 5;
                    if (!inFirstCell && world.GetDensity(new Vector3Int(x, 2, z)) >= Iso)
                    {
                        besideOnTheGround++;
                    }
                }
            }
            Assert.AreEqual(4, besideOnTheGround, "the next cell over fills, on the ground");
        }

        [Test]
        public void Cone_KeepsItsSlope()
        {
            // A steep 60° cone of 30 samples: no sample sits higher than distance x tan(60°)
            // above the ground under the middle allows.
            var settings = new PileSettings(PileShape.Cone, 60f, new BuildGrid(1f));
            TerrainPile.Place(world, new Vector3(4f, 2f, 4f), Loose, 30, Iso, settings);

            float tan = Mathf.Tan(60f * Mathf.Deg2Rad);
            for (int z = 0; z <= Size; z++)
            {
                for (int y = 2; y <= Size; y++)
                {
                    for (int x = 0; x <= Size; x++)
                    {
                        if (world.GetDensity(new Vector3Int(x, y, z)) < Iso)
                        {
                            continue;
                        }
                        float distance = Mathf.Sqrt((x - 4) * (x - 4) + (z - 4) * (z - 4));
                        float height = y - 2;
                        Assert.LessOrEqual(height, Mathf.Max(0f, 4f - distance) * tan + 1e-3f, $"sample {x},{y},{z}");
                    }
                }
            }
            Assert.AreEqual(1f, world.GetDensity(new Vector3Int(4, 3, 4)), "it does climb in the middle");
        }

        [Test]
        public void Pour_InBatches_GrowsTheSameConeAsOneDrop()
        {
            var settings = new PileSettings(PileShape.Cone, 40f, new BuildGrid(1f));
            var point = new Vector3(4f, 2f, 4f);
            var pour = default(PilePour);
            int poured = 0;
            foreach (int batch in new[] { 1, 4, 10, 25 })
            {
                poured += TerrainPile.Pour(world, ref pour, point + new Vector3(batch, 0f, -batch) * 0.1f, Loose, batch, Iso, settings);
            }
            var pouredPile = Solids();

            SetUp();
            int dropped = TerrainPile.Place(world, point, Loose, 40, Iso, settings);

            Assert.AreEqual(40, poured);
            Assert.AreEqual(dropped, poured);
            Assert.AreEqual(new Vector3Int(4, 2, 4), pour.Base, "Every batch measured from the first base.");
            // The same cone: the same shells (distance from the base plus height over the slope),
            // though samples tied on a shell may be filled in another order.
            CollectionAssert.AreEqual(Shells(Solids(), 40f), Shells(pouredPile, 40f),
                "Poured a batch at a time (wherever the aim drifts), the pile is the cone one drop of it all makes.");
        }

        // Each sample's cone score around (4, 2, 4), rounded and sorted.
        private static List<float> Shells(List<Vector3Int> samples, float angle)
        {
            float climb = 1f / Mathf.Tan(angle * Mathf.Deg2Rad);
            var shells = new List<float>();
            foreach (Vector3Int sample in samples)
            {
                Vector3Int d = sample - new Vector3Int(4, 2, 4);
                shells.Add(Mathf.Round((Mathf.Sqrt(d.x * d.x + d.z * d.z) + d.y * climb) * 1000f) / 1000f);
            }
            shells.Sort();
            return shells;
        }

        private List<Vector3Int> Solids()
        {
            var solids = new List<Vector3Int>();
            for (int z = 0; z <= Size; z++)
            {
                for (int y = 2; y <= Size; y++)
                {
                    for (int x = 0; x <= Size; x++)
                    {
                        if (world.GetDensity(new Vector3Int(x, y, z)) >= Iso)
                        {
                            solids.Add(new Vector3Int(x, y, z));
                        }
                    }
                }
            }
            return solids;
        }

        [Test]
        public void Place_StopsWhereTheWorldEnds()
        {
            // 7 x 7 inner columns plus the borders: far fewer than a million fit in one chunk.
            int filled = TerrainPile.Place(world, new Vector3(4f, 2f, 4f), Loose, 1000000, Iso);

            Assert.Less(filled, 1000000);
            Assert.AreEqual(filled, Solid() - (Size + 1) * (Size + 1) * 2);
        }
    }
}
