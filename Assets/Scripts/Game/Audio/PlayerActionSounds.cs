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

        private void OnCrafted(Recipe recipe) => Play(sounds != null ? sounds.Crafted : null);

        private void OnSold(int count) => Play(sounds != null ? sounds.Sold : null);

        private void OnPlaced(PlacedObject placed) => Play(sounds != null ? sounds.Placed : null);

        private void OnParcelBought(Vector2Int parcel) => Play(sounds != null ? sounds.ParcelBought : null);

        private void Play(SoundSet sound)
        {
            sound?.Play(source);
        }
    }
}
