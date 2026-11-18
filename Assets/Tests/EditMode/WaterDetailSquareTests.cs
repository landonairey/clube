using System.Collections.Generic;
using Clube.Core;
using NUnit.Framework;
using Unity.Mathematics;

namespace Clube.Core.Tests
{
    /// <summary>GL761: the detailed far water only replaces the coarse sheets where its tiles are built.</summary>
    public class WaterDetailSquareTests
    {
        private const int Across = 10;
        private const float Width = 64f;
        private const float Wanted = 256f;

        // The middle of tile (5, 5).
        private static readonly float2 Centre = new float2(352f, 352f);

        private static int Tile(int x, int z) => x + z * Across;

        [Test]
        public void WithEveryTileBuiltTheSquareIsFullSize()
        {
            float half = WaterDetailSquare.ReadyHalfWidth(Centre, new HashSet<int>(), Across, Width, Wanted);

            Assert.AreEqual(Wanted, half);
        }

        [Test]
        public void AMissingTileStopsTheSquareAtItsNearSide()
        {
            // Tile (7, 5) starts at x = 448, 96 m from the centre.
            var missing = new HashSet<int> { Tile(7, 5) };

            Assert.AreEqual(96f, WaterDetailSquare.ReadyHalfWidth(Centre, missing, Across, Width, Wanted));
        }

        [Test]
        public void TheNearestOfSeveralMissingTilesDecides()
        {
            var missing = new HashSet<int> { Tile(9, 5), Tile(5, 2), Tile(7, 7) };

            // Tile (7, 7) is 96 m off along both axes; (5, 2) ends at z = 192, 160 m away; (9, 5) is 224 m away.
            Assert.AreEqual(96f, WaterDetailSquare.ReadyHalfWidth(Centre, missing, Across, Width, Wanted));
        }

        [Test]
        public void AMissingTileUnderTheViewerLeavesNoSquare()
        {
            var missing = new HashSet<int> { Tile(5, 5) };

            Assert.AreEqual(0f, WaterDetailSquare.ReadyHalfWidth(Centre, missing, Across, Width, Wanted));
        }

        [Test]
        public void AMissingTileBeyondTheSquareChangesNothing()
        {
            var wide = new HashSet<int> { Tile(9, 9) };

            Assert.AreEqual(100f, WaterDetailSquare.ReadyHalfWidth(Centre, wide, Across, Width, 100f));
        }

        [Test]
        public void TheNearestMissingTileIsBuiltFirst()
        {
            var missing = new HashSet<int> { Tile(0, 0), Tile(9, 9), Tile(6, 5), Tile(5, 8) };

            Assert.AreEqual(Tile(6, 5), WaterDetailSquare.Nearest(Centre, missing, Across, Width));
        }

        [Test]
        public void NothingMissingHasNoNearest()
        {
            Assert.AreEqual(-1, WaterDetailSquare.Nearest(Centre, new HashSet<int>(), Across, Width));
        }

        [Test]
        public void BuildingNearestFirstOnlyEverGrowsTheSquare()
        {
            var missing = new HashSet<int>();
            for (int i = 0; i < Across * Across; i++)
            {
                missing.Add(i);
            }

            float last = 0f;
            while (missing.Count > 0)
            {
                missing.Remove(WaterDetailSquare.Nearest(Centre, missing, Across, Width));
                float half = WaterDetailSquare.ReadyHalfWidth(Centre, missing, Across, Width, Wanted);
                Assert.GreaterOrEqual(half, last);
                last = half;
            }
            Assert.AreEqual(Wanted, last);
        }
    }
}
