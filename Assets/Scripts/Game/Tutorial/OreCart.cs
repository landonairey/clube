using UnityEngine;

namespace Clube.Game
{
    /// <summary>The Mining Corp's ore cart at the tutorial's quarry (GL479): Interact hands in the company's stone for pay.</summary>
    public class OreCart : MonoBehaviour, IInteractable
    {
        /// <summary>The tutorial that takes the stone and pays for it.</summary>
        public MiningCorpTutorial Tutorial { get; set; }

        public string Prompt => Tutorial != null ? Tutorial.HandInPrompt : "Ore cart";

        public void Interact(PlayerInteractor by)
        {
            if (Tutorial != null)
            {
                Tutorial.HandIn();
            }
        }
    }
}
