using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// What the load screen needs to know of the bounded map before its scene starts (GL762): the scene's world config, so
    /// the water can be worked out while the screen keeps drawing. Loaded from Resources at <see cref="ResourcePath"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Clube/Map Load Settings", fileName = "MapLoadSettings")]
    public sealed class MapLoadSettings : ScriptableObject
    {
        /// <summary>The asset's path under Resources.</summary>
        public const string ResourcePath = "MapLoadSettings";

        [Tooltip("The world config of the bounded map's scene (MapGen).")]
        [SerializeField]
        private WorldConfig world;

        public WorldConfig World => world;
    }
}
