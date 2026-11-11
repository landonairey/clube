using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// An item that becomes an object in the world when placed (GL23, first pass of PK17): a
    /// furnace, an anvil, a merchant table. Placing snaps it to the build grid and spends the
    /// item; picking the object back up gives the item back.
    /// </summary>
    [CreateAssetMenu(fileName = "Placeable", menuName = "Clube/Placeable")]
    public class PlaceableDefinition : ItemDefinition
    {
        [Tooltip("What placing it puts in the world. Its origin is the centre of its base.")]
        [SerializeField]
        private GameObject prefab;

        public GameObject Prefab => prefab;

        [Tooltip("Crafting it leaves out what its inputs were made of (GL563): a crate carries no \"0.1% leaves\". Off for furnaces, whose quality is their clay's mix.")]
        [SerializeField]
        private bool dropsMix;

        /// <summary>True when what its inputs were made of means nothing in it and is not passed on (GL563).</summary>
        public bool DropsMix => dropsMix;

        /// <summary>Sets <see cref="DropsMix"/>, for items made in code (tests).</summary>
        public PlaceableDefinition WithDropsMix(bool drops = true)
        {
            dropsMix = drops;
            return this;
        }

        /// <summary>Makes a placeable in code (tests); the game's are assets.</summary>
        public static PlaceableDefinition Create(string id, string displayName)
        {
            return Create<PlaceableDefinition>(id, displayName, 1);
        }
    }
}
