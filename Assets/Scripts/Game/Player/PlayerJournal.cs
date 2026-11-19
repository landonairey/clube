using System.Collections.Generic;
using System.Text;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The player's mining journal (GL73): fills a <see cref="MiningJournal"/> as they work. An ore
    /// is found the first time an inventory entry of it is seen (GL796, <see cref="OreSighting"/>): its own stack mined
    /// with a tool aimed at it, or popped out by refining; a dirt load with some of it inside doesn't count. Even before
    /// they can tell it apart (<see cref="OreKnowledge"/>; the screen then shows it as unknown, GL77);
    /// the journal keeps how deep below the generated surface it first was, and the grades mined since. Recipes are learned as they're finished,
    /// and give an ore its uses. <see cref="JournalScreen"/> shows it.
    /// </summary>
    [RequireComponent(typeof(PlayerToolUser))]
    public class PlayerJournal : MonoBehaviour
    {
        [SerializeField]
        private WorldView worldView;

        [Tooltip("The ores the journal has pages for, in order: three to a page.")]
        [SerializeField]
        private List<VoxelMaterial> ores = new List<VoxelMaterial>();

        [Tooltip("Every recipe, so an ore's uses can be named once the player has made them.")]
        [SerializeField]
        private List<Recipe> recipes = new List<Recipe>();

        private PlayerToolUser tools;
        private PlayerSkills skills;
        private OreKnowledge knowledge;
        private PlayerInventory inventory;
        private Inventory watched;
        private bool inventoryChanged;

        public MiningJournal Journal { get; } = new MiningJournal();

        public IReadOnlyList<VoxelMaterial> Ores => ores;

        /// <summary>Every recipe in the game, for the crafting journal (GL137).</summary>
        public IReadOnlyList<Recipe> Recipes => recipes;

        /// <summary>The player's skills, or null.</summary>
        public SkillSet Skills => skills != null ? skills.Skills : null;

        /// <summary>Which ores the player can tell apart, and at what level the rest show; null when nothing is hidden.</summary>
        public OreKnowledge Knowledge => knowledge;

        /// <summary>
        /// True when the player can tell the ore apart (<see cref="OreKnowledge"/>). An ore found before
        /// that has an entry, but the journal shows it as unknown (GL77).
        /// </summary>
        public bool Knows(VoxelMaterial ore)
        {
            return knowledge == null || knowledge.Knows(ore);
        }

        /// <summary>What the player needs to tell an unknown ore apart, e.g. "Raise Mining to level 5", or null when nothing hides it.</summary>
        public string HowToKnow(VoxelMaterial ore)
        {
            int level = knowledge != null ? knowledge.LevelToKnow(ore) : 0;
            return level > 0 ? $"Raise {knowledge.Skill} to level {level}" : null;
        }

        private void Awake()
        {
            tools = GetComponent<PlayerToolUser>();
            skills = GetComponent<PlayerSkills>();
            knowledge = GetComponent<OreKnowledge>();
            inventory = GetComponent<PlayerInventory>();
        }

        private void OnEnable()
        {
            tools.Unearthed += OnUnearthed;
            if (skills != null)
            {
                skills.RecipeFinished += Journal.RecordMade;
            }
        }

        private void OnDisable()
        {
            tools.Unearthed -= OnUnearthed;
            if (skills != null)
            {
                skills.RecipeFinished -= Journal.RecordMade;
            }
            if (watched != null)
            {
                watched.Changed -= OnInventoryChanged;
                watched = null;
            }
        }

        private void OnInventoryChanged()
        {
            inventoryChanged = true;
        }

        // Once a frame after the inventory changed (GL796), so a save loading its items and its journal in one go raises
        // nothing: each slot's ore, when it is an entry of that ore type and the ore is new to the journal.
        private void LateUpdate()
        {
            // Watched from the first frame, as the inventory is made in its own Awake.
            Inventory items = inventory != null ? inventory.Inventory : null;
            if (items != watched)
            {
                if (watched != null)
                {
                    watched.Changed -= OnInventoryChanged;
                }
                watched = items;
                if (items != null)
                {
                    items.Changed += OnInventoryChanged;
                    inventoryChanged = true;
                }
            }
            if (!inventoryChanged || items == null)
            {
                return;
            }
            inventoryChanged = false;
            for (int slot = 0; slot < items.SlotCount; slot++)
            {
                ItemStack stack = items[slot];
                if (stack.IsEmpty)
                {
                    continue;
                }
                ItemDefinition item = knowledge != null ? knowledge.RealItem(stack.Item) : stack.Item;
                VoxelMaterial ore = OreSighting.OreShownBy(item, ores);
                if (ore != null && !Journal.HasFound(ore))
                {
                    float grade = stack.Contents != null ? GradeOf(ore, stack.Contents) : Mathf.Clamp01(stack.Quality);
                    Journal.Record(ore, DepthAt(transform.position), grade);
                }
            }
        }

        private float DepthAt(Vector3 position)
        {
            if (worldView == null || worldView.Generator == null)
            {
                return 0f;
            }
            return Mathf.Max(0f, worldView.Generator.Depth(worldView.transform.InverseTransformPoint(position)));
        }

        /// <summary>
        /// What the player has found an ore good for, a line per product: "Smelts into Tin bun
        /// (campfire, furnace)", then what that goes on to make, "Tin bun → Tin ingot, Bronze bun".
        /// Only recipes they have made count. Empty when none.
        /// </summary>
        public List<string> UsesOf(VoxelMaterial ore)
        {
            var lines = new List<string>();
            ItemDefinition item = ore != null ? ore.Drop : null;
            if (item == null)
            {
                return lines;
            }
            // Each product, with the stations that make it from the ore.
            var products = new List<ItemDefinition>();
            var stations = new Dictionary<ItemDefinition, List<string>>();
            var verbs = new Dictionary<ItemDefinition, string>();
            foreach (Recipe recipe in recipes)
            {
                if (!Journal.HasMade(recipe) || !recipe.Takes(item) || recipe.Output.Item == null)
                {
                    continue;
                }
                ItemDefinition product = recipe.Output.Item;
                if (!stations.TryGetValue(product, out List<string> where))
                {
                    where = new List<string>();
                    stations.Add(product, where);
                    verbs.Add(product, Verb(recipe.Station));
                    products.Add(product);
                }
                string station = recipe.Station.ToString().ToLowerInvariant();
                if (!where.Contains(station))
                {
                    where.Add(station);
                }
            }
            foreach (ItemDefinition product in products)
            {
                lines.Add($"{verbs[product]} into {product.DisplayName} ({string.Join(", ", stations[product])})");
                string next = NextProducts(product);
                if (next != null)
                {
                    lines.Add($"{product.DisplayName} → {next}");
                }
            }
            return lines;
        }

        // "Tin ingot, Bronze bun": what the player has made from an item.
        private string NextProducts(ItemDefinition item)
        {
            var names = new StringBuilder();
            var seen = new HashSet<ItemDefinition>();
            foreach (Recipe recipe in recipes)
            {
                if (!Journal.HasMade(recipe) || !recipe.Takes(item) || recipe.Output.Item == null || !seen.Add(recipe.Output.Item))
                {
                    continue;
                }
                if (names.Length > 0)
                {
                    names.Append(", ");
                }
                names.Append(recipe.Output.Item.DisplayName);
            }
            return names.Length > 0 ? names.ToString() : null;
        }

        private static string Verb(StationKind station)
        {
            switch (station)
            {
                case StationKind.Furnace:
                case StationKind.Campfire:
                    return "Smelts";
                case StationKind.Anvil:
                    return "Hammers";
                default:
                    return "Works";
            }
        }

        private void OnUnearthed(VoxelMaterial material, Vector3 position, Composition contents)
        {
            // Digging only adds samples to an ore already found (GL796): the find itself is its inventory entry.
            VoxelMaterial ore = OreOf(material);
            if (ore == null || !Journal.HasFound(ore))
            {
                return;
            }
            Journal.Record(ore, DepthAt(position), GradeOf(ore, contents));
        }

        // The journal's ore a mined material belongs to: itself, or the ore it broke down from.
        private VoxelMaterial OreOf(VoxelMaterial material)
        {
            foreach (VoxelMaterial ore in ores)
            {
                foreach (VoxelMaterial stage in Stages(ore))
                {
                    if (stage == material)
                    {
                        return ore;
                    }
                }
            }
            return null;
        }

        // Its share of the sample, counting its broken-down stages too; all of it when nothing says otherwise.
        private static float GradeOf(VoxelMaterial ore, Composition contents)
        {
            if (contents == null)
            {
                return 1f;
            }
            float share = 0f;
            foreach (VoxelMaterial stage in Stages(ore))
            {
                share += contents.FractionOf(stage);
            }
            return share > 0f ? share : 1f;
        }

        // An ore and what it breaks down into (cracked, loose), a few steps at most.
        private static IEnumerable<VoxelMaterial> Stages(VoxelMaterial ore)
        {
            VoxelMaterial stage = ore;
            for (int step = 0; step < 4 && stage != null; step++)
            {
                yield return stage;
                stage = stage.BreaksInto;
            }
        }
    }
}
