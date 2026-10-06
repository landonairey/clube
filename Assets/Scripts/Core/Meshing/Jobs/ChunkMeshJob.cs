using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Core
{
    /// <summary>
    /// Builds one chunk's whole mesh in a Burst job (K12, K35), off the main thread: the
    /// surface (<see cref="SurfaceExtraction"/>), its normals, the material split for the
    /// <see cref="MaterialDisplay"/> (M13, M15), then the vertex and index buffers, written
    /// straight into a <see cref="Mesh.MeshData"/> that the main thread hands to the mesh in
    /// one call (<see cref="ChunkMeshJobs.Apply"/>). Gives the same mesh as the managed path
    /// (<see cref="ChunkMesher"/> then the managed material pass); no step recording (A11).
    /// </summary>
    /// <remarks>
    /// The variants (edge placement, shading, material display) are picked once at the top of
    /// <see cref="Execute"/>, each running its own compiled loop (A6). Reads a snapshot of the
    /// chunk (<see cref="ChunkMeshInput"/>), so the chunk can be edited while the job runs.
    /// </remarks>
    [BurstCompile]
    public struct ChunkMeshJob : IJob
    {
        [ReadOnly] public NativeArray<float> Densities;

        /// <summary>Single-byte densities (M12), used instead of <see cref="Densities"/> when not empty.</summary>
        [ReadOnly] public NativeArray<byte> DensityBytes;

        [ReadOnly] public NativeArray<byte> Materials;
        public byte UniformMaterial;

        public int3 SampleCount;
        public float IsoLevel;
        public float VoxelSize;
        public EdgePlacement EdgePlacement;
        public Shading Shading;
        public MaterialDisplay MaterialDisplay;

        /// <summary>Where the mesh is written: positions and normals, plus material ids (UV2) and weights (UV3) when materials show.</summary>
        public Mesh.MeshData Output;

        public void Execute()
        {
            NativeArray<float> densities = DensityBytes.Length > 0 ? ToFloats(DensityBytes) : Densities;
            var positions = new NativeList<float3>(1024, Allocator.Temp);
            var vertexMaterials = new NativeList<byte>(1024, Allocator.Temp);
            var indices = new NativeList<int>(4096, Allocator.Temp);

            bool midpoint = EdgePlacement == EdgePlacement.Midpoint;
            if (Shading == Shading.Smooth)
            {
                var shared = new SharedVertices { ByEdge = new NativeHashMap<int, int>(1024, Allocator.Temp) };
                if (midpoint)
                {
                    Extract(densities, new MidpointPlacement(), ref shared, ref positions, ref vertexMaterials, ref indices);
                }
                else
                {
                    Extract(densities, new InterpolatedPlacement(), ref shared, ref positions, ref vertexMaterials, ref indices);
                }
            }
            else
            {
                var separate = new SeparateVertices();
                if (midpoint)
                {
                    Extract(densities, new MidpointPlacement(), ref separate, ref positions, ref vertexMaterials, ref indices);
                }
                else
                {
                    Extract(densities, new InterpolatedPlacement(), ref separate, ref positions, ref vertexMaterials, ref indices);
                }
            }

            var normals = new NativeArray<float3>(positions.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            MeshKernels.Normals(positions, indices, normals);

            var bounds = new Bounds((float3)(SampleCount - 1) * VoxelSize * 0.5f, (float3)(SampleCount - 1) * VoxelSize);
            if (MaterialDisplay == MaterialDisplay.None)
            {
                WritePlain(positions, normals, indices, bounds);
                return;
            }

            var vertices = new NativeList<MaterialVertex>(positions.Length, Allocator.Temp);
            var splitIndices = new NativeList<int>(indices.Length, Allocator.Temp);
            if (MaterialDisplay == MaterialDisplay.Blended)
            {
                MeshKernels.SplitBlended(positions, normals, indices, vertexMaterials, ref vertices, ref splitIndices);
            }
            else
            {
                MeshKernels.SplitHardSeams(positions, normals, indices, vertexMaterials, ref vertices, ref splitIndices);
            }
            WriteWithMaterials(vertices, splitIndices, bounds);
        }

        private void Extract<TPlacement, TSharing>(
            NativeArray<float> densities, TPlacement placement, ref TSharing sharing,
            ref NativeList<float3> positions, ref NativeList<byte> vertexMaterials, ref NativeList<int> indices)
            where TPlacement : struct, IEdgePlacement
            where TSharing : struct, IVertexSharing
        {
            SurfaceExtraction.Extract(
                densities, Materials, UniformMaterial, SampleCount, IsoLevel, VoxelSize,
                placement, ref sharing, ref positions, ref vertexMaterials, ref indices);
        }

        private static NativeArray<float> ToFloats(NativeArray<byte> bytes)
        {
            var floats = new NativeArray<float>(bytes.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < bytes.Length; i++)
            {
                floats[i] = ByteVoxelStorage.ToDensity(bytes[i]);
            }
            return floats;
        }

        private void WritePlain(NativeList<float3> positions, NativeArray<float3> normals, NativeList<int> indices, Bounds bounds)
        {
            var attributes = new NativeArray<VertexAttributeDescriptor>(2, Allocator.Temp);
            attributes[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3);
            attributes[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3);
            Output.SetVertexBufferParams(positions.Length, attributes);
            NativeArray<PlainVertex> vertices = Output.GetVertexData<PlainVertex>();
            for (int i = 0; i < positions.Length; i++)
            {
                vertices[i] = new PlainVertex { Position = positions[i], Normal = normals[i] };
            }
            WriteIndices(indices, positions.Length, bounds);
        }

        private void WriteWithMaterials(NativeList<MaterialVertex> vertices, NativeList<int> indices, Bounds bounds)
        {
            var attributes = new NativeArray<VertexAttributeDescriptor>(4, Allocator.Temp);
            attributes[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3);
            attributes[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3);
            attributes[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 3);
            attributes[3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 3);
            Output.SetVertexBufferParams(vertices.Length, attributes);
            Output.GetVertexData<MaterialVertex>().CopyFrom(vertices.AsArray());
            WriteIndices(indices, vertices.Length, bounds);
        }

        // 16-bit indices top out at 65,535 vertices, which a 32³ chunk can pass.
        private void WriteIndices(NativeList<int> indices, int vertexCount, Bounds bounds)
        {
            if (vertexCount > ushort.MaxValue)
            {
                Output.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);
                Output.GetIndexData<int>().CopyFrom(indices.AsArray());
            }
            else
            {
                Output.SetIndexBufferParams(indices.Length, IndexFormat.UInt16);
                NativeArray<ushort> shortIndices = Output.GetIndexData<ushort>();
                for (int i = 0; i < indices.Length; i++)
                {
                    shortIndices[i] = (ushort)indices[i];
                }
            }

            Output.subMeshCount = 1;
            Output.SetSubMesh(
                0,
                new SubMeshDescriptor(0, indices.Length) { bounds = bounds, vertexCount = vertexCount },
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
        }
    }
}
