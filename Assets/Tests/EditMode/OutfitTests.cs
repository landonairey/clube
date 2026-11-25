using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>Clothes and shopkeepers (list inventory prototype, item 20): the outfit's points as a disposition bonus.</summary>
    public class OutfitTests
    {
        [Test]
        public void Outfit_RagsClothLeather_RiseInOrder()
        {
            Assert.AreEqual(0, Outfit.DispositionBonus(0));
            Assert.AreEqual("rags", Outfit.Tier(0));
            Assert.AreEqual("cloth", Outfit.Tier(3 * 2));
            Assert.AreEqual("leather", Outfit.Tier(6 * 3));
            Assert.Less(Outfit.DispositionBonus(3 * 2), Outfit.DispositionBonus(6 * 3));
        }

        [Test]
        public void Disposition_Bonus_MovesPricesNotValue()
        {
            var keeper = new Disposition(10);
            int changes = 0;
            keeper.Changed += () => changes++;
            keeper.Bonus = 18;
            Assert.AreEqual(10, keeper.Value);
            Assert.AreEqual(28, keeper.Effective);
            Assert.AreEqual(0.28f, keeper.PayShare, 1e-5f);
            Assert.AreEqual(1, changes);
            keeper.Bonus = 18;
            Assert.AreEqual(1, changes, "the same bonus again changes nothing");
            keeper.Bonus = 0;
            Assert.AreEqual(0.1f, keeper.PayShare, 1e-5f);
        }

        [Test]
        public void Disposition_Bonus_StopsAtFair()
        {
            var keeper = new Disposition(95) { Bonus = 18 };
            Assert.AreEqual(Disposition.Max, keeper.Effective);
            Assert.AreEqual(1f, keeper.PayShare, 1e-5f);
        }

        [Test]
        public void WornItem_KnowsItsSlotAndPoints()
        {
            ItemDefinition shirt = ItemDefinition.Create("test-shirt", "Shirt", 1).WithWear(WearSlot.Chest, 2);
            Assert.IsTrue(shirt.IsWorn);
            Assert.AreEqual(WearSlot.Chest, shirt.WornOn);
            Assert.AreEqual(2, shirt.OutfitPoints);
            Assert.IsFalse(ItemDefinition.Create("test-stone", "Stone").IsWorn);
            Object.DestroyImmediate(shirt);
        }
    }
}
