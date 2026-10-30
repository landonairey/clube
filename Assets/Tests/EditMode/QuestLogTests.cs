using System.Linq;
using Clube.Core;
using NUnit.Framework;

namespace Clube.Core.Tests
{
    public class QuestLogTests
    {
        private static QuestLog TwoSections()
        {
            var log = new QuestLog();
            log.Add("a", "First", "Main");
            log.Add("x", "Side one", "Side");
            log.Add("b", "Second", "Main");
            return log;
        }

        [Test]
        public void Quests_KeepSectionOrder_AndTheActiveIsTheFirstNotDone()
        {
            QuestLog log = TwoSections();
            CollectionAssert.AreEqual(new[] { "a", "b", "x" }, log.Quests.Select(q => q.Id).ToArray());
            Assert.AreEqual("a", log.Active.Id);
            log.Complete("b");
            Assert.AreEqual("a", log.Active.Id);
            log.Complete("a");
            Assert.AreEqual("x", log.Active.Id, "Side quests come once the main ones are done.");
            log.Complete("x");
            Assert.IsNull(log.Active);
            Assert.AreEqual(3, log.DoneCount);
        }

        [Test]
        public void Complete_RaisesOnce_AndRestoreIsQuiet()
        {
            QuestLog log = TwoSections();
            int raised = 0;
            log.Completed += _ => raised++;
            Assert.IsTrue(log.Complete("a"));
            Assert.IsFalse(log.Complete("a"));
            Assert.IsFalse(log.Complete("missing"));
            Assert.AreEqual(1, raised);

            QuestLog loaded = TwoSections();
            loaded.Completed += _ => raised++;
            loaded.Restore(log.DoneIds.Concat(new[] { "gone" }));
            Assert.IsTrue(loaded.IsDone("a"));
            Assert.IsFalse(loaded.IsDone("b"));
            Assert.AreEqual(1, raised);
        }

        [Test]
        public void Add_RefusesADuplicateId()
        {
            QuestLog log = TwoSections();
            Assert.Throws<System.ArgumentException>(() => log.Add("a", "Again", "Main"));
        }

        // GL216: the quests done are kept with the player in the save (version 3).
        [Test]
        public void QuestsDone_ComeBackFromASave()
        {
            var save = new GameSave { Header = new SaveHeader { Mode = "World", Seed = 1, Build = "b" }, Player = new PlayerSave() };
            save.Player.Quests.Add("pick-up-stone");
            save.Player.Quests.Add("find-gold");
            using var stream = new System.IO.MemoryStream();
            GameSaveFile.Write(stream, save);
            stream.Position = 0;
            GameSave read = GameSaveFile.Read(stream);
            CollectionAssert.AreEqual(new[] { "pick-up-stone", "find-gold" }, read.Player.Quests);
        }
    }
}
