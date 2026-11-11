using System.Collections.Generic;
using System.Text;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The skills notebook (GL573), a tab of the field log (<see cref="FieldLogScreen"/>): one chapter, a page, per skill,
    /// listing what each level reached so far unlocked and the next level's unlocks only. The lists come from the
    /// recipes' skill requirements, the ore groups and <see cref="ProspectReading"/> (<see cref="SkillUnlocks"/>).
    /// </summary>
    [RequireComponent(typeof(PlayerSkills))]
    public class SkillsJournalScreen : MonoBehaviour
    {
        private static readonly SkillType[] Chapters =
        {
            SkillType.Crafting, SkillType.Woodcutting, SkillType.Mining, SkillType.Smelting,
            SkillType.Blacksmithing, SkillType.Prospecting, SkillType.Goldsmithing,
        };

        private readonly List<SkillChapter> chapters = new List<SkillChapter>();
        private PlayerSkills skills;
        private PlayerJournal journal;
        private OreKnowledge knowledge;
        private int spread;

        /// <summary>The chapters as last written.</summary>
        public IReadOnlyList<SkillChapter> Pages => chapters;

        private void Awake()
        {
            skills = GetComponent<PlayerSkills>();
            journal = GetComponent<PlayerJournal>();
            knowledge = GetComponent<OreKnowledge>();
        }

        /// <summary>Writes the chapters from the player's levels now; the field log calls it when the tab opens.</summary>
        public void Refresh()
        {
            chapters.Clear();
            IReadOnlyList<Recipe> recipes = journal != null ? journal.Recipes : null;
            var ores = new List<(string, int)>();
            if (knowledge != null)
            {
                foreach (OreKnowledge.HiddenOres group in knowledge.Groups)
                {
                    ores.Add((group.Name, group.Level));
                }
            }
            foreach (SkillType skill in Chapters)
            {
                List<SkillUnlock> all = SkillUnlocks.Collect(skill, recipes, ores, knowledge != null ? knowledge.Skill : SkillType.None);
                chapters.Add(SkillUnlocks.Build(skill, skills.Skills.Level(skill), all));
            }
        }

        /// <summary>Draws the skills in the open book; the field log calls it from its OnGUI.</summary>
        public void DrawBook()
        {
            if (chapters.Count == 0)
            {
                Refresh();
            }
            if (Event.current.type == EventType.Layout)
            {
                JournalBook.Prepare("Level 0123456789 Next unlocks: nothing more to learn, start ");
                foreach (SkillChapter chapter in chapters)
                {
                    JournalBook.Prepare(Describe(chapter));
                }
            }
            spread = JournalBook.Draw(spread, chapters.Count, (area, page) =>
            {
                SkillChapter chapter = chapters[page];
                Rect inner = JournalBook.Inner(area);
                float y = JournalBook.Heading(inner, $"{chapter.Skill}  (level {chapter.Level})", page);
                GUI.Label(new Rect(inner.x, y, inner.width, inner.yMax - y), Describe(chapter), JournalBook.Text);
            });
        }

        /// <summary>The page's text: each level reached with what it unlocked, then the next level's unlocks.</summary>
        public static string Describe(SkillChapter chapter)
        {
            var text = new StringBuilder();
            foreach (SkillUnlockGroup group in chapter.Reached)
            {
                text.AppendLine(group.Level == 0 ? "From the start:" : $"Level {group.Level}:");
                AppendAll(text, group);
                text.AppendLine();
            }
            if (chapter.Reached.Count == 0)
            {
                text.AppendLine("Nothing unlocked yet.");
                text.AppendLine();
            }
            if (chapter.Next != null)
            {
                text.AppendLine($"Next, level {chapter.Next.Level}:");
                AppendAll(text, chapter.Next);
            }
            else
            {
                text.AppendLine("Nothing more to learn.");
            }
            return text.ToString();
        }

        private static void AppendAll(StringBuilder text, SkillUnlockGroup group)
        {
            foreach (string line in group.Texts)
            {
                text.AppendLine("  - " + line);
            }
        }
    }
}
