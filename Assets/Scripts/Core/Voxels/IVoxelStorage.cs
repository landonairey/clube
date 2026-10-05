using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Density samples on the corners of a chunk's voxel grid (A12). Meshing and
    /// editing only talk to this interface, so storage schemes (flat array, RLE,
    /// octree) can be swapped and compared. Material IDs, iteration and
    /// serialization join the interface when a chapter needs them.
    /// </summary>
    public interface IVoxelStorage
    {
        /// <summary>Number of samples along each axis. A grid of N voxels has N + 1 samples.</summary>
        Vector3Int SampleCount { get; }

        float GetDensity(int x, int y, int z);

        void SetDensity(int x, int y, int z, float density);

        /// <summary>
        /// Copies every sample with the given <paramref name="z"/> into <paramref name="layer"/>,
        /// X varying fastest, then Y: <c>SampleCount.x * SampleCount.y</c> values. The mesher's
        /// bulk read (K32): one call per layer instead of one per sample.
        /// </summary>
        void ReadLayer(int z, Span<float> layer);
    }
}
