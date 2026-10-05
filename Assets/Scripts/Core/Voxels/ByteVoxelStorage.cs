using System;
using System.IO;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Single-byte densities (K28): one byte per sample in a flat array, X fastest, then Y,
    /// then Z, a quarter of <see cref="FlatVoxelStorage"/>'s size. Densities run from 0 to 1
    /// with the surface at the iso level (0.5 by default), so a byte maps them directly:
    /// 0 is empty, 255 solid, and every value is rounded to the nearest 1/255. 0 and 1 stay
    /// exact, which the brush's solid and empty tests (K16) rely on.
    /// </summary>
    public sealed class ByteVoxelStorage : IVoxelStorage, IReadableStorage
    {
        private const float Steps = 255f;

        private readonly byte[] densities;

        /// <param name="sampleCount">Samples per axis; at least 2 (one voxel) on each.</param>
        public ByteVoxelStorage(Vector3Int sampleCount)
        {
            SampleGrid.Validate(sampleCount);
            SampleCount = sampleCount;
            densities = new byte[sampleCount.x * sampleCount.y * sampleCount.z];
        }

        public VoxelStorageType Type => VoxelStorageType.FlatByte;

        public Vector3Int SampleCount { get; }

        public long MemoryBytes => densities.Length;

        public float GetDensity(int x, int y, int z)
        {
            SampleGrid.CheckSample(SampleCount, x, y, z);
            return densities[SampleGrid.FlatIndex(SampleCount, x, y, z)] / Steps;
        }

        public void SetDensity(int x, int y, int z, float density)
        {
            SampleGrid.CheckSample(SampleCount, x, y, z);
            densities[SampleGrid.FlatIndex(SampleCount, x, y, z)] = Quantize(density);
        }

        public void ReadLayer(int z, Span<float> layer)
        {
            SampleGrid.CheckLayer(SampleCount, z);
            int layerSize = SampleCount.x * SampleCount.y;
            int start = z * layerSize;
            for (int i = 0; i < layerSize; i++)
            {
                layer[i] = densities[start + i] / Steps;
            }
        }

        public void WriteData(BinaryWriter writer)
        {
            writer.Write(densities);
        }

        void IReadableStorage.ReadData(BinaryReader reader)
        {
            int read = reader.Read(densities, 0, densities.Length);
            if (read != densities.Length)
            {
                throw new EndOfStreamException($"Expected {densities.Length} density bytes, got {read}.");
            }
        }

        /// <summary>The byte a density is stored as: clamped to 0-1, then to the nearest 1/255.</summary>
        public static byte Quantize(float density)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(density) * Steps);
        }
    }
}
