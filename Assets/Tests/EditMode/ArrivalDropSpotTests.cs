using NUnit.Framework;

namespace Clube.Core.Tests
{
    /// <summary>Where the arrival cart sets the player down (GL765, GL766).</summary>
    public class ArrivalDropSpotTests
    {
        private static int Pick(bool[] usable, bool[] ground, bool[] view)
        {
            return ArrivalDropSpot.Pick(usable.Length, i => usable[i], i => ground[i], i => view[i]);
        }

        [Test]
        public void ThePlaceWithClearGroundAndAClearView_IsPreferred()
        {
            bool[] yes = { true, true, true };
            Assert.AreEqual(2, Pick(yes, new[] { false, true, true }, new[] { true, false, true }));
        }

        [Test]
        public void ClearGround_ComesBeforeAClearView()
        {
            bool[] yes = { true, true, true };
            Assert.AreEqual(1, Pick(yes, new[] { false, true, true }, new[] { true, false, false }));
        }

        [Test]
        public void WithTreesEverywhere_TheFirstUsablePlaceIsTaken()
        {
            bool[] no = { false, false, false };
            Assert.AreEqual(1, Pick(new[] { false, true, true }, no, no));
        }

        [Test]
        public void APlaceTheCartCannotReach_IsNeverPicked()
        {
            bool[] no = { false, false };
            bool[] yes = { true, true };
            Assert.AreEqual(-1, Pick(no, yes, yes));
        }
    }
}
