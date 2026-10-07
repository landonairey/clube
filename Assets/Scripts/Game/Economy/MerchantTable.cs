using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A merchant's table in the world (GL14, first pass of TR4): buys items at its
    /// <see cref="PriceList"/>'s prices. Using it opens the player's
    /// <see cref="MerchantPanel"/>. Placed by hand for now; merchants in towns come with 7C.
    /// Needs a collider on it or a child so <see cref="PlayerInteractor"/> can target it.
    /// </summary>
    public class MerchantTable : MonoBehaviour, IInteractable
    {
        [SerializeField]
        private string displayName = "Merchant";

        [SerializeField]
        private PriceList prices;

        public string DisplayName => displayName;

        public PriceList Prices => prices;

        public string Prompt => $"Trade with {displayName.ToLowerInvariant()}";

        public void Interact(PlayerInteractor by)
        {
            MerchantPanel panel = by.GetComponent<MerchantPanel>();
            if (panel != null)
            {
                panel.Open(this);
            }
        }
    }
}
