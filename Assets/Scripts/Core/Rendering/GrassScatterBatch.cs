using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Runs <see cref="GrassScatterJob"/> for several chunks at once (GL775): each chunk's inputs go in a <see cref="Slot"/>, then
    /// <see cref="Run"/> reads their meshes through one <see cref="Mesh.MeshDataArray"/>, schedules a job per chunk on the
    /// worker threads, and waits for them all. Keeps its buffers between runs.
    /// </summary>
    public sealed class GrassScatterBatch : IDisposable
    {
        /// <summary>Most chunks one run takes.</summary>
        public const int MaxChunks = 16;

        private readonly Slot[] slots = new Slot[MaxChunks];
        private readonly List<Mesh> meshes = new List<Mesh>(MaxChunks);

        /// <summary>One chunk's inputs and, after <see cref="Run"/>, its tufts.</summary>
        public sealed class Slot : IDisposable
        {
            public Mesh Mesh;
            public float4x4 ChunkToWorld;
            public float4x4 WorldToLocal;
            public GrassTuftRules Rules;
            public TreeCoverGrid Cover;
            public NativeList<float3> Roots = new NativeList<float3>(64, Allocator.Persistent);
            public NativeList<Vector3> DahliaNodes = new NativeList<Vector3>(4, Allocator.Persistent);
            public NativeList<int> DahliaPatches = new NativeList<int>(4, Allocator.Persistent);

            public NativeList<float4x4> Grass = new NativeList<float4x4>(256, Allocator.Persistent);
            public NativeList<float4x4> Wheat = new NativeList<float4x4>(64, Allocator.Persistent);
            public NativeList<DahliaTuft> Dahlias = new NativeList<DahliaTuft>(8, Allocator.Persistent);

            internal int MeshIndex;

            public void Dispose()
            {
                Roots.Dispose();
                DahliaNodes.Dispose();
                DahliaPatches.Dispose();
                Grass.Dispose();
                Wheat.Dispose();
                Dahlias.Dispose();
            }
        }

        /// <summary>Chunks added since the last <see cref="Clear"/>.</summary>
        public int Count { get; private set; }

        public bool IsFull => Count >= MaxChunks;

        public Slot this[int index] => slots[index];

        /// <summary>The next chunk's slot, its lists cleared: fill it in, then <see cref="Run"/>.</summary>
        public Slot Add()
        {
            Slot slot = slots[Count] ??= new Slot();
            slot.Mesh = null;
            slot.Roots.Clear();
            slot.DahliaNodes.Clear();
            slot.DahliaPatches.Clear();
            slot.Grass.Clear();
            slot.Wheat.Clear();
            slot.Dahlias.Clear();
            Count++;
            return slot;
        }

        public void Clear()
        {
            Count = 0;
        }

        /// <summary>Scatters every added chunk's grass, on the worker threads, and returns once all are done.</summary>
        public void Run()
        {
            meshes.Clear();
            for (int i = 0; i < Count; i++)
            {
                Slot slot = slots[i];
                slot.MeshIndex = -1;
                if (slot.Mesh != null && slot.Mesh.vertexCount > 0 && slot.Rules.Cell > 0f)
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
                handles[slot.MeshIndex] = new GrassScatterJob
                {
                    Meshes = data,
                    MeshIndex = slot.MeshIndex,
                    ChunkToWorld = slot.ChunkToWorld,
                    WorldToLocal = slot.WorldToLocal,
                    Rules = slot.Rules,
                    Cover = slot.Cover,
                    Roots = slot.Roots.AsArray(),
                    DahliaNodes = slot.DahliaNodes.AsArray(),
                    DahliaPatches = slot.DahliaPatches.AsArray(),
                    Grass = slot.Grass,
                    Wheat = slot.Wheat,
                    Dahlias = slot.Dahlias,
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
