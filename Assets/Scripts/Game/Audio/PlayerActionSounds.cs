using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Sounds for what the player does besides walking and hitting: a recipe finished at a station
    /// (<see cref="PlayerSkills.RecipeFinished"/>), a sale at a merchant table, an object placed,
    /// and a parcel of land bought. Each part listens only when its component is on the player.
    /// </summary>
    public class PlayerActionSounds : MonoBehaviour
    {
        [SerializeField]
        private PlayerSounds sounds;

        [SerializeField]
        private AudioSource source;

        private PlayerSkills skills;
        private MerchantPanel merchant;
        private PlayerBuilder builder;
        private PlayerParcels parcels;

        private void Awake()
        {
            skills = GetComponent<PlayerSkills>();
            merchant = GetComponent<MerchantPanel>();
            builder = GetComponent<PlayerBuilder>();
            parcels = GetComponent<PlayerParcels>();
        }

        private void OnEnable()
        {
            if (skills != null)
            {
                skills.RecipeFinished += OnCrafted;
            }
            if (merchant != null)
            {
                merchant.Sold += OnSold;
            }
            if (builder != null)
            {
                builder.Placed += OnPlaced;
            }
            if (parcels != null)
            {
                parcels.Bought += OnParcelBought;
            }
        }

        private void OnDisable()
        {
            if (skills != null)
            {
                skills.RecipeFinished -= OnCrafted;
            }
            if (merchant != null)
            {
                merchant.Sold -= OnSold;
            }
            if (builder != null)
            {
                builder.Placed -= OnPlaced;
            }
            if (parcels != null)
            {
                parcels.Bought -= OnParcelBought;
            }
        }

        /// <summary>The sound settings, for the lab panel's on/off switch.</summary>
        public PlayerSounds Sounds => sounds;

        private void OnCrafted(Recipe recipe)
        {
            if (sounds != null)
            {
                sounds.Play(sounds.Finished(recipe), source);
            }
        }

        private void OnSold(int count)
        {
            if (sounds != null)
            {
                sounds.Play(sounds.Sold, source);
            }
        }

        private void OnPlaced(PlacedObject placed)
        {
            if (sounds != null)
            {
                sounds.Play(sounds.Placed, source);
            }
        }

        private void OnParcelBought(Vector2Int parcel)
        {
            if (sounds != null)
            {
                sounds.Play(sounds.ParcelBought, source);
            }
        }
    }
}
