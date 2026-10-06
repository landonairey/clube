using System;
using Unity.Collections;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A block of voxels backed by an <see cref="IVoxelStorage"/> for densities and
    /// <see cref="VoxelMaterials"/> for material ids (M10). A single voxel is simply a
    /// 1x1x1 chunk (A8).
    /// </summary>
    /// <remarks>
    /// <para>All density edits go through <see cref="SetDensity"/>, which marks the chunk
    /// dirty so whoever owns its mesh knows to rebuild (A7). Lab sliders and the
    /// in-game brush are both just callers of it.</para>
    /// <para>A world chunk can be <b>uniform</b> (<see cref="Uniform"/>): every sample holds the
    /// same density, as all-air chunks above the ground and all-solid ones deep below it do,
    /// which is most of a streamed world. It holds no density storage and has no surface to
    /// mesh until an edit changes a sample; then its storage is made from the factory it was
    /// given and filled with the uniform value (K35).</para>
    /// </remarks>
    public sealed class Chunk : IDensityField
    {
        private readonly VoxelMaterials materials;
        private readonly Func<Vector3Int, IVoxelStorage> createStorage;

        // Null while the chunk is uniform.
        private IVoxelStorage storage;
        private float uniformDensity;

        /// <summary>Creates a chunk with flat-array storage and every density at 0 (empty).</summary>
        public Chunk(Vector3Int voxelCount)
            : this(new FlatVoxelStorage(voxelCount + Vector3Int.one))
        {
        }

        public Chunk(IVoxelStorage storage)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            SampleCount = storage.SampleCount;
            materials = new VoxelMaterials(SampleCount);
        }

        private Chunk(Vector3Int sampleCount, float density, byte material, Func<Vector3Int, IVoxelStorage> createStorage)
        {
            SampleCount = sampleCount;
            uniformDensity = density;
            materials = new VoxelMaterials(sampleCount, material);
            this.createStorage = createStorage ?? throw new ArgumentNullException(nameof(createStorage));
        }

        /// <summary>
        /// A chunk whose samples all hold <paramref name="density"/> and <paramref name="material"/>,
        /// with no storage until an edit needs one: then <paramref name="createStorage"/> makes it.
        /// </summary>
        public static Chunk Uniform(Vector3Int sampleCount, float density, byte material, Func<Vector3Int, IVoxelStorage> createStorage)
        {
            return new Chunk(sampleCount, density, material, createStorage);
        }

        public Vector3Int VoxelCount => SampleCount - Vector3Int.one;

        public Vector3Int SampleCount { get; }

        /// <summary>True while every sample holds <see cref="UniformDensity"/> and no density storage exists.</summary>
        public bool IsUniform => storage == null;

        /// <summary>The density every sample holds while <see cref="IsUniform"/>.</summary>
        public float UniformDensity => uniformDensity;

        /// <summary>
        /// The densities' storage, for inspection (benchmarks, the storage view). A uniform chunk
        /// makes its storage on first access. Edit through <see cref="SetDensity"/> (A7), never
        /// the storage directly, or the chunk won't rebuild.
        /// </summary>
        public IVoxelStorage Storage
        {
            get
            {
                EnsureStorage();
                return storage;
            }
        }

        /// <summary>Bytes held for densities: 0 while uniform.</summary>
        public long DensityMemoryBytes => storage?.MemoryBytes ?? 0;

        /// <summary>True when the densities are single bytes (M12), which mesh jobs read without converting.</summary>
        public bool StoresBytes => storage is ByteVoxelStorage;

        /// <summary>Every sample's material id (M10), for inspection. Edit through <see cref="SetMaterial"/>.</summary>
        public VoxelMaterials Materials => materials;

        /// <summary>True when the densities or meshing inputs changed since the last rebuild.</summary>
        public bool IsDirty { get; private set; } = true;

        public float GetDensity(Vector3Int sample)
        {
            return storage != null ? storage.GetDensity(sample.x, sample.y, sample.z) : uniformDensity;
        }

        /// <summary>Copies one Z layer of samples, X fastest then Y (see <see cref="IVoxelStorage.ReadLayer"/>).</summary>
        public void ReadLayer(int z, Span<float> layer)
        {
            if (storage != null)
            {
                storage.ReadLayer(z, layer);
                return;
            }
            SampleGrid.CheckLayer(SampleCount, z);
            layer.Slice(0, SampleCount.x * SampleCount.y).Fill(uniformDensity);
        }

        /// <summary>The density at a sample, or false outside the chunk.</summary>
        public bool TryGetDensity(Vector3Int sample, out float density)
        {
            Vector3Int count = SampleCount;
            if ((uint)sample.x >= (uint)count.x || (uint)sample.y >= (uint)count.y || (uint)sample.z >= (uint)count.z)
            {
                density = 0f;
                return false;
            }
            density = GetDensity(sample);
            return true;
        }

        /// <summary>The single density-edit path (A7): writes the sample and marks the chunk dirty.</summary>
        public void SetDensity(Vector3Int sample, float density)
        {
            if (GetDensity(sample) == density)
            {
                return;
            }

            EnsureStorage();
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

        /// <summary>
        /// Overwrites every sample with what a generation job wrote (K35): a chunk made by
        /// <see cref="Uniform"/> stays (or becomes) storage-free when the summary says the
        /// densities are uniform; otherwise the densities are copied in one block when the
        /// storage holds bytes, a layer at a time when it doesn't. Marks the chunk dirty.
        /// </summary>
        public void Load(in ChunkFillOutput output, ChunkFillSummary summary)
        {
            if (summary.UniformDensity)
            {
                LoadUniformDensity(summary.Density);
            }
            else
            {
                // Every sample is overwritten next, so a new storage needn't be filled first.
                storage ??= createStorage(SampleCount);
                if (output.Format == DensityFormat.Byte && storage is ByteVoxelStorage bytes)
                {
                    bytes.CopyFrom(output.DensityBytes);
                }
                else
                {
                    WriteLayers(output);
                }
            }

            if (summary.UniformMaterial)
            {
                materials.SetUniform(summary.Material);
            }
            else
            {
                materials.CopyFrom(output.Materials);
            }
            IsDirty = true;
        }

        /// <summary>Copies every density's byte into a mesh job's input. Needs <see cref="StoresBytes"/>.</summary>
        public void CopyDensities(NativeArray<byte> into)
        {
            if (!(storage is ByteVoxelStorage bytes))
            {
                throw new InvalidOperationException("The chunk doesn't store single-byte densities.");
            }
            bytes.CopyTo(into);
        }

        /// <summary>Copies every density into a mesh job's input, a Z layer at a time, from any storage.</summary>
        public void CopyDensities(NativeArray<float> into)
        {
            int layerSize = SampleCount.x * SampleCount.y;
            Span<float> all = into.AsSpan();
            for (int z = 0; z < SampleCount.z; z++)
            {
                ReadLayer(z, all.Slice(z * layerSize, layerSize));
            }
        }

        /// <summary>
        /// Gives the chunk's memory back to <see cref="VoxelArrayPool"/> (P2): the world calls this
        /// when it drops a chunk for good. The chunk can't be used afterwards.
        /// </summary>
        public void Release()
        {
            (storage as IPooledStorage)?.Release();
            storage = null;
            uniformDensity = 0f;
            materials.SetUniform(0);
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

        // A uniform chunk keeps no storage; a chunk given its storage (the labs) can't drop it.
        private void LoadUniformDensity(float density)
        {
            if (createStorage != null)
            {
                storage = null;
                uniformDensity = density;
                return;
            }

            int layerSize = SampleCount.x * SampleCount.y;
            var layer = new float[layerSize];
            Array.Fill(layer, density);
            for (int z = 0; z < SampleCount.z; z++)
            {
                storage.WriteLayer(z, layer);
            }
        }

        private void WriteLayers(in ChunkFillOutput output)
        {
            int layerSize = SampleCount.x * SampleCount.y;
            if (output.Format == DensityFormat.Float)
            {
                ReadOnlySpan<float> densities = output.Densities.AsReadOnlySpan();
                for (int z = 0; z < SampleCount.z; z++)
                {
                    storage.WriteLayer(z, densities.Slice(z * layerSize, layerSize));
                }
                return;
            }

            var layer = new float[layerSize];
            for (int z = 0; z < SampleCount.z; z++)
            {
                for (int i = 0; i < layerSize; i++)
                {
                    layer[i] = ByteVoxelStorage.ToDensity(output.DensityBytes[z * layerSize + i]);
                }
                storage.WriteLayer(z, layer);
            }
        }

        // Turns a uniform chunk into a stored one, every sample keeping the uniform density.
        private void EnsureStorage()
        {
            if (storage != null)
            {
                return;
            }

            storage = createStorage(SampleCount);
            if (uniformDensity != 0f && storage is ByteVoxelStorage bytes)
            {
                bytes.Fill(uniformDensity);
            }
            else if (uniformDensity != 0f)
            {
                int layerSize = SampleCount.x * SampleCount.y;
                var layer = new float[layerSize];
                Array.Fill(layer, uniformDensity);
                for (int z = 0; z < SampleCount.z; z++)
                {
                    storage.WriteLayer(z, layer);
                }
            }
        }
    }
}
