using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The water's surface in a chunk (GL315): marching cubes over the water stored per sample (<see cref="World.GetWater"/>),
    /// so the water has the terrain's own low-poly facets, a level surface on lakes and ponds, a sloping one down rivers,
    /// and falling streams wrapped round. A sample's water maps to the field as 0.5 + half its amount (none: 0), so even a
    /// thin stream shows. Solid samples take the water of the air beside or above them, so the surface runs on into the
    /// banks (the ground hides its edge) and no face is drawn against the ground; triangles lying wholly inside the ground
    /// are dropped. Every triangle has its own three vertices: the shader shades it flat and shimmers it as one facet.
    /// </summary>
    public sealed class WaterMesher
    {
        /// <summary>Less water than this isn't drawn.</summary>
        public const float MinVisible = 0.02f;

        private const float Iso = 0.5f;

        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> flows = new List<Vector2>();
        private readonly List<Vector4> looks = new List<Vector4>();
        private readonly List<Vector4> ripples = new List<Vector4>();
        private readonly List<Vector2> banks = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();
        private float[] field;
        private bool[] solid;
        private Vector3Int padded;
        private float[] water;
        private float[] smoothed;
        private readonly Chunk[] around = new Chunk[27];
        private readonly float[] corner = new float[8];
        private readonly bool[] cornerSolid = new bool[8];
        private readonly Vector3[] edgePoint = new Vector3[12];
        private readonly bool[] edgeInGround = new bool[12];
        private readonly bool[] edgeAtBank = new bool[12];
        // The padded rows holding any water: the cubes looked at are only those near them.
        private int lowRow;
        private int highRow;

        /// <summary>Vertices of the last build, relative to the chunk's origin.</summary>
        public List<Vector3> Vertices => vertices;

        /// <summary>UV0: the facet's flow (x, z), its length the flow's speed (steeper runs faster).</summary>
        public List<Vector2> Flows => flows;

        /// <summary>UV1: the facet's centre in the world (x, y, z) and its kind (1 river, 2 still, 3 falling).</summary>
        public List<Vector4> Looks => looks;

        /// <summary>UV2: the river's flow (x, z) at the vertex and whether it's river, for the ripples.</summary>
        public List<Vector4> Ripples => ripples;

        /// <summary>UV3: 1 on triangles meeting the bank, where the shader laps foam (GL318).</summary>
        public List<Vector2> Banks => banks;

        public List<int> Triangles => triangles;

        /// <summary>Builds a chunk's water surface into the lists. Returns false when there's nothing to draw.</summary>
        public bool Build(World world, Vector3Int coord, float iso, in WaterMap map)
        {
            vertices.Clear();
            flows.Clear();
            looks.Clear();
            ripples.Clear();
            banks.Clear();
            triangles.Clear();
            WorldGrid grid = world.Grid;
            Vector3Int size = grid.ChunkSize;
            float voxel = grid.VoxelSize;
            Vector3 origin = grid.ChunkOrigin(coord);
            if (!Gather(world, coord, size, iso))
            {
                return false;
            }
            for (int z = 0; z < size.z; z++)
            {
                for (int y = Mathf.Max(0, lowRow - 3); y < Mathf.Min(size.y, highRow + 1); y++)
                {
                    for (int x = 0; x < size.x; x++)
                    {
                        int cube = 0;
                        for (int c = 0; c < 8; c++)
                        {
                            int i = Index(x + MarchingCubesTables.CornerOffsets[c, 0] + 1, y + MarchingCubesTables.CornerOffsets[c, 1] + 1,
                                z + MarchingCubesTables.CornerOffsets[c, 2] + 1);
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
                            int c0 = MarchingCubesTables.EdgeCorners[e, 0], c1 = MarchingCubesTables.EdgeCorners[e, 1];
                            if ((corner[c0] < Iso) == (corner[c1] < Iso))
                            {
                                continue;
                            }
                            float t = (Iso - corner[c0]) / (corner[c1] - corner[c0]);
                            var p0 = new Vector3(x + MarchingCubesTables.CornerOffsets[c0, 0], y + MarchingCubesTables.CornerOffsets[c0, 1], z + MarchingCubesTables.CornerOffsets[c0, 2]);
                            var p1 = new Vector3(x + MarchingCubesTables.CornerOffsets[c1, 0], y + MarchingCubesTables.CornerOffsets[c1, 1], z + MarchingCubesTables.CornerOffsets[c1, 2]);
                            edgePoint[e] = Vector3.Lerp(p0, p1, t) * voxel;
                            edgeInGround[e] = cornerSolid[c0] && cornerSolid[c1];
                            edgeAtBank[e] = cornerSolid[c0] || cornerSolid[c1];
                        }
                        for (int k = 0; k < 15 && MarchingCubesTables.Triangles[cube, k] >= 0; k += 3)
                        {
                            int e0 = MarchingCubesTables.Triangles[cube, k], e1 = MarchingCubesTables.Triangles[cube, k + 1], e2 = MarchingCubesTables.Triangles[cube, k + 2];
                            if (edgeInGround[e0] && edgeInGround[e1] && edgeInGround[e2])
                            {
                                continue;
                            }
                            bool bank = edgeAtBank[e0] || edgeAtBank[e1] || edgeAtBank[e2];
                            AddTriangle(edgePoint[e0], edgePoint[e1], edgePoint[e2], origin, map, bank);
                        }
                    }
                }
            }
            return triangles.Count > 0;
        }

        // Reads the chunk's samples and one more all round; false when none of them holds water. The 27 chunks round it
        // are looked up once, not per sample.
        private bool Gather(World world, Vector3Int coord, Vector3Int size, float iso)
        {
            // Samples -1 .. size + 1 on each axis.
            padded = size + new Vector3Int(3, 3, 3);
            int count = padded.x * padded.y * padded.z;
            if (field == null || field.Length != count)
            {
                field = new float[count];
                solid = new bool[count];
                water = new float[count];
            }
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        world.TryGetChunk(coord + new Vector3Int(dx, dy, dz), out Chunk next);
                        around[dx + 1 + 3 * (dy + 1 + 3 * (dz + 1))] = next;
                    }
                }
            }
            bool any = false;
            lowRow = int.MaxValue;
            highRow = -1;
            for (int z = 0; z < padded.z; z++)
            {
                Side(z - 1, size.z, out int cz, out int lz);
                for (int y = 0; y < padded.y; y++)
                {
                    Side(y - 1, size.y, out int cy, out int ly);
                    for (int x = 0; x < padded.x; x++)
                    {
                        Side(x - 1, size.x, out int cx, out int lx);
                        int i = Index(x, y, z);
                        Chunk chunk = around[cx + 1 + 3 * (cy + 1 + 3 * (cz + 1))];
                        if (chunk == null)
                        {
                            solid[i] = false;
                            water[i] = 0f;
                            continue;
                        }
                        var local = new Vector3Int(lx, ly, lz);
                        float amount = chunk.HasWater ? chunk.GetWater(local) : 0f;
                        solid[i] = chunk.GetDensity(local) >= iso;
                        if (solid[i] || amount < MinVisible)
                        {
                            water[i] = 0f;
                            continue;
                        }
                        water[i] = 0.5f + 0.5f * amount;
                        any = true;
                        lowRow = Mathf.Min(lowRow, y);
                        highRow = Mathf.Max(highRow, y);
                    }
                }
            }
            if (!any)
            {
                return false;
            }
            Smooth();
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
                        Take(x + 1, y, z, ref best, water);
                        Take(x - 1, y, z, ref best, water);
                        Take(x, y, z + 1, ref best, water);
                        Take(x, y, z - 1, ref best, water);
                        Take(x, y + 1, z, ref best, water);
                        field[i] = best;
                    }
                }
            }
            return true;
        }


        // Which neighbouring chunk (-1, 0, 1) a local sample index falls in along one axis, and its index there. The
        // chunk's own border samples (0 and size) are its own.
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

        // Running water is uneven sample to sample (it moves in packets), which drew streams full of holes and spikes
        // (GL318). For the drawing only: a dry or thin sample standing on something, with water on most of the open sides
        // round it, is drawn as water; a lone thin sample with no water anywhere beside it isn't drawn at all.
        private void Smooth()
        {
            smoothed ??= new float[water.Length];
            if (smoothed.Length != water.Length)
            {
                smoothed = new float[water.Length];
            }
            System.Array.Copy(water, smoothed, water.Length);
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
                        Neighbour(i + dx, ref sum, ref open, ref wet);
                        Neighbour(i - dx, ref sum, ref open, ref wet);
                        Neighbour(i + dz, ref sum, ref open, ref wet);
                        Neighbour(i - dz, ref sum, ref open, ref wet);
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
            (water, smoothed) = (smoothed, water);
        }

        private void Neighbour(int n, ref float sum, ref int open, ref int wet)
        {
            if (solid[n])
            {
                return;
            }
            open++;
            sum += water[n];
            wet += water[n] > 0f ? 1 : 0;
        }
        private void Take(int x, int y, int z, ref float best, float[] water)
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

        private void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 origin, in WaterMap map, bool bank)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude < 1e-12f)
            {
                return;
            }
            normal.Normalize();
            if (normal.y < 0f)
            {
                normal = -normal;
            }
            Vector3 centre = origin + (a + b + c) / 3f;
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
                if (map.IsCreated && map.TryCell(new float2(centre.x, centre.z), out int cell) && map.Kind[cell] == (byte)WaterKind.River)
                {
                    river = true;
                    riverFlow = map.Flow[cell];
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
            AddVertex(a, flow, look, origin, map, bank);
            AddVertex(b, flow, look, origin, map, bank);
            AddVertex(c, flow, look, origin, map, bank);
        }

        private void AddVertex(Vector3 p, Vector2 flow, Vector4 look, Vector3 origin, in WaterMap map, bool bank)
        {
            triangles.Add(vertices.Count);
            vertices.Add(p);
            flows.Add(flow);
            looks.Add(look);
            ripples.Add(RippleAt(origin + p, map));
            banks.Add(new Vector2(bank ? 1f : 0f, 0f));
        }

        // The river's flow at a point and whether it's river, from the map alone, so every triangle sharing a corner
        // ripples it alike and the surface never cracks.
        private static Vector4 RippleAt(Vector3 position, in WaterMap map)
        {
            if (!map.IsCreated || !map.TryCell(new float2(position.x, position.z), out int cell) || map.Kind[cell] != (byte)WaterKind.River)
            {
                return Vector4.zero;
            }
            float2 f = map.Flow[cell];
            return new Vector4(f.x, f.y, 1f, 0f);
        }
    }
}
