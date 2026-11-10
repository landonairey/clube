using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A Mining Corp crate on the tutorial map (GL514): Interact takes what's inside, which is theft
    /// (<see cref="MiningCorpTutorial.Steal"/>). Once emptied it has nothing more to give.
    /// </summary>
    public class CompanyCrate : MonoBehaviour, IInteractable
    {
        /// <summary>The tutorial that sees the theft.</summary>
        public MiningCorpTutorial Tutorial { get; set; }

        /// <summary>What's inside, one of each.</summary>
        public ItemDefinition[] Loot { get; set; } = new ItemDefinition[0];

        /// <summary>The crate's name in its prompt.</summary>
        public string Title { get; set; } = "A crate";

        public string Prompt => Loot.Length > 0 ? $"{Title} (company property): take what's inside" : $"{Title}: empty";

        public void Interact(PlayerInteractor by)
        {
            if (Tutorial == null || Loot.Length == 0)
            {
                return;
            }
            ItemDefinition[] taken = Loot;
            Loot = new ItemDefinition[0];
            Tutorial.Steal(taken);
        }
    }
}
