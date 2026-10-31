using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The player's skills (GL47): experience and levels in crafting, woodcutting, mining and
    /// smelting (<see cref="SkillSet"/>). Breaking ground with a tool trains woodcutting (wood,
    /// leaves) or mining (stone, ore, soil), by the amounts below; finishing a recipe at a station
    /// trains the skill the recipe names (<see cref="StationObject"/> reports it). Levels unlock
    /// recipes (GL48) and what the player can see (<see cref="OreKnowledge"/>).
    /// </summary>
    public class PlayerSkills : MonoBehaviour
    {
        [Header("Experience per sample broken")]
        [Tooltip("Woodcutting: wood and stumps.")]
        [SerializeField, Min(0f)]
        private float wood = 1f;

        [Tooltip("Woodcutting: leaves.")]
        [SerializeField, Min(0f)]
        private float leaves = 0.2f;

        [Tooltip("Mining: stone and other hard ground (GL183: set so a Mining level takes about ten times the work it did).")]
        [SerializeField, Min(0f)]
        private float stone = 0.08f;

        [Tooltip("Mining: ore.")]
        [SerializeField, Min(0f)]
        private float ore = 0.16f;

        [Tooltip("Mining: soft ground (grass, dirt, clay).")]
        [SerializeField, Min(0f)]
        private float soil = 0.02f;

        private PlayerToolUser tools;

        /// <summary>Experience and levels in the four skills.</summary>
        public SkillSet Skills { get; } = new SkillSet();

        private void Awake()
        {
            tools = GetComponent<PlayerToolUser>();
        }

        private void OnEnable()
        {
            if (tools != null)
            {
                tools.Mined += OnMined;
            }
        }

        private void OnDisable()
        {
            if (tools != null)
            {
                tools.Mined -= OnMined;
            }
        }

        /// <summary>Raised for each recipe the player finishes at a station (GL73: the journal learns its uses).</summary>
        public event System.Action<Recipe> RecipeFinished;

        /// <summary>Experience for a finished recipe (the station that made it calls this).</summary>
        public void Completed(Recipe recipe)
        {
            if (recipe != null)
            {
                Skills.Award(recipe.ExperienceSkill, recipe.Experience);
                RecipeFinished?.Invoke(recipe);
            }
        }

        // One sample broken by the player's tool.
        private void OnMined(VoxelMaterial material)
        {
            if (material == null)
            {
                return;
            }
            switch (material.Category)
            {
                case VoxelMaterialCategory.Plant:
                    Skills.Award(SkillType.Woodcutting, material.IsSoft ? leaves : wood);
                    break;
                case VoxelMaterialCategory.Ore:
                    Skills.Award(SkillType.Mining, ore);
                    break;
                default:
                    Skills.Award(SkillType.Mining, material.IsSoft ? soil : stone);
                    break;
            }
        }
    }
}
