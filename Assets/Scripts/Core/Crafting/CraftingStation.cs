using System;

namespace Clube.Core
{
    /// <summary>
    /// A station working one <see cref="Recipe"/> at a time (GL8): <see cref="Start"/> takes
    /// the inputs from an inventory, the work is done by time (<see cref="Tick"/>) or by
    /// strikes (<see cref="Strike"/>), and the result waits in <see cref="Output"/> until
    /// <see cref="TakeOutput"/> moves it into an inventory. Finished results of the same item
    /// pile up, so a furnace can keep smelting while nobody empties it.
    /// </summary>
    /// <remarks>
    /// Plain data with no UI, like <see cref="Inventory"/>, so it's unit tested and the same
    /// model runs a furnace, an anvil, and later the kiln and alchemy bench (CR4).
    /// </remarks>
    public sealed class CraftingStation
    {
        private float done;

        public CraftingStation(StationKind kind)
        {
            Kind = kind;
        }

        /// <summary>Raised after anything about the station changes, progress included.</summary>
        public event Action Changed;

        public StationKind Kind { get; }

        /// <summary>The recipe being worked, or null when idle.</summary>
        public Recipe Active { get; private set; }

        public bool IsBusy => Active != null;

        /// <summary>How far the active recipe is, 0-1; 0 when idle.</summary>
        public float Progress => Active != null ? Math.Min(1f, done / Active.Amount) : 0f;

        /// <summary>Strikes still needed by an active strike recipe; 0 otherwise.</summary>
        public int StrikesLeft => Active != null && Active.Work == RecipeWork.Strikes
            ? Math.Max(0, (int)Math.Ceiling(Active.Amount - done - 1e-4f))
            : 0;

        /// <summary>Finished results waiting to be taken.</summary>
        public ItemStack Output { get; private set; }

        /// <summary>True when the station could start the recipe with this inventory's items.</summary>
        public bool CanStart(Recipe recipe, Inventory from)
        {
            if (recipe == null || recipe.Station != Kind || IsBusy || recipe.Output.Item == null || !recipe.HasInputs(from))
            {
                return false;
            }
            // The result has to fit on what's already waiting.
            return Output.IsEmpty
                || (Output.Item == recipe.Output.Item && Output.Space >= recipe.Output.Count);
        }

        /// <summary>Takes the inputs and starts the work. False, changing nothing, when it can't.</summary>
        public bool Start(Recipe recipe, Inventory from)
        {
            if (!CanStart(recipe, from))
            {
                return false;
            }
            foreach (ItemAmount input in recipe.Inputs)
            {
                from.Remove(input.Item, input.Count);
            }
            Active = recipe;
            done = 0f;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Lets time pass: works an active time recipe.</summary>
        public void Tick(float seconds)
        {
            if (Active != null && Active.Work == RecipeWork.Time && seconds > 0f)
            {
                Work(seconds);
            }
        }

        /// <summary>One strike: works an active strike recipe. False when there was nothing to strike.</summary>
        public bool Strike()
        {
            if (Active == null || Active.Work != RecipeWork.Strikes)
            {
                return false;
            }
            Work(1f);
            return true;
        }

        /// <summary>Moves the waiting output into an inventory; returns how many didn't fit (and stay).</summary>
        public int TakeOutput(Inventory to)
        {
            if (Output.IsEmpty)
            {
                return 0;
            }
            int left = to.Add(Output.Item, Output.Count);
            if (left != Output.Count)
            {
                Output = Output.WithCount(left);
                Changed?.Invoke();
            }
            return left;
        }

        private void Work(float amount)
        {
            done += amount;
            if (done + 1e-4f >= Active.Amount)
            {
                ItemAmount result = Active.Output;
                Output = new ItemStack(result.Item, (Output.IsEmpty ? 0 : Output.Count) + result.Count);
                Active = null;
                done = 0f;
            }
            Changed?.Invoke();
        }
    }
}
