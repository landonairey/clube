using System.Collections.Generic;
using System.Linq;
using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The crafting journal (GL137), a second notebook beside the mining journal: CraftingJournal (J)
    /// opens it, drawn in the same book (<see cref="JournalBook"/>), three entries to a page. Chapter
    /// one, Crafting: every recipe made at a worktop, the flat rock or the anvil, with an ink sketch of
    /// what it makes (<see cref="ItemSketch"/>), what it takes, the work, the level it needs and the
    /// experience it gives. Chapter two, Mining: what ore each Mining level lets the player see
    /// (<see cref="OreKnowledge"/>). Chapter three, Smelting: the kinds of furnace and what each does,
    /// then the smelting recipes and their unlocks. Things not yet made are only outlined.
    /// </summary>
    [RequireComponent(typeof(PlayerJournal))]
    public class CraftingJournalScreen : MonoBehaviour
    {
        private const int EntriesPerPage = 3;

        [Tooltip("Opens and closes the crafting journal (J).")]
        [SerializeField]
        private InputActionReference toggleAction;

        private readonly List<Page> pages = new List<Page>();
        private PlayerJournal journal;
        private PlayerController player;
        private JournalScreen miningJournal;
        private InventoryScreen inventoryScreen;
        private StationPanel stationPanel;
        private MerchantPanel merchantPanel;
        private int spread;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            journal = GetComponent<PlayerJournal>();
            player = GetComponent<PlayerController>();
            miningJournal = GetComponent<JournalScreen>();
            inventoryScreen = GetComponent<InventoryScreen>();
            stationPanel = GetComponent<StationPanel>();
            merchantPanel = GetComponent<MerchantPanel>();
        }

        private void OnEnable()
        {
            toggleAction?.action.Enable();
        }

        private void OnDisable()
        {
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
                || (miningJournal != null && miningJournal.IsOpen);
            if (toggleAction != null && toggleAction.action.WasPressedThisFrame() && !otherOpen)
            {
                SetOpen(!IsOpen);
            }
        }

        /// <summary>Opens or closes the journal; opening writes its pages from what the player knows now.</summary>
        public void SetOpen(bool open)
        {
            IsOpen = open;
            if (open)
            {
                Write();
            }
            if (player != null)
            {
                player.IsInMenu = open;
            }
        }

        /// <summary>The pages as last written: a heading and up to three entries each.</summary>
        public IReadOnlyList<Page> Pages => pages;

        private void OnGUI()
        {
            if (!IsOpen)
            {
                return;
            }
            spread = JournalBook.Draw(spread, pages.Count, DrawPage);
        }

        private void DrawPage(Rect area, int index)
        {
            Page page = pages[index];
            Rect inner = JournalBook.Inner(area);
            float top = JournalBook.Heading(inner, page.Heading, index);
            float entryHeight = (inner.yMax - top) / EntriesPerPage;
            for (int i = 0; i < page.Entries.Count; i++)
            {
                var entry = new Rect(inner.x, top + i * entryHeight, inner.width, entryHeight);
                if (i > 0)
                {
                    JournalBook.InkLine(new Rect(entry.x, entry.y - 4f, entry.width, 1.5f));
                }
                DrawEntry(entry, page.Entries[i]);
            }
        }

        private static void DrawEntry(Rect area, Entry entry)
        {
            float sketchWidth = Mathf.Min(OreSketch.Width, area.width * 0.3f);
            if (entry.Sketch != null)
            {
                float sketchHeight = sketchWidth * entry.Sketch.height / entry.Sketch.width;
                GUI.DrawTexture(new Rect(area.x, area.y + 6f, sketchWidth, Mathf.Min(sketchHeight, area.height - 12f)), entry.Sketch, ScaleMode.ScaleToFit);
            }
            float x = area.x + sketchWidth + 14f;
            float width = area.xMax - x;
            GUI.Label(new Rect(x, area.y + 2f, width, 26f), entry.Title, JournalBook.Title);
            GUI.Label(new Rect(x, area.y + 28f, width, area.height - 34f), entry.Body, JournalBook.Text);
        }

        // Writes every page from the recipes, the ore groups and the player's skills.
        private void Write()
        {
            pages.Clear();
            SkillSet skills = journal.Skills;
            IReadOnlyList<Recipe> recipes = journal.Recipes;

            var crafting = recipes.Where(r => r != null && !IsSmelting(r.Station))
                .OrderBy(r => r.UnlockLevel).ThenBy(r => r.Station).ToList();
            AddChapter("Chapter one: Crafting", "Crafting (cont.)", crafting.Select(r => RecipeEntry(r, skills)));

            AddChapter("Chapter two: Mining", "Mining (cont.)", MiningEntries(skills));

            var furnaces = recipes.Where(r => r != null && r.Output.Item is PlaceableDefinition placeable && Smelter(placeable) != null)
                .OrderBy(r => Smelter((PlaceableDefinition)r.Output.Item).Heat).Select(r => FurnaceEntry(r, skills));
            var smelting = recipes.Where(r => r != null && IsSmelting(r.Station))
                .OrderBy(r => r.UnlockLevel).ThenBy(r => r.Station == StationKind.Campfire ? 0 : 1).Select(r => RecipeEntry(r, skills));
            AddChapter("Chapter three: Smelting", "Smelting (cont.)", furnaces.Concat(smelting));
        }

        // A chapter starts on a page of its own; its entries run on, three to a page.
        private void AddChapter(string heading, string continued, IEnumerable<Entry> entries)
        {
            Page page = null;
            foreach (Entry entry in entries)
            {
                if (page == null || page.Entries.Count == EntriesPerPage)
                {
                    page = new Page(page == null ? heading : continued);
                    pages.Add(page);
                }
                page.Entries.Add(entry);
            }
            if (page == null)
            {
                pages.Add(new Page(heading));
            }
        }

        private Entry RecipeEntry(Recipe recipe, SkillSet skills)
        {
            bool made = journal.Journal.HasMade(recipe);
            ItemAmount output = recipe.Output;
            string title = output.Count > 1 ? $"{output.Item.DisplayName} ×{output.Count}" : output.Item.DisplayName;
            string takes = string.Join(" + ", recipe.Inputs.Select(input => $"{input.Count} {input.Item.DisplayName}"));
            string work = recipe.Work == RecipeWork.Strikes ? $"{recipe.Amount:0} strikes" : $"{recipe.Amount:0.#} s of heat";
            var lines = new List<string>
            {
                $"At the {StationName(recipe.Station)}: {takes}; {work}.",
                recipe.UnlockSkill == SkillType.None || recipe.UnlockLevel <= 0 ? "Anyone can make it." : $"Needs {recipe.UnlockSkill} {recipe.UnlockLevel}.",
            };
            if (recipe.ExperienceSkill != SkillType.None && recipe.Experience > 0f)
            {
                lines.Add($"Gives {recipe.Experience:0} {recipe.ExperienceSkill} experience.");
            }
            if (recipe.Extracts != null)
            {
                lines.Add($"Yield follows the ore: {recipe.TypicalGrade * 100f:0}% {recipe.Extracts.DisplayName} makes the full count.");
            }
            lines.Add(made ? "Made it." : recipe.IsUnlockedFor(skills) ? "Not made yet." : "Not unlocked yet.");
            return new Entry(ItemSketch.For(output.Item, made), title, string.Join("\n", lines));
        }

        // What each Mining level shows (GL54, GL72): the ores anyone sees, then each hidden group at its level.
        private IEnumerable<Entry> MiningEntries(SkillSet skills)
        {
            OreKnowledge knowledge = journal.Knowledge;
            var hidden = new HashSet<VoxelMaterial>();
            if (knowledge != null)
            {
                foreach (OreKnowledge.HiddenOres group in knowledge.Groups)
                {
                    hidden.UnionWith(group.Materials);
                }
            }
            List<VoxelMaterial> plain = journal.Ores.Where(ore => ore != null && !hidden.Contains(ore)).ToList();
            if (plain.Count > 0)
            {
                yield return new Entry(OreSketch.For(plain[0]), "From the start",
                    $"{Names(plain)} can be told apart in the rock by anyone.");
            }
            if (knowledge == null)
            {
                yield break;
            }
            int level = skills != null ? skills.Level(knowledge.Skill) : 0;
            foreach (OreKnowledge.HiddenOres group in knowledge.Groups.OrderBy(g => g.Level))
            {
                List<VoxelMaterial> ores = group.Materials.Where(m => m != null && journal.Ores.Contains(m)).ToList();
                bool seen = level >= group.Level;
                string body = $"{knowledge.Skill} {group.Level} lets me see {group.Name}: {Names(ores)}. Until then it looks like plain rock, and what I mine of it is \"? ore\".\n"
                    + (seen ? "I can see these now." : $"I'm at {knowledge.Skill} {level}.");
                yield return new Entry(ores.Count > 0 ? OreSketch.For(ores[0], seen) : null, $"{knowledge.Skill} {group.Level}: {group.Name}", body);
            }
        }

        // A kind of furnace (or the campfire): how hot it works, what builds it and what it smelts.
        private Entry FurnaceEntry(Recipe build, SkillSet skills)
        {
            var placeable = (PlaceableDefinition)build.Output.Item;
            StationObject station = Smelter(placeable);
            bool made = journal.Journal.HasMade(build);
            string takes = string.Join(" + ", build.Inputs.Select(input => $"{input.Count} {input.Item.DisplayName}"));
            string smelts = string.Join(", ", station.Recipes.Where(r => r != null && r.Output.Item != null).Select(r => r.Output.Item.DisplayName).Distinct());
            var lines = new List<string>
            {
                $"Heat ×{station.Heat:0.##}: works that much faster on the same fuel.",
                $"Built at the {StationName(build.Station)} from {takes}" + (build.UnlockLevel > 0 ? $" ({build.UnlockSkill} {build.UnlockLevel})." : "."),
                "Built of impure material it is less efficient: slower, and it yields less.",
                $"Makes: {smelts}.",
            };
            return new Entry(ItemSketch.For(placeable, made), placeable.DisplayName, string.Join("\n", lines));
        }

        private static StationObject Smelter(PlaceableDefinition placeable)
        {
            return placeable.Prefab != null && placeable.Prefab.TryGetComponent(out StationObject station) && station.BurnsFuel ? station : null;
        }

        private static bool IsSmelting(StationKind kind)
        {
            return kind == StationKind.Furnace || kind == StationKind.Campfire;
        }

        private static string StationName(StationKind kind)
        {
            switch (kind)
            {
                case StationKind.Worktop:
                    return "stump worktop";
                case StationKind.FlatRock:
                    return "flat rock";
                default:
                    return kind.ToString().ToLowerInvariant();
            }
        }

        private static string Names(IEnumerable<VoxelMaterial> ores)
        {
            return string.Join(", ", ores.Select(ore => ore.DisplayName));
        }

        /// <summary>One page of the journal: a heading and its entries.</summary>
        public sealed class Page
        {
            public Page(string heading)
            {
                Heading = heading;
            }

            public string Heading { get; }

            public List<Entry> Entries { get; } = new List<Entry>();
        }

        /// <summary>One entry: a sketch, a title and its notes.</summary>
        public readonly struct Entry
        {
            public Entry(Texture2D sketch, string title, string body)
            {
                Sketch = sketch;
                Title = title;
                Body = body;
            }

            public Texture2D Sketch { get; }

            public string Title { get; }

            public string Body { get; }
        }
    }
}
