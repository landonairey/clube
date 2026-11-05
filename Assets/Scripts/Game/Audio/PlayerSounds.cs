using System;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The player's sound effects, in one asset shared by every scene with a player: footsteps by
    /// the ground underfoot, a hit sound per tool, and the sounds of crafting, selling, building
    /// and buying land. Read by <see cref="PlayerFootsteps"/>, <see cref="PlayerToolSounds"/> and
    /// <see cref="PlayerActionSounds"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Clube/Player Sounds", fileName = "PlayerSounds")]
    public class PlayerSounds : ScriptableObject
    {
        [Tooltip("Plays the sound effects. Off for now: the clips are placeholders still being picked (lab panel, Sound section).")]
        [SerializeField]
        private bool effectsOn;

        [Header("Footsteps")]
        [Tooltip("Rock and ore, and anything of kind Rock not listed below.")]
        [SerializeField]
        private SoundSet stoneSteps = new SoundSet();

        [Tooltip("Dirt, and any soft ground not listed below.")]
        [SerializeField]
        private SoundSet dirtSteps = new SoundSet();

        [Tooltip("Grass, and plants (wood, leaves).")]
        [SerializeField]
        private SoundSet grassSteps = new SoundSet();

        [SerializeField]
        private SoundSet claySteps = new SoundSet();

        [Tooltip("The materials with their own footsteps; others go by their kind.")]
        [SerializeField]
        private VoxelMaterial dirt;

        [SerializeField]
        private VoxelMaterial grass;

        [SerializeField]
        private VoxelMaterial clay;

        [Header("Tools")]
        [Tooltip("A hit sound per tool. Tools not listed use Other tools; chisels use Chisel.")]
        [SerializeField]
        private ToolSound[] toolHits = Array.Empty<ToolSound>();

        [SerializeField]
        private SoundSet otherToolHits = new SoundSet();

        [SerializeField]
        private SoundSet chiselHits = new SoundSet();

        [Tooltip("A pick's hit on stone or ore, in place of the pick's own sound: rock cracking and crunching (GL280).")]
        [SerializeField]
        private SoundSet pickRockHits = new SoundSet();

        [Tooltip("Any tool's hit on soft ground (dirt, grass, clay), in place of the tool's own sound.")]
        [SerializeField]
        private SoundSet soilHits = new SoundSet();

        [Tooltip("Any tool's hit on soft plants (leaves), in place of the tool's own sound.")]
        [SerializeField]
        private SoundSet leafHits = new SoundSet();

        [Header("Breaking and gathering")]
        [Tooltip("Rock or ore coming away after a hit (GL271): a crumble of grit.")]
        [SerializeField]
        private SoundSet rockBreaks = new SoundSet();

        [Tooltip("Soft ground coming away: a soft thud of earth.")]
        [SerializeField]
        private SoundSet soilBreaks = new SoundSet();

        [Tooltip("Wood or leaves coming away, and a picked bush: a rustle.")]
        [SerializeField]
        private SoundSet plantBreaks = new SoundSet();

        [Tooltip("What a hit gathered going into the pack: a soft pick-up.")]
        [SerializeField]
        private SoundSet gathered = new SoundSet();

        [Tooltip("A swing at nothing in reach: a whoosh (GL302).")]
        [SerializeField]
        private SoundSet swishes = new SoundSet();

        [Tooltip("A hit the tool can't make (a hand axe on a stump): a low err (GL302).")]
        [SerializeField]
        private SoundSet refusals = new SoundSet();

        [Header("Actions")]
        [Tooltip("A finished recipe that trains Crafting (worktops: sticks, tools), or no skill.")]
        [SerializeField]
        private SoundSet crafted = new SoundSet();

        [Tooltip("A finished recipe that trains Smelting (campfire, furnaces: buns, ingots).")]
        [SerializeField]
        private SoundSet smelted = new SoundSet();

        [Tooltip("A finished recipe that trains Blacksmithing (anvil, flat rock).")]
        [SerializeField]
        private SoundSet smithed = new SoundSet();

        [SerializeField]
        private SoundSet sold = new SoundSet();

        [SerializeField]
        private SoundSet placed = new SoundSet();

        [SerializeField]
        private SoundSet parcelBought = new SoundSet();

        [Tooltip("A stone knapped into a hand axe on a boulder (GL381).")]
        [SerializeField]
        private SoundSet knapped = new SoundSet();

        [Header("Water (GL397)")]
        [Tooltip("Going into water.")]
        [SerializeField]
        private SoundSet splash = new SoundSet();

        [Tooltip("Striding through water.")]
        [SerializeField]
        private SoundSet wade = new SoundSet();

        [Tooltip("Coming out of water.")]
        [SerializeField]
        private SoundSet emerge = new SoundSet();

        /// <summary>Whether any sound effect plays (the lab panel's Sound section).</summary>
        public bool EffectsOn
        {
            get => effectsOn;
            set => effectsOn = value;
        }

        /// <summary>
        /// Plays a sound on a source, unless sound effects are off, at the player's volume for its channel (GL276).
        /// </summary>
        public void Play(SoundSet sound, AudioSource source, float volumeScale = 1f, AudioChannel channel = AudioChannel.Effects)
        {
            if (effectsOn && sound != null)
            {
                sound.Play(source, volumeScale * SoundSettings.Gain(channel));
            }
        }

        /// <summary>The sound of a finished recipe, by the skill it trains.</summary>
        public SoundSet Finished(Recipe recipe)
        {
            switch (recipe != null ? recipe.ExperienceSkill : SkillType.None)
            {
                case SkillType.Smelting:
                    return smelted;
                case SkillType.Blacksmithing:
                    return smithed;
                default:
                    return crafted;
            }
        }

        /// <summary>The sound of a material coming away after a hit (GL271), by its kind.</summary>
        public SoundSet BreakOf(VoxelMaterial material)
        {
            if (material == null)
            {
                return soilBreaks;
            }
            switch (material.Kind)
            {
                case MaterialKind.Rock:
                    return rockBreaks;
                case MaterialKind.Plant:
                    return plantBreaks;
                default:
                    return soilBreaks;
            }
        }

        /// <summary>A picked bush's rustle.</summary>
        public SoundSet Plants => plantBreaks;

        /// <summary>Gathered things going into the pack.</summary>
        public SoundSet Gathered => gathered;

        /// <summary>A swing through the air (GL302).</summary>
        public SoundSet Swish => swishes;

        /// <summary>A hit that does nothing because the tool can't work it (GL302).</summary>
        public SoundSet Refused => refusals;

        public SoundSet Sold => sold;

        public SoundSet Placed => placed;

        public SoundSet Knapped => knapped;

        public SoundSet Splash => splash;

        public SoundSet Wade => wade;

        public SoundSet Emerge => emerge;

        public SoundSet ParcelBought => parcelBought;

        /// <summary>The footsteps for walking on a material; dirt when it's unknown.</summary>
        public SoundSet StepsOn(VoxelMaterial material)
        {
            if (material == null)
            {
                return dirtSteps;
            }
            if (material == clay)
            {
                return claySteps;
            }
            if (material == grass)
            {
                return grassSteps;
            }
            if (material == dirt)
            {
                return dirtSteps;
            }
            switch (material.Kind)
            {
                case MaterialKind.Rock:
                    return stoneSteps;
                case MaterialKind.Plant:
                    return grassSteps;
                default:
                    return dirtSteps;
            }
        }

        /// <summary>
        /// The sound of a hit with a tool on a material: chisels always chisel; soft ground and
        /// leaves sound like themselves whatever hits them; a pick crunches into rock; otherwise the tool's own sound.
        /// </summary>
        public SoundSet HitWith(ToolDefinition tool, VoxelMaterial material = null)
        {
            if (tool != null && tool.Chisels)
            {
                return chiselHits;
            }
            if (material != null && material.IsSoft)
            {
                return material.Kind == MaterialKind.Plant ? leafHits : soilHits;
            }
            if (material != null && material.Kind == MaterialKind.Rock && HeldToolMeshes.ShapeOf(tool) == HeldShape.Pick && pickRockHits.HasClips)
            {
                return pickRockHits;
            }
            foreach (ToolSound entry in toolHits)
            {
                if (entry.Tool == tool)
                {
                    return entry.Sound;
                }
            }
            return otherToolHits;
        }

        /// <summary>One tool's hit sound.</summary>
        [Serializable]
        public class ToolSound
        {
            [SerializeField]
            private ToolDefinition tool;

            [SerializeField]
            private SoundSet sound = new SoundSet();

            public ToolDefinition Tool => tool;

            public SoundSet Sound => sound;
        }
    }
}
