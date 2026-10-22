using System.Collections.Generic;
using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The mining journal (GL73): Journal (Q) opens a field notebook on graph paper, two pages
    /// side by side, three ores to a page. An ore the player hasn't found is a blank numbered entry;
    /// a found one has a sketch (<see cref="OreSketch"/>) and notes: where it was first found, the
    /// grades mined, its hardness, luster, colour and feel (<see cref="MineralNotes"/>), and its
    /// uses once the player has made them. One found before the player can tell it apart is an
    /// "Unknown Ore": the outline of its sketch and the Mining level that tells it apart (GL77, GL91). A short notice says when
    /// a new entry is written.
    /// </summary>
    [RequireComponent(typeof(PlayerJournal))]
    public class JournalScreen : MonoBehaviour
    {
        private const int OresPerPage = 3;
        private const float NoticeSeconds = 4f;

        [Tooltip("Opens and closes the journal (Q).")]
        [SerializeField]
        private InputActionReference toggleAction;

        private PlayerJournal journal;
        private PlayerController player;
        private InventoryScreen inventoryScreen;
        private StationPanel stationPanel;
        private MerchantPanel merchantPanel;
        private CraftingJournalScreen craftingJournal;
        private int spread;
        private string notice;
        private float noticeUntil;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            journal = GetComponent<PlayerJournal>();
            player = GetComponent<PlayerController>();
            inventoryScreen = GetComponent<InventoryScreen>();
            stationPanel = GetComponent<StationPanel>();
            merchantPanel = GetComponent<MerchantPanel>();
            craftingJournal = GetComponent<CraftingJournalScreen>();
        }

        private void OnEnable()
        {
            toggleAction?.action.Enable();
            journal.Journal.Discovered += OnDiscovered;
        }

        private void OnDisable()
        {
            journal.Journal.Discovered -= OnDiscovered;
            if (IsOpen)
            {
                SetOpen(false);
            }
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
                || (merchantPanel != null && merchantPanel.IsOpen)
                || (craftingJournal != null && craftingJournal.IsOpen);
            if (toggleAction != null && toggleAction.action.WasPressedThisFrame() && !otherOpen)
            {
                SetOpen(!IsOpen);
            }
        }

        private void SetOpen(bool open)
        {
            IsOpen = open;
            if (player != null)
            {
                player.IsInMenu = open;
            }
        }

        private void OnDiscovered(MiningJournal.Entry entry)
        {
            notice = $"New journal entry: {(journal.Knows(entry.Ore) ? entry.Ore.DisplayName : "an unknown ore")}  (Q)";
            noticeUntil = Time.time + NoticeSeconds;
        }

        private void OnGUI()
        {
            if (!IsOpen)
            {
                if (notice != null && Time.time < noticeUntil)
                {
                    JournalBook.Notice(notice);
                }
                return;
            }

            IReadOnlyList<VoxelMaterial> ores = journal.Ores;
            int pages = Mathf.Max(1, Mathf.CeilToInt(ores.Count / (float)OresPerPage));
            spread = JournalBook.Draw(spread, pages, (area, page) => DrawPage(area, page, ores));
        }

        private void DrawPage(Rect area, int page, IReadOnlyList<VoxelMaterial> ores)
        {
            Rect inner = JournalBook.Inner(area);
            float top = JournalBook.Heading(inner, page == 0 ? "Field notes: ores" : "Field notes: ores (cont.)", page);
            float entryHeight = (inner.yMax - top) / OresPerPage;
            for (int i = 0; i < OresPerPage; i++)
            {
                int index = page * OresPerPage + i;
                if (index >= ores.Count)
                {
                    break;
                }
                var entry = new Rect(inner.x, top + i * entryHeight, inner.width, entryHeight);
                if (i > 0)
                {
                    JournalBook.InkLine(new Rect(entry.x, entry.y - 4f, entry.width, 1.5f));
                }
                DrawEntry(entry, index + 1, ores[index]);
            }
        }

        private void DrawEntry(Rect area, int number, VoxelMaterial ore)
        {
            MiningJournal.Entry found = journal.Journal.EntryFor(ore);
            GUI.Label(new Rect(area.x, area.y + 2f, 60f, 20f), $"No. {number}", JournalBook.Small);
            if (found == null)
            {
                // Blank until it's found.
                return;
            }
            bool known = journal.Knows(ore);
            float sketchWidth = Mathf.Min(OreSketch.Width, area.width * 0.32f);
            float sketchHeight = sketchWidth * OreSketch.Height / OreSketch.Width;
            GUI.DrawTexture(new Rect(area.x, area.y + 22f, sketchWidth, sketchHeight), OreSketch.For(ore, known), ScaleMode.ScaleToFit);

            float x = area.x + sketchWidth + 14f;
            float width = area.xMax - x;
            float y = area.y + 2f;
            GUI.Label(new Rect(x, y, width, 26f), known ? ore.DisplayName : "Unknown Ore", JournalBook.Title);
            y += 26f;
            if (!known)
            {
                // Found before the player can tell it apart (GL77, GL91): an outline, the name and how to learn more.
                string how = journal.HowToKnow(ore);
                GUI.Label(new Rect(x, y, width, area.yMax - y - 6f),
                    "Something in the rock I can't yet tell apart. " + (how != null ? how + " to discover it." : "More mining may tell what it is."), JournalBook.Text);
                return;
            }

            MineralNotes notes = ore.Notes;
            string grades = Mathf.Approximately(found.LowestGrade, found.HighestGrade)
                ? $"{found.HighestGrade * 100f:0}%"
                : $"{found.LowestGrade * 100f:0} to {found.HighestGrade * 100f:0}%";
            var lines = new List<string>
            {
                $"First found {found.FirstDepth:0.#} m below the surface. Grade {grades} ({found.Samples} mined).",
            };
            if (notes != null)
            {
                AddNote(lines, "Hardness", notes.Hardness);
                AddNote(lines, "Luster", notes.Luster);
                AddNote(lines, "Colour", notes.Colour);
                AddNote(lines, "Feel", notes.Feel);
            }
            List<string> uses = journal.UsesOf(ore);
            lines.Add(uses.Count > 0 ? "Uses: " + string.Join("; ", uses) + "." : "Uses: not yet worked out.");
            GUI.Label(new Rect(x, y, width, area.yMax - y - 6f), string.Join("\n", lines), JournalBook.Text);
        }

        private static void AddNote(List<string> lines, string label, string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                lines.Add($"{label}: {text}");
            }
        }
    }
}
