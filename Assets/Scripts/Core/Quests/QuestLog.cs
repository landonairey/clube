using System;
using System.Collections.Generic;

namespace Clube.Core
{
    /// <summary>
    /// The player's quests (GL216): a checklist in sections ("Main Objectives", "Side Quests"), each quest done
    /// once and for good. Plain data: what counts as doing a quest is decided by whoever watches the game
    /// (<c>PlayerQuests</c>), which calls <see cref="Complete"/>. The active quest is the first one not done,
    /// in section order, so the main objectives lead until they're all done.
    /// </summary>
    public sealed class QuestLog
    {
        private readonly List<Quest> quests = new List<Quest>();
        private readonly List<string> sections = new List<string>();
        private readonly Dictionary<string, Quest> byId = new Dictionary<string, Quest>();

        /// <summary>Raised once when a quest is first done.</summary>
        public event Action<Quest> Completed;

        /// <summary>Every quest, in section order and then the order added.</summary>
        public IReadOnlyList<Quest> Quests => quests;

        /// <summary>The sections, in the order they were first named.</summary>
        public IReadOnlyList<string> Sections => sections;

        /// <summary>The first quest not done, main sections first; null once every quest is done.</summary>
        public Quest Active
        {
            get
            {
                foreach (string section in sections)
                {
                    foreach (Quest quest in quests)
                    {
                        if (quest.Section == section && !quest.Done)
                        {
                            return quest;
                        }
                    }
                }
                return null;
            }
        }

        /// <summary>How many quests are done.</summary>
        public int DoneCount
        {
            get
            {
                int count = 0;
                foreach (Quest quest in quests)
                {
                    count += quest.Done ? 1 : 0;
                }
                return count;
            }
        }

        /// <summary>The ids of the quests done, for saving.</summary>
        public IEnumerable<string> DoneIds
        {
            get
            {
                foreach (Quest quest in quests)
                {
                    if (quest.Done)
                    {
                        yield return quest.Id;
                    }
                }
            }
        }

        /// <summary>Adds a quest to a section (the section is made on first use). Ids are unique.</summary>
        public Quest Add(string id, string title, string section)
        {
            if (string.IsNullOrEmpty(id) || byId.ContainsKey(id))
            {
                throw new ArgumentException($"Quest id '{id}' is empty or already used.", nameof(id));
            }
            if (!sections.Contains(section))
            {
                sections.Add(section);
            }
            var quest = new Quest(id, title, section);
            // Kept in section order, so the list reads as the journal does.
            int at = quests.Count;
            int sectionIndex = sections.IndexOf(section);
            for (int i = 0; i < quests.Count; i++)
            {
                if (sections.IndexOf(quests[i].Section) > sectionIndex)
                {
                    at = i;
                    break;
                }
            }
            quests.Insert(at, quest);
            byId.Add(id, quest);
            return quest;
        }

        /// <summary>The quests of one section, in order.</summary>
        public IEnumerable<Quest> InSection(string section)
        {
            foreach (Quest quest in quests)
            {
                if (quest.Section == section)
                {
                    yield return quest;
                }
            }
        }

        public bool IsDone(string id)
        {
            return byId.TryGetValue(id, out Quest quest) && quest.Done;
        }

        /// <summary>Marks a quest done; true the first time, false when it was done already or doesn't exist.</summary>
        public bool Complete(string id)
        {
            if (!byId.TryGetValue(id, out Quest quest) || quest.Done)
            {
                return false;
            }
            quest.Done = true;
            Completed?.Invoke(quest);
            return true;
        }

        /// <summary>Marks the quests a save had done (GL216), without raising <see cref="Completed"/>. Unknown ids are skipped.</summary>
        public void Restore(IEnumerable<string> doneIds)
        {
            foreach (string id in doneIds)
            {
                if (id != null && byId.TryGetValue(id, out Quest quest))
                {
                    quest.Done = true;
                }
            }
        }

        /// <summary>One quest: an id for saving, what to do, the section it's in and whether it's done.</summary>
        public sealed class Quest
        {
            internal Quest(string id, string title, string section)
            {
                Id = id;
                Title = title;
                Section = section;
            }

            public string Id { get; }

            public string Title { get; }

            public string Section { get; }

            public bool Done { get; internal set; }
        }
    }
}
