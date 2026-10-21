using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A crafting station standing in the world (GL10, GL11, GL19; CR4): a furnace, an anvil.
    /// Holds the <see cref="CraftingStation"/> that does the work, with its slots and the
    /// recipes it offers, lets time pass for it, and opens the player's
    /// <see cref="StationPanel"/> when used.
    /// </summary>
    /// <remarks>
    /// Placed by hand in the scene for now; building them is PK17. Needs a collider on it or
    /// a child so <see cref="PlayerInteractor"/> can target it.
    /// </remarks>
    public class StationObject : MonoBehaviour, IInteractable
    {
        [SerializeField]
        private string displayName = "Furnace";

        [SerializeField]
        private StationKind kind;

        [Tooltip("What it can make. Recipes for another kind of station are ignored.")]
        [SerializeField]
        private List<Recipe> recipes = new List<Recipe>();

        [Tooltip("Needs fuel to work its time recipes (the furnace, GL35): it gets a fuel slot.")]
        [SerializeField]
        private bool burnsFuel;

        [Tooltip("How hot it works (GL98): time recipes go this many times faster on the same fuel. Stone furnace 1, clay furnace hotter.")]
        [SerializeField, Min(0.1f)]
        private float heat = 1f;

        public string DisplayName => displayName;

        public CraftingStation Station { get; private set; }

        public IReadOnlyList<Recipe> Recipes => recipes;

        /// <summary>How hot it works (GL98); 1 for a stone furnace.</summary>
        public float Heat => heat;

        /// <summary>How well it was built (GL100): the share of what it's made of that's its own material, 0-1; 1 when made of pure items.</summary>
        public float Quality { get; private set; } = 1f;

        /// <summary>
        /// Sets how well it was built (GL100, GL118): the quality of the item it was placed from, which a mix
        /// gives it, so a clay furnace of 76% clay works at 76% of its heat and yields 76% (GL127).
        /// </summary>
        public void MadeOf(float quality)
        {
            Quality = Mathf.Clamp01(quality);
            Station.Heat = heat * Quality;
            Station.Quality = Quality;
        }

        public string Prompt => $"Use {displayName.ToLowerInvariant()}";

        private void Awake()
        {
            Station = new CraftingStation(kind, recipes, CraftingStation.DefaultInputSlots, burnsFuel) { Heat = heat };
            ConnectToPlayer();
        }

        /// <summary>Sets up a station made in code, such as a stump worktop (GL36), replacing what Awake built.</summary>
        public void Configure(string name, StationKind stationKind, IEnumerable<Recipe> offered)
        {
            displayName = name;
            kind = stationKind;
            recipes = new List<Recipe>(offered);
            burnsFuel = false;
            Station = new CraftingStation(kind, recipes, CraftingStation.DefaultInputSlots, burnsFuel);
            ConnectToPlayer();
        }

        private void Update()
        {
            Station.Tick(Time.deltaTime);
        }

        // The player's skills decide which recipes can start (GL48), and finished ones train them (GL47).
        // One player: found once, inactive too (the labs keep the player off until P).
        private void ConnectToPlayer()
        {
            var skills = FindFirstObjectByType<PlayerSkills>(FindObjectsInactive.Include);
            if (skills == null)
            {
                return;
            }
            Station.Learner = skills.Skills;
            Station.Completed += skills.Completed;
        }

        public void Interact(PlayerInteractor by)
        {
            StationPanel panel = by.GetComponent<StationPanel>();
            if (panel != null)
            {
                panel.Open(this);
            }
        }
    }
}
