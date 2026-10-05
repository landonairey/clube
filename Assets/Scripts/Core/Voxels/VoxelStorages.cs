using System;
using System.IO;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>The voxel storage schemes a chunk can use (A12, 2G).</summary>
    public enum VoxelStorageType
    {
        /// <summary>One float per sample in a flat array (K23).</summary>
        Flat,

        /// <summary>One byte per sample in a flat array, densities rounded to 1/255 (K28).</summary>
        FlatByte,

        /// <summary>Runs of equal densities along X (K24).</summary>
        RunLengthX,

        /// <summary>Runs of equal densities along Y, up and down columns (K24).</summary>
        RunLengthY,

        /// <summary>Runs of equal densities along Z (K24).</summary>
        RunLengthZ,

        /// <summary>A sparse octree whose uniform regions collapse into single nodes (K25).</summary>
        Octree,
    }

    /// <summary>
    /// Makes, writes and reads voxel storages of any <see cref="VoxelStorageType"/>, so chunks,
    /// benchmarks and saving don't need to know the concrete classes.
    /// </summary>
    public static class VoxelStorages
    {
        /// <summary>The octree depth used when none is given.</summary>
        public const int DefaultOctreeMaxDepth = 4;

        /// <summary>A new, all-empty storage of the given scheme.</summary>
        /// <param name="octreeMaxDepth">How deep the octree may divide (K25); ignored by other schemes.</param>
        public static IVoxelStorage Create(VoxelStorageType type, Vector3Int sampleCount, int octreeMaxDepth = DefaultOctreeMaxDepth)
        {
            switch (type)
            {
                case VoxelStorageType.Flat:
                    return new FlatVoxelStorage(sampleCount);
                case VoxelStorageType.FlatByte:
                    return new ByteVoxelStorage(sampleCount);
                case VoxelStorageType.RunLengthX:
                    return new RunLengthVoxelStorage(sampleCount, 0);
                case VoxelStorageType.RunLengthY:
                    return new RunLengthVoxelStorage(sampleCount, 1);
                case VoxelStorageType.RunLengthZ:
                    return new RunLengthVoxelStorage(sampleCount, 2);
                case VoxelStorageType.Octree:
                    return new OctreeVoxelStorage(sampleCount, octreeMaxDepth);
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        /// <summary>Writes the type, sample count and the storage's own data.</summary>
        public static void Write(IVoxelStorage storage, BinaryWriter writer)
        {
            writer.Write((byte)storage.Type);
            writer.Write(storage.SampleCount.x);
            writer.Write(storage.SampleCount.y);
            writer.Write(storage.SampleCount.z);
            if (storage is OctreeVoxelStorage octree)
            {
                writer.Write((byte)octree.MaxDepth);
            }
            storage.WriteData(writer);
        }

        /// <summary>Reads a storage written by <see cref="Write"/>.</summary>
        public static IVoxelStorage Read(BinaryReader reader)
        {
            var type = (VoxelStorageType)reader.ReadByte();
            var sampleCount = new Vector3Int(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
            int depth = type == VoxelStorageType.Octree ? reader.ReadByte() : DefaultOctreeMaxDepth;
            IVoxelStorage storage = Create(type, sampleCount, depth);
            ((IReadableStorage)storage).ReadData(reader);
            return storage;
        }

        /// <summary>The serialized size in bytes (K26), by writing it to a counting stream.</summary>
        public static long SerializedBytes(IVoxelStorage storage)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                Write(storage, writer);
                writer.Flush();
                return stream.Length;
            }
        }
    }

    /// <summary>Fills a freshly made storage from what its <see cref="IVoxelStorage.WriteData"/> wrote.</summary>
    internal interface IReadableStorage
    {
        void ReadData(BinaryReader reader);
    }
}
