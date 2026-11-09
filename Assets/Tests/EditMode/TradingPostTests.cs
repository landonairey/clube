using System.Collections.Generic;
using Clube.Core;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>The trading post (GL470-GL475): its name, the keepers' disposition, the open pit and the cave.</summary>
    public class TradingPostTests
    {
        [Test]
        public void SettlementNames_SameSeedAndParcel_SameName()
        {
            Assert.AreEqual(SettlementNames.Generate(1234, new Vector2Int(3, -2)), SettlementNames.Generate(1234, new Vector2Int(3, -2)));
        }

        [Test]
        public void SettlementNames_AreCapitalisedWordsAndVary()
        {
            var names = new HashSet<string>();
            for (int i = 0; i < 50; i++)
            {
                string name = SettlementNames.Generate(i * 7919, new Vector2Int(i, -i));
                Assert.IsFalse(string.IsNullOrEmpty(name));
                Assert.IsTrue(char.IsUpper(name[0]), name);
                Assert.IsFalse(name.Contains(" "), name);
                names.Add(name);
            }
            // Fifty settlements get many different names, not a handful.
            Assert.Greater(names.Count, 35);
        }

        [Test]
        public void SettlementNames_OtherParcelOfOneWorld_UsuallyAnotherName()
        {
            int differ = 0;
            for (int i = 0; i < 20; i++)
            {
                if (SettlementNames.Generate(99, new Vector2Int(i, 0)) != SettlementNames.Generate(99, new Vector2Int(i, 10)))
                {
                    differ++;
                }
            }
            Assert.Greater(differ, 15);
        }

        [Test]
        public void Disposition_StartsAtTen_RisesOnePointPerHundredCoins()
        {
            var disposition = new Disposition();
            Assert.AreEqual(10, disposition.Value);
            Assert.AreEqual(0, disposition.Record(99f));
            Assert.AreEqual(1, disposition.Record(1f));
            Assert.AreEqual(11, disposition.Value);
            Assert.AreEqual(3, disposition.Record(350f));
            Assert.AreEqual(14, disposition.Value);
            Assert.AreEqual(50f, disposition.Carried, 1e-3f);
        }

        [Test]
        public void Disposition_StopsAtHundred()
        {
            var disposition = new Disposition(98);
            Assert.AreEqual(2, disposition.Record(100000f));
            Assert.AreEqual(100, disposition.Value);
            Assert.AreEqual(0f, disposition.Carried);
            Assert.AreEqual(0, disposition.Record(500f));
        }

        [Test]
        public void Disposition_EachPointUnderHundred_IsAPointOfMarkupAndAPointOffWhatTheyPay()
        {
            var cold = new Disposition(10);
            Assert.AreEqual(190, cold.SellingPrice(100));
            Assert.AreEqual(0.1f, cold.PayShare, 1e-5f);
            var fair = new Disposition(100);
            Assert.AreEqual(100, fair.SellingPrice(100));
            Assert.AreEqual(1f, fair.PayShare, 1e-5f);
            var warm = new Disposition(75);
            Assert.AreEqual(63, warm.SellingPrice(50)); // 62.5 rounds up
            Assert.AreEqual(0.75f, warm.PayShare, 1e-5f);
        }

        [Test]
        public void PriceList_Scaled_PaysItsShare()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            PriceList list = PriceList.Create(new ItemPrice(item, 40));
            PriceList scaled = list.Scaled(0.25f);
            Assert.IsTrue(scaled.TryGetPrice(item, out int coins));
            Assert.AreEqual(40, coins);
            var inventory = new Inventory(4);
            inventory.Add(item, 2);
            var wallet = new Wallet();
            Assert.AreEqual(2, scaled.Sell(item, 2, inventory, wallet));
            Assert.AreEqual(20f, wallet.Coins, 1e-3f);
            Assert.AreEqual(1f, list.Share);
        }

        [Test]
        public void Disposition_Restore_ClampsAndRaisesChanged()
        {
            var disposition = new Disposition();
            int changes = 0;
            disposition.Changed += () => changes++;
            disposition.Restore(140, 50f);
            Assert.AreEqual(100, disposition.Value);
            Assert.AreEqual(0f, disposition.Carried);
            Assert.AreEqual(1, changes);
        }
    }
}
