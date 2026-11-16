using System;
using System.Collections.Generic;
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
        Prospecting,

        /// <summary>Working gold and silver into jewellery (GL455; the recipes come later).</summary>
        Goldsmithing,
    }

    /// <summary>
    /// Experience and levels in each <see cref="SkillType"/> (GL47): experience only grows, and a level
    /// is reached at a set total, each level costing 60% more than the last (300 xp for level 1, then 480,
    /// 768, ...; GL629). Plain data, so the player, saves (S3) and tests share it.
    /// </summary>
    public sealed class SkillSet
    {
        /// <summary>The highest level a skill reaches.</summary>
        public const int MaxLevel = 20;

        /// <summary>Every skill there is, in the enum's order, without <see cref="SkillType.None"/>: a new one is listed with no other change.</summary>
        public static readonly IReadOnlyList<SkillType> Types = ListTypes();

        private static SkillType[] ListTypes()
        {
            var types = new List<SkillType>();
            foreach (SkillType type in Enum.GetValues(typeof(SkillType)))
            {
                if (type != SkillType.None)
                {
                    types.Add(type);
                }
            }
            return types.ToArray();
        }

        private readonly float[] experience = new float[Enum.GetValues(typeof(SkillType)).Length];

        /// <summary>Raised when a skill reaches a new level, with the skill and the new level.</summary>
        public event Action<SkillType, int> LevelReached;

        /// <summary>Raised after any experience is gained.</summary>
        public event Action Changed;

        /// <summary>
        /// Experience the first level costs (GL184; 30 since GL289; 300 since GL629, "so it takes 10x as long"). With
        /// <see cref="LevelCostGrowth"/> this is the whole curve: every level of every skill scales with it, whatever the
        /// experience came from.
        /// </summary>
        public const float FirstLevelCost = 300f;

        /// <summary>What <see cref="FirstLevelCost"/> was before GL629: saves from then carry no cost of their own.</summary>
        public const float FirstLevelCostBeforeGL629 = 30f;

        /// <summary>How much more each level costs than the one before (GL184; 60% more since GL289).</summary>
        public const float LevelCostGrowth = 1.6f;

        /// <summary>
        /// Total experience needed for a level (GL184): the first costs <see cref="FirstLevelCost"/>, and each after it
        /// <see cref="LevelCostGrowth"/> times the last (300, 480, 768 ...), so the totals run 300, 780, 1548 ...
        /// </summary>
        public static float ExperienceFor(int level)
        {
            level = Mathf.Clamp(level, 0, MaxLevel);
            // The sum of a geometric series: 300 (1.6^n - 1) / 0.6.
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

        /// <summary>True while no experience is earned (the tutorial, GL527).</summary>
        public bool Suppressed { get; set; }

        /// <summary>Adds experience; raises <see cref="LevelReached"/> once for every level it passes.</summary>
        public void Award(SkillType skill, float amount)
        {
            if (Suppressed || skill == SkillType.None || amount <= 0f)
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

        /// <summary>Sets a skill's experience as a save had it (GL205), raising no level events.</summary>
        public void SetExperience(SkillType skill, float amount)
        {
            if (skill == SkillType.None)
            {
                return;
            }
            experience[(int)skill] = Mathf.Max(0f, amount);
            Changed?.Invoke();
        }

        /// <summary>
        /// Experience earned on a curve whose first level cost <paramref name="firstLevelCost"/>, as experience on today's
        /// curve (GL629): the same level and the same share of the way to the next, since only the first level's cost
        /// differs. A cost of 0 or less is a save from before the cost was written down
        /// (<see cref="FirstLevelCostBeforeGL629"/>).
        /// </summary>
        public static float FromCurve(float experience, float firstLevelCost)
        {
            if (firstLevelCost <= 0f)
            {
                firstLevelCost = FirstLevelCostBeforeGL629;
            }
            return Mathf.Max(0f, experience) * (FirstLevelCost / firstLevelCost);
        }

        /// <summary>
        /// Puts a skill at exactly <paramref name="level"/> (0 to <see cref="MaxLevel"/>), up or down, with no experience
        /// towards the next (GL630: the lab's cheats). Going up raises <see cref="LevelReached"/> for every level passed,
        /// as earning them would; works while <see cref="Suppressed"/>.
        /// </summary>
        public void SetLevel(SkillType skill, int level)
        {
            if (skill == SkillType.None)
            {
                return;
            }
            level = Mathf.Clamp(level, 0, MaxLevel);
            int before = Level(skill);
            experience[(int)skill] = ExperienceFor(level);
            for (int reached = before + 1; reached <= level; reached++)
            {
                LevelReached?.Invoke(skill, reached);
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
