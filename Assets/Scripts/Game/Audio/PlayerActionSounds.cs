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
        private BoulderBashing bashing;
        private PlayerProspecting prospecting;

        private void Awake()
        {
            lickClips = lickClips ?? Make("Lick", SoundSynth.Lick, 440);
            pickClips = pickClips ?? Make("Pick", SoundSynth.Pick, 450);
            skills = GetComponent<PlayerSkills>();
            merchant = GetComponent<MerchantPanel>();
            builder = GetComponent<PlayerBuilder>();
            parcels = GetComponent<PlayerParcels>();
            bashing = GetComponent<BoulderBashing>();
            prospecting = GetComponent<PlayerProspecting>();
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
            if (bashing != null)
            {
                bashing.HandAxeMade += OnKnapped;
                bashing.OreCrushed += OnCrushed;
                bashing.RefinePassed += OnPicked;
            }
            if (prospecting != null)
            {
                prospecting.Licked += OnLicked;
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
            if (bashing != null)
            {
                bashing.HandAxeMade -= OnKnapped;
                bashing.OreCrushed -= OnCrushed;
                bashing.RefinePassed -= OnPicked;
            }
            if (prospecting != null)
            {
                prospecting.Licked -= OnLicked;
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

        private void OnKnapped()
        {
            if (sounds != null)
            {
                sounds.Play(sounds.Knapped, source);
            }
        }

        // Ore crushed on a boulder (GL459).
        private void OnCrushed(Vector3 at, VoxelMaterial ore)
        {
            if (sounds != null)
            {
                sounds.Play(sounds.Crushed, source);
            }
        }

        // Licking and picking over are made from scratch when first needed (GL492, GL493), a few variants each.
        private static AudioClip[] lickClips;
        private static AudioClip[] pickClips;

        private void PlayMade(AudioClip[] clips, float volume)
        {
            if (sounds.EffectsOn && source != null)
            {
                source.PlayOneShot(clips[Random.Range(0, clips.Length)], volume * SoundSettings.Gain(AudioChannel.Effects));
            }
        }

        private static AudioClip[] Make(string name, System.Func<int, float[]> synth, int seed)
        {
            var clips = new AudioClip[3];
            for (int i = 0; i < clips.Length; i++)
            {
                float[] samples = synth(seed + i);
                clips[i] = AudioClip.Create(name, samples.Length, 1, SoundSynth.SampleRate, false);
                clips[i].SetData(samples, 0);
            }
            return clips;
        }

        private void OnLicked(ItemDefinition ore)
        {
            if (sounds != null)
            {
                PlayMade(lickClips, 0.9f);
            }
        }

        private void OnPicked()
        {
            if (sounds != null)
            {
                PlayMade(pickClips, 0.7f);
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
