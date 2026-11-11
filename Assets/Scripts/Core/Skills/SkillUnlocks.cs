using System.Collections.Generic;
using System.Linq;

namespace Clube.Core
{
    /// <summary>One thing a skill level unlocks (GL573): "Hand axe", "you can tell copper ore apart".</summary>
    public readonly struct SkillUnlock
    {
        public SkillUnlock(int level, string text)
        {
            Level = level;
            Text = text;
        }

        public int Level { get; }

        public string Text { get; }
    }

    /// <summary>Everything one skill level unlocks.</summary>
    public sealed class SkillUnlockGroup
    {
        public SkillUnlockGroup(int level, IReadOnlyList<string> texts)
        {
            Level = level;
            Texts = texts;
        }

        public int Level { get; }

        public IReadOnlyList<string> Texts { get; }
    }

    /// <summary>
    /// A skill's chapter in the skills notebook (GL573): what each level reached so far unlocked, and the next level's
    /// unlocks only. Nothing further off is told.
    /// </summary>
    public sealed class SkillChapter
    {
        public SkillChapter(SkillType skill, int level, IReadOnlyList<SkillUnlockGroup> reached, SkillUnlockGroup next)
        {
            Skill = skill;
            Level = level;
            Reached = reached;
            Next = next;
        }

        public SkillType Skill { get; }

        public int Level { get; }

        /// <summary>The unlocks at each level up to the current one, lowest first.</summary>
        public IReadOnlyList<SkillUnlockGroup> Reached { get; }

        /// <summary>The nearest level above the current one that unlocks anything, or null when there is none.</summary>
        public SkillUnlockGroup Next { get; }
    }

    /// <summary>Works out what a skill unlocks from the game's own data rather than a hand-written list (GL573).</summary>
    public static class SkillUnlocks
    {
        /// <summary>
        /// Every unlock of a skill: the recipes that need a level of it (named by what they make), the hidden ore groups it
        /// reveals, and, for Prospecting, what each level lets the player read of a mix (<see cref="ProspectReading.UnlockAt"/>).
        /// </summary>
        public static List<SkillUnlock> Collect(SkillType skill, IEnumerable<Recipe> recipes, IEnumerable<(string Name, int Level)> revealedOres, SkillType oreSkill)
        {
            var all = new List<SkillUnlock>();
            if (recipes != null)
            {
                foreach (Recipe recipe in recipes)
                {
                    if (recipe != null && recipe.UnlockSkill == skill && recipe.Output.Item != null)
                    {
                        all.Add(new SkillUnlock(recipe.UnlockLevel, recipe.Output.Item.DisplayName));
                    }
                }
            }
            if (revealedOres != null && oreSkill == skill)
            {
                foreach ((string name, int level) in revealedOres)
                {
                    all.Add(new SkillUnlock(level, $"you can tell {name} apart"));
                }
            }
            if (skill == SkillType.Prospecting)
            {
                for (int level = 1; level <= ProspectReading.ExactLevel; level++)
                {
                    string text = ProspectReading.UnlockAt(level);
                    if (text != null)
                    {
                        all.Add(new SkillUnlock(level, text));
                    }
                }
            }
            return all;
        }

        /// <summary>Splits a skill's unlocks at the player's level: those reached, grouped by level, and the next level's.</summary>
        public static SkillChapter Build(SkillType skill, int level, IEnumerable<SkillUnlock> unlocks)
        {
            List<SkillUnlockGroup> groups = unlocks
                .GroupBy(u => u.Level)
                .OrderBy(g => g.Key)
                .Select(g => new SkillUnlockGroup(g.Key, g.Select(u => u.Text).Distinct().ToList()))
                .ToList();
            return new SkillChapter(skill, level, groups.Where(g => g.Level <= level).ToList(), groups.FirstOrDefault(g => g.Level > level));
        }
    }
}
