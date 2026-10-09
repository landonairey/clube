using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The player's skill levels, 0 (none) to 1 (mastered), as far as recipes read them (GL33):
    /// blacksmithing lowers the scale lost hammering an ingot. A placeholder set in the
    /// Inspector until skills are designed and earned (SK1); stations read it through
    /// <see cref="StationPanel"/>.
    /// </summary>
    public class PlayerSkills : MonoBehaviour
    {
        [Tooltip("Lowers the scale lost on the anvil: 0 loses the recipe's base share, 1 its skilled share.")]
        [SerializeField, Range(0f, 1f)]
        private float blacksmithing;

        /// <summary>The level of a skill, 0-1; 0 for <see cref="CraftSkill.None"/>.</summary>
        public float Level(CraftSkill skill)
        {
            return skill == CraftSkill.Blacksmithing ? blacksmithing : 0f;
        }

        public float Blacksmithing
        {
            get => blacksmithing;
            set => blacksmithing = Mathf.Clamp01(value);
        }
    }
}
