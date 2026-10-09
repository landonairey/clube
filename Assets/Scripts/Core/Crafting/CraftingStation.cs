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
    /// The inputs stay in their slots while they're worked and are used up only when the
    /// recipe finishes, so a struck bun is still a bun until the last strike; taking them out
    /// part way resets the work.
    /// </summary>
    /// <remarks>
    /// <para>Recipes by volume (GL33) make a volume of their output rather than a count; what's
    /// short of a whole item is <see cref="Held"/> until later runs make it up, like copper
    /// gathering in a crucible.</para>
    /// <para>A station that burns fuel (the furnace, GL35) only works time recipes while
    /// something burns: it takes the next item from its <see cref="Fuel"/> slot whenever the
    /// last one has burnt out and there's work to do. What's burning burns down either way.</para>
    /// <para>Plain data with no UI, like <see cref="Inventory"/>, so it's unit tested and the same
    /// model runs a furnace, an anvil, and later the kiln and alchemy bench (CR4).</para>
    /// </remarks>
    public sealed class CraftingStation
    {
        public const int DefaultInputSlots = 2;

        // Volumes this close to a whole item count as one (sums of tenths aren't exact).
        private const float VolumeTolerance = 1e-4f;

        private readonly List<Recipe> recipes = new List<Recipe>();
        private readonly Dictionary<ItemDefinition, float> held = new Dictionary<ItemDefinition, float>();
        private float done;

        /// <param name="recipes">What it can make; recipes for another kind of station are left out.</param>
        /// <param name="burnsFuel">True for a station that needs fuel to work time recipes (GL35); it gets a <see cref="Fuel"/> slot.</param>
        public CraftingStation(StationKind kind, IEnumerable<Recipe> recipes, int inputSlots = DefaultInputSlots, bool burnsFuel = false)
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
            if (burnsFuel)
            {
                Fuel = new Inventory(1);
                Fuel.Changed += OnFuelChanged;
            }
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

        /// <summary>One slot of fuel waiting to burn (GL35); null for a station that needs none.</summary>
        public Inventory Fuel { get; }

        public bool BurnsFuel => Fuel != null;

        /// <summary>Seconds the fuel burning now has left; 0 when nothing burns.</summary>
        public float BurnLeft { get; private set; }

        /// <summary>Seconds the fuel burning now lasts in all.</summary>
        public float BurnTotal { get; private set; }

        /// <summary>How much of the fuel burning now is left, 1-0: the burn-down bar.</summary>
        public float BurnFraction => BurnTotal > 0f ? BurnLeft / BurnTotal : 0f;

        public bool IsBurning => BurnLeft > 0f;

        /// <summary>
        /// The skill levels of whoever is using it, 0-1 per skill (GL33): they improve the yield of
        /// recipes with a <see cref="Recipe.Skill"/>. Null counts every skill as 0. A placeholder
        /// until SK1, set by the player's screen.
        /// </summary>
        public Func<CraftSkill, float> Skills { get; set; }

        /// <summary>The recipe being worked, or null when idle.</summary>
        public Recipe Active { get; private set; }

        public bool IsBusy => Active != null;

        /// <summary>True while a time recipe is under way but waits for fuel.</summary>
        public bool NeedsFuel => BurnsFuel && Active != null && Active.Work == RecipeWork.Time && !IsBurning && !HasFuel;

        /// <summary>How far the active recipe is, 0-1; 0 when idle.</summary>
        public float Progress => Active != null ? Math.Min(1f, done / Active.Amount) : 0f;

        /// <summary>Strikes still needed by an active strike recipe; 0 otherwise.</summary>
        public int StrikesLeft => Active != null && Active.Work == RecipeWork.Strikes
            ? Math.Max(0, (int)Math.Ceiling(Active.Amount - done - 1e-4f))
            : 0;

        private bool HasFuel => Fuel != null && !Fuel[0].IsEmpty;

        /// <summary>
        /// Volume of an output made but short of a whole item (GL33), e.g. 0.3 of a 0.5 copper
        /// bun, waiting for the next runs.
        /// </summary>
        public float Held(ItemDefinition item)
        {
            return item != null && held.TryGetValue(item, out float volume) ? volume : 0f;
        }

        /// <summary>
        /// Loads one of another inventory's stacks (the player's): fuel goes into the
        /// <see cref="Fuel"/> slot unless a recipe here uses it, everything else into
        /// <see cref="Input"/>. Returns how many moved.
        /// </summary>
        public int Load(Inventory from, int slot)
        {
            ItemStack stack = from[slot];
            if (stack.IsEmpty)
            {
                return 0;
            }
            bool fuel = BurnsFuel && stack.Item.IsFuel && !UsesAsInput(stack.Item);
            return from.MoveTo(slot, fuel ? Fuel : Input);
        }

        /// <summary>True when one of its recipes takes the item in.</summary>
        public bool UsesAsInput(ItemDefinition item)
        {
            foreach (Recipe recipe in recipes)
            {
                foreach (ItemAmount input in recipe.Inputs)
                {
                    if (input.Item == item)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>The recipe that would start next with what's loaded, or null.</summary>
        public Recipe Ready(RecipeWork work)
        {
            foreach (Recipe recipe in recipes)
            {
                if (recipe.Work == work && recipe.HasInputs(Input) && Fits(recipe))
                {
                    return recipe;
                }
            }
            return null;
        }

        /// <summary>Lets time pass: burns fuel, and starts and works time recipes, as many as the time covers.</summary>
        public void Tick(float seconds)
        {
            while (seconds > 0f)
            {
                if (Active == null && !TryStart(RecipeWork.Time))
                {
                    break;
                }
                if (Active.Work != RecipeWork.Time)
                {
                    break;
                }
                if (BurnsFuel && !IsBurning && !Ignite())
                {
                    break;
                }
                float step = Math.Min(seconds, Active.Amount - done);
                if (BurnsFuel)
                {
                    step = Math.Min(step, BurnLeft);
                    Burn(step);
                }
                seconds -= step;
                Work(step);
            }

            // Idle (or waiting): the fire burns down all the same.
            if (seconds > 0f && IsBurning)
            {
                Burn(Math.Min(seconds, BurnLeft));
                Changed?.Invoke();
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

        // The inputs stay put until the recipe finishes (Work).
        private bool TryStart(RecipeWork work)
        {
            Recipe recipe = Ready(work);
            if (recipe == null)
            {
                return false;
            }
            Active = recipe;
            done = 0f;
            Changed?.Invoke();
            return true;
        }

        // Lights the next fuel item; false when there's none.
        private bool Ignite()
        {
            if (!HasFuel)
            {
                return false;
            }
            float seconds = Fuel[0].Item.BurnSeconds;
            Fuel.RemoveAt(0);
            BurnLeft = BurnTotal = seconds;
            return BurnLeft > 0f;
        }

        private void Burn(float seconds)
        {
            BurnLeft = Math.Max(0f, BurnLeft - seconds);
            if (BurnLeft <= 0f)
            {
                BurnTotal = 0f;
            }
        }

        // The result has to fit on what's already waiting: a whole count, or one more item
        // for a recipe by volume (it may make less, which is held).
        private bool Fits(Recipe recipe)
        {
            ItemStack waiting = Output[0];
            int needed = recipe.ByVolume ? 1 : recipe.Output.Count;
            return waiting.IsEmpty || (waiting.Item == recipe.Output.Item && waiting.Space >= needed);
        }

        private void Work(float amount)
        {
            done += amount;
            if (done + 1e-4f >= Active.Amount)
            {
                // Finished: the inputs turn into the result.
                Recipe finished = Active;
                Active = null;
                done = 0f;
                float volume = finished.ByVolume ? finished.OutputVolume(Input.ContentsOf, Skills?.Invoke(finished.Skill) ?? 0f) : 0f;
                foreach (ItemAmount input in finished.Inputs)
                {
                    Input.Remove(input.Item, input.Count);
                }
                if (finished.ByVolume)
                {
                    Make(finished.Output.Item, volume);
                }
                else
                {
                    Output.Add(finished.Output.Item, finished.Output.Count);
                }
            }
            Changed?.Invoke();
        }

        // Adds a volume of an item to what's held and moves every whole item that fits out.
        private void Make(ItemDefinition item, float volume)
        {
            float total = Held(item) + volume;
            int whole = (int)Math.Floor((total + VolumeTolerance) / item.UnitVolume);
            if (whole > 0)
            {
                int left = Output.Add(item, whole);
                total -= (whole - left) * item.UnitVolume;
            }
            held[item] = Math.Max(0f, total);
        }

        // Inputs taken out, or the output filled, part way through: the work starts over.
        private void OnContentsChanged()
        {
            if (Active != null && (!Active.HasInputs(Input) || !Fits(Active)))
            {
                Active = null;
                done = 0f;
            }
            Changed?.Invoke();
        }

        private void OnFuelChanged()
        {
            Changed?.Invoke();
        }
    }
}
