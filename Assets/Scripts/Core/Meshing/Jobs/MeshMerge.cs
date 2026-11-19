using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Core
{
    /// <summary>
    /// Copies several meshes into one, each moved by its own offset (GL776), so many chunks draw as one renderer: a draw call
    /// per region in each pass instead of one per chunk. Every source must have one vertex stream, the same layout as the
    /// others, its position as three floats at the start of each vertex, and one submesh. Add any number of merges, then
    /// <see cref="Run"/> copies them all in Burst jobs at once and hands each its mesh. Main thread only.
    /// </summary>
    public sealed class MeshMerge
    {
        private const MeshUpdateFlags Flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;

        private readonly List<Request> requests = new List<Request>();
        private readonly Stack<Request> spare = new Stack<Request>();
        private readonly List<VertexAttributeDescriptor> layout = new List<VertexAttributeDescriptor>();

        /// <summary>Merges waiting for <see cref="Run"/>.</summary>
        public int Count => requests.Count;

        /// <summary>
        /// Queues a merge of <paramref name="sources"/> into <paramref name="target"/>, each source moved by the matching
        /// offset. Sources without vertices are skipped; with none left the target is emptied. Returns false, queuing
        /// nothing, when the sources don't share one layout the merge can copy (e.g. halfway through a change of mesh
        /// settings): try again once they do.
        /// </summary>
        public bool Add(Mesh target, IReadOnlyList<Mesh> sources, IReadOnlyList<Vector3> offsets)
        {
            Request request = spare.Count > 0 ? spare.Pop() : new Request();
            request.Clear();
            request.Target = target;
            int stride = 0;
            int attributes = 0;
            for (int i = 0; i < sources.Count; i++)
            {
                Mesh source = sources[i];
                int vertices = source.vertexCount;
                if (vertices == 0)
                {
                    continue;
                }
                if (request.Sources.Count == 0)
                {
                    if (!CanCopy(source))
                    {
                        spare.Push(request);
                        return false;
                    }
                    stride = source.GetVertexBufferStride(0);
                    attributes = source.vertexAttributeCount;
                }
                else if (source.vertexBufferCount != 1 || source.GetVertexBufferStride(0) != stride || source.vertexAttributeCount != attributes
                    || source.subMeshCount != 1)
                {
                    spare.Push(request);
                    return false;
                }
                Bounds bounds = source.bounds;
                bounds.center += offsets[i];
                if (request.Sources.Count == 0)
                {
                    request.Bounds = bounds;
                }
                else
                {
                    request.Bounds.Encapsulate(bounds);
                }
                request.Sources.Add(source);
                request.Offsets.Add(offsets[i]);
                request.Vertices += vertices;
                request.Indices += (int)source.GetIndexCount(0);
            }
            request.Stride = stride;
            requests.Add(request);
            return true;
        }

        /// <summary>Copies every queued merge (worker threads, waited for) and gives each target its mesh.</summary>
        public void Run()
        {
            if (requests.Count == 0)
            {
                return;
            }
            var handles = new NativeArray<JobHandle>(requests.Count, Allocator.Temp);
            int scheduled = 0;
            foreach (Request request in requests)
            {
                if (request.Sources.Count == 0)
                {
                    continue;
                }
                request.Source = Mesh.AcquireReadOnlyMeshData(request.Sources);
                request.Output = Mesh.AllocateWritableMeshData(1);
                Mesh.MeshData output = request.Output[0];
                request.Sources[0].GetVertexAttributes(layout);
                var attributes = new NativeArray<VertexAttributeDescriptor>(layout.Count, Allocator.Temp);
                for (int i = 0; i < layout.Count; i++)
                {
                    attributes[i] = layout[i];
                }
                output.SetVertexBufferParams(request.Vertices, attributes);
                attributes.Dispose();
                bool wide = request.Vertices > ushort.MaxValue;
                output.SetIndexBufferParams(request.Indices, wide ? IndexFormat.UInt32 : IndexFormat.UInt16);
                request.OffsetData = new NativeArray<float3>(request.Offsets.Count, Allocator.TempJob);
                for (int i = 0; i < request.Offsets.Count; i++)
                {
                    request.OffsetData[i] = request.Offsets[i];
                }
                handles[scheduled++] = new MeshMergeJob
                {
                    Sources = request.Source,
                    Offsets = request.OffsetData,
                    Output = request.Output,
                    Stride = request.Stride,
                    Wide = wide,
                }.Schedule();
            }
            JobHandle.CompleteAll(handles.GetSubArray(0, scheduled));
            handles.Dispose();

            foreach (Request request in requests)
            {
                if (request.Sources.Count == 0)
                {
                    request.Target.Clear();
                }
                else
                {
                    request.Source.Dispose();
                    request.OffsetData.Dispose();
                    Mesh.MeshData output = request.Output[0];
                    output.subMeshCount = 1;
                    output.SetSubMesh(0, new SubMeshDescriptor(0, request.Indices) { bounds = request.Bounds, vertexCount = request.Vertices }, Flags);
                    Mesh.ApplyAndDisposeWritableMeshData(request.Output, request.Target, Flags);
                    request.Target.bounds = request.Bounds;
                }
                request.Clear();
                spare.Push(request);
            }
            requests.Clear();
        }

        // One vertex stream with the position first, as three floats, and one submesh: what the job copies.
        private static bool CanCopy(Mesh mesh)
        {
            return mesh.vertexBufferCount == 1 && mesh.subMeshCount == 1
                && mesh.GetVertexAttributeFormat(VertexAttribute.Position) == VertexAttributeFormat.Float32
                && mesh.GetVertexAttributeDimension(VertexAttribute.Position) == 3
                && mesh.GetVertexAttributeOffset(VertexAttribute.Position) == 0
                && mesh.GetVertexBufferStride(0) % 4 == 0;
        }

        private sealed class Request
        {
            public readonly List<Mesh> Sources = new List<Mesh>();
            public readonly List<Vector3> Offsets = new List<Vector3>();
            public Mesh Target;
            public Bounds Bounds;
            public int Vertices;
            public int Indices;
            public int Stride;
            public Mesh.MeshDataArray Source;
            public Mesh.MeshDataArray Output;
            public NativeArray<float3> OffsetData;

            public void Clear()
            {
                Sources.Clear();
                Offsets.Clear();
                Target = null;
                Vertices = 0;
                Indices = 0;
            }
        }
    }

    /// <summary>
    /// One merge of <see cref="MeshMerge"/>: copies each source's vertices one after another, moving each position by its
    /// source's offset, and its indices raised by the vertices before it.
    /// </summary>
    [BurstCompile]
    public struct MeshMergeJob : IJob
    {
        [ReadOnly] public Mesh.MeshDataArray Sources;
        [ReadOnly] public NativeArray<float3> Offsets;
        public Mesh.MeshDataArray Output;
        public int Stride;
        public bool Wide;

        public void Execute()
        {
            Mesh.MeshData output = Output[0];
            NativeArray<byte> vertices = output.GetVertexData<byte>(0);
            NativeArray<float> floats = vertices.Reinterpret<float>(1);
            NativeArray<ushort> shortIndices = Wide ? default : output.GetIndexData<ushort>();
            NativeArray<int> wideIndices = Wide ? output.GetIndexData<int>() : default;
            int strideFloats = Stride / 4;
            int vertexBase = 0;
            int indexBase = 0;
            for (int s = 0; s < Sources.Length; s++)
            {
                Mesh.MeshData source = Sources[s];
                int count = source.vertexCount;
                NativeArray<byte>.Copy(source.GetVertexData<byte>(0), 0, vertices, vertexBase * Stride, count * Stride);
                float3 offset = Offsets[s];
                for (int v = 0; v < count; v++)
                {
                    int f = (vertexBase + v) * strideFloats;
                    floats[f] += offset.x;
                    floats[f + 1] += offset.y;
                    floats[f + 2] += offset.z;
                }

                SubMeshDescriptor part = source.GetSubMesh(0);
                int first = part.indexStart;
                int indices = part.indexCount;
                int raise = vertexBase + part.baseVertex;
                if (source.indexFormat == IndexFormat.UInt16)
                {
                    NativeArray<ushort> from = source.GetIndexData<ushort>();
                    for (int i = 0; i < indices; i++)
                    {
                        Write(shortIndices, wideIndices, indexBase + i, from[first + i] + raise);
                    }
                }
                else
                {
                    NativeArray<int> from = source.GetIndexData<int>();
                    for (int i = 0; i < indices; i++)
                    {
                        Write(shortIndices, wideIndices, indexBase + i, from[first + i] + raise);
                    }
                }
                vertexBase += count;
                indexBase += indices;
            }
        }

        private void Write(NativeArray<ushort> shortIndices, NativeArray<int> wideIndices, int at, int index)
        {
            if (Wide)
            {
                wideIndices[at] = index;
            }
            else
            {
                shortIndices[at] = (ushort)index;
            }
        }
    }
}
