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
    }
}
