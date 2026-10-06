using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Core
{
    /// <summary>
    /// The native copy of a chunk a <see cref="ChunkMeshJob"/> reads (K35): its densities (as
    /// bytes when the chunk stores bytes, M12, otherwise floats) and its material ids (none when
    /// they're uniform). Taken on the main thread in one block copy each, so the chunk can be
    /// edited while the job runs; the job's result then shows the chunk as it was at the copy.
    /// </summary>
    public struct ChunkMeshInput : IDisposable
    {
        public NativeArray<float> Densities;
        public NativeArray<byte> DensityBytes;
        public NativeArray<byte> Materials;
        public byte UniformMaterial;
        public int3 SampleCount;

        /// <summary>Copies the chunk's densities and materials into new native arrays.</summary>
        public static ChunkMeshInput Snapshot(Chunk chunk, Allocator allocator)
        {
            int length = chunk.SampleCount.x * chunk.SampleCount.y * chunk.SampleCount.z;
            bool bytes = chunk.StoresBytes;
            var input = new ChunkMeshInput
            {
                Densities = new NativeArray<float>(bytes ? 0 : length, allocator, NativeArrayOptions.UninitializedMemory),
                DensityBytes = new NativeArray<byte>(bytes ? length : 0, allocator, NativeArrayOptions.UninitializedMemory),
                Materials = new NativeArray<byte>(chunk.Materials.IsUniform ? 0 : length, allocator, NativeArrayOptions.UninitializedMemory),
                UniformMaterial = chunk.Materials.UniformId,
                SampleCount = new int3(chunk.SampleCount.x, chunk.SampleCount.y, chunk.SampleCount.z),
            };
            if (bytes)
            {
                chunk.CopyDensities(input.DensityBytes);
            }
            else
            {
                chunk.CopyDensities(input.Densities);
            }
            chunk.Materials.TryCopyTo(input.Materials);
            return input;
        }

        public void Dispose()
        {
            Densities.Dispose();
            DensityBytes.Dispose();
            Materials.Dispose();
        }

        /// <summary>Frees the arrays once <paramref name="job"/> (which reads them) completes.</summary>
        public JobHandle Dispose(JobHandle job)
        {
            return JobHandle.CombineDependencies(Densities.Dispose(job), DensityBytes.Dispose(job), Materials.Dispose(job));
        }
    }

    /// <summary>
    /// Schedules <see cref="ChunkMeshJob"/>s and hands their results to meshes (K35): the one
    /// Burst mesh path, used by the world's <see cref="ChunkPipeline"/> (many chunks in parallel,
    /// across frames) and by <see cref="ChunkMeshBuilder"/> (one chunk, waited for).
    /// </summary>
    public static class ChunkMeshJobs
    {
        /// <summary>
        /// Schedules building a mesh from <paramref name="input"/> into <paramref name="output"/>[0].
        /// The input is freed when the job completes; the output is the caller's, for <see cref="Apply"/>.
        /// </summary>
        public static JobHandle Schedule(ChunkMeshInput input, ChunkMeshSettings settings, Mesh.MeshDataArray output, JobHandle dependsOn = default)
        {
            JobHandle job = new ChunkMeshJob
            {
                Densities = input.Densities,
                DensityBytes = input.DensityBytes,
                Materials = input.Materials,
                UniformMaterial = input.UniformMaterial,
                SampleCount = input.SampleCount,
                IsoLevel = settings.IsoLevel,
                VoxelSize = settings.VoxelSize,
                EdgePlacement = settings.EdgePlacement,
                Shading = settings.Shading,
                MaterialDisplay = settings.MaterialDisplay,
                Output = output[0],
            }.Schedule(dependsOn);
            return input.Dispose(job);
        }

        /// <summary>
        /// Gives a completed job's mesh data to <paramref name="mesh"/> (main thread), disposing
        /// the data, and sets the bounds to the whole chunk so the renderer stays visible
        /// whenever any part of the chunk is in view.
        /// </summary>
        public static void Apply(Mesh.MeshDataArray data, Mesh mesh, Vector3Int sampleCount, float voxelSize)
        {
            Mesh.ApplyAndDisposeWritableMeshData(
                data, mesh, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            Vector3 size = (Vector3)(sampleCount - Vector3Int.one) * voxelSize;
            mesh.bounds = new Bounds(size * 0.5f, size);
        }

        /// <summary>Builds a chunk's mesh with the Burst job and waits for it (the single-chunk labs).</summary>
        public static void BuildNow(Chunk chunk, ChunkMeshSettings settings, Mesh mesh)
        {
            Mesh.MeshDataArray data = Mesh.AllocateWritableMeshData(1);
            Schedule(ChunkMeshInput.Snapshot(chunk, Allocator.TempJob), settings, data).Complete();
            Apply(data, mesh, chunk.SampleCount, settings.VoxelSize);
        }
    }
}
