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

        private static readonly Color Paper = new Color(0.96f, 0.94f, 0.86f, 1f);
        private static readonly Color FineLine = new Color(0.66f, 0.79f, 0.9f, 1f);
        private static readonly Color BoldLine = new Color(0.5f, 0.66f, 0.84f, 1f);
        private static readonly Color Ink = new Color(0.1f, 0.1f, 0.16f, 1f);

        [Tooltip("Opens and closes the journal (Q).")]
        [SerializeField]
        private InputActionReference toggleAction;

        private PlayerJournal journal;
        private PlayerController player;
        private InventoryScreen inventoryScreen;
        private StationPanel stationPanel;
        private MerchantPanel merchantPanel;
        private Texture2D graphPaper;
        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private GUIStyle smallStyle;
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

        private void OnDestroy()
        {
            if (graphPaper != null)
            {
                Destroy(graphPaper);
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
                || (merchantPanel != null && merchantPanel.IsOpen);
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
            EnsureStyles();
            if (!IsOpen)
            {
                if (notice != null && Time.time < noticeUntil)
                {
                    var area = new Rect(Screen.width * 0.5f - 200f, 70f, 400f, 30f);
                    InventoryGridGui.Panel(area);
                    GUI.Label(area, notice, new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic });
                }
                return;
            }

            IReadOnlyList<VoxelMaterial> ores = journal.Ores;
            int pages = Mathf.Max(1, Mathf.CeilToInt(ores.Count / (float)OresPerPage));
            int spreads = (pages + 1) / 2;
            spread = Mathf.Clamp(spread, 0, spreads - 1);

            float width = Mathf.Min(Screen.width - 60f, 1120f);
            float height = Mathf.Min(Screen.height - 60f, 700f);
            var book = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            InventoryGridGui.Panel(new Rect(book.x - 10f, book.y - 10f, book.width + 20f, book.height + 20f));
            float pageWidth = book.width * 0.5f;
            for (int side = 0; side < 2; side++)
            {
                int page = spread * 2 + side;
                var area = new Rect(book.x + side * pageWidth, book.y, pageWidth, book.height);
                DrawPaper(area);
                if (page < pages)
                {
                    DrawPage(area, page, pages, ores);
                }
            }
            // The fold between the pages.
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.25f);
            GUI.DrawTexture(new Rect(book.x + pageWidth - 2f, book.y, 4f, book.height), Texture2D.whiteTexture);
            GUI.color = previous;

            if (spreads > 1)
            {
                GUI.enabled = spread > 0;
                if (GUI.Button(new Rect(book.x, book.yMax + 14f, 90f, 26f), "< Back"))
                {
                    spread--;
                }
                GUI.enabled = spread < spreads - 1;
                if (GUI.Button(new Rect(book.xMax - 90f, book.yMax + 14f, 90f, 26f), "Next >"))
                {
                    spread++;
                }
                GUI.enabled = true;
            }
        }

        private void DrawPage(Rect area, int page, int pages, IReadOnlyList<VoxelMaterial> ores)
        {
            var inner = new Rect(area.x + 28f, area.y + 18f, area.width - 48f, area.height - 36f);
            GUI.Label(new Rect(inner.x, inner.y, inner.width, 28f), page == 0 ? "Field notes: ores" : "Field notes: ores (cont.)", titleStyle);
            GUI.Label(new Rect(inner.x, inner.y, inner.width, 28f), $"p. {page + 1}", new GUIStyle(smallStyle) { alignment = TextAnchor.UpperRight });
            float top = inner.y + 36f;
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
                    InkLine(new Rect(entry.x, entry.y - 4f, entry.width, 1.5f));
                }
                DrawEntry(entry, index + 1, ores[index]);
            }
        }

        private void DrawEntry(Rect area, int number, VoxelMaterial ore)
        {
            MiningJournal.Entry found = journal.Journal.EntryFor(ore);
            GUI.Label(new Rect(area.x, area.y + 2f, 60f, 20f), $"No. {number}", smallStyle);
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
            GUI.Label(new Rect(x, y, width, 26f), known ? ore.DisplayName : "Unknown Ore", titleStyle);
            y += 26f;
            if (!known)
            {
                // Found before the player can tell it apart (GL77, GL91): an outline, the name and how to learn more.
                string how = journal.HowToKnow(ore);
                GUI.Label(new Rect(x, y, width, area.yMax - y - 6f),
                    "Something in the rock I can't yet tell apart. " + (how != null ? how + " to discover it." : "More mining may tell what it is."), textStyle);
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
            GUI.Label(new Rect(x, y, width, area.yMax - y - 6f), string.Join("\n", lines), textStyle);
        }

        private static void AddNote(List<string> lines, string label, string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                lines.Add($"{label}: {text}");
            }
        }

        // The page: cream paper ruled in a fine blue grid, bolder every fifth line.
        private void DrawPaper(Rect area)
        {
            if (graphPaper == null)
            {
                graphPaper = PaintGraphPaper();
            }
            GUI.DrawTextureWithTexCoords(area, graphPaper, new Rect(0f, 0f, area.width / graphPaper.width, area.height / graphPaper.height));
        }

        private static Texture2D PaintGraphPaper()
        {
            const int size = 80;
            const int cell = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, name = "Graph paper" };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color color = Paper;
                    if (x % cell == 0 || y % cell == 0)
                    {
                        color = FineLine;
                    }
                    if (x == 0 || y == 0)
                    {
                        color = BoldLine;
                    }
                    texture.SetPixel(x, y, color);
                }
            }
            texture.Apply();
            return texture;
        }

        private static void InkLine(Rect area)
        {
            Color previous = GUI.color;
            GUI.color = new Color(Ink.r, Ink.g, Ink.b, 0.7f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.BoldAndItalic, normal = { textColor = Ink } };
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Italic, wordWrap = true, normal = { textColor = Ink } };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Italic, normal = { textColor = new Color(Ink.r, Ink.g, Ink.b, 0.7f) } };
        }
    }
}
