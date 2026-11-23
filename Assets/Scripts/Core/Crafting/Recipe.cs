using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>What kind of station runs a <see cref="Recipe"/> (GL8, CR4).</summary>
    public enum StationKind
    {
        /// <summary>Smelts ore over time (GL10).</summary>
        Furnace,

        /// <summary>Works metal by striking it (GL11).</summary>
        Anvil,

        /// <summary>A stump or bench for working wood by hand (GL36).</summary>
        Worktop,

        /// <summary>An open fire of wood ringed with stone (GL52): the first smelter, before the furnace.</summary>
        Campfire,

        /// <summary>A surface rock chiselled flat on top (GL88): the stone worktop between the stump and the anvil.</summary>
        FlatRock,
    }

    /// <summary>How a <see cref="Recipe"/>'s work is measured.</summary>
    public enum RecipeWork
    {
        /// <summary>Seconds that pass on their own once it starts, like smelting.</summary>
        Time,

        /// <summary>Strikes the player makes, like hammering on the anvil.</summary>
        Strikes,
    }

    /// <summary>A number of one item: a recipe's input or output.</summary>
    [Serializable]
    public struct ItemAmount
    {
        public ItemDefinition Item;

        [Min(1)]
        public int Count;

        public ItemAmount(ItemDefinition item, int count)
        {
            Item = item;
            Count = count;
        }
    }

    /// <summary>
    /// Turns items into other items at a station (GL8, first pass of CR1): its inputs, its
    /// output, which <see cref="StationKind"/> runs it, and how much work it takes, in seconds
    /// or strikes. An asset, so recipes are added without code changes; a
    /// <see cref="CraftingStation"/> runs it.
    /// </summary>
    /// <remarks>
    /// Later passes add what the full CR1 needs: required tools in hand, tech or skill,
    /// several outputs, and quality from the inputs (PR4, CR3).
    /// </remarks>
    [CreateAssetMenu(fileName = "Recipe", menuName = "Clube/Recipe")]
    public class Recipe : ScriptableObject
    {
        [SerializeField]
        private StationKind station;

        [SerializeField]
        private List<ItemAmount> inputs = new List<ItemAmount>();

        [SerializeField]
        private ItemAmount output;

        [SerializeField]
        private RecipeWork work;

        [Tooltip("Seconds (Time) or strikes (Strikes) the work takes.")]
        [SerializeField, Min(0.01f)]
        private float amount = 5f;

        [Tooltip("The skill finishing it trains (GL47), and how much experience it gives.")]
        [SerializeField]
        private SkillType experienceSkill;

        [SerializeField, Min(0f)]
        private float experience;

        [Tooltip("The skill level it needs before it can be made (GL48). None: it can always be made.")]
        [SerializeField]
        private SkillType unlockSkill;

        [SerializeField, Range(0, SkillSet.MaxLevel)]
        private int unlockLevel;

        [Tooltip("Times the quality of what it makes (GL151: maple whittles into sticks at 1.4). 1: as good as its inputs.")]
        [SerializeField, Range(0.1f, 2f)]
        private float qualityFactor = 1f;

        [Tooltip("Smelting (GL128, GL192): the mineral this recipe extracts. The output count is what pure mineral gives; ore makes its mineral share of it, and the metal comes out pure. None: the inputs' quality carries over.")]
        [SerializeField]
        private VoxelMaterial extracts;

        [Tooltip("Smelting (GL128): the mineral's share of typical ore, 0-1: what ore with no known contents counts as, and what the journal quotes.")]
        [SerializeField, Range(0.01f, 1f)]
        private float typicalGrade = 0.2f;

        [Tooltip("An alloy that takes a bun or its ingot for each input that has an ingot form (GL700): copper bun and tin ingot make a bronze bun, copper ingot and tin ingot a bronze ingot (the lesser of the two forms). The station derives these variants itself.")]
        [SerializeField]
        private bool takesIngotForms;

        [Tooltip("An input other items may replace (GL813-GL816): birch wood, where pine and maple wood do as well. The station derives one variant for each item in Swap For.")]
        [SerializeField]
        private ItemDefinition swapInput;

        [SerializeField]
        private List<ItemDefinition> swapFor = new List<ItemDefinition>();

        [Tooltip("A second input other items may replace (GL833: boards of any wood and wool from any animal in one recipe). The station derives a variant for every combination.")]
        [SerializeField]
        private ItemDefinition swapInput2;

        [SerializeField]
        private List<ItemDefinition> swapFor2 = new List<ItemDefinition>();

        /// <summary>
        /// The recipes that take another item where this one takes <c>swapInput</c> or <c>swapInput2</c> (GL813-GL816,
        /// GL833): everything else the same, named after the items they take, one for every combination. Empty for a
        /// recipe with no swaps.
        /// </summary>
        public List<Recipe> SwapVariants()
        {
            var variants = new List<Recipe>();
            if (swapInput == null && swapInput2 == null)
            {
                return variants;
            }
            List<ItemDefinition> first = WithAlternatives(swapInput, swapFor);
            List<ItemDefinition> second = WithAlternatives(swapInput2, swapFor2);
            foreach (ItemDefinition one in first)
            {
                foreach (ItemDefinition two in second)
                {
                    if (one == swapInput && two == swapInput2)
                    {
                        continue;
                    }
                    Recipe variant = Instantiate(this);
                    variant.swapInput = null;
                    variant.swapFor = new List<ItemDefinition>();
                    variant.swapInput2 = null;
                    variant.swapFor2 = new List<ItemDefinition>();
                    string label = "";
                    for (int i = 0; i < variant.inputs.Count; i++)
                    {
                        ItemDefinition item = variant.inputs[i].Item;
                        ItemDefinition replacement = item == null ? null : item == swapInput ? one : item == swapInput2 ? two : null;
                        if (replacement != null && replacement != item)
                        {
                            variant.inputs[i] = new ItemAmount(replacement, variant.inputs[i].Count);
                            label += (label.Length > 0 ? ", " : "") + replacement.DisplayName;
                        }
                    }
                    variant.name = $"{name} ({label})";
                    variants.Add(variant);
                }
            }
            return variants;
        }

        // The input first, then the items that may replace it; just null (no swap) for a recipe with none.
        private static List<ItemDefinition> WithAlternatives(ItemDefinition input, List<ItemDefinition> others)
        {
            var all = new List<ItemDefinition> { input };
            if (input != null)
            {
                foreach (ItemDefinition other in others)
                {
                    if (other != null && other != input && !all.Contains(other))
                    {
                        all.Add(other);
                    }
                }
            }
            return all;
        }

        /// <summary>
        /// Lets <c>other</c> replace <c>input</c> in this recipe (GL813), for recipes made in code (tests). A second,
        /// different input takes the second swap (GL833).
        /// </summary>
        public Recipe WithSwap(ItemDefinition input, ItemDefinition other)
        {
            if (swapInput == null || swapInput == input)
            {
                swapInput = input;
                swapFor.Add(other);
            }
            else
            {
                swapInput2 = input;
                swapFor2.Add(other);
            }
            return this;
        }

        /// <summary>True for an alloy that also takes the ingot form of its inputs (GL700).</summary>
        public bool TakesIngotForms => takesIngotForms;

        /// <summary>Sets <see cref="TakesIngotForms"/>, for recipes made in code (tests).</summary>
        public Recipe WithIngotForms()
        {
            takesIngotForms = true;
            return this;
        }

        /// <summary>
        /// The recipes an alloy that <see cref="TakesIngotForms"/> also makes (GL700): one for every way of swapping
        /// some of its inputs for their ingot form (<see cref="ItemDefinition.IngotForm"/>), everything else the same.
        /// It makes the ingot form of its output only when every input that has one is an ingot; any bun in it makes a bun.
        /// Empty for any other recipe.
        /// </summary>
        public List<Recipe> IngotVariants()
        {
            var variants = new List<Recipe>();
            if (!takesIngotForms || output.Item == null)
            {
                return variants;
            }
            var formed = new List<int>();
            for (int i = 0; i < inputs.Count; i++)
            {
                if (inputs[i].Item != null && inputs[i].Item.IngotForm != null)
                {
                    formed.Add(i);
                }
            }
            if (formed.Count == 0 || output.Item.IngotForm == null)
            {
                return variants;
            }
            int all = (1 << formed.Count) - 1;
            for (int mask = 1; mask <= all; mask++)
            {
                Recipe variant = Instantiate(this);
                variant.takesIngotForms = false;
                for (int bit = 0; bit < formed.Count; bit++)
                {
                    if ((mask & (1 << bit)) != 0)
                    {
                        ItemAmount input = inputs[formed[bit]];
                        variant.inputs[formed[bit]] = new ItemAmount(input.Item.IngotForm, input.Count);
                    }
                }
                if (mask == all)
                {
                    variant.output = new ItemAmount(output.Item.IngotForm, output.Count);
                }
                variant.name = $"{name} ({(mask == all ? "ingots" : "bun and ingot")})";
                variants.Add(variant);
            }
            return variants;
        }

        /// <summary>Makes a recipe in code (tests); the game's recipes are assets.</summary>
        public static Recipe Create(StationKind station, ItemAmount output, RecipeWork work, float amount, params ItemAmount[] inputs)
        {
            var recipe = CreateInstance<Recipe>();
            recipe.station = station;
            recipe.output = output;
            recipe.work = work;
            recipe.amount = Mathf.Max(0.01f, amount);
            recipe.inputs.AddRange(inputs);
            recipe.name = output.Item != null ? output.Item.DisplayName : "Recipe";
            return recipe;
        }

        /// <summary>Sets the experience it gives, for recipes made in code (tests).</summary>
        public Recipe WithExperience(SkillType skillType, float amount)
        {
            experienceSkill = skillType;
            experience = Mathf.Max(0f, amount);
            return this;
        }

        /// <summary>Sets the mineral it extracts and its typical grade (GL128), for recipes made in code (tests).</summary>
        public Recipe WithExtraction(VoxelMaterial mineral, float grade)
        {
            extracts = mineral;
            typicalGrade = Mathf.Clamp(grade, 0.01f, 1f);
            return this;
        }

        /// <summary>Sets the level it needs, for recipes made in code (tests).</summary>
        public Recipe WithUnlock(SkillType skillType, int level)
        {
            unlockSkill = skillType;
            unlockLevel = Mathf.Clamp(level, 0, SkillSet.MaxLevel);
            return this;
        }

        public StationKind Station => station;

        /// <summary>Times the quality of what it makes (GL151).</summary>
        public float QualityFactor => qualityFactor;

        /// <summary>Sets <see cref="QualityFactor"/>, for recipes made in code (tests).</summary>
        public Recipe WithQualityFactor(float factor)
        {
            qualityFactor = Mathf.Clamp(factor, 0.1f, 2f);
            return this;
        }

        /// <summary>The mineral a smelting recipe extracts (GL128), or null.</summary>
        public VoxelMaterial Extracts => extracts;

        /// <summary>The extracted mineral's share of typical ore (GL128).</summary>
        public float TypicalGrade => typicalGrade;

        /// <summary>The skill finishing it trains (GL47); <see cref="SkillType.None"/> for none.</summary>
        public SkillType ExperienceSkill => experienceSkill;

        /// <summary>Experience finishing it gives.</summary>
        public float Experience => experience;

        /// <summary>The skill it needs a level in (GL48); <see cref="SkillType.None"/> when anyone can make it.</summary>
        public SkillType UnlockSkill => unlockSkill;

        public int UnlockLevel => unlockLevel;

        /// <summary>True when a player with these skills can make it; always with no skills given.</summary>
        public bool IsUnlockedFor(SkillSet skills)
        {
            return skills == null || skills.Has(unlockSkill, unlockLevel);
        }

        /// <summary>
        /// True when the recipe is unlocked or needs at most one more level (GL450): station lists show these and hide
        /// recipes further off, to keep the list short.
        /// </summary>
        public bool IsInSightFor(SkillSet skills)
        {
            return skills == null || skills.Level(unlockSkill) + 1 >= unlockLevel;
        }

        public IReadOnlyList<ItemAmount> Inputs => inputs;

        public ItemAmount Output => output;

        public RecipeWork Work => work;

        /// <summary>Seconds or strikes, by <see cref="Work"/>.</summary>
        public float Amount => amount;

        /// <summary>True when the item is one of its inputs.</summary>
        public bool Takes(ItemDefinition item)
        {
            foreach (ItemAmount input in inputs)
            {
                if (input.Item == item)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True when the inventory holds every input.</summary>
        public bool HasInputs(Inventory inventory)
        {
            foreach (ItemAmount input in inputs)
            {
                if (input.Item == null || inventory.Count(input.Item) < input.Count)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
