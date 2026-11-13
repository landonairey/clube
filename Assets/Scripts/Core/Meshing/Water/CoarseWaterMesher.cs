using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Distant water with few triangles (GL626): the map's water (<see cref="WaterMap"/>) cut into square regions, each a
    /// quadtree whose squares are single sheets wherever the water is still and level (the sea, lakes), down to the
    /// single cells of <see cref="WaterSheetMesh.AddCell"/> along shores and down rivers. A square is one sheet only when
    /// every cell in it is <em>flat</em>: wet, not river, and at the height of every wet cell touching it; so each grid
    /// corner on its rim is at that one height whoever draws it. Where smaller squares lie along a sheet's side, the
    /// sheet is a fan through their corners, so no corner ever lies part-way along another triangle's edge (no pinholes).
    /// It covers exactly the cells the cell-per-quad sheets cover.
    /// </summary>
    public sealed class CoarseWaterMesher
    {
        /// <summary>A square of cells drawn as one piece: one cell, or a level sheet.</summary>
        public readonly struct Quad
        {
            public readonly int X, Z, Size;

            public Quad(int x, int z, int size)
            {
                X = x;
                Z = z;
                Size = size;
            }
        }

        private readonly WaterMap water;
        private readonly int regionCells;
        private readonly List<Quad>[] quads;
        // The grid corners some quad has a corner at: (width + 1) x (depth + 1).
        private readonly BitArray corners;
        private readonly List<Vector3> rim = new List<Vector3>();

        /// <summary>Regions along x; a region's index is x + z * <see cref="RegionsAcross"/>.</summary>
        public int RegionsAcross { get; }

        public int RegionCount => quads.Length;

        /// <summary>Cells along a region's side.</summary>
        public int RegionCells => regionCells;

        /// <summary>Squares over the whole map, single cells included.</summary>
        public int QuadCount { get; }

        /// <summary>Cuts the map's water up. <paramref name="regionCells"/> is a power of two: the largest sheet.</summary>
        public CoarseWaterMesher(in WaterMap water, int regionCells)
        {
            this.water = water;
            this.regionCells = math.ceilpow2(math.max(1, regionCells));
            int2 size = water.Size;
            RegionsAcross = (size.x + this.regionCells - 1) / this.regionCells;
            int regionsDeep = (size.y + this.regionCells - 1) / this.regionCells;
            quads = new List<Quad>[RegionsAcross * regionsDeep];
            corners = new BitArray((size.x + 1) * (size.y + 1));
            bool[] flat = FlatCells(water);
            int stride = this.regionCells + 1;
            int[] flatSum = new int[stride * stride], wetSum = new int[stride * stride];
            for (int region = 0; region < quads.Length; region++)
            {
                int x0 = region % RegionsAcross * this.regionCells, z0 = region / RegionsAcross * this.regionCells;
                // Counts of flat and wet cells below and left of each corner of the region, to count any square in one step.
                for (int z = 0; z < this.regionCells; z++)
                {
                    int flatRow = 0, wetRow = 0;
                    for (int x = 0; x < this.regionCells; x++)
                    {
                        int cx = x0 + x, cz = z0 + z;
                        if (cx < size.x && cz < size.y)
                        {
                            int cell = cx + cz * size.x;
                            flatRow += flat[cell] ? 1 : 0;
                            wetRow += water.Kind[cell] != (byte)WaterKind.Dry ? 1 : 0;
                        }
                        int at = x + 1 + (z + 1) * stride;
                        flatSum[at] = flatSum[at - stride] + flatRow;
                        wetSum[at] = wetSum[at - stride] + wetRow;
                    }
                }
                if (wetSum[stride * stride - 1] == 0)
                {
                    continue;
                }
                var list = new List<Quad>();
                Split(0, 0, this.regionCells, x0, z0, flatSum, wetSum, list);
                quads[region] = list;
                QuadCount += list.Count;
                foreach (Quad quad in list)
                {
                    int row = size.x + 1;
                    corners[quad.X + quad.Z * row] = true;
                    corners[quad.X + quad.Size + quad.Z * row] = true;
                    corners[quad.X + (quad.Z + quad.Size) * row] = true;
                    corners[quad.X + quad.Size + (quad.Z + quad.Size) * row] = true;
                }
            }
        }

        /// <summary>The squares of a region; empty where it holds no water.</summary>
        public IReadOnlyList<Quad> Quads(int region) => quads[region] ?? (IReadOnlyList<Quad>)System.Array.Empty<Quad>();

        /// <summary>True when the region has any water to draw.</summary>
        public bool HasWater(int region) => quads[region] != null;

        /// <summary>Adds a region's triangles to a sheet (not cleared first).</summary>
        public void Build(int region, WaterSheetMesh sheet)
        {
            foreach (Quad quad in Quads(region))
            {
                if (quad.Size == 1)
                {
                    sheet.AddCell(water, quad.X, quad.Z);
                }
                else
                {
                    AddSheet(quad, sheet);
                }
            }
        }

        // Wet, still, and level with every wet cell touching it (corners too).
        private static bool[] FlatCells(in WaterMap water)
        {
            int2 size = water.Size;
            var flat = new bool[size.x * size.y];
            for (int z = 0; z < size.y; z++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    int cell = x + z * size.x;
                    if (water.Kind[cell] != (byte)WaterKind.Lake)
                    {
                        continue;
                    }
                    float surface = water.Surface[cell];
                    bool level = true;
                    for (int nz = math.max(0, z - 1); nz <= math.min(size.y - 1, z + 1) && level; nz++)
                    {
                        for (int nx = math.max(0, x - 1); nx <= math.min(size.x - 1, x + 1); nx++)
                        {
                            int n = nx + nz * size.x;
                            if (water.Kind[n] != (byte)WaterKind.Dry && water.Surface[n] != surface)
                            {
                                level = false;
                                break;
                            }
                        }
                    }
                    flat[cell] = level;
                }
            }
            return flat;
        }

        private void Split(int x, int z, int size, int x0, int z0, int[] flatSum, int[] wetSum, List<Quad> list)
        {
            if (Count(wetSum, x, z, size) == 0)
            {
                return;
            }
            if (size == 1 || Count(flatSum, x, z, size) == size * size)
            {
                list.Add(new Quad(x0 + x, z0 + z, size));
                return;
            }
            int half = size / 2;
            Split(x, z, half, x0, z0, flatSum, wetSum, list);
            Split(x + half, z, half, x0, z0, flatSum, wetSum, list);
            Split(x, z + half, half, x0, z0, flatSum, wetSum, list);
            Split(x + half, z + half, half, x0, z0, flatSum, wetSum, list);
        }

        private int Count(int[] sum, int x, int z, int size)
        {
            int stride = regionCells + 1;
            return sum[x + size + (z + size) * stride] - sum[x + (z + size) * stride] - sum[x + size + z * stride] + sum[x + z * stride];
        }

        // A level sheet: two triangles when only its four corners are in use, else a fan from its centre through every
        // corner the squares beside it put on its rim.
        private void AddSheet(Quad quad, WaterSheetMesh sheet)
        {
            float height = water.Surface[quad.X + quad.Z * water.Size.x];
            rim.Clear();
            int s = quad.Size;
            // Clockwise seen from above: up the low-x side, along the high-z side, down the high-x side, back along low z.
            for (int i = 0; i < s; i++)
            {
                AddRim(quad.X, quad.Z + i, height);
            }
            for (int i = 0; i < s; i++)
            {
                AddRim(quad.X + i, quad.Z + s, height);
            }
            for (int i = 0; i < s; i++)
            {
                AddRim(quad.X + s, quad.Z + s - i, height);
            }
            for (int i = 0; i < s; i++)
            {
                AddRim(quad.X + s - i, quad.Z, height);
            }
            Vector2 still = Vector2.zero;
            Vector4 calm = Vector4.zero;
            if (rim.Count == 4)
            {
                sheet.Add(rim[0], rim[1], rim[2], calm, calm, calm, still, WaterSheetMesh.StillLook);
                sheet.Add(rim[0], rim[2], rim[3], calm, calm, calm, still, WaterSheetMesh.StillLook);
                return;
            }
            Vector3 centre = (rim[0] + Point(quad.X + s, quad.Z + s, height)) * 0.5f;
            for (int i = 0; i < rim.Count; i++)
            {
                sheet.Add(centre, rim[i], rim[(i + 1) % rim.Count], calm, calm, calm, still, WaterSheetMesh.StillLook);
            }
        }

        private void AddRim(int cx, int cz, float height)
        {
            if (corners[cx + cz * (water.Size.x + 1)])
            {
                rim.Add(Point(cx, cz, height));
            }
        }

        private Vector3 Point(int cx, int cz, float height)
        {
            return new Vector3(water.Origin.x + cx * water.CellSize, height, water.Origin.y + cz * water.CellSize);
        }
    }
}
