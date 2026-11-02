using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A torch (GL287): placed on the ground or held in the right hand. An unlit torch keeps; it lights as soon as it's
    /// held or placed, and from then burns for <see cref="LifeSeconds"/> of game time and goes out, gone. A lit torch is its
    /// own item (<see cref="Lit"/>), one to a stack, its <see cref="ItemStack.Wear"/> the seconds it has burned; it sells
    /// for the unlit torch's price scaled by what's left of it (<see cref="PriceList"/>).
    /// </summary>
    [CreateAssetMenu(fileName = "Torch", menuName = "Clube/Torch")]
    public class TorchDefinition : PlaceableDefinition
    {
        [Tooltip("Seconds of game time a lit torch burns.")]
        [SerializeField, Min(1f)]
        private float lifeSeconds = 1200f;

        [Tooltip("The lit item this torch becomes; on the lit torch itself, none.")]
        [SerializeField]
        private TorchDefinition lit;

        [Tooltip("On the lit torch: the unlit one it was, whose price it sells at.")]
        [SerializeField]
        private TorchDefinition unlit;

        public float LifeSeconds => lifeSeconds;

        /// <summary>True for a burning torch.</summary>
        public bool IsLit => unlit != null;

        /// <summary>What this torch becomes when lit; itself when it's burning already.</summary>
        public TorchDefinition Lit => IsLit ? this : lit;

        /// <summary>The unlit torch this one was; itself when unlit.</summary>
        public TorchDefinition Unlit => IsLit ? unlit : this;

        /// <summary>The share of a lit stack's burning still to come, 0-1; 1 for an unlit torch.</summary>
        public float Left(ItemStack stack)
        {
            return IsLit ? Mathf.Clamp01(1f - stack.Wear / lifeSeconds) : 1f;
        }

        /// <summary>Makes a pair of torches in code (tests): the unlit one, whose <see cref="Lit"/> is the other.</summary>
        public static TorchDefinition CreatePair(float lifeSeconds = 1200f)
        {
            TorchDefinition unlitTorch = Create<TorchDefinition>("torch", "Torch", 16);
            TorchDefinition litTorch = Create<TorchDefinition>("lit-torch", "Lit torch", 1);
            unlitTorch.lifeSeconds = litTorch.lifeSeconds = lifeSeconds;
            unlitTorch.lit = litTorch;
            litTorch.unlit = unlitTorch;
            return unlitTorch;
        }
    }
}
