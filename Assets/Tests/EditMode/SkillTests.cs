using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>Skills, recipe unlocks and the experience a finished recipe gives (GL47, GL48).</summary>
    public class SkillTests
    {
        [Test]
        public void Levels_FollowTheCurve()
        {
            var skills = new SkillSet();
            Assert.AreEqual(0, skills.Level(SkillType.Mining));
            skills.Award(SkillType.Mining, 19f);
            Assert.AreEqual(0, skills.Level(SkillType.Mining));
            skills.Award(SkillType.Mining, 1f);
            Assert.AreEqual(1, skills.Level(SkillType.Mining));
            skills.Award(SkillType.Mining, 40f);
            Assert.AreEqual(2, skills.Level(SkillType.Mining), "60 xp in total is level 2.");
            Assert.AreEqual(0, skills.Level(SkillType.Crafting), "Skills are separate.");
        }

        [Test]
        public void Award_RaisesLevelReached_OncePerLevelPassed()
        {
            var skills = new SkillSet();
            var reached = new List<int>();
            skills.LevelReached += (skill, level) => reached.Add(level);
            skills.Award(SkillType.Smelting, 130f);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, reached);
        }

        [Test]
        public void RaiseTo_NeverLowers()
        {
            var skills = new SkillSet();
            skills.RaiseTo(SkillType.Crafting, 3);
            Assert.AreEqual(3, skills.Level(SkillType.Crafting));
            skills.RaiseTo(SkillType.Crafting, 1);
            Assert.AreEqual(3, skills.Level(SkillType.Crafting));
            Assert.IsTrue(skills.Has(SkillType.None, 20), "No skill needed is always had.");
        }

        [Test]
        public void LockedRecipe_DoesNotStart_UntilItsLevel_ThenGivesExperience()
        {
            ItemDefinition stone = ItemDefinition.Create("stone", "Stone");
            ItemDefinition stick = ItemDefinition.Create("stick", "Stick");
            ItemDefinition pick = ItemDefinition.Create("stone-pick", "Stone pick");
            Recipe recipe = Recipe.Create(StationKind.Worktop, new ItemAmount(pick, 1), RecipeWork.Strikes, 1f,
                    new ItemAmount(stick, 1), new ItemAmount(stone, 2))
                .WithUnlock(SkillType.Crafting, 2)
                .WithExperience(SkillType.Crafting, 10f);
            try
            {
                var skills = new SkillSet();
                var station = new CraftingStation(StationKind.Worktop, new[] { recipe }) { Learner = skills };
                station.Completed += done => skills.Award(done.ExperienceSkill, done.Experience);
                station.Input.Add(stick, 1);
                station.Input.Add(stone, 2);

                Assert.IsNull(station.Ready(RecipeWork.Strikes), "Locked below Crafting 2.");
                Assert.IsFalse(station.Strike());

                skills.RaiseTo(SkillType.Crafting, 2);
                Assert.AreSame(recipe, station.Ready(RecipeWork.Strikes));
                Assert.IsTrue(station.Strike());
                Assert.AreEqual(pick, station.Output[0].Item);
                Assert.AreEqual(SkillSet.ExperienceFor(2) + 10f, skills.Experience(SkillType.Crafting), 1e-3f);
            }
            finally
            {
                Object.DestroyImmediate(recipe);
                Object.DestroyImmediate(stone);
                Object.DestroyImmediate(stick);
                Object.DestroyImmediate(pick);
            }
        }

        [Test]
        public void StationWithoutLearner_IgnoresUnlocks()
        {
            ItemDefinition wood = ItemDefinition.Create("wood", "Wood");
            ItemDefinition stick = ItemDefinition.Create("stick", "Stick");
            Recipe recipe = Recipe.Create(StationKind.Worktop, new ItemAmount(stick, 1), RecipeWork.Strikes, 1f, new ItemAmount(wood, 1))
                .WithUnlock(SkillType.Crafting, 5);
            try
            {
                var station = new CraftingStation(StationKind.Worktop, new[] { recipe });
                station.Input.Add(wood, 1);
                Assert.AreSame(recipe, station.Ready(RecipeWork.Strikes));
            }
            finally
            {
                Object.DestroyImmediate(recipe);
                Object.DestroyImmediate(wood);
                Object.DestroyImmediate(stick);
            }
        }
    }
}
