using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A felled limb's mesh (GL344), built by the world's own Burst mesh job (<see cref="ChunkMeshJobs"/>) from a scratch
    /// grid one sample bigger than the limb all round (empty there, so the surface closes), meshed exactly as the terrain
    /// draws it. The job runs while the frame goes on; <see cref="Complete"/> hands over the mesh once
    /// <see cref="IsCompleted"/>. (Built on the main thread and waited for, a big crown's mesh stalled a frame by 20-120 ms.)
    /// </summary>
    public sealed class LimbMeshJob
    {
        private readonly Vector3Int sampleCount;
        private readonly float voxelSize;
        private Mesh.MeshDataArray data;
        private JobHandle handle;
        private bool done;

        public LimbMeshJob(FelledLimb limb, WorldGrid grid, ChunkMeshSettings settings)
        {
            Vector3Int first = limb.Min - Vector3Int.one;
            sampleCount = limb.Max - limb.Min + Vector3Int.one * 3;
            voxelSize = settings.VoxelSize;
            Origin = grid.SampleToWorld(first);
            int length = sampleCount.x * sampleCount.y * sampleCount.z;
            // Persistent: a big crown may take more frames than temp memory may live.
            var input = new ChunkMeshInput
            {
                Densities = new NativeArray<float>(length, Allocator.Persistent),
                DensityBytes = new NativeArray<byte>(0, Allocator.Persistent),
                Materials = new NativeArray<byte>(length, Allocator.Persistent),
                UniformMaterial = 0,
                SampleCount = new int3(sampleCount.x, sampleCount.y, sampleCount.z),
                Carves = new NativeArray<byte>(0, Allocator.Persistent),
            };
            Write(input, limb.Solid, first);
            Write(input, limb.Fringe, first);
            data = Mesh.AllocateWritableMeshData(1);
            handle = ChunkMeshJobs.Schedule(input, settings, data);
            JobHandle.ScheduleBatchedJobs();
        }

        /// <summary>World position (in the world view's space) of the mesh's corner.</summary>
        public Vector3 Origin { get; }

        /// <summary>True once the job has finished and <see cref="Complete"/> won't wait.</summary>
        public bool IsCompleted => handle.IsCompleted;

        /// <summary>Waits for the job if it's still running and gives the mesh (the caller's to destroy).</summary>
        public Mesh Complete()
        {
            handle.Complete();
            done = true;
            var mesh = new Mesh { name = "Falling limb" };
            ChunkMeshJobs.Apply(data, mesh, sampleCount, voxelSize);
            return mesh;
        }

        /// <summary>Drops the job's result without making a mesh (the feller went away first).</summary>
        public void Cancel()
        {
            if (done)
            {
                return;
            }
            handle.Complete();
            data.Dispose();
            done = true;
        }

        private void Write(ChunkMeshInput input, System.Collections.Generic.List<FelledSample> samples, Vector3Int first)
        {
            NativeArray<float> densities = input.Densities;
            NativeArray<byte> materials = input.Materials;
            for (int i = 0; i < samples.Count; i++)
            {
                Vector3Int local = samples[i].Sample - first;
                int index = SampleGrid.FlatIndex(sampleCount, local.x, local.y, local.z);
                densities[index] = samples[i].Density;
                materials[index] = samples[i].Material;
            }
        }
    }
}
