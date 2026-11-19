using Unity.Collections;
using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// <see cref="TreeCover"/> for Burst jobs (GL775): the tree roots of a block of tree cells, filled on the main thread by
    /// <see cref="TreeCover.Fill"/>, and the same cover worked out from them. <c>TreeCoverGridTests</c> checks the two agree.
    /// </summary>
    public struct TreeCoverGrid
    {
        /// <summary>The block's first tree cell, x and z.</summary>
        public int FromX;
        public int FromZ;

        /// <summary>Cells across the block, x and z.</summary>
        public int Width;
        public int Depth;

        public float Cell;
        public float Radius;
        public float Dense;

        /// <summary>False when the world has no trees: the cover is 0 everywhere.</summary>
        public bool HasTrees;

        /// <summary>
        /// The cover at (x, z) in the world's own space, 0-1, from <paramref name="roots"/>: per cell of the block (x fastest),
        /// the root's x and z and 1 when a tree grows there. A cell outside the block counts as treeless.
        /// </summary>
        public float At(in NativeArray<float3> roots, float x, float z)
        {
            if (!HasTrees)
            {
                return 0f;
            }
            int fromX = (int)math.floor((x - Radius) / Cell), toX = (int)math.floor((x + Radius) / Cell);
            int fromZ = (int)math.floor((z - Radius) / Cell), toZ = (int)math.floor((z + Radius) / Cell);
            float sum = 0f;
            for (int cz = fromZ; cz <= toZ; cz++)
            {
                for (int cx = fromX; cx <= toX; cx++)
                {
                    int gx = cx - FromX, gz = cz - FromZ;
                    if ((uint)gx >= (uint)Width || (uint)gz >= (uint)Depth)
                    {
                        continue;
                    }
                    float3 root = roots[gx + gz * Width];
                    if (root.z <= 0f)
                    {
                        continue;
                    }
                    float dx = root.x - x, dz = root.y - z;
                    float distance = (float)System.Math.Sqrt(dx * dx + dz * dz);
                    if (distance < Radius)
                    {
                        sum += 1f - distance / Radius;
                    }
                }
            }
            return math.saturate(sum / Dense);
        }
    }
}
