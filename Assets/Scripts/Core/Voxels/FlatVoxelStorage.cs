using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Baseline voxel storage (K23): one float per sample in a flat array,
    /// with X varying fastest, then Y, then Z.
    /// </summary>
    public sealed class FlatVoxelStorage : IVoxelStorage
    {
        private readonly float[] densities;

        /// <param name="sampleCount">Samples per axis; at least 2 (one voxel) on each.</param>
        public FlatVoxelStorage(Vector3Int sampleCount)
        {
            if (sampleCount.x < 2 || sampleCount.y < 2 || sampleCount.z < 2)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sampleCount), sampleCount, "Need at least 2 samples (one voxel) per axis.");
            }

            SampleCount = sampleCount;
            densities = new float[sampleCount.x * sampleCount.y * sampleCount.z];
        }

        public Vector3Int SampleCount { get; }

        public float GetDensity(int x, int y, int z)
        {
            return densities[IndexOf(x, y, z)];
        }

        public void SetDensity(int x, int y, int z, float density)
        {
            densities[IndexOf(x, y, z)] = density;
        }

        public void ReadLayer(int z, Span<float> layer)
        {
            if ((uint)z >= (uint)SampleCount.z)
            {
                throw new ArgumentOutOfRangeException(nameof(z), z, $"Outside the {SampleCount} storage.");
            }

            // Z varies slowest, so a layer is one contiguous block.
            int layerSize = SampleCount.x * SampleCount.y;
            densities.AsSpan(z * layerSize, layerSize).CopyTo(layer);
        }

        // Checked per axis: an out-of-range x or y would otherwise silently
        // land on a neighbouring row instead of throwing.
        private int IndexOf(int x, int y, int z)
        {
            if ((uint)x >= (uint)SampleCount.x || (uint)y >= (uint)SampleCount.y || (uint)z >= (uint)SampleCount.z)
            {
                throw new ArgumentOutOfRangeException(
                    $"Sample ({x}, {y}, {z}) is outside the {SampleCount} storage.");
            }

            return x + SampleCount.x * (y + SampleCount.y * z);
        }
    }
}
