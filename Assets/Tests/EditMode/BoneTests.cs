using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Clube.Core.Tests
{
    /// <summary>Bones and the bone tools (GL877, GL878), from the item assets.</summary>
    public class BoneTests
    {
        private ItemCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Config/Items/ItemCatalog.asset");
        }

        private ToolDefinition Tool(string id) => (ToolDefinition)catalog.Find(id);

        [Test]
        public void EverySpeciesLeavesBones_OfTheCatalogsBoneItem()
        {
            Assert.IsNotNull(catalog.Find(AnimalSpeciesTable.BoneId));
            foreach (AnimalSpecies species in AnimalSpeciesTable.All)
            {
                var loot = new List<AnimalLoot>();
                species.RollDrops(new System.Random(7), loot);
                Assert.IsTrue(loot.Any(l => l.ItemId == AnimalSpeciesTable.BoneId && l.Count > 0), species.Id + " leaves bones");
            }
        }

        [Test]
        public void BoneTools_SitBetweenStoneAndCopper()
        {
            foreach (string kind in new[] { "pick", "hatchet" })
            {
                ToolDefinition stone = Tool("stone-" + kind), bone = Tool("bone-" + kind), copper = Tool("copper-" + kind);
                Assert.IsNotNull(bone, kind);
                Assert.That(bone.Power, Is.GreaterThanOrEqualTo(stone.Power).And.LessThanOrEqualTo(copper.Power), kind + " power");
                Assert.That(bone.MaxHits, Is.GreaterThan(stone.MaxHits).And.LessThan(copper.MaxHits), kind + " durability");
                Assert.That(bone.HitsPerSecond, Is.GreaterThan(stone.HitsPerSecond).And.LessThan(copper.HitsPerSecond), kind + " speed");
                Assert.That(bone.Tier, Is.GreaterThanOrEqualTo(stone.Tier).And.LessThanOrEqualTo(copper.Tier), kind + " tier");
            }
        }

        [Test]
        public void HandAxeKeepsItsId_UnderItsNewStoneName()
        {
            Assert.AreEqual("Stone hand axe", catalog.Find("hand-axe").DisplayName);
            Assert.AreEqual("hand-axe", catalog.Find("Stone hand axe").Id);
            Assert.IsNotNull(catalog.Find("bone-hand-axe"));
        }
    }
}
