using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Where the water is, for things moving through it (GL316: swimming, the underwater view): each sample's water fills
    /// the cell round it from the bottom up (<see cref="World.GetWater"/>), so a point is under water when it's below that
    /// fill in its nearest sample's cell.
    /// </summary>
    public static class WaterQuery
    {
        /// <summary>True when a point in the world is under water.</summary>
        public static bool IsUnderwater(World world, Vector3 position)
        {
            if (world == null)
            {
                return false;
            }
            WorldGrid grid = world.Grid;
            Vector3Int sample = grid.WorldToNearestSample(position);
            float amount = world.GetWater(sample);
            if (amount <= 0f)
            {
                return false;
            }
            float bottom = grid.SampleToWorld(sample).y - grid.VoxelSize * 0.5f;
            return position.y < bottom + amount * grid.VoxelSize;
        }

        /// <summary>
        /// How much of an upright body is under water, 0-1, from <paramref name="steps"/> points spread between its feet and
        /// its top.
        /// </summary>
        public static float Submersion(World world, Vector3 feet, float height, int steps = 8)
        {
            if (world == null || steps <= 0)
            {
                return 0f;
            }
            int under = 0;
            for (int i = 0; i < steps; i++)
            {
                if (IsUnderwater(world, feet + Vector3.up * (height * (i + 0.5f) / steps)))
                {
                    under++;
                }
            }
            return under / (float)steps;
        }
    }
}
