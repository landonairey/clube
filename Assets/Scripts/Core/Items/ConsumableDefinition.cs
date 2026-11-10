using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// An item used up for an effect that lasts a while in game time (GL456: coffee, tools a quarter quicker for three
    /// hours). Drinking another while it lasts doesn't add to the effect; it starts the time again.
    /// </summary>
    [CreateAssetMenu(fileName = "Consumable", menuName = "Clube/Consumable")]
    public class ConsumableDefinition : ItemDefinition
    {
        [Tooltip("How long the effect lasts, in game hours.")]
        [SerializeField, Min(0.01f)]
        private float effectHours = 3f;

        [Tooltip("Tool cooldown while it lasts, as a share of the usual: 0.75 is a quarter quicker.")]
        [SerializeField, Range(0.1f, 1f)]
        private float cooldownShare = 0.75f;

        [Tooltip("What the HUD calls the effect while it lasts, e.g. \"Coffee\".")]
        [SerializeField]
        private string effectName = "Coffee";

        public float EffectHours => effectHours;

        public float CooldownShare => cooldownShare;

        public string EffectName => string.IsNullOrEmpty(effectName) ? DisplayName : effectName;
    }
}
