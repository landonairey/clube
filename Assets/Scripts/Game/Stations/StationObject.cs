using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A crafting station standing in the world (GL10, GL11; CR4): a furnace, an anvil. Holds
    /// the <see cref="CraftingStation"/> that does the work and the recipes it offers, lets
    /// time pass for it, and opens the player's <see cref="StationPanel"/> when used.
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

        public string DisplayName => displayName;

        public CraftingStation Station { get; private set; }

        public IReadOnlyList<Recipe> Recipes => recipes;

        public string Prompt => $"Use {displayName.ToLowerInvariant()}";

        private void Awake()
        {
            Station = new CraftingStation(kind);
        }

        private void Update()
        {
            Station.Tick(Time.deltaTime);
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
