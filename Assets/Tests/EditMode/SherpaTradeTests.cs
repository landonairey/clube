using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>GL868: when a sherpa notices a hand in their pack, and what they ask for their goods.</summary>
    public class SherpaTradeTests
    {
        [Test]
        public void AWalkingSherpa_MissesAThiefBehind_ButSeesOneBesideOrAhead()
        {
            Vector2 facing = Vector2.up;
            Assert.IsFalse(SherpaTrade.Notices(true, facing, new Vector2(0f, -1f)), "right behind");
            Assert.IsFalse(SherpaTrade.Notices(true, facing, new Vector2(0.3f, -1f)), "behind, a little to one side");
            Assert.IsTrue(SherpaTrade.Notices(true, facing, new Vector2(1f, 0f)), "beside");
            Assert.IsTrue(SherpaTrade.Notices(true, facing, new Vector2(0f, 1f)), "ahead");
        }

        [Test]
        public void AStandingSherpa_NoticesFromAnywhere()
        {
            Assert.IsTrue(SherpaTrade.Notices(false, Vector2.up, new Vector2(0f, -1f)));
            Assert.IsTrue(SherpaTrade.Notices(true, Vector2.up, Vector2.zero), "on top of them");
        }

        [Test]
        public void ANoticedTheft_EvenASmallOne_StopsAFreshSherpaTrading()
        {
            var disposition = new Disposition();
            Assert.IsTrue(SherpaTrade.Trades(disposition.Value));
            disposition.Lower(SherpaTrade.OwnPointsFor(3f));
            Assert.AreEqual(10, SherpaTrade.OwnPointsFor(3f), "twice the least points of a theft");
            Assert.IsFalse(SherpaTrade.Trades(disposition.Value));
        }

        [Test]
        public void TheirPrice_IsHalfAsMuchAgainAsTheList_AtLeastACoin()
        {
            Assert.AreEqual(15, SherpaTrade.Asking(10));
            Assert.AreEqual(2, SherpaTrade.Asking(1));
            Assert.AreEqual(1, SherpaTrade.Asking(0));
        }
    }
}
