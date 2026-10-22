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
    /// <para>Every recipe is a fixed batch (GL61): a set count of inputs makes a set count of
    /// outputs, and no inputs are held between runs, so whatever is loaded next starts clean. The
    /// count is scaled by the station's yield (GL127, GL128), which can come out as a decimal amount
    /// (GL136: 0.48 of a tin bun).</para>
    /// <para>A station that burns fuel (the furnace, GL35) only works time recipes while
    /// something burns: it takes the next item from its <see cref="Fuel"/> slot whenever the
    /// last one has burnt out and there's work to do. What's burning burns down either way.</para>
    /// <para>Plain data with no UI, like <see cref="Inventory"/>, so it's unit tested and the same
    /// model runs a furnace, an anvil, and later the kiln and alchemy bench (CR4).</para>
    /// </remarks>
    public sealed class CraftingStation
    {
        public const int DefaultInputSlots = 2;

        /// <summary>The most a smelt yields of its count from ore richer than typical (GL128).</summary>
        public const float MaxGradeYield = 2f;

        private readonly List<Recipe> recipes = new List<Recipe>();
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

        /// <summary>Raised when a recipe finishes (GL47), for the experience it gives.</summary>
        public event Action<Recipe> Completed;

        /// <summary>
        /// The skills of whoever uses the station (GL48): recipes they haven't unlocked never start.
        /// Null: every recipe can start.
        /// </summary>
        public SkillSet Learner { get; set; }

        public StationKind Kind { get; }

        /// <summary>The recipes it can make.</summary>
        public IReadOnlyList<Recipe> Recipes => recipes;

        /// <summary>
        /// The recipe the player picked (ML1), when several share inputs (sticks and stone make a
        /// spear, a hatchet or a hammer): only it starts. Null: the first recipe whose inputs are
        /// loaded starts, as before. Setting one this station doesn't make clears it.
        /// </summary>
        public Recipe Chosen
        {
            get => chosen;
            set
            {
                chosen = value != null && recipes.Contains(value) ? value : null;
                Changed?.Invoke();
            }
        }

        private Recipe chosen;

        /// <summary>What's loaded to be worked.</summary>
        public Inventory Input { get; }

        /// <summary>One slot of finished results.</summary>
        public Inventory Output { get; }

        /// <summary>One slot of fuel waiting to burn (GL35); null for a station that needs none.</summary>
        public Inventory Fuel { get; }

        public bool BurnsFuel => Fuel != null;

        /// <summary>
        /// How hot it works (GL98): time recipes go this many times faster, on the same fuel per second, so
        /// a hotter furnace (clay over stone) smelts quicker and on less fuel. 1 is the stone furnace.
        /// </summary>
        public float Heat { get; set; } = 1f;

        /// <summary>
        /// How well the station is built, 0-1 (GL127): a share of every recipe's yield, so a 50% furnace makes
        /// half as many buns from the same ore. Set with its <see cref="Heat"/> when it's placed.
        /// </summary>
        public float Quality { get; set; } = 1f;

        /// <summary>Seconds the fuel burning now has left; 0 when nothing burns.</summary>
        public float BurnLeft { get; private set; }

        /// <summary>Seconds the fuel burning now lasts in all.</summary>
        public float BurnTotal { get; private set; }

        /// <summary>How much of the fuel burning now is left, 1-0: the burn-down bar.</summary>
        public float BurnFraction => BurnTotal > 0f ? BurnLeft / BurnTotal : 0f;

        public bool IsBurning => BurnLeft > 0f;

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

        /// <summary>
        /// True for a station that works on its own once loaded (a smelter: it has a time recipe),
        /// so what's loaded stays when the player leaves. Worktops and anvils only work while used.
        /// </summary>
        public bool WorksUnattended
        {
            get
            {
                foreach (Recipe recipe in recipes)
                {
                    if (recipe.Work == RecipeWork.Time)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Moves what's left in the input slots into another inventory (the player's, GL60), as
        /// much as fits; the rest stays. Returns how many items moved.
        /// </summary>
        public int ReturnInputs(Inventory to)
        {
            int moved = 0;
            for (int slot = 0; slot < Input.SlotCount; slot++)
            {
                moved += Input.MoveTo(slot, to);
            }
            return moved;
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

        /// <summary>
        /// Loads a recipe from another inventory (the player's, GL80, GL140): whatever in the input slots
        /// the recipe doesn't take goes back there first, then each input gets one batch, and then each
        /// is topped up to a full stack from what's there, onto the stacks already loaded. Returns how many
        /// items moved in.
        /// </summary>
        public int LoadFor(Recipe recipe, Inventory from)
        {
            if (recipe == null || from == null)
            {
                return 0;
            }
            for (int slot = 0; slot < Input.SlotCount; slot++)
            {
                ItemDefinition item = Input[slot].Item;
                if (item != null && !recipe.Takes(item))
                {
                    Input.MoveTo(slot, from);
                }
            }
            int moved = 0;
            foreach (ItemAmount input in recipe.Inputs)
            {
                int missing = input.Count - Input.Count(input.Item);
                if (input.Item != null && missing > 0)
                {
                    moved += from.MoveItemTo(input.Item, missing, Input);
                }
            }
            // A full stack of each (GL140), onto what each input already holds so every input keeps its slot.
            foreach (ItemAmount input in recipe.Inputs)
            {
                if (input.Item == null)
                {
                    continue;
                }
                for (int slot = 0; slot < from.SlotCount; slot++)
                {
                    ItemStack stack = from[slot];
                    if (stack.Item != input.Item || stack.Count == 0)
                    {
                        continue;
                    }
                    int placed = stack.Count - Input.AddToStacks(stack.Item, stack.Count, stack.Contents, stack.Quality);
                    moved += from.RemoveAt(slot, placed);
                }
            }
            return moved;
        }

        /// <summary>The recipe that would start next with what's loaded, or null.</summary>
        public Recipe Ready(RecipeWork work)
        {
            // A chosen recipe is the only one that starts; others sharing its inputs wait (ML1).
            if (Chosen != null)
            {
                return Chosen.Work == work && Chosen.IsUnlockedFor(Learner) && Chosen.HasInputs(Input) && OutputFits(Chosen) ? Chosen : null;
            }
            return Best(recipe => recipe.Work == work && recipe.IsUnlockedFor(Learner) && recipe.HasInputs(Input) && OutputFits(recipe));
        }

        // Of the recipes that pass, the one that uses the most of what's loaded (GL81): the most
        // different inputs, then the most items, so sticks and stone make a stone pick, not a
        // pointy stick. Ties go to the first listed.
        private Recipe Best(Func<Recipe, bool> passes)
        {
            Recipe best = null;
            int bestKinds = -1;
            int bestCount = -1;
            foreach (Recipe recipe in recipes)
            {
                if (!passes(recipe))
                {
                    continue;
                }
                int kinds = recipe.Inputs.Count;
                int count = 0;
                foreach (ItemAmount input in recipe.Inputs)
                {
                    count += input.Count;
                }
                if (kinds > bestKinds || (kinds == bestKinds && count > bestCount))
                {
                    best = recipe;
                    bestKinds = kinds;
                    bestCount = count;
                }
            }
            return best;
        }

        /// <summary>
        /// What the station is about to make (GL58): the recipe under way, else the one the player
        /// picked, else the unlocked one that uses the most of what's loaded (GL81), whether or not its result
        /// fits yet. Null when nothing is loaded for any recipe.
        /// </summary>
        public Recipe Upcoming
        {
            get
            {
                if (Active != null)
                {
                    return Active;
                }
                if (Chosen != null)
                {
                    return Chosen.IsUnlockedFor(Learner) ? Chosen : null;
                }
                return Best(recipe => recipe.IsUnlockedFor(Learner) && recipe.HasInputs(Input));
            }
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
                float heat = Math.Max(0.01f, Heat);
                float step = Math.Min(seconds, (Active.Amount - done) / heat);
                if (BurnsFuel)
                {
                    step = Math.Min(step, BurnLeft);
                    Burn(step);
                }
                seconds -= step;
                Work(step * heat);
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
            // Mixed fuel burns for its quality's share (GL124): grass with dirt in it burns shorter.
            float seconds = Fuel[0].Item.BurnSeconds * Fuel[0].Quality;
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

        /// <summary>
        /// True when the recipe's result fits on what's already waiting in <see cref="Output"/>:
        /// an empty slot, or the same item with room for the whole count. A different item there
        /// (tin buns left in the campfire) holds the recipe back until it's taken.
        /// </summary>
        public bool OutputFits(Recipe recipe)
        {
            ItemStack waiting = Output[0];
            return waiting.IsEmpty || (waiting.Item == recipe.Output.Item && waiting.Space >= (int)Math.Ceiling(recipe.Output.Count * YieldOf(recipe) - 1e-4f));
        }

        /// <summary>
        /// What a run of the recipe yields of its output count with what's loaded now (GL127, GL128): the station's
        /// <see cref="Quality"/>, times, for a smelt, how rich the ore is against its typical grade (up to
        /// <see cref="MaxGradeYield"/>). 1 for a plain recipe at a station of full quality.
        /// </summary>
        public float YieldOf(Recipe recipe)
        {
            float yield = Math.Max(0f, Quality);
            if (recipe != null && recipe.Extracts != null)
            {
                yield *= Math.Min(MaxGradeYield, Concentration(recipe) / recipe.TypicalGrade);
            }
            return yield;
        }

        /// <summary>How good a Blacksmithing recipe's result can be at a level (GL129): 50% at level 0 up to 100% at level 10.</summary>
        public static float SmithingQuality(int level)
        {
            return 0.5f + 0.05f * Math.Min(10, Math.Max(0, level));
        }

        // What a finished run gives (GL136): its count at the current yield, as a decimal amount.
        private float TakeYield(Recipe recipe)
        {
            return recipe.Output.Count * YieldOf(recipe);
        }

        // The extracted mineral's share of what's loaded for the recipe, by count (GL128); an input with no
        // contents counts as typical ore.
        private float Concentration(Recipe recipe)
        {
            float total = 0f;
            int count = 0;
            foreach (ItemAmount input in recipe.Inputs)
            {
                Composition contents = Input.ContentsOf(input.Item);
                float share = contents != null ? contents.FractionOf(recipe.Extracts) : recipe.TypicalGrade;
                total += share * input.Count;
                count += input.Count;
            }
            return count > 0 ? total / count : recipe.TypicalGrade;
        }

        // How good the result is: smelted metal comes out pure (GL128), a smith's work is no better than the
        // smith (GL129) nor its inputs, and anything else is as good as its inputs (GL118).
        private float OutputQuality(Recipe recipe)
        {
            if (recipe.Extracts != null)
            {
                return 1f;
            }
            float quality = InputQuality(recipe);
            if (recipe.ExperienceSkill == SkillType.Blacksmithing)
            {
                int level = Learner != null ? Learner.Level(SkillType.Blacksmithing) : 0;
                quality = Math.Min(quality, SmithingQuality(level));
            }
            return quality;
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
                // Something built (a furnace) keeps what it was made of, so a mix of 76% clay makes one
                // that works at 76% (GL100, MaterialRegistry.Quality).
                Composition madeOf = finished.Output.Item is PlaceableDefinition ? InputContents(finished) : null;
                float quality = OutputQuality(finished);
                float made = TakeYield(finished);
                foreach (ItemAmount input in finished.Inputs)
                {
                    Input.Remove(input.Item, input.Count);
                }
                int whole = (int)Math.Floor(made + 1e-4f);
                Output.Add(finished.Output.Item, whole, madeOf, 0, quality, made - whole);
                Completed?.Invoke(finished);
            }
            Changed?.Invoke();
        }

        // How well a recipe's result is made (GL118): its inputs' quality, weighted by how many of each it takes,
        // so a pointy stick from sticks of 60% wood is a 60% stick.
        private float InputQuality(Recipe recipe)
        {
            float total = 0f;
            int count = 0;
            foreach (ItemAmount input in recipe.Inputs)
            {
                total += Input.QualityOf(input.Item) * input.Count;
                count += input.Count;
            }
            return count > 0 ? total / count : 1f;
        }

        // What a recipe's inputs are made of, blended by count; null when none carries contents.
        private Composition InputContents(Recipe recipe)
        {
            Composition blended = null;
            float volume = 0f;
            foreach (ItemAmount input in recipe.Inputs)
            {
                Composition contents = Input.ContentsOf(input.Item);
                if (contents != null)
                {
                    blended = Composition.Blend(blended, volume, contents, input.Count);
                    volume += input.Count;
                }
            }
            return blended;
        }

        // Inputs taken out, or the output filled, part way through: the work starts over.
        private void OnContentsChanged()
        {
            // Other inputs swapped in for the picked recipe's (chalcopyrite for cassiterite, GL142): let what's
            // loaded pick the recipe again.
            if (chosen != null && !HoldsAnyOf(chosen) && HoldsAnything())
            {
                chosen = null;
            }
            if (Active != null && (!Active.HasInputs(Input) || !OutputFits(Active)))
            {
                Active = null;
                done = 0f;
            }
            Changed?.Invoke();
        }

        private bool HoldsAnyOf(Recipe recipe)
        {
            foreach (ItemAmount input in recipe.Inputs)
            {
                if (input.Item != null && Input.Count(input.Item) > 0)
                {
                    return true;
                }
            }
            return false;
        }

        private bool HoldsAnything()
        {
            for (int slot = 0; slot < Input.SlotCount; slot++)
            {
                if (!Input[slot].IsEmpty)
                {
                    return true;
                }
            }
            return false;
        }

        private void OnFuelChanged()
        {
            Changed?.Invoke();
        }
    }
}
