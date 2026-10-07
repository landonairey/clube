using System;
using System.Collections.Generic;

namespace Clube.Core
{
    /// <summary>
    /// A station that works its recipes on what's loaded into it (GL8, GL19): items go into
    /// its <see cref="Input"/> slots, it works one recipe at a time, and results stack in its
    /// <see cref="Output"/> slot until taken. A recipe starts as soon as its inputs are loaded
    /// and its result fits: on its own for time recipes (the furnace smelts a whole stack of ore
    /// bun after bun), on the next <see cref="Strike"/> for strike recipes (the anvil hammers
    /// one bun at a time). It stops when the inputs run out or the output is full.
    /// </summary>
    /// <remarks>
    /// Plain data with no UI, like <see cref="Inventory"/>, so it's unit tested and the same
    /// model runs a furnace, an anvil, and later the kiln and alchemy bench (CR4).
    /// </remarks>
    public sealed class CraftingStation
    {
        public const int DefaultInputSlots = 2;

        private readonly List<Recipe> recipes = new List<Recipe>();
        private float done;

        /// <param name="recipes">What it can make; recipes for another kind of station are left out.</param>
        public CraftingStation(StationKind kind, IEnumerable<Recipe> recipes, int inputSlots = DefaultInputSlots)
        {
            Kind = kind;
            foreach (Recipe recipe in recipes)
            {
                if (recipe != null && recipe.Station == kind && recipe.Output.Item != null)
                {
                    this.recipes.Add(recipe);
                }
            }
            Input = new Inventory(inputSlots);
            Output = new Inventory(1);
            Input.Changed += OnContentsChanged;
            Output.Changed += OnContentsChanged;
        }

        /// <summary>Raised after anything about the station changes, progress included.</summary>
        public event Action Changed;

        public StationKind Kind { get; }

        /// <summary>The recipes it can make.</summary>
        public IReadOnlyList<Recipe> Recipes => recipes;

        /// <summary>What's loaded to be worked.</summary>
        public Inventory Input { get; }

        /// <summary>One slot of finished results.</summary>
        public Inventory Output { get; }

        /// <summary>The recipe being worked, or null when idle.</summary>
        public Recipe Active { get; private set; }

        public bool IsBusy => Active != null;

        /// <summary>How far the active recipe is, 0-1; 0 when idle.</summary>
        public float Progress => Active != null ? Math.Min(1f, done / Active.Amount) : 0f;

        /// <summary>Strikes still needed by an active strike recipe; 0 otherwise.</summary>
        public int StrikesLeft => Active != null && Active.Work == RecipeWork.Strikes
            ? Math.Max(0, (int)Math.Ceiling(Active.Amount - done - 1e-4f))
            : 0;

        /// <summary>The recipe that would start next with what's loaded, or null.</summary>
        public Recipe Ready(RecipeWork work)
        {
            foreach (Recipe recipe in recipes)
            {
                if (recipe.Work == work && recipe.HasInputs(Input) && Fits(recipe.Output))
                {
                    return recipe;
                }
            }
            return null;
        }

        /// <summary>Lets time pass: starts and works time recipes, as many as the time covers.</summary>
        public void Tick(float seconds)
        {
            while (seconds > 0f)
            {
                if (Active == null && !TryStart(RecipeWork.Time))
                {
                    return;
                }
                if (Active.Work != RecipeWork.Time)
                {
                    return;
                }
                float needed = Active.Amount - done;
                float used = Math.Min(seconds, needed);
                seconds -= used;
                Work(used);
            }
        }

        /// <summary>One strike: starts a strike recipe if none is under way, then works it. False when there was nothing to strike.</summary>
        public bool Strike()
        {
            if (Active == null && !TryStart(RecipeWork.Strikes))
            {
                return false;
            }
            if (Active.Work != RecipeWork.Strikes)
            {
                return false;
            }
            Work(1f);
            return true;
        }

        private bool TryStart(RecipeWork work)
        {
            Recipe recipe = Ready(work);
            if (recipe == null)
            {
                return false;
            }
            foreach (ItemAmount input in recipe.Inputs)
            {
                Input.Remove(input.Item, input.Count);
            }
            Active = recipe;
            done = 0f;
            Changed?.Invoke();
            return true;
        }

        // The result has to fit on what's already waiting.
        private bool Fits(ItemAmount result)
        {
            ItemStack waiting = Output[0];
            return waiting.IsEmpty || (waiting.Item == result.Item && waiting.Space >= result.Count);
        }

        private void Work(float amount)
        {
            done += amount;
            if (done + 1e-4f >= Active.Amount)
            {
                ItemAmount result = Active.Output;
                Active = null;
                done = 0f;
                Output.Add(result.Item, result.Count);
            }
            Changed?.Invoke();
        }

        private void OnContentsChanged()
        {
            Changed?.Invoke();
        }
    }
}
