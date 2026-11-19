using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// What <see cref="WaterMeshJob"/> reads of a chunk and the 26 round it (GL775): each one's densities (single bytes, or
    /// floats from other storages, or one uniform value) and water, copied in blocks on the main thread by <see cref="Gather"/>.
    /// Keeps its buffers between builds.
    /// </summary>
    public sealed class WaterMeshInput : IDisposable
    {
        /// <summary>How a neighbour's densities are given (<see cref="Modes"/>).</summary>
        public const int Missing = 0, UniformDensity = 1, ByteDensities = 2, FloatDensities = 3;

        public NativeArray<byte> DensityBytes;
        public NativeArray<float> DensityFloats;
        public NativeArray<byte> Water;
        public NativeArray<int> Modes = new NativeArray<int>(27, Allocator.Persistent);
        public NativeArray<float> Uniform = new NativeArray<float>(27, Allocator.Persistent);
        public NativeArray<bool> HasWater = new NativeArray<bool>(27, Allocator.Persistent);

        /// <summary>Samples per chunk on each axis.</summary>
        public Vector3Int SampleCount { get; private set; }

        public WaterMeshInput()
        {
            DensityBytes = new NativeArray<byte>(1, Allocator.Persistent);
            DensityFloats = new NativeArray<float>(1, Allocator.Persistent);
            Water = new NativeArray<byte>(1, Allocator.Persistent);
        }

        /// <summary>
        /// Copies the chunk at <paramref name="coord"/> and those round it; false when none of them holds water (nothing to
        /// draw), or the chunk isn't loaded.
        /// </summary>
        public bool Gather(World world, Vector3Int coord)
        {
            if (!world.TryPeekChunk(coord, out Chunk centre))
            {
                return false;
            }
            SampleCount = centre.SampleCount;
            int count = SampleCount.x * SampleCount.y * SampleCount.z;
            Fit(ref DensityBytes, 27 * count);
            Fit(ref Water, 27 * count);
            bool anyWater = false;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int n = dx + 1 + 3 * (dy + 1 + 3 * (dz + 1));
                        HasWater[n] = false;
                        if (!world.TryPeekChunk(coord + new Vector3Int(dx, dy, dz), out Chunk chunk))
                        {
                            Modes[n] = Missing;
                            continue;
                        }
                        if (chunk.IsUniform)
                        {
                            Modes[n] = UniformDensity;
                            Uniform[n] = chunk.UniformDensity;
                        }
                        else if (chunk.StoresBytes)
                        {
                            Modes[n] = ByteDensities;
                            chunk.CopyDensities(DensityBytes.GetSubArray(n * count, count));
                        }
                        else
                        {
                            Modes[n] = FloatDensities;
                            Fit(ref DensityFloats, 27 * count);
                            chunk.CopyDensities(DensityFloats.GetSubArray(n * count, count));
                        }
                        if (chunk.HasWater)
                        {
                            HasWater[n] = true;
                            anyWater = true;
                            chunk.CopyWater(Water.GetSubArray(n * count, count));
                        }
                    }
                }
            }
            return anyWater;
        }

        private static void Fit<T>(ref NativeArray<T> array, int length) where T : struct
        {
            if (array.Length < length)
            {
                array.Dispose();
                array = new NativeArray<T>(length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }
        }

        public void Dispose()
        {
            DensityBytes.Dispose();
            DensityFloats.Dispose();
            Water.Dispose();
            Modes.Dispose();
            Uniform.Dispose();
            HasWater.Dispose();
        }
    }

    /// <summary>A water mesh's streams, as <see cref="WaterMeshJob"/> fills them: one entry per vertex (three per triangle).</summary>
    public sealed class WaterMeshOutput : IDisposable
    {
        public NativeList<Vector3> Vertices = new NativeList<Vector3>(1024, Allocator.Persistent);
        public NativeList<Vector3> Normals = new NativeList<Vector3>(1024, Allocator.Persistent);
        public NativeList<Vector2> Flows = new NativeList<Vector2>(1024, Allocator.Persistent);
        public NativeList<Vector4> Looks = new NativeList<Vector4>(1024, Allocator.Persistent);
        public NativeList<Vector4> Ripples = new NativeList<Vector4>(1024, Allocator.Persistent);
        public NativeList<Vector2> Banks = new NativeList<Vector2>(1024, Allocator.Persistent);
        public NativeList<int> Triangles = new NativeList<int>(1024, Allocator.Persistent);

        public void Dispose()
        {
            Vertices.Dispose();
            Normals.Dispose();
            Flows.Dispose();
            Looks.Dispose();
            Ripples.Dispose();
            Banks.Dispose();
            Triangles.Dispose();
        }
    }

    /// <summary>
    /// <see cref="WaterMesher"/> as a Burst job (GL775): the same surface from the same samples, copied in by
    /// <see cref="WaterMeshInput"/>, with each triangle's flat normal worked out here rather than by
    /// <see cref="Mesh.RecalculateNormals()"/>. <c>WaterMeshJobTests</c> checks the two agree.
    /// </summary>
    [BurstCompile]
    public struct WaterMeshJob : IJob
    {
        private const float Iso = 0.5f;

        /// <summary>Voxels per chunk on each axis.</summary>
        public int3 Size;
        public float VoxelSize;
        public Vector3 Origin;

        /// <summary>The terrain's iso level: a sample at least this dense is solid.</summary>
        public float GroundIso;

        [ReadOnly] public NativeArray<byte> DensityBytes;
        [ReadOnly] public NativeArray<float> DensityFloats;
        [ReadOnly] public NativeArray<byte> WaterSamples;
        [ReadOnly] public NativeArray<int> Modes;
        [ReadOnly] public NativeArray<float> Uniform;
        [ReadOnly] public NativeArray<bool> HasWater;
        public WaterMap Map;

        public NativeList<Vector3> Vertices;
        public NativeList<Vector3> Normals;
        public NativeList<Vector2> Flows;
        public NativeList<Vector4> Looks;
        public NativeList<Vector4> Ripples;
        public NativeList<Vector2> Banks;
        public NativeList<int> Triangles;

        private int3 padded;

        public void Execute()
        {
            Vertices.Clear();
            Normals.Clear();
            Flows.Clear();
            Looks.Clear();
            Ripples.Clear();
            Banks.Clear();
            Triangles.Clear();
            padded = Size + 3;
            int count = padded.x * padded.y * padded.z;
            var field = new NativeArray<float>(count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var solid = new NativeArray<bool>(count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var water = new NativeArray<float>(count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            if (!Gather(solid, water, out int lowRow, out int highRow))
            {
                return;
            }
            water = Smooth(solid, water);
            // Solid samples take the most water of the air beside and above them.
            for (int z = 0; z < padded.z; z++)
            {
                for (int y = 0; y < padded.y; y++)
                {
                    for (int x = 0; x < padded.x; x++)
                    {
                        int i = Index(x, y, z);
                        if (!solid[i])
                        {
                            field[i] = water[i];
                            continue;
                        }
                        float best = 0f;
                        Take(x + 1, y, z, ref best, solid, water);
                        Take(x - 1, y, z, ref best, solid, water);
                        Take(x, y, z + 1, ref best, solid, water);
                        Take(x, y, z - 1, ref best, solid, water);
                        Take(x, y + 1, z, ref best, solid, water);
                        field[i] = best;
                    }
                }
            }
            March(field, solid, lowRow, highRow);
        }

        private void March(NativeArray<float> field, NativeArray<bool> solid, int lowRow, int highRow)
        {
            var corner = new NativeArray<float>(8, Allocator.Temp);
            var cornerSolid = new NativeArray<bool>(8, Allocator.Temp);
            var edgePoint = new NativeArray<Vector3>(12, Allocator.Temp);
            var edgeInGround = new NativeArray<bool>(12, Allocator.Temp);
            var edgeAtBank = new NativeArray<bool>(12, Allocator.Temp);
            for (int z = 0; z < Size.z; z++)
            {
                for (int y = math.max(0, lowRow - 3); y < math.min(Size.y, highRow + 1); y++)
                {
                    for (int x = 0; x < Size.x; x++)
                    {
                        int cube = 0;
                        for (int c = 0; c < 8; c++)
                        {
                            int i = Index(x + MarchingCubesJobTables.CornerOffsets[c * 3] + 1, y + MarchingCubesJobTables.CornerOffsets[c * 3 + 1] + 1,
                                z + MarchingCubesJobTables.CornerOffsets[c * 3 + 2] + 1);
                            corner[c] = field[i];
                            cornerSolid[c] = solid[i];
                            if (corner[c] < Iso)
                            {
                                cube |= 1 << c;
                            }
                        }
                        if (cube == 0 || cube == 255)
                        {
                            continue;
                        }
                        for (int e = 0; e < 12; e++)
                        {
                            int c0 = MarchingCubesJobTables.EdgeCorners[e * 2], c1 = MarchingCubesJobTables.EdgeCorners[e * 2 + 1];
                            if ((corner[c0] < Iso) == (corner[c1] < Iso))
                            {
                                continue;
                            }
                            float t = (Iso - corner[c0]) / (corner[c1] - corner[c0]);
                            var p0 = new Vector3(x + MarchingCubesJobTables.CornerOffsets[c0 * 3], y + MarchingCubesJobTables.CornerOffsets[c0 * 3 + 1],
                                z + MarchingCubesJobTables.CornerOffsets[c0 * 3 + 2]);
                            var p1 = new Vector3(x + MarchingCubesJobTables.CornerOffsets[c1 * 3], y + MarchingCubesJobTables.CornerOffsets[c1 * 3 + 1],
                                z + MarchingCubesJobTables.CornerOffsets[c1 * 3 + 2]);
                            edgePoint[e] = Vector3.Lerp(p0, p1, t) * VoxelSize;
                            edgeInGround[e] = cornerSolid[c0] && cornerSolid[c1];
                            edgeAtBank[e] = cornerSolid[c0] || cornerSolid[c1];
                        }
                        int row = cube * MarchingCubesJobTables.TriangleStride;
                        for (int k = 0; k < 15 && MarchingCubesJobTables.Triangles[row + k] >= 0; k += 3)
                        {
                            int e0 = MarchingCubesJobTables.Triangles[row + k], e1 = MarchingCubesJobTables.Triangles[row + k + 1],
                                e2 = MarchingCubesJobTables.Triangles[row + k + 2];
                            if (edgeInGround[e0] && edgeInGround[e1] && edgeInGround[e2])
                            {
                                continue;
                            }
                            bool bank = edgeAtBank[e0] || edgeAtBank[e1] || edgeAtBank[e2];
                            AddTriangle(edgePoint[e0], edgePoint[e1], edgePoint[e2], bank);
                        }
                    }
                }
            }
        }

        // Reads the chunk's samples and one more all round; false when none of them holds water.
        private bool Gather(NativeArray<bool> solid, NativeArray<float> water, out int lowRow, out int highRow)
        {
            int3 samples = Size + 1;
            int chunkSamples = samples.x * samples.y * samples.z;
            bool any = false;
            lowRow = int.MaxValue;
            highRow = -1;
            for (int z = 0; z < padded.z; z++)
            {
                Side(z - 1, Size.z, out int cz, out int lz);
                for (int y = 0; y < padded.y; y++)
                {
                    Side(y - 1, Size.y, out int cy, out int ly);
                    for (int x = 0; x < padded.x; x++)
                    {
                        Side(x - 1, Size.x, out int cx, out int lx);
                        int i = Index(x, y, z);
                        int n = cx + 1 + 3 * (cy + 1 + 3 * (cz + 1));
                        int mode = Modes[n];
                        if (mode == WaterMeshInput.Missing)
                        {
                            solid[i] = false;
                            water[i] = 0f;
                            continue;
                        }
                        int sample = n * chunkSamples + lx + samples.x * (ly + samples.y * lz);
                        float density = mode == WaterMeshInput.UniformDensity ? Uniform[n]
                            : mode == WaterMeshInput.ByteDensities ? ByteVoxelStorage.ToDensity(DensityBytes[sample]) : DensityFloats[sample];
                        float amount = HasWater[n] ? WaterSamples[sample] * (1f / Chunk.WaterLevels) : 0f;
                        solid[i] = density >= GroundIso;
                        if (solid[i] || amount < WaterMesher.MinVisible)
                        {
                            water[i] = 0f;
                            continue;
                        }
                        water[i] = 0.5f + 0.5f * amount;
                        any = true;
                        lowRow = math.min(lowRow, y);
                        highRow = math.max(highRow, y);
                    }
                }
            }
            return any;
        }

        private static void Side(int local, int size, out int chunk, out int inChunk)
        {
            if (local < 0)
            {
                chunk = -1;
                inChunk = local + size;
            }
            else if (local > size)
            {
                chunk = 1;
                inChunk = local - size;
            }
            else
            {
                chunk = 0;
                inChunk = local;
            }
        }

        // As WaterMesher.Smooth: fills small holes in running water and drops lone thin samples, for the drawing only.
        private NativeArray<float> Smooth(NativeArray<bool> solid, NativeArray<float> water)
        {
            var smoothed = new NativeArray<float>(water, Allocator.Temp);
            int dx = 1, dy = padded.x, dz = padded.x * padded.y;
            for (int z = 1; z < padded.z - 1; z++)
            {
                for (int y = 1; y < padded.y - 1; y++)
                {
                    for (int x = 1; x < padded.x - 1; x++)
                    {
                        int i = Index(x, y, z);
                        float own = water[i];
                        smoothed[i] = own;
                        if (solid[i])
                        {
                            continue;
                        }
                        float sum = 0f;
                        int open = 0, wet = 0;
                        Neighbour(i + dx, ref sum, ref open, ref wet, solid, water);
                        Neighbour(i - dx, ref sum, ref open, ref wet, solid, water);
                        Neighbour(i + dz, ref sum, ref open, ref wet, solid, water);
                        Neighbour(i - dz, ref sum, ref open, ref wet, solid, water);
                        if (own <= 0f || own < 0.75f)
                        {
                            bool heldUp = solid[i - dy] || water[i - dy] > 0f;
                            if (heldUp && open > 0 && wet >= 2 && sum / open * 0.95f >= Iso)
                            {
                                smoothed[i] = Mathf.Max(own, sum / open * 0.95f);
                                continue;
                            }
                        }
                        if (own > 0f && own < 0.75f && wet == 0 && water[i + dy] <= 0f && water[i - dy] <= 0f)
                        {
                            smoothed[i] = 0f;
                        }
                    }
                }
            }
            return smoothed;
        }

        private static void Neighbour(int n, ref float sum, ref int open, ref int wet, NativeArray<bool> solid, NativeArray<float> water)
        {
            if (solid[n])
            {
                return;
            }
            open++;
            sum += water[n];
            wet += water[n] > 0f ? 1 : 0;
        }

        private void Take(int x, int y, int z, ref float best, NativeArray<bool> solid, NativeArray<float> water)
        {
            if (x < 0 || y < 0 || z < 0 || x >= padded.x || y >= padded.y || z >= padded.z)
            {
                return;
            }
            int i = Index(x, y, z);
            if (!solid[i] && water[i] > best)
            {
                best = water[i];
            }
        }

        private int Index(int x, int y, int z)
        {
            return x + padded.x * (y + padded.y * z);
        }

        private void AddTriangle(Vector3 a, Vector3 b, Vector3 c, bool bank)
        {
            Vector3 cross = Vector3.Cross(b - a, c - a);
            if (cross.sqrMagnitude < 1e-12f)
            {
                return;
            }
            // The triangle's own facing, as Mesh.RecalculateNormals gives a vertex used by one triangle alone.
            Vector3 facing = cross.normalized;
            Vector3 normal = facing;
            if (normal.y < 0f)
            {
                normal = -normal;
            }
            Vector3 centre = Origin + (a + b + c) / 3f;
            var horizontal = new Vector2(normal.x, normal.z);
            float slope = normal.y > 0.05f ? horizontal.magnitude / normal.y : 10f;
            float kind;
            Vector2 along;
            if (normal.y < 0.35f)
            {
                // A wall of falling water.
                kind = 3f;
                along = Vector2.zero;
                slope = 3f;
            }
            else
            {
                bool river = false;
                float2 riverFlow = float2.zero;
                if (Map.IsCreated && Map.TryCell(new float2(centre.x, centre.z), out int cell) && Map.Kind[cell] == (byte)WaterKind.River)
                {
                    river = true;
                    riverFlow = Map.Flow[cell];
                }
                if (river && math.lengthsq(riverFlow) > 1e-6f)
                {
                    along = new Vector2(riverFlow.x, riverFlow.y).normalized;
                }
                else
                {
                    // Downhill is where the surface leans.
                    along = horizontal.sqrMagnitude > 1e-6f ? horizontal.normalized : Vector2.zero;
                }
                kind = river || slope > 0.04f ? 1f : 2f;
            }
            // Speed by slope: a gentle run creeps, a steep one races.
            Vector2 flow = along * Mathf.Clamp(0.15f + slope * 6f, 0.15f, 3f);
            var look = new Vector4(centre.x, centre.y, centre.z, kind);
            AddVertex(a, facing, flow, look, bank);
            AddVertex(b, facing, flow, look, bank);
            AddVertex(c, facing, flow, look, bank);
        }

        private void AddVertex(Vector3 p, Vector3 facing, Vector2 flow, Vector4 look, bool bank)
        {
            Triangles.Add(Vertices.Length);
            Vertices.Add(p);
            Normals.Add(facing);
            Flows.Add(flow);
            Looks.Add(look);
            Ripples.Add(RippleAt(Origin + p));
            Banks.Add(new Vector2(bank ? 1f : 0f, 0f));
        }

        // The river's flow at a point and whether it's river, from the map alone.
        private Vector4 RippleAt(Vector3 position)
        {
            if (!Map.IsCreated || !Map.TryCell(new float2(position.x, position.z), out int cell) || Map.Kind[cell] != (byte)WaterKind.River)
            {
                return Vector4.zero;
            }
            float2 f = Map.Flow[cell];
            return new Vector4(f.x, f.y, 1f, 0f);
        }
    }
}
