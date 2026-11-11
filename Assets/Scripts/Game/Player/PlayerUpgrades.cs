using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The permanent upgrades the player has bought (GL585). Today just the adventurer's climbing upgrade, which multiplies
    /// the player's climbing speed (<see cref="PlayerController.ClimbSpeedMultiplier"/>). Saved as a value (no save-version
    /// bump) and applied again on load. <see cref="PlayerController"/> adds this itself, so no scene needs editing.
    /// </summary>
    public class PlayerUpgrades : MonoBehaviour, ISavedValues
    {
        private const string ClimbingKey = "upgrades.climbing";

        [Tooltip("What the climbing upgrade multiplies the starting climbing speed by.")]
        [SerializeField, Min(1f)]
        private float climbingMultiplier = 2f;

        private readonly UpgradeSale climbing = new UpgradeSale(AdventurerVisit.ClimbingPrice);
        private PlayerController player;

        /// <summary>True once the climbing upgrade has been bought.</summary>
        public bool HasClimbing => climbing.Owned;

        /// <summary>The climbing upgrade's sale, for a trader to offer.</summary>
        public UpgradeSale Climbing => climbing;

        /// <summary>What a trader offers on the trade screen to sell the climbing upgrade; <paramref name="sellerPurse"/> gets the coins.</summary>
        public ITraderDeal ClimbingDeal(Wallet sellerPurse)
        {
            return new ClimbingUpgradeDeal(this, sellerPurse);
        }

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            Apply();
        }

        private void Apply()
        {
            if (player != null)
            {
                player.ClimbSpeedMultiplier = climbing.Owned ? climbingMultiplier : 1f;
            }
        }

        private bool Buy(Wallet payer, Wallet seller)
        {
            if (!climbing.TryBuy(payer, seller))
            {
                return false;
            }
            Apply();
            return true;
        }

        public void SaveValues(IDictionary<string, string> values)
        {
            if (climbing.Owned)
            {
                values[ClimbingKey] = "1";
            }
        }

        public void RestoreValues(IReadOnlyDictionary<string, string> values)
        {
            climbing.Restore(values.TryGetValue(ClimbingKey, out string owned) && owned == "1");
            Apply();
        }

        // The trade screen's button: on offer until bought, refused with a message when the player is short.
        private sealed class ClimbingUpgradeDeal : ITraderDeal
        {
            private readonly PlayerUpgrades owner;
            private readonly Wallet seller;

            public ClimbingUpgradeDeal(PlayerUpgrades owner, Wallet seller)
            {
                this.owner = owner;
                this.seller = seller;
            }

            public string Label => $"Learn to climb twice as fast ({owner.climbing.Price}c, once)";

            public bool IsOffered(Wallet player) => !owner.climbing.Owned;

            public bool Accept(Wallet player) => owner.Buy(player, seller);

            public string RefusalText(Wallet player) => $"You need {owner.climbing.Price} coins to learn that";
        }
    }
}
