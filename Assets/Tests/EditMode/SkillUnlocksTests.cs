using System.Collections.Generic;
using Clube.Core;
using NUnit.Framework;

namespace Clube.Core.Tests
{
    public class SkillUnlocksTests
    {
        [Test]
        public void Build_SplitsAtTheLevel_AndPreviewsOnlyTheNextLevel()
        {
            var unlocks = new List<SkillUnlock>
            {
                new SkillUnlock(1, "A"), new SkillUnlock(1, "B"), new SkillUnlock(3, "C"), new SkillUnlock(5, "D"), new SkillUnlock(7, "E"),
            };
            SkillChapter chapter = SkillUnlocks.Build(SkillType.Crafting, 3, unlocks);
            Assert.AreEqual(2, chapter.Reached.Count);
            Assert.AreEqual(new[] { "A", "B" }, chapter.Reached[0].Texts);
            Assert.AreEqual(3, chapter.Reached[1].Level);
            Assert.AreEqual(5, chapter.Next.Level);
            Assert.AreEqual(new[] { "D" }, chapter.Next.Texts);
        }

        [Test]
        public void Build_AtTheTop_HasNoNext()
        {
            SkillChapter chapter = SkillUnlocks.Build(SkillType.Mining, 9, new[] { new SkillUnlock(2, "X") });
            Assert.IsNull(chapter.Next);
            Assert.AreEqual(1, chapter.Reached.Count);
        }

        [Test]
        public void Build_AtLevelZero_ShowsNothingReached()
        {
            SkillChapter chapter = SkillUnlocks.Build(SkillType.Mining, 0, new[] { new SkillUnlock(2, "X") });
            Assert.AreEqual(0, chapter.Reached.Count);
            Assert.AreEqual(2, chapter.Next.Level);
        }

        [Test]
        public void Collect_ReadsRecipeRequirements_ForTheirSkillOnly()
        {
            ItemDefinition stone = ItemDefinition.Create("stone", "Stone");
            ItemDefinition pick = ItemDefinition.Create("pick", "Pick");
            Recipe crafting = Recipe.Create(StationKind.Worktop, new ItemAmount(pick, 1), RecipeWork.Strikes, 1f, new ItemAmount(stone, 1))
                .WithUnlock(SkillType.Crafting, 2);
            Recipe smelting = Recipe.Create(StationKind.Worktop, new ItemAmount(stone, 1), RecipeWork.Strikes, 1f, new ItemAmount(pick, 1))
                .WithUnlock(SkillType.Smelting, 4);
            List<SkillUnlock> found = SkillUnlocks.Collect(SkillType.Crafting, new[] { crafting, smelting }, null, SkillType.Mining);
            Assert.AreEqual(1, found.Count);
            Assert.AreEqual(2, found[0].Level);
            Assert.AreEqual("Pick", found[0].Text);
        }

        [Test]
        public void Collect_AddsOreReveals_OnlyForTheOreSkill()
        {
            var ores = new[] { ("copper ore", 3) };
            Assert.AreEqual(1, SkillUnlocks.Collect(SkillType.Mining, null, ores, SkillType.Mining).Count);
            Assert.AreEqual(0, SkillUnlocks.Collect(SkillType.Smelting, null, ores, SkillType.Mining).Count);
        }

        [Test]
        public void Prospecting_LevelOne_SeesTheSecondItemInAMix()
        {
            StringAssert.Contains("second item in a material mix", ProspectReading.UnlockAt(1));
            StringAssert.Contains("third item", ProspectReading.UnlockAt(2));
            StringAssert.Contains("exact shares", ProspectReading.UnlockAt(ProspectReading.ExactLevel));
            Assert.IsNull(ProspectReading.UnlockAt(0));
            Assert.IsNull(ProspectReading.UnlockAt(ProspectReading.ExactLevel + 1));
        }

        [Test]
        public void Collect_Prospecting_ListsEveryLevelUpToExact()
        {
            Assert.AreEqual(ProspectReading.ExactLevel, SkillUnlocks.Collect(SkillType.Prospecting, null, null, SkillType.Mining).Count);
        }
    }
}
