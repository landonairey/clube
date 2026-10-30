using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>The player's skills on the beginning path (GL47, first pass of SK1).</summary>
    public enum SkillType
    {
        None,
        Crafting,
        Woodcutting,
        Mining,
        Smelting,
        Blacksmithing,
    }

    /// <summary>
    /// Experience and levels in each <see cref="SkillType"/> (GL47): experience only grows, and a level
    /// is reached at a set total, each level costing 50% more than the last (GL184: 20 xp for level 1,
    /// then 30, 45, ...). Plain data, so the player, saves (S3) and tests share it.
    /// </summary>
    public sealed class SkillSet
    {
        /// <summary>The highest level a skill reaches.</summary>
        public const int MaxLevel = 20;

        private readonly float[] experience = new float[Enum.GetValues(typeof(SkillType)).Length];

        /// <summary>Raised when a skill reaches a new level, with the skill and the new level.</summary>
        public event Action<SkillType, int> LevelReached;

        /// <summary>Raised after any experience is gained.</summary>
        public event Action Changed;

        /// <summary>Experience the first level costs (GL184).</summary>
        public const float FirstLevelCost = 20f;

        /// <summary>How much more each level costs than the one before (GL184): 50% more.</summary>
        public const float LevelCostGrowth = 1.5f;

        /// <summary>
        /// Total experience needed for a level (GL184): the first costs 20, and each after it 50% more than
        /// the last (20, 30, 45, 67.5 ...), so the totals run 20, 50, 95, 162.5, 263.8 ...
        /// </summary>
        public static float ExperienceFor(int level)
        {
            level = Mathf.Clamp(level, 0, MaxLevel);
            // The sum of a geometric series: 20 (1.5^n - 1) / 0.5.
            return FirstLevelCost * (Mathf.Pow(LevelCostGrowth, level) - 1f) / (LevelCostGrowth - 1f);
        }

        public float Experience(SkillType skill)
        {
            return skill == SkillType.None ? 0f : experience[(int)skill];
        }

        public int Level(SkillType skill)
        {
            float xp = Experience(skill);
            int level = 0;
            while (level < MaxLevel && xp + 1e-4f >= ExperienceFor(level + 1))
            {
                level++;
            }
            return level;
        }

        /// <summary>True when the skill is at least <paramref name="level"/>; always for <see cref="SkillType.None"/>.</summary>
        public bool Has(SkillType skill, int level)
        {
            return skill == SkillType.None || Level(skill) >= level;
        }

        /// <summary>Adds experience; raises <see cref="LevelReached"/> once for every level it passes.</summary>
        public void Award(SkillType skill, float amount)
        {
            if (skill == SkillType.None || amount <= 0f)
            {
                return;
            }
            int before = Level(skill);
            experience[(int)skill] += amount;
            int after = Level(skill);
            for (int level = before + 1; level <= after; level++)
            {
                LevelReached?.Invoke(skill, level);
            }
            Changed?.Invoke();
        }

        /// <summary>Raises a skill to at least <paramref name="level"/> (cheats, tests); never lowers it.</summary>
        public void RaiseTo(SkillType skill, int level)
        {
            if (skill == SkillType.None)
            {
                return;
            }
            float needed = ExperienceFor(level) - Experience(skill);
            if (needed > 0f)
            {
                Award(skill, needed);
            }
        }
    }
}
