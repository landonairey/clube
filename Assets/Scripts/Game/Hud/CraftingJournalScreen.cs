using System.Collections.Generic;
using System.Linq;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The crafting journal (GL137), now the tool pages of the field log (GL215, <see cref="FieldLogScreen"/>), drawn
    /// in the same book (<see cref="JournalBook"/>), three entries to a page. Chapter one, Crafting: first the places
    /// to craft with nothing built, the tree stump and the stone boulder (GL214), then every recipe made at a worktop,
    /// the flat rock or the anvil, with an ink sketch of what it makes (<see cref="ItemSketch"/>), what it takes, the
    /// work, the level it needs and the experience it gives. Chapter two, Smelting: the kinds of furnace and what each
    /// does, then the smelting recipes and their unlocks. (What each Mining level shows is on the ore pages; GL182.)
    /// How much an entry tells follows the player's level (GL181): an unlocked recipe has its sketch and every note;
    /// one a level away is only outlined, with its name and the level it unlocks at; anything further off is a big
    /// question mark.
    /// </summary>
    [RequireComponent(typeof(PlayerJournal))]
    public class CraftingJournalScreen : MonoBehaviour
    {
        private const int EntriesPerPage = 3;

        private static GUIStyle questionStyle;

        private readonly List<Page> pages = new List<Page>();
        private PlayerJournal journal;
        private int spread;

        private void Awake()
        {
            journal = GetComponent<PlayerJournal>();
        }

        /// <summary>Writes the pages from what the player knows now; the field log calls it when the tool pages open.</summary>
        public void Refresh()
        {
            Write();
        }

        /// <summary>Turns to the spread holding a page (0-based), e.g. the start of a chapter.</summary>
        public void ShowPage(int page)
        {
            spread = Mathf.Max(0, page) / 2;
        }

        /// <summary>The pages as last written: a heading and up to three entries each.</summary>
        public IReadOnlyList<Page> Pages => pages;

        /// <summary>Draws the tool pages in the open book; the field log calls it from its OnGUI.</summary>
        public void DrawBook()
        {
            if (Event.current.type == EventType.Layout)
            {
                // Every glyph into the font atlas before any text is laid out (GL187).
                JournalBook.Prepare("p. 0123456789?");
                foreach (Page page in pages)
                {
                    JournalBook.Prepare(page.Heading);
                    foreach (Entry entry in page.Entries)
                    {
                        JournalBook.Prepare(entry.Title + entry.Body);
                    }
                }
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
            if (entry.IsMystery)
            {
                // More than a level away (GL181): nothing but a big question mark where the sketch goes.
                questionStyle ??= new GUIStyle(JournalBook.Title) { fontSize = 64, alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(area.x, area.y + 6f, sketchWidth, area.height - 12f), "?", questionStyle);
                return;
            }
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
                .OrderBy(r => StationOrder(r.Station)).ThenBy(r => r.UnlockLevel).ToList();
            AddChapter("Chapter one: Crafting", "Crafting (cont.)", PlacesEntries().Concat(crafting.Select(r => RecipeEntry(r, skills))));

            var furnaces = recipes.Where(r => r != null && r.Output.Item is PlaceableDefinition placeable && Smelter(placeable) != null)
                .OrderBy(r => Smelter((PlaceableDefinition)r.Output.Item).Heat).Select(r => FurnaceEntry(r, skills));
            var smelting = recipes.Where(r => r != null && IsSmelting(r.Station))
                .OrderBy(r => r.Station == StationKind.Campfire ? 0 : 1).ThenBy(r => r.UnlockLevel).ThenBy(r => r.Extracts != null ? 0 : 1)
                .Select(r => RecipeEntry(r, skills));
            AddChapter("Chapter two: Smelting", "Smelting (cont.)", furnaces.Concat(smelting));
        }

        // The two places to craft that nobody builds (GL214), first in the Crafting chapter: they're easy to miss.
        private static IEnumerable<Entry> PlacesEntries()
        {
            yield return new Entry(PlaceSketch.Stump, "Tree stump",
                "Every tree stands on a stump, a worktop. Look at the foot of the trunk and press E.\n" +
                "Whittle sticks, make the first tools, build a campfire and, later, furnaces.");
            yield return new Entry(PlaceSketch.Boulder, "Stone boulder",
                "Rock too big to pick up. Aim at it and use the right mouse button:\n" +
                "a stone in hand is knocked into a hand axe;\n" +
                "hold with raw ore to crush it (from Mining 2), an item every 0.25 s;\n" +
                "hold with a mixed stack, like clay with dirt in it, to pick it over.\n" +
                "Chisel its top flat for a flat rock, a better worktop.");
        }

        // How many levels short of a recipe the player is (GL181): 0 when it's unlocked.
        private static int LevelsAway(Recipe recipe, SkillSet skills)
        {
            if (recipe.IsUnlockedFor(skills) || recipe.UnlockSkill == SkillType.None)
            {
                return 0;
            }
            int level = skills != null ? skills.Level(recipe.UnlockSkill) : 0;
            return Mathf.Max(1, recipe.UnlockLevel - level);
        }

        // An entry for something still locked (GL181): a level away, its outline, name and unlock level; further, a question mark.
        private static Entry LockedEntry(Recipe recipe, ItemDefinition item, string title, SkillSet skills)
        {
            if (LevelsAway(recipe, skills) > 1)
            {
                return Entry.Mystery;
            }
            return new Entry(ItemSketch.For(item, false), title, $"Unlocks at {recipe.UnlockSkill} {recipe.UnlockLevel}.");
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
            if (LevelsAway(recipe, skills) > 0)
            {
                return LockedEntry(recipe, output.Item, title, skills);
            }
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
                lines.Add($"The count is what pure {recipe.Extracts.DisplayName} gives; ore yields its share (typical ore is {recipe.TypicalGrade * 100f:0}%).");
            }
            lines.Add(made ? "Made it." : "Not made yet.");
            return new Entry(ItemSketch.For(output.Item), title, string.Join("\n", lines));
        }


        // A kind of furnace (or the campfire): how hot it works, what builds it and what it smelts.
        private Entry FurnaceEntry(Recipe build, SkillSet skills)
        {
            var placeable = (PlaceableDefinition)build.Output.Item;
            StationObject station = Smelter(placeable);
            if (LevelsAway(build, skills) > 0)
            {
                return LockedEntry(build, placeable, placeable.DisplayName, skills);
            }
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
            return new Entry(ItemSketch.For(placeable), placeable.DisplayName, string.Join("\n", lines));
        }

        private static StationObject Smelter(PlaceableDefinition placeable)
        {
            return placeable.Prefab != null && placeable.Prefab.TryGetComponent(out StationObject station) && station.BurnsFuel ? station : null;
        }

        // The order a player meets the crafting stations: the stump, then the flat rock, then the anvil.
        private static int StationOrder(StationKind kind)
        {
            switch (kind)
            {
                case StationKind.Worktop:
                    return 0;
                case StationKind.FlatRock:
                    return 1;
                case StationKind.Anvil:
                    return 2;
                default:
                    return 3;
            }
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

        /// <summary>One entry: a sketch, a title and its notes; or, for something far from unlocked, a question mark (GL181).</summary>
        public readonly struct Entry
        {
            /// <summary>An entry more than a level from being unlocked: a big question mark and nothing else.</summary>
            public static Entry Mystery => new Entry(null, null, null, true);

            public Entry(Texture2D sketch, string title, string body, bool mystery = false)
            {
                IsMystery = mystery;
                Sketch = sketch;
                Title = title;
                Body = body;
            }

            public Texture2D Sketch { get; }

            public string Title { get; }

            public string Body { get; }

            public bool IsMystery { get; }
        }
    }
}
