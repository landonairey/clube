using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class MiningJournalTests
    {
        private VoxelMaterial tin;
        private ItemDefinition ore;
        private ItemDefinition bun;
        private Recipe smelt;

        [SetUp]
        public void SetUp()
        {
            tin = VoxelMaterial.Create(19, "Cassiterite", VoxelMaterialCategory.Ore);
            ore = ItemDefinition.Create("cassiterite", "Cassiterite");
            bun = ItemDefinition.Create("tin-bun", "Tin bun");
            smelt = Recipe.Create(StationKind.Campfire, new ItemAmount(bun, 1), RecipeWork.Time, 6f, new ItemAmount(ore, 4));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(smelt);
            Object.DestroyImmediate(bun);
            Object.DestroyImmediate(ore);
            Object.DestroyImmediate(tin);
        }

        [Test]
        public void FirstFind_KeepsItsDepth_AndLaterFindsWidenTheGrades()
        {
            var journal = new MiningJournal();
            int discovered = 0;
            journal.Discovered += _ => discovered++;

            Assert.IsFalse(journal.HasFound(tin));
            Assert.IsTrue(journal.Record(tin, 2.5f, 0.3f));
            Assert.IsFalse(journal.Record(tin, 9f, 0.5f), "Only the first find is a discovery.");
            journal.Record(tin, 4f, 0.25f);

            MiningJournal.Entry entry = journal.EntryFor(tin);
            Assert.AreEqual(1, discovered);
            Assert.AreEqual(2.5f, entry.FirstDepth);
            Assert.AreEqual(0.25f, entry.LowestGrade);
            Assert.AreEqual(0.5f, entry.HighestGrade);
            Assert.AreEqual(3, entry.Samples);
        }

        [Test]
        public void Recipes_AreKnownOnceMade()
        {
            var journal = new MiningJournal();
            Assert.IsFalse(journal.HasMade(smelt));
            journal.RecordMade(smelt);
            Assert.IsTrue(journal.HasMade(smelt));
        }
    }
}
