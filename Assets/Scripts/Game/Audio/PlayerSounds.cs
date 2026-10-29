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

        [Tooltip("Any tool's hit on soft ground (dirt, grass, clay), in place of the tool's own sound.")]
        [SerializeField]
        private SoundSet soilHits = new SoundSet();

        [Tooltip("Any tool's hit on soft plants (leaves), in place of the tool's own sound.")]
        [SerializeField]
        private SoundSet leafHits = new SoundSet();

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

        /// <summary>Whether any sound effect plays (the lab panel's Sound section).</summary>
        public bool EffectsOn
        {
            get => effectsOn;
            set => effectsOn = value;
        }

        /// <summary>Plays a sound on a source, unless sound effects are off.</summary>
        public void Play(SoundSet sound, AudioSource source, float volumeScale = 1f)
        {
            if (effectsOn && sound != null)
            {
                sound.Play(source, volumeScale);
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

        public SoundSet Sold => sold;

        public SoundSet Placed => placed;

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
        /// leaves sound like themselves whatever hits them; otherwise the tool's own sound.
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
