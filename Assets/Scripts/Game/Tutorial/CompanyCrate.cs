using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A Mining Corp crate on the tutorial map (GL514): Interact opens it to look inside (GL521). Looking is fine; it's
    /// theft only when an item goes into the player's inventory (<see cref="MiningCorpTutorial.Steal"/>).
    /// </summary>
    public class CompanyCrate : MonoBehaviour, IInteractable
    {
        /// <summary>The tutorial that sees the theft.</summary>
        public MiningCorpTutorial Tutorial { get; set; }

        /// <summary>What's inside.</summary>
        public System.Collections.Generic.List<ItemDefinition> Loot { get; } = new System.Collections.Generic.List<ItemDefinition>();

        /// <summary>The crate's name in its prompt.</summary>
        public string Title { get; set; } = "A crate";

        public string Prompt => $"{Title} (company property): Do not take";

        public void Interact(PlayerInteractor by)
        {
            if (Tutorial != null)
            {
                Tutorial.OpenCrate(this);
            }
        }
    }
}
