using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A block of voxels backed by an <see cref="IVoxelStorage"/> for densities and
    /// <see cref="VoxelMaterials"/> for material ids (M10). A single voxel is simply a
    /// 1x1x1 chunk (A8).
    /// </summary>
    /// <remarks>
    /// All density edits go through <see cref="SetDensity"/>, which marks the chunk
    /// dirty so whoever owns its mesh knows to rebuild (A7). Lab sliders and the
    /// in-game brush are both just callers of it.
    /// </remarks>
    public sealed class Chunk : IDensityField
    {
        private readonly IVoxelStorage storage;
        private readonly VoxelMaterials materials;

        /// <summary>Creates a chunk with flat-array storage and every density at 0 (empty).</summary>
        public Chunk(Vector3Int voxelCount)
            : this(new FlatVoxelStorage(voxelCount + Vector3Int.one))
        {
        }

        public Chunk(IVoxelStorage storage)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            materials = new VoxelMaterials(storage.SampleCount);
        }

        public Vector3Int VoxelCount => storage.SampleCount - Vector3Int.one;

        public Vector3Int SampleCount => storage.SampleCount;

        /// <summary>
        /// The densities' storage, for inspection (benchmarks, the storage view). Edit through
        /// <see cref="SetDensity"/> (A7), never the storage directly, or the chunk won't rebuild.
        /// </summary>
        public IVoxelStorage Storage => storage;

        /// <summary>Every sample's material id (M10), for inspection. Edit through <see cref="SetMaterial"/>.</summary>
        public VoxelMaterials Materials => materials;

        /// <summary>True when the densities or meshing inputs changed since the last rebuild.</summary>
        public bool IsDirty { get; private set; } = true;

        public float GetDensity(Vector3Int sample)
        {
            return storage.GetDensity(sample.x, sample.y, sample.z);
        }

        /// <summary>Copies one Z layer of samples, X fastest then Y (see <see cref="IVoxelStorage.ReadLayer"/>).</summary>
        public void ReadLayer(int z, Span<float> layer)
        {
            storage.ReadLayer(z, layer);
        }

        /// <summary>The density at a sample, or false outside the chunk.</summary>
        public bool TryGetDensity(Vector3Int sample, out float density)
        {
            Vector3Int count = storage.SampleCount;
            if ((uint)sample.x >= (uint)count.x || (uint)sample.y >= (uint)count.y || (uint)sample.z >= (uint)count.z)
            {
                density = 0f;
                return false;
            }
            density = storage.GetDensity(sample.x, sample.y, sample.z);
            return true;
        }

        /// <summary>The single density-edit path (A7): writes the sample and marks the chunk dirty.</summary>
        public void SetDensity(Vector3Int sample, float density)
        {
            if (storage.GetDensity(sample.x, sample.y, sample.z) == density)
            {
                return;
            }

            storage.SetDensity(sample.x, sample.y, sample.z, density);
            IsDirty = true;
        }

        public byte GetMaterial(Vector3Int sample)
        {
            return materials.Get(sample.x, sample.y, sample.z);
        }

        /// <summary>The material-edit path (M10, A7): writes the sample's material id and marks the chunk dirty.</summary>
        public void SetMaterial(Vector3Int sample, byte material)
        {
            if (materials.Get(sample.x, sample.y, sample.z) == material)
            {
                return;
            }

            materials.Set(sample.x, sample.y, sample.z, material);
            IsDirty = true;
        }

        /// <summary>Requests a rebuild without changing densities, e.g. after the iso level changes.</summary>
        public void MarkDirty()
        {
            IsDirty = true;
        }

        /// <summary>Called by whatever rebuilt the chunk's mesh.</summary>
        public void MarkClean()
        {
            IsDirty = false;
        }
    }
}
