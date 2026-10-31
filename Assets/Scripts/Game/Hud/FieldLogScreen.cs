using System.Collections.Generic;
using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The field log (GL215): one graph-paper notebook (<see cref="JournalBook"/>) for the player's journals. Journal
    /// (Q) opens and closes it; tabs down its left side pick the journal: Quests (the checklist, <see cref="PlayerQuests"/>,
    /// GL216), Ores (the mining journal, <see cref="JournalScreen"/>) and Tools (the crafting journal,
    /// <see cref="CraftingJournalScreen"/>). CraftingJournal (J) still opens it straight on the tools. It reopens on the
    /// journal last shown.
    /// </summary>
    [RequireComponent(typeof(PlayerJournal))]
    public class FieldLogScreen : MonoBehaviour
    {
        /// <summary>The journals in the log, in tab order.</summary>
        public enum Page
        {
            Quests,
            Ores,
            Tools,
        }

        private static readonly (Page Page, string Label)[] Tabs =
        {
            (Page.Quests, "Quests"),
            (Page.Ores, "Ore journal"),
            (Page.Tools, "Tool journal"),
        };

        [Tooltip("Opens and closes the field log (Q).")]
        [SerializeField]
        private InputActionReference toggleAction;

        [Tooltip("Opens the field log on the tool journal (J).")]
        [SerializeField]
        private InputActionReference toolsAction;

        private PlayerController player;
        private InventoryScreen inventoryScreen;
        private StationPanel stationPanel;
        private MerchantPanel merchantPanel;
        private JournalScreen ores;
        private CraftingJournalScreen tools;
        private PlayerQuests quests;
        private GUIStyle tabStyle;
        private GUIStyle questStyle;
        private GUIStyle doneStyle;

        public bool IsOpen { get; private set; }

        /// <summary>The journal showing, or shown last.</summary>
        public Page Current { get; private set; } = Page.Quests;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            inventoryScreen = GetComponent<InventoryScreen>();
            stationPanel = GetComponent<StationPanel>();
            merchantPanel = GetComponent<MerchantPanel>();
            ores = GetComponent<JournalScreen>();
            tools = GetComponent<CraftingJournalScreen>();
            quests = GetComponent<PlayerQuests>();
        }

        private void OnEnable()
        {
            toggleAction?.action.Enable();
            toolsAction?.action.Enable();
        }

        private void OnDisable()
        {
            Close();
        }

        private void Update()
        {
            if (player != null && !player.enabled)
            {
                return;
            }
            // One screen at a time.
            bool otherOpen = (inventoryScreen != null && inventoryScreen.IsOpen)
                || (stationPanel != null && stationPanel.IsOpen)
                || (merchantPanel != null && merchantPanel.IsOpen);
            if (otherOpen)
            {
                return;
            }
            if (toggleAction != null && toggleAction.action.WasPressedThisFrame())
            {
                if (IsOpen)
                {
                    Close();
                }
                else
                {
                    Open(Current);
                }
            }
            else if (toolsAction != null && toolsAction.action.WasPressedThisFrame())
            {
                if (IsOpen && Current == Page.Tools)
                {
                    Close();
                }
                else
                {
                    Open(Page.Tools);
                }
            }
        }

        /// <summary>Opens the log on a journal.</summary>
        public void Open(Page page)
        {
            IsOpen = true;
            Show(page);
            if (player != null)
            {
                player.IsInMenu = true;
            }
        }

        /// <summary>Closes the log (Q, or Esc, GL204).</summary>
        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }
            IsOpen = false;
            if (player != null)
            {
                player.IsInMenu = false;
            }
        }

        private void Show(Page page)
        {
            Current = page;
            if (page == Page.Tools && tools != null)
            {
                // Written from what the player knows now.
                tools.Refresh();
            }
        }

        private void OnGUI()
        {
            if (!IsOpen)
            {
                return;
            }
            switch (Current)
            {
                case Page.Ores when ores != null:
                    ores.DrawBook();
                    break;
                case Page.Tools when tools != null:
                    tools.DrawBook();
                    break;
                default:
                    DrawQuests();
                    break;
            }
            DrawTabs();
        }

        // A tab per journal down the book's left edge: the one open is paper, the others darker.
        private void DrawTabs()
        {
            tabStyle ??= new GUIStyle(JournalBook.Title) { fontSize = 15, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            Rect book = JournalBook.Area;
            float width = JournalBook.TabWidth - 24f;
            float x = book.x - 10f - width;
            float y = book.y + 20f;
            foreach ((Page page, string label) in Tabs)
            {
                var tab = new Rect(x, y, width, 54f);
                bool open = page == Current;
                Color previous = GUI.color;
                GUI.color = open ? JournalBook.PaperColor : new Color(0.62f, 0.6f, 0.54f, 1f);
                GUI.DrawTexture(tab, Texture2D.whiteTexture);
                GUI.color = previous;
                if (GUI.Button(tab, label, tabStyle) && !open)
                {
                    Show(page);
                }
                y += 62f;
            }
        }

        // The quest journal (GL216): the checklist by section, done quests ticked and struck through, the active one marked.
        private void DrawQuests()
        {
            QuestLog log = quests != null ? quests.Log : null;
            IReadOnlyList<string> sections = log != null ? log.Sections : new List<string>();
            if (Event.current.type == EventType.Layout)
            {
                JournalBook.Prepare("Quests p. 0123456789 done of [x] [ ] ▶ ✓");
                if (log != null)
                {
                    foreach (QuestLog.Quest quest in log.Quests)
                    {
                        JournalBook.Prepare(quest.Title + quest.Section);
                    }
                }
            }
            JournalBook.Draw(0, Mathf.Max(1, sections.Count), (area, page) =>
            {
                if (log != null && page < sections.Count)
                {
                    DrawSection(area, page, log, sections[page]);
                }
            });
        }

        private void DrawSection(Rect area, int page, QuestLog log, string section)
        {
            questStyle ??= new GUIStyle(JournalBook.Text) { fontSize = 15, fontStyle = FontStyle.Italic };
            doneStyle ??= new GUIStyle(questStyle) { normal = { textColor = new Color(JournalBook.Ink.r, JournalBook.Ink.g, JournalBook.Ink.b, 0.45f) } };
            Rect inner = JournalBook.Inner(area);
            int done = 0;
            int count = 0;
            foreach (QuestLog.Quest quest in log.InSection(section))
            {
                count++;
                done += quest.Done ? 1 : 0;
            }
            float y = JournalBook.Heading(inner, $"{section}  ({done} of {count} done)", page);
            QuestLog.Quest active = log.Active;
            foreach (QuestLog.Quest quest in log.InSection(section))
            {
                var row = new GUIContent((quest.Done ? "[x]  " : quest == active ? "[ ]  ▶ " : "[ ]  ") + quest.Title);
                GUIStyle style = quest.Done ? doneStyle : questStyle;
                float height = style.CalcHeight(row, inner.width);
                var rect = new Rect(inner.x, y, inner.width, height);
                GUI.Label(rect, row, style);
                if (quest.Done)
                {
                    // Struck through, as a ticked line in a notebook.
                    float width = Mathf.Min(inner.width, style.CalcSize(row).x);
                    JournalBook.InkLine(new Rect(rect.x + 34f, rect.y + Mathf.Min(height, 24f) * 0.5f + 1f, Mathf.Max(0f, width - 34f), 1.5f));
                }
                y += height + 10f;
            }
        }
    }
}
