using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>The bounds checks every <see cref="IVoxelStorage"/> makes, so they all fail the same way.</summary>
    internal static class SampleGrid
    {
        /// <summary>Throws unless there are at least 2 samples (one voxel) on every axis.</summary>
        public static void Validate(Vector3Int sampleCount)
        {
            if (sampleCount.x < 2 || sampleCount.y < 2 || sampleCount.z < 2)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sampleCount), sampleCount, "Need at least 2 samples (one voxel) per axis.");
            }
        }

        // Checked per axis: an out-of-range x or y would otherwise silently
        // land on a neighbouring row of a flat array instead of throwing.
        public static void CheckSample(Vector3Int sampleCount, int x, int y, int z)
        {
            if ((uint)x >= (uint)sampleCount.x || (uint)y >= (uint)sampleCount.y || (uint)z >= (uint)sampleCount.z)
            {
                throw new ArgumentOutOfRangeException($"Sample ({x}, {y}, {z}) is outside the {sampleCount} storage.");
            }
        }

        public static void CheckLayer(Vector3Int sampleCount, int z)
        {
            if ((uint)z >= (uint)sampleCount.z)
            {
                throw new ArgumentOutOfRangeException(nameof(z), z, $"Outside the {sampleCount} storage.");
            }
        }

        /// <summary>Index of a sample in a flat array with X varying fastest, then Y, then Z.</summary>
        public static int FlatIndex(Vector3Int sampleCount, int x, int y, int z)
        {
            return x + sampleCount.x * (y + sampleCount.y * z);
        }
    }
}
