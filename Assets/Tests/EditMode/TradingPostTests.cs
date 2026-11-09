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
    }
}
