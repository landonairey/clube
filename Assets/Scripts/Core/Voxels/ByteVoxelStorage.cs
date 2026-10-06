using System;
using System.IO;
using Unity.Collections;
using Unity.Mathematics;
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
    /// <remarks>
    /// The world's storage (M12), so it also copies its bytes to and from native arrays in one
    /// block: how generation results arrive and mesh jobs read them (K35).
    /// </remarks>
    public sealed class ByteVoxelStorage : IVoxelStorage, IReadableStorage, IPooledStorage
    {
        private const float Steps = 255f;

        private byte[] densities;

        /// <param name="sampleCount">Samples per axis; at least 2 (one voxel) on each.</param>
        public ByteVoxelStorage(Vector3Int sampleCount)
        {
            SampleGrid.Validate(sampleCount);
            SampleCount = sampleCount;
            densities = VoxelArrayPool.Rent(sampleCount.x * sampleCount.y * sampleCount.z);
        }

        public VoxelStorageType Type => VoxelStorageType.FlatByte;

        public Vector3Int SampleCount { get; }

        public long MemoryBytes => densities.Length;

        public float GetDensity(int x, int y, int z)
        {
            SampleGrid.CheckSample(SampleCount, x, y, z);
            return ToDensity(densities[SampleGrid.FlatIndex(SampleCount, x, y, z)]);
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
                layer[i] = ToDensity(densities[start + i]);
            }
        }

        public void WriteLayer(int z, ReadOnlySpan<float> layer)
        {
            SampleGrid.CheckLayer(SampleCount, z);
            int layerSize = SampleCount.x * SampleCount.y;
            int start = z * layerSize;
            for (int i = 0; i < layerSize; i++)
            {
                densities[start + i] = Quantize(layer[i]);
            }
        }

        /// <summary>Overwrites every sample from bytes already quantized (a generation job's output).</summary>
        public void CopyFrom(NativeArray<byte> bytes)
        {
            bytes.CopyTo(densities);
        }

        /// <summary>Copies every sample's byte into a native array of the same length (a mesh job's input).</summary>
        public void CopyTo(NativeArray<byte> bytes)
        {
            bytes.CopyFrom(densities);
        }

        /// <summary>Sets every sample to one density (a uniform chunk turning into a stored one).</summary>
        public void Fill(float density)
        {
            Array.Fill(densities, Quantize(density));
        }

        /// <summary>Gives the array back to <see cref="VoxelArrayPool"/>; the storage can't be used afterwards.</summary>
        public void Release()
        {
            VoxelArrayPool.Return(densities);
            densities = null;
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

        /// <summary>The byte a density is stored as: clamped to 0-1, then to the nearest 1/255 (ties to even).</summary>
        /// <remarks>Burst-compatible: generation jobs quantize with it too.</remarks>
        public static byte Quantize(float density)
        {
            return (byte)math.round(math.saturate(density) * Steps);
        }

        /// <summary>The density a stored byte stands for.</summary>
        public static float ToDensity(byte value)
        {
            return value / Steps;
        }
    }
}
