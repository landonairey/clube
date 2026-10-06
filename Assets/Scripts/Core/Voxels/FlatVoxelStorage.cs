using System;
using System.IO;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Baseline voxel storage (K23): one float per sample in a flat array,
    /// with X varying fastest, then Y, then Z.
    /// </summary>
    public sealed class FlatVoxelStorage : IVoxelStorage, IReadableStorage
    {
        private readonly float[] densities;

        /// <param name="sampleCount">Samples per axis; at least 2 (one voxel) on each.</param>
        public FlatVoxelStorage(Vector3Int sampleCount)
        {
            SampleGrid.Validate(sampleCount);
            SampleCount = sampleCount;
            densities = new float[sampleCount.x * sampleCount.y * sampleCount.z];
        }

        public VoxelStorageType Type => VoxelStorageType.Flat;

        public Vector3Int SampleCount { get; }

        public long MemoryBytes => densities.Length * sizeof(float);

        public float GetDensity(int x, int y, int z)
        {
            SampleGrid.CheckSample(SampleCount, x, y, z);
            return densities[SampleGrid.FlatIndex(SampleCount, x, y, z)];
        }

        public void SetDensity(int x, int y, int z, float density)
        {
            SampleGrid.CheckSample(SampleCount, x, y, z);
            densities[SampleGrid.FlatIndex(SampleCount, x, y, z)] = density;
        }

        public void ReadLayer(int z, Span<float> layer)
        {
            SampleGrid.CheckLayer(SampleCount, z);

            // Z varies slowest, so a layer is one contiguous block.
            int layerSize = SampleCount.x * SampleCount.y;
            densities.AsSpan(z * layerSize, layerSize).CopyTo(layer);
        }

        public void WriteLayer(int z, ReadOnlySpan<float> layer)
        {
            SampleGrid.CheckLayer(SampleCount, z);
            int layerSize = SampleCount.x * SampleCount.y;
            layer.Slice(0, layerSize).CopyTo(densities.AsSpan(z * layerSize, layerSize));
        }

        public void WriteData(BinaryWriter writer)
        {
            foreach (float density in densities)
            {
                writer.Write(density);
            }
        }

        void IReadableStorage.ReadData(BinaryReader reader)
        {
            for (int i = 0; i < densities.Length; i++)
            {
                densities[i] = reader.ReadSingle();
            }
        }
    }
}
