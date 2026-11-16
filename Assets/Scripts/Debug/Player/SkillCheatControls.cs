using Clube.Core;
using Clube.Game;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Skill cheats in the lab panel's Cheats section (GL630), and so in the <see cref="LabPanel"/>'s Inspector (M22):
    /// "Max all skills", "Reset all skills", and a level slider for every skill there is (<see cref="SkillSet.Types"/>,
    /// so a new skill gets its slider with no change here). They set levels through <see cref="SkillSet.SetLevel"/>, which
    /// the skill bars and journals read like any other. Lab-only (<c>Clube.Debug</c>): the Game scene never has them
    /// (A10).
    /// </summary>
    public static class SkillCheatControls
    {
        /// <summary>Draws the controls; returns what a cheat did, or null when nothing was pressed.</summary>
        public static string Draw(LabPanelFrame frame, PlayerSkills skills)
        {
            if (skills == null)
            {
                return null;
            }
            string result = null;
            switch (frame.ButtonRow("Max all skills", "Reset all skills"))
            {
                case 0:
                    result = CheatCommands.SetAllSkills(skills, SkillSet.MaxLevel);
                    break;
                case 1:
                    result = CheatCommands.SetAllSkills(skills, 0);
                    break;
            }
            foreach (SkillType skill in SkillSet.Types)
            {
                int level = skills.Skills.Level(skill);
                int picked = Mathf.RoundToInt(frame.Slider($"{skill} level", level, 0f, SkillSet.MaxLevel, "0"));
                if (picked != level)
                {
                    result = CheatCommands.SetSkill(skills, skill, picked);
                }
            }
            return result;
        }
    }
}
