using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>One search for ground spots (<see cref="GroundSpotSearch.Find"/>): which materials, how upward, and the grid.</summary>
    public struct GroundSpotQuery
    {
        public FixedList32Bytes<float> Grounds;
        public float MinUp;
        public float Cell;
        public int Seed;
        public int SaltX;
        public int SaltZ;
        public float Margin;

        /// <summary>The ids of <paramref name="grounds"/> (nulls skipped), up to <see cref="GroundSpotSearch.MaxGrounds"/>.</summary>
        public static FixedList32Bytes<float> Ids(IReadOnlyList<VoxelMaterial> grounds)
        {
            var ids = new FixedList32Bytes<float>();
            foreach (VoxelMaterial ground in grounds)
            {
                if (ground != null && ids.Length < GroundSpotSearch.MaxGrounds)
                {
                    ids.Add(ground.Id);
                }
            }
            return ids;
        }
    }

    /// <summary>Finds a chunk mesh's spots for two queries in one read of the mesh (GL775: bushes and reeds).</summary>
    [BurstCompile]
    public struct GroundSpotsJob : IJob
    {
        [ReadOnly] public Mesh.MeshDataArray Meshes;
        public int MeshIndex;
        public float4x4 ToWorld;
        public GroundSpotQuery First;
        public GroundSpotQuery Second;
        public NativeList<GroundSpot> FirstSpots;
        public NativeList<GroundSpot> SecondSpots;

        public void Execute()
        {
            FirstSpots.Clear();
            SecondSpots.Clear();
            GroundMesh mesh = GroundMesh.Read(Meshes[MeshIndex], Allocator.Temp);
            GroundSpotSearch.Find(mesh, ToWorld, First.Grounds, First.MinUp, First.Cell, First.Seed, First.SaltX, First.SaltZ, First.Margin, ref FirstSpots);
            GroundSpotSearch.Find(mesh, ToWorld, Second.Grounds, Second.MinUp, Second.Cell, Second.Seed, Second.SaltX, Second.SaltZ, Second.Margin, ref SecondSpots);
        }
    }

    /// <summary>
    /// Runs <see cref="GroundSpotsJob"/> for several chunks at once on the worker threads (GL775), as <see cref="GrassScatterBatch"/>
    /// does for grass. Keeps its buffers between runs.
    /// </summary>
    public sealed class GroundSpotBatch : IDisposable
    {
        public const int MaxChunks = 16;

        private readonly Slot[] slots = new Slot[MaxChunks];
        private readonly List<Mesh> meshes = new List<Mesh>(MaxChunks);

        /// <summary>One chunk's mesh and queries and, after <see cref="Run"/>, its spots.</summary>
        public sealed class Slot : IDisposable
        {
            public Mesh Mesh;
            public float4x4 ToWorld;
            public GroundSpotQuery First;
            public GroundSpotQuery Second;
            public NativeList<GroundSpot> FirstSpots = new NativeList<GroundSpot>(16, Allocator.Persistent);
            public NativeList<GroundSpot> SecondSpots = new NativeList<GroundSpot>(16, Allocator.Persistent);
            internal int MeshIndex;

            public void Dispose()
            {
                FirstSpots.Dispose();
                SecondSpots.Dispose();
            }
        }

        public int Count { get; private set; }

        public bool IsFull => Count >= MaxChunks;

        public Slot this[int index] => slots[index];

        /// <summary>The next chunk's slot, its spots cleared and its queries empty.</summary>
        public Slot Add()
        {
            Slot slot = slots[Count] ??= new Slot();
            slot.Mesh = null;
            slot.First = default;
            slot.Second = default;
            slot.FirstSpots.Clear();
            slot.SecondSpots.Clear();
            Count++;
            return slot;
        }

        public void Clear()
        {
            Count = 0;
        }

        /// <summary>Finds every added chunk's spots on the worker threads and returns once all are done.</summary>
        public void Run()
        {
            meshes.Clear();
            for (int i = 0; i < Count; i++)
            {
                Slot slot = slots[i];
                slot.MeshIndex = -1;
                if (slot.Mesh != null && slot.Mesh.vertexCount > 0)
                {
                    slot.MeshIndex = meshes.Count;
                    meshes.Add(slot.Mesh);
                }
            }
            if (meshes.Count == 0)
            {
                return;
            }
            Mesh.MeshDataArray data = Mesh.AcquireReadOnlyMeshData(meshes);
            var handles = new NativeArray<JobHandle>(meshes.Count, Allocator.Temp);
            for (int i = 0; i < Count; i++)
            {
                Slot slot = slots[i];
                if (slot.MeshIndex < 0)
                {
                    continue;
                }
                handles[slot.MeshIndex] = new GroundSpotsJob
                {
                    Meshes = data,
                    MeshIndex = slot.MeshIndex,
                    ToWorld = slot.ToWorld,
                    First = slot.First,
                    Second = slot.Second,
                    FirstSpots = slot.FirstSpots,
                    SecondSpots = slot.SecondSpots,
                }.Schedule();
            }
            JobHandle.CompleteAll(handles);
            handles.Dispose();
            data.Dispose();
        }

        public void Dispose()
        {
            foreach (Slot slot in slots)
            {
                slot?.Dispose();
            }
        }
    }
}
