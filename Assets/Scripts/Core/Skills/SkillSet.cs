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
    }

    /// <summary>
    /// Experience and levels in each <see cref="SkillType"/> (GL47): experience only grows, and a level
    /// is reached at a set total, each level costing more than the last (20 xp for level 1, then
    /// 60, 120, 200, ...). Plain data, so the player, saves (S3) and tests share it.
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

        /// <summary>Total experience needed for a level: 10 n (n + 1), so 20, 60, 120, 200, 300 ...</summary>
        public static float ExperienceFor(int level)
        {
            level = Mathf.Clamp(level, 0, MaxLevel);
            return 10f * level * (level + 1);
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
