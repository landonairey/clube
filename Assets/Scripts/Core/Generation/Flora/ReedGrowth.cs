using System;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Where reeds grow (GL723): round the shores of lakes and rivers and out into their shallows, never by the sea. Like the
    /// forests (<see cref="TreeForests"/>), a wide patch noise thickens them into reed beds along some stretches of a bank
    /// and leaves others bare. One clump per bush cell at most (the same grid as <see cref="BushGrowth"/>, so a cell holds a
    /// bush or reeds, and a picked one stays gone in the same save list); picked, a clump gives reeds, which a worktop
    /// strips into fibres and twists into twine.
    /// </summary>
    [Serializable]
    public class ReedGrowth
    {
        // Keeps the reeds' rolls apart from the bushes', the grass's and the trees'.
        private const int ReedSalt = 0x52454544;

        [Tooltip("Furthest from the water's edge reeds grow on the bank (m).")]
        [SerializeField, Range(0f, 8f)]
        private float shoreReach = 2.5f;

        [Tooltip("Deepest water reeds stand in (m).")]
        [SerializeField, Range(0f, 2f)]
        private float maxDepth = 0.7f;

        [Tooltip("Chance of a clump in a cell at the water's edge in a thick reed bed; it falls off away from the edge and in thin stretches.")]
        [SerializeField, Range(0f, 1f)]
        private float bedChance = 0.9f;

        [Tooltip("Chance of a clump in a cell at the water's edge where the bank has no reed bed.")]
        [SerializeField, Range(0f, 1f)]
        private float thinChance = 0.04f;

        [Tooltip("Typical length of a reed bed along a bank, and of the bare stretches between (m).")]
        [SerializeField, Min(4f)]
        private float bedScale = 30f;

        [Tooltip("Share of the banks that are reed beds.")]
        [SerializeField, Range(0f, 1f)]
        private float bedCover = 0.45f;

        [Tooltip("Reeds' height (m), smallest and largest.")]
        [SerializeField]
        private Vector2 height = new Vector2(1.5f, 2.4f);

        [Tooltip("Fewest and most reeds a clump gives when picked.")]
        [SerializeField]
        private Vector2Int reeds = new Vector2Int(3, 6);

        public float ShoreReach
        {
            get => shoreReach;
            set => shoreReach = Mathf.Clamp(value, 0f, 8f);
        }

        public float MaxDepth
        {
            get => maxDepth;
            set => maxDepth = Mathf.Clamp(value, 0f, 2f);
        }

        public float BedCover
        {
            get => bedCover;
            set => bedCover = Mathf.Clamp01(value);
        }

        public Vector2 Height => new Vector2(Mathf.Min(height.x, height.y), Mathf.Max(height.x, height.y));

        /// <summary>
        /// How fit the place at (x, z) is for reeds, 0-1 (before the reed beds' noise): 1 at a lake's or river's edge and in its
        /// shallows, falling to 0 at <see cref="ShoreReach"/> metres up the bank or <see cref="MaxDepth"/> out; 0 within reach
        /// of the sea, anywhere off the water map, or with no fresh water near.
        /// </summary>
        public float ShoreAt(WaterMap water, float x, float z)
        {
            if (!water.IsCreated || !water.TryCell(new float2(x, z), out int cell))
            {
                return 0f;
            }
            float depth = water.Depth[cell];
            float fit;
            if (depth > 0f)
            {
                if (water.IsSea(cell) || depth > maxDepth)
                {
                    return 0f;
                }
                fit = 1f - Mathf.InverseLerp(maxDepth * 0.6f, maxDepth, depth);
            }
            else
            {
                float distance = Mathf.Min(water.LakeDistance[cell], water.RiverDistance[cell]);
                if (distance > shoreReach)
                {
                    return 0f;
                }
                fit = 1f - Mathf.InverseLerp(0f, shoreReach, distance - 1f);
            }
            // Fresh water only: the sea's salt reaches past its own shore.
            return fit > 0f && !NearSea(water, cell) ? fit : 0f;
        }

        // Any sea cell within the shore's reach (plus a metre) of the cell.
        private bool NearSea(WaterMap water, int cell)
        {
            if (!water.HasSea)
            {
                return false;
            }
            int reach = Mathf.CeilToInt(shoreReach + 1f);
            int cx = cell % water.Size.x, cz = cell / water.Size.x;
            for (int dz = -reach; dz <= reach; dz++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    int x = cx + dx, z = cz + dz;
                    if (x >= 0 && z >= 0 && x < water.Size.x && z < water.Size.y && water.IsSea(x + z * water.Size.x))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>How thick the reed beds are along the banks at (x, z), 0 (bare) to 1 (a bed), from a wide patch noise.</summary>
        public float BedAt(int seed, float x, float z)
        {
            float noise = ValueNoise.At(seed, x / bedScale, z / bedScale, 0, ReedSalt);
            float threshold = 1f - bedCover;
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(threshold - 0.08f, threshold + 0.08f, noise));
        }

        /// <summary>Whether the bush-grid cell at (x, z) grows reeds, and how tall.</summary>
        public bool TryGrow(int seed, Vector2Int cell, WaterMap water, float x, float z, out float tall)
        {
            tall = 0f;
            float shore = ShoreAt(water, x, z);
            if (shore <= 0f)
            {
                return false;
            }
            float chance = Mathf.Lerp(thinChance, bedChance, BedAt(seed, x, z)) * shore;
            if (VoxelHash.Uniform(seed, cell.x, 0, cell.y, ReedSalt) >= chance)
            {
                return false;
            }
            Vector2 range = Height;
            tall = Mathf.Lerp(range.x, range.y, VoxelHash.Uniform(seed, cell.x, 1, cell.y, ReedSalt));
            return true;
        }

        /// <summary>The clump's turn about its up axis, in degrees.</summary>
        public float YawOf(int seed, Vector2Int cell)
        {
            return VoxelHash.Uniform(seed, cell.x, 2, cell.y, ReedSalt) * 360f;
        }

        /// <summary>How many reeds a clump gives when picked or broken.</summary>
        public int ReedsOf(int seed, Vector2Int cell)
        {
            int fewest = Mathf.Min(reeds.x, reeds.y), most = Mathf.Max(reeds.x, reeds.y);
            return fewest + Mathf.Min(most - fewest, (int)(VoxelHash.Uniform(seed, cell.x, 3, cell.y, ReedSalt) * (most - fewest + 1)));
        }
    }
}
