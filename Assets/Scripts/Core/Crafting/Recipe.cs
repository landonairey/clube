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
