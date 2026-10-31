using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>The three sizes of bush (GL236).</summary>
    public enum BushSize
    {
        /// <summary>A low bulb, a little taller than the grass.</summary>
        Small,

        /// <summary>About the size of a boulder.</summary>
        Medium,

        /// <summary>As tall as the player.</summary>
        Large,
    }

    /// <summary>
    /// Where bushes grow and how big they are (GL236): at most one per cell of a grid across the land, more of them
    /// in the forests than out in the fields, none in a wheat field. Each is a small bulb, a boulder-sized bush or one
    /// as tall as the player, rolled from the seed and the cell, and drops leaves and a stick or two when picked or
    /// broken. Bushes are drawn meshes, not voxels (<see cref="BushScatter"/>).
    /// </summary>
    [Serializable]
    public class BushGrowth
    {
        // Keeps the bushes' rolls apart from the grass's and the trees'.
        private const int BushSalt = 0x42555348;

        [Tooltip("Width of a cell (m): at most one bush stands in each.")]
        [SerializeField, Min(0.5f)]
        private float cellSize = 3f;

        [Tooltip("Chance of a bush in a cell out in open field.")]
        [SerializeField, Range(0f, 1f)]
        private float fieldChance = 0.08f;

        [Tooltip("Chance of a bush in a cell deep in a forest.")]
        [SerializeField, Range(0f, 1f)]
        private float forestChance = 0.22f;

        [Tooltip("Shares of small, medium and large bushes (they needn't add up to 1).")]
        [SerializeField]
        private Vector3 sizeShares = new Vector3(0.5f, 0.35f, 0.15f);

        [Tooltip("Small bushes' height (m), smallest and largest.")]
        [SerializeField]
        private Vector2 smallHeight = new Vector2(0.45f, 0.65f);

        [Tooltip("Medium bushes' height (m), smallest and largest.")]
        [SerializeField]
        private Vector2 mediumHeight = new Vector2(0.8f, 1.1f);

        [Tooltip("Large bushes' height (m), smallest and largest.")]
        [SerializeField]
        private Vector2 largeHeight = new Vector2(1.6f, 1.9f);

        [Tooltip("Leaves a small, medium and large bush drops.")]
        [SerializeField]
        private Vector3Int leaves = new Vector3Int(3, 6, 12);

        public float CellSize
        {
            get => cellSize;
            set => cellSize = Mathf.Max(0.5f, value);
        }

        public float FieldChance
        {
            get => fieldChance;
            set => fieldChance = Mathf.Clamp01(value);
        }

        public float ForestChance
        {
            get => forestChance;
            set => forestChance = Mathf.Clamp01(value);
        }

        public Vector3 SizeShares
        {
            get => sizeShares;
            set => sizeShares = Vector3.Max(Vector3.zero, value);
        }

        /// <summary>Whether the cell grows a bush where the forest is <paramref name="forest"/> (0-1), and how big.</summary>
        public bool TryGrow(int seed, Vector2Int cell, float forest, out BushSize size, out float height)
        {
            size = BushSize.Small;
            height = 0f;
            float chance = Mathf.Lerp(fieldChance, forestChance, Mathf.Clamp01(forest));
            if (VoxelHash.Uniform(seed, cell.x, 0, cell.y, BushSalt) >= chance)
            {
                return false;
            }
            float total = sizeShares.x + sizeShares.y + sizeShares.z;
            if (total <= 0f)
            {
                return false;
            }
            float pick = VoxelHash.Uniform(seed, cell.x, 1, cell.y, BushSalt) * total;
            size = pick < sizeShares.x ? BushSize.Small : pick < sizeShares.x + sizeShares.y ? BushSize.Medium : BushSize.Large;
            Vector2 range = HeightRange(size);
            height = Mathf.Lerp(range.x, range.y, VoxelHash.Uniform(seed, cell.x, 2, cell.y, BushSalt));
            return true;
        }

        /// <summary>
        /// Where in its cell the bush stands (<see cref="GrassyGround"/> rolls it): these salts, kept this share
        /// of the cell off its edges, so neighbouring bushes don't grow into each other.
        /// </summary>
        public const int SpotSaltX = BushSalt + 3;
        public const int SpotSaltZ = BushSalt + 4;
        public const float SpotMargin = 0.2f;

        /// <summary>The bush's turn about its up axis, in degrees.</summary>
        public float YawOf(int seed, Vector2Int cell)
        {
            return VoxelHash.Uniform(seed, cell.x, 5, cell.y, BushSalt) * 360f;
        }

        /// <summary>The heights a bush of that size grows to, smallest and largest.</summary>
        public Vector2 HeightRange(BushSize size)
        {
            switch (size)
            {
                case BushSize.Small:
                    return smallHeight;
                case BushSize.Medium:
                    return mediumHeight;
                default:
                    return largeHeight;
            }
        }

        /// <summary>What a bush drops when picked or broken: leaves by its size, and a stick or two.</summary>
        public void DropsOf(int seed, Vector2Int cell, BushSize size, out int leafCount, out int stickCount)
        {
            switch (size)
            {
                case BushSize.Small:
                    leafCount = leaves.x;
                    stickCount = 1;
                    break;
                case BushSize.Medium:
                    leafCount = leaves.y;
                    stickCount = VoxelHash.Uniform(seed, cell.x, 6, cell.y, BushSalt) < 0.5f ? 1 : 2;
                    break;
                default:
                    leafCount = leaves.z;
                    stickCount = 2;
                    break;
            }
        }
    }
}
