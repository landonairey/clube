using System;

namespace Clube.Core
{
    /// <summary>
    /// Lessons bought from a shopkeeper (GL402): a lesson raises a skill to its next level, for coins in proportion to the
    /// experience that level still needs, so each level costs more than the last and xp already earned is a discount.
    /// </summary>
    public static class SkillTraining
    {
        /// <summary>Experience the skill needs to reach its next level; 0 at the top level.</summary>
        public static float ExperienceToNext(SkillSet skills, SkillType skill)
        {
            int level = skills.Level(skill);
            if (skill == SkillType.None || level >= SkillSet.MaxLevel)
            {
                return 0f;
            }
            return Math.Max(0f, SkillSet.ExperienceFor(level + 1) - skills.Experience(skill));
        }

        /// <summary>Whole coins a lesson to the next level costs; 0 when there's no level left to reach.</summary>
        public static int Cost(SkillSet skills, SkillType skill, float coinsPerExperience)
        {
            float needed = ExperienceToNext(skills, skill);
            return needed <= 0f ? 0 : Math.Max(1, (int)Math.Ceiling(needed * coinsPerExperience - 1e-4f));
        }

        /// <summary>Pays for a lesson and gives the experience; false (and nothing changes) when it can't be afforded.</summary>
        public static bool Train(SkillSet skills, Wallet wallet, SkillType skill, float coinsPerExperience)
        {
            float needed = ExperienceToNext(skills, skill);
            if (needed <= 0f || !wallet.Spend(Cost(skills, skill, coinsPerExperience)))
            {
                return false;
            }
            // A hair over, so float error never leaves the skill a sliver short of the level.
            skills.Award(skill, needed + 1e-3f);
            return true;
        }
    }
}
