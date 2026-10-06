using System;
using Unity.Collections;

namespace Clube.Core
{
    /// <summary>How a generation job writes densities: what the chunk's storage will hold.</summary>
    public enum DensityFormat
    {
        /// <summary>Exact floats, for any storage scheme.</summary>
        Float,

        /// <summary>Single bytes (K28), copied straight into <see cref="ByteVoxelStorage"/> (M12).</summary>
        Byte,
    }

    /// <summary>What a generation job needs besides the terrain shape and the chunk's samples (K35).</summary>
    public struct ChunkFillSettings
    {
        /// <summary>Materials by depth below the surface (M10).</summary>
        public LayerTable Layers;

        /// <summary>The world seed: ore rolls hash with it (O4).</summary>
        public int Seed;

        public DensityFormat Format;

        public ChunkFillSettings(LayerTable layers, int seed, DensityFormat format)
        {
            Layers = layers;
            Seed = seed;
            Format = format;
        }
    }

    /// <summary>
    /// What a generation job learned about the chunk as a whole: whether every sample has the
    /// same density (all air, or all solid underground) and the same material. A uniform chunk
    /// needs no density storage and no mesh (no surface crosses it), which is most of a world.
    /// </summary>
    public struct ChunkFillSummary
    {
        public bool UniformDensity;

        /// <summary>The density every sample has, when <see cref="UniformDensity"/>.</summary>
        public float Density;

        public bool UniformMaterial;

        /// <summary>The material every sample has, when <see cref="UniformMaterial"/>.</summary>
        public byte Material;
    }

    /// <summary>
    /// The buffers one chunk's generation job writes (K35): densities in one
    /// <see cref="DensityFormat"/> (the other array is empty), a material id per sample, and the
    /// <see cref="ChunkFillSummary"/>. Arrays run X fastest, then Y, then Z. When the summary
    /// says the densities are uniform the arrays may not have been written at all.
    /// </summary>
    public struct ChunkFillOutput : IDisposable
    {
        public NativeArray<float> Densities;
        public NativeArray<byte> DensityBytes;
        public NativeArray<byte> Materials;

        /// <summary>One element.</summary>
        public NativeArray<ChunkFillSummary> Summary;

        public static ChunkFillOutput Allocate(int length, DensityFormat format, Allocator allocator)
        {
            return new ChunkFillOutput
            {
                Densities = new NativeArray<float>(format == DensityFormat.Float ? length : 0, allocator, NativeArrayOptions.UninitializedMemory),
                DensityBytes = new NativeArray<byte>(format == DensityFormat.Byte ? length : 0, allocator, NativeArrayOptions.UninitializedMemory),
                Materials = new NativeArray<byte>(length, allocator, NativeArrayOptions.UninitializedMemory),
                Summary = new NativeArray<ChunkFillSummary>(1, allocator),
            };
        }

        public bool IsCreated => Materials.IsCreated;

        public DensityFormat Format => DensityBytes.Length > 0 ? DensityFormat.Byte : DensityFormat.Float;

        public int Length => Materials.Length;

        public void Dispose()
        {
            if (Densities.IsCreated)
            {
                Densities.Dispose();
            }
            if (DensityBytes.IsCreated)
            {
                DensityBytes.Dispose();
            }
            if (Materials.IsCreated)
            {
                Materials.Dispose();
            }
            if (Summary.IsCreated)
            {
                Summary.Dispose();
            }
        }
    }
}
