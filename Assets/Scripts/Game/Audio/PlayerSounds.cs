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

        [Header("Actions")]
        [SerializeField]
        private SoundSet crafted = new SoundSet();

        [SerializeField]
        private SoundSet sold = new SoundSet();

        [SerializeField]
        private SoundSet placed = new SoundSet();

        [SerializeField]
        private SoundSet parcelBought = new SoundSet();

        public SoundSet Crafted => crafted;

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

        /// <summary>The sound of a hit with a tool.</summary>
        public SoundSet HitWith(ToolDefinition tool)
        {
            if (tool != null && tool.Chisels)
            {
                return chiselHits;
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
