using System;
using System.IO;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Density samples on the corners of a chunk's voxel grid (A12). Meshing, editing and
    /// saving only talk to this interface, so storage schemes (flat array, single-byte,
    /// run-length, octree; K23-K28) can be swapped and compared. Material IDs join it in M10.
    /// </summary>
    /// <remarks>
    /// Densities run from 0 (empty) to 1 (solid). Every scheme returns exactly what was
    /// written, except <see cref="ByteVoxelStorage"/>, which rounds to 1/255.
    /// </remarks>
    public interface IVoxelStorage
    {
        /// <summary>Which scheme this is, for the factory and serialization (<see cref="VoxelStorages"/>).</summary>
        VoxelStorageType Type { get; }

        /// <summary>Number of samples along each axis. A grid of N voxels has N + 1 samples.</summary>
        Vector3Int SampleCount { get; }

        /// <summary>Approximate managed memory the storage holds, in bytes (K26).</summary>
        long MemoryBytes { get; }

        float GetDensity(int x, int y, int z);

        void SetDensity(int x, int y, int z, float density);

        /// <summary>
        /// Copies every sample with the given <paramref name="z"/> into <paramref name="layer"/>,
        /// X varying fastest, then Y: <c>SampleCount.x * SampleCount.y</c> values. The mesher's
        /// bulk read (K32): one call per layer instead of one per sample.
        /// </summary>
        void ReadLayer(int z, Span<float> layer);

        /// <summary>
        /// Writes the densities in the scheme's own compact form (not the sample count or type;
        /// <see cref="VoxelStorages.Write"/> adds those). The matching reader is <see cref="VoxelStorages.Read"/>.
        /// </summary>
        void WriteData(BinaryWriter writer);
    }
}
