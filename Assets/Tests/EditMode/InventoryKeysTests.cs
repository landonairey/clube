using NUnit.Framework;

namespace Clube.Core.Tests
{
    /// <summary>When E opens or closes the inventory (GL298, GL852).</summary>
    public class InventoryKeysTests
    {
        [Test]
        public void EOpensTheInventoryWithNothingAimedAt()
        {
            Assert.AreEqual(InventoryKeys.Result.Open, InventoryKeys.OnInteract(false, false, false, false));
        }

        [Test]
        public void EClosesTheOpenInventory()
        {
            Assert.AreEqual(InventoryKeys.Result.Close, InventoryKeys.OnInteract(true, false, false, false));
        }

        [Test]
        public void EUsesWhatIsAimedAtInstead()
        {
            Assert.AreEqual(InventoryKeys.Result.Nothing, InventoryKeys.OnInteract(false, false, false, true));
        }

        [Test]
        public void EClosingAClamLeavesTheInventoryShut()
        {
            // The clam panel is still open when the inventory reads the key first...
            Assert.AreEqual(InventoryKeys.Result.Nothing, InventoryKeys.OnInteract(false, true, false, false));
            // ...or has just closed on this key when it reads it after.
            Assert.AreEqual(InventoryKeys.Result.Nothing, InventoryKeys.OnInteract(false, false, true, false));
        }
    }
}
