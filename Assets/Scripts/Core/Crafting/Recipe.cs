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
    }

    /// <summary>How a <see cref="Recipe"/>'s work is measured.</summary>
    public enum RecipeWork
    {
        /// <summary>Seconds that pass on their own once it starts, like smelting.</summary>
        Time,

        /// <summary>Strikes the player makes, like hammering on the anvil.</summary>
        Strikes,
    }

    /// <summary>
    /// A skill that improves a recipe's yield (GL33). A placeholder until skills are designed
    /// (SK1): the player holds a level per skill from 0 to 1.
    /// </summary>
    public enum CraftSkill
    {
        None,

        /// <summary>Less scale lost hammering metal (the anvil).</summary>
        Blacksmithing,
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

        [Tooltip("Off: the output count is made from the input counts. On (GL33): the output is a volume, the inputs' volume (or their Content's share of it) times the yield; the station keeps what's short of a whole output item for the next run.")]
        [SerializeField]
        private bool byVolume;

        [Tooltip("By volume: the part of the inputs that becomes the output, e.g. Copper for smelting copper ore (GL32). None: all of it.")]
        [SerializeField]
        private VoxelMaterial content;

        [Tooltip("By volume: share of that volume that ends up in the output, e.g. 0.5 smelting copper ore, 0.8 hammering a bun (20% scale loss).")]
        [SerializeField, Range(0f, 1f)]
        private float yield = 1f;

        [Tooltip("The skill that improves the yield (GL33), and the yield at full skill.")]
        [SerializeField]
        private CraftSkill skill;

        [SerializeField, Range(0f, 1f)]
        private float skilledYield = 1f;

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

        public StationKind Station => station;

        public IReadOnlyList<ItemAmount> Inputs => inputs;

        public ItemAmount Output => output;

        public RecipeWork Work => work;

        /// <summary>Seconds or strikes, by <see cref="Work"/>.</summary>
        public float Amount => amount;

        /// <summary>True when the output is a volume (GL33) rather than a count.</summary>
        public bool ByVolume => byVolume;

        /// <summary>The part of the inputs that becomes the output, or null for all of it.</summary>
        public VoxelMaterial Content => content;

        public CraftSkill Skill => skill;

        /// <summary>Makes the output by volume (GL33), for recipes made in code (tests).</summary>
        public Recipe WithYield(float yield, VoxelMaterial content = null, CraftSkill skill = CraftSkill.None, float skilledYield = -1f)
        {
            byVolume = true;
            this.yield = Mathf.Clamp01(yield);
            this.content = content;
            this.skill = skill;
            this.skilledYield = skilledYield < 0f ? this.yield : Mathf.Clamp01(skilledYield);
            return this;
        }

        /// <summary>The yield at a skill level from 0 (none) to 1 (mastered); just the yield for recipes no skill improves.</summary>
        public float Yield(float skillLevel)
        {
            return skill == CraftSkill.None ? yield : Mathf.Lerp(yield, skilledYield, Mathf.Clamp01(skillLevel));
        }

        /// <summary>
        /// The volume one run makes (GL33): each input's volume, or its <see cref="Content"/>'s
        /// share of it, times the yield. 1 copper ore of 20% copper smelted at 50% makes 0.1.
        /// </summary>
        /// <param name="contentsOf">What each input item is made of (GL32); null or a null result counts as all <see cref="Content"/>.</param>
        public float OutputVolume(Func<ItemDefinition, Composition> contentsOf, float skillLevel)
        {
            float volume = 0f;
            foreach (ItemAmount input in inputs)
            {
                if (input.Item == null)
                {
                    continue;
                }
                float share = 1f;
                if (content != null)
                {
                    Composition contents = contentsOf?.Invoke(input.Item);
                    share = contents != null ? contents.FractionOf(content) : 1f;
                }
                volume += input.Count * input.Item.UnitVolume * share;
            }
            return volume * Yield(skillLevel);
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
