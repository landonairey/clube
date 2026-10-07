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
        public void Place_StopsWhereTheWorldEnds()
        {
            // 7 x 7 inner columns plus the borders: far fewer than a million fit in one chunk.
            int filled = TerrainPile.Place(world, new Vector3(4f, 2f, 4f), Loose, 1000000, Iso);

            Assert.Less(filled, 1000000);
            Assert.AreEqual(filled, Solid() - (Size + 1) * (Size + 1) * 2);
        }
    }
}
