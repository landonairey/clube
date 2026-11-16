using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>What one cell of a <see cref="ResourceSurvey"/> holds.</summary>
    public struct SurveyCell
    {
        /// <summary>Mean ground height, in metres.</summary>
        public float Height;

        /// <summary>1 on level ground, 0 where the mean slope is 40% or more.</summary>
        public float Flatness;

        /// <summary>Index of the biome at the cell's middle, in <see cref="ResourceSurvey.Biomes"/>.</summary>
        public int Biome;

        /// <summary>Share of the cell that is dry land.</summary>
        public float Land;

        /// <summary>Share of the cell under the sea.</summary>
        public float Sea;

        /// <summary>Share of the cell under a river or a lake.</summary>
        public float FreshWater;

        /// <summary>True when a river runs through the cell.</summary>
        public bool River;

        /// <summary>True when the cell has both sea and land: a shore.</summary>
        public bool Coast;

        /// <summary>Trees standing in the cell (a count, from the trees' own placement rolls).</summary>
        public float Wood;

        /// <summary>Share of the land that is bare rock at the surface.</summary>
        public float Stone;

        /// <summary>Share of the land with sand at the surface (beaches, banks, deserts).</summary>
        public float Sand;

        /// <summary>Share of the land with clay at the surface (deposits and river banks).</summary>
        public float Clay;

        /// <summary>Limestone nodes in the cell: the ores whose material is named limestone (none until one is added).</summary>
        public float Limestone;

        /// <summary>How good the cell is for grazing, 0 to 1: its biomes' grazing, on flat dry land.</summary>
        public float Grazing;

        /// <summary>How much game the cell holds, 0 to 1, from its biomes.</summary>
        public float Game;

        /// <summary>
        /// True when the cell's middle lies past the playable edge (GL639) or inside an edge's limit (GL640), where the
        /// player is held back. Such a cell keeps its height, biome and
        /// sea, and holds nothing else: no land, fresh water or resources.
        /// </summary>
        public bool Outside;
    }

    /// <summary>
    /// What the whole bounded map holds, cell by cell on a coarse grid, without generating a voxel (GL592, ST1): each
    /// ore, wood, stone, sand, clay, grazing and game, fresh water, coast and river access, flatness and biome. Settlement
    /// placing (GL593 on) reads it.
    /// </summary>
    /// <remarks>
    /// <para>It agrees with the chunks because it asks the same code: ore nodes are <see cref="OreField"/>'s own tries
    /// and rule (<see cref="OreField.AppendTries"/>, <see cref="OreField.MarkPlaced"/>), trees are
    /// <see cref="TreeField.TryGetSpot"/> and <see cref="TreeField.IsFlatEnough"/>, clay is the fill kernel's
    /// <see cref="ChunkFillKernel.ClayDepthAt"/>, sand and bank clay are <see cref="WaterMap.MaterialAt"/>, bare rock is
    /// <see cref="LayerTable.IsSteep"/> and the biome is the fill's <see cref="BiomeField"/>.</para>
    /// <para>It stays fast by sampling the ground in Burst jobs, a few hundred thousand points at a time, rather than
    /// one managed call per point (placing ore nodes one by one over a 550 m square took seconds, architecture §7).
    /// Slopes are measured between samples <see cref="CellSize"/> / <see cref="SamplesPerSide"/> apart, so bare rock is
    /// an estimate; ore nodes are looked for from a little above the ground down to the deepest ore.</para>
    /// </remarks>
    public sealed class ResourceSurvey
    {
        /// <summary>Ground samples along each side of a cell.</summary>
        public const int SamplesPerSide = 4;

        // Slope at which a cell's flatness reaches 0.
        private const float SteepestFlat = 0.4f;

        // Most tree cells looked at along each side of a survey cell; more are sampled evenly and scaled up.
        private const int TreeCellsPerSide = 4;

        private readonly SurveyCell[] cells;
        private readonly float[][] ore;
        private readonly VoxelMaterial[] ores;

        private ResourceSurvey(Rect area, float cellSize, int size, IReadOnlyList<BiomeDefinition> biomes, VoxelMaterial[] ores)
        {
            Area = area;
            CellSize = cellSize;
            Size = size;
            Biomes = biomes;
            this.ores = ores;
            cells = new SurveyCell[size * size];
            ore = new float[ores.Length][];
            for (int i = 0; i < ores.Length; i++)
            {
                ore[i] = new float[size * size];
            }
        }

        /// <summary>The square surveyed (x and z): the map's bounds.</summary>
        public Rect Area { get; }

        /// <summary>Length of a cell's sides, in metres.</summary>
        public float CellSize { get; }

        /// <summary>Cells along each side of the grid.</summary>
        public int Size { get; }

        /// <summary>The biomes <see cref="SurveyCell.Biome"/> indexes.</summary>
        public IReadOnlyList<BiomeDefinition> Biomes { get; }

        /// <summary>The ores surveyed, in the order <see cref="Ore"/> indexes them (the terrain's ore specs).</summary>
        public IReadOnlyList<VoxelMaterial> Ores => ores;

        /// <summary>How long each part of the survey took, for the log.</summary>
        public string Timings { get; private set; }

        public SurveyCell Cell(int x, int z)
        {
            return cells[x + z * Size];
        }

        /// <summary>Nodes of an ore (an index in <see cref="Ores"/>) centred in a cell.</summary>
        public float Ore(int oreIndex, int x, int z)
        {
            return ore[oreIndex][x + z * Size];
        }

        /// <summary>Nodes of an ore, by its material, centred in a cell; 0 for a material that isn't an ore here.</summary>
        public float Ore(VoxelMaterial material, int x, int z)
        {
            int index = Array.IndexOf(ores, material);
            return index >= 0 && material != null ? ore[index][x + z * Size] : 0f;
        }

        /// <summary>The cell holding a position (x, z), or false outside the map.</summary>
        public bool TryCell(Vector2 position, out Vector2Int cell)
        {
            cell = new Vector2Int(
                Mathf.FloorToInt((position.x - Area.xMin) / CellSize), Mathf.FloorToInt((position.y - Area.yMin) / CellSize));
            return cell.x >= 0 && cell.y >= 0 && cell.x < Size && cell.y < Size;
        }

        /// <summary>The middle of a cell (x, z).</summary>
        public Vector2 CellCentre(int x, int z)
        {
            return new Vector2(Area.xMin + (x + 0.5f) * CellSize, Area.yMin + (z + 0.5f) * CellSize);
        }

        /// <summary>Surveys the map a terrain's settings give, building its generator, ores and trees first.</summary>
        public static ResourceSurvey Build(TerrainSettings terrain, float cellSize = 40f)
        {
            ITerrainGenerator generator = TerrainGenerators.Create(terrain);
            return Build(terrain, generator, OreField.Create(terrain, generator), TreeField.Create(terrain, generator), cellSize);
        }

        /// <summary>
        /// Surveys a bounded map with the generator, ores and trees its world uses (either may be null for none).
        /// </summary>
        /// <param name="cellSize">Length of a cell's sides in metres; 25 to 50 suits a 5 km map.</param>
        /// <exception cref="ArgumentException">The generator isn't a bounded map's.</exception>
        public static ResourceSurvey Build(
            TerrainSettings terrain, ITerrainGenerator generator, OreField oreField, TreeField trees, float cellSize = 40f)
        {
            if (!(generator is BoundedMapGenerator map))
            {
                throw new ArgumentException("The resource survey needs a bounded map's generator (TerrainGeneratorType.BoundedMap).", nameof(generator));
            }
            Rect area = map.Layout.Bounds;
            cellSize = Mathf.Max(4f, cellSize);
            int size = Mathf.CeilToInt(area.width / cellSize);
            var oreMaterials = new VoxelMaterial[oreField != null ? oreField.SpecCount : 0];
            for (int i = 0; i < oreMaterials.Length; i++)
            {
                oreMaterials[i] = oreField.OreOf(i);
            }
            var survey = new ResourceSurvey(area, cellSize, size, map.Biomes.Biomes, oreMaterials);
            ChunkFillSettings fill = ChunkGenerator.SettingsFor(terrain.Layers, oreField, DensityFormat.Byte, terrain.StartArea, generator);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            survey.SurveyGround(map, fill);
            long ground = watch.ElapsedMilliseconds;
            survey.SurveyWater(map.Water);
            long water = watch.ElapsedMilliseconds;
            if (trees != null)
            {
                survey.SurveyTrees(map, trees);
            }
            long treesDone = watch.ElapsedMilliseconds;
            if (oreField != null)
            {
                survey.SurveyOres(map, oreField);
            }
            survey.MarkOutside(map.Layout);
            survey.Timings = $"ground {ground} ms, water {water - ground} ms, trees {treesDone - water} ms, ores {watch.ElapsedMilliseconds - treesDone} ms";
            return survey;
        }

        // Past the playable edge (GL639), and where an edge's limit holds the player back (GL640), nothing is the map's to use: those cells keep their height, biome and sea,
        // and hold no land, fresh water, wood, game, grazing or ore, so nothing is sited on them or counts them.
        private void MarkOutside(MapLayout layout)
        {
            for (int cz = 0; cz < Size; cz++)
            {
                for (int cx = 0; cx < Size; cx++)
                {
                    Vector2 centre = CellCentre(cx, cz);
                    if (layout.WalksFreely(centre.x, centre.y))
                    {
                        continue;
                    }
                    ref SurveyCell cell = ref cells[cx + cz * Size];
                    cell.Outside = true;
                    cell.Land = 0f;
                    cell.FreshWater = 0f;
                    cell.River = false;
                    cell.Coast = false;
                    cell.Wood = 0f;
                    cell.Limestone = 0f;
                    cell.Grazing = 0f;
                    cell.Game = 0f;
                    cell.Flatness = 0f;
                    foreach (float[] counts in ore)
                    {
                        counts[cx + cz * Size] = 0f;
                    }
                }
            }
        }

        // Height, flatness, biome and the surface materials, from a grid of ground samples in each cell.
        private void SurveyGround(BoundedMapGenerator map, ChunkFillSettings fill)
        {
            const int s = SamplesPerSide;
            float spacing = CellSize / s;
            int side = Size * s;
            float[] heights = Heights(map, side * side, i => new float2(
                Area.xMin + (i % side + 0.5f) * spacing, Area.yMin + (i / side + 0.5f) * spacing));
            BiomeField biomes = map.BiomeField;
            WaterMap water = map.Water;
            LayerTable layers = fill.Layers;
            byte sand = water.Banks.Sand, clay = layers.Clay != 0 ? layers.Clay : water.Banks.Clay;
            for (int cz = 0; cz < Size; cz++)
            {
                for (int cx = 0; cx < Size; cx++)
                {
                    var cell = new SurveyCell();
                    float slope = 0f;
                    int dry = 0;
                    for (int sz = 0; sz < s; sz++)
                    {
                        for (int sx = 0; sx < s; sx++)
                        {
                            int x = cx * s + sx, z = cz * s + sz;
                            float h = heights[x + z * side];
                            // Slope to the next sample each way (the one before on the grid's far edges).
                            float hx = heights[(x + 1 < side ? x + 1 : x - 1) + z * side];
                            float hz = heights[x + (z + 1 < side ? z + 1 : z - 1) * side];
                            float gradient = math.length(new float2(hx - h, hz - h)) / spacing;
                            cell.Height += h;
                            slope += gradient;

                            var p = new float2(Area.xMin + (x + 0.5f) * spacing, Area.yMin + (z + 0.5f) * spacing);
                            if (water.TryCell(p, out int wet) && water.Kind[wet] != 0)
                            {
                                continue;
                            }
                            dry++;
                            BiomeRow row = biomes.Rows[biomes.IndexAt(p.x, p.y, h)];
                            BiomeDefinition biome = Biomes[biomes.IndexAt(p.x, p.y, h)];
                            cell.Grazing += biome.Grazing;
                            cell.Game += biome.Game;
                            // The surface material the fill would lay: bare rock, else clay, else the water's banks, else the biome's top.
                            byte banks = water.MaterialAt(p, 0.05f, fill.Seed);
                            if (layers.IsSteep(gradient, h) || (row.Top == layers.Base && layers.Base != layers.Top))
                            {
                                cell.Stone++;
                            }
                            else if (banks == sand && sand != 0)
                            {
                                cell.Sand++;
                            }
                            else if ((banks != WaterMap.Keep && banks == clay && clay != 0) || ChunkFillKernel.ClayDepthAt(fill.Seed, p, layers) > 0f)
                            {
                                cell.Clay++;
                            }
                            else if (row.Top == sand && sand != 0)
                            {
                                cell.Sand++;
                            }
                        }
                    }
                    cell.Height /= s * s;
                    cell.Flatness = 1f - Mathf.Clamp01(slope / (s * s) / SteepestFlat);
                    Vector2 centre = CellCentre(cx, cz);
                    cell.Biome = biomes.IndexAt(centre.x, centre.y, cell.Height);
                    if (dry > 0)
                    {
                        cell.Stone /= dry;
                        cell.Sand /= dry;
                        cell.Clay /= dry;
                        cell.Grazing = cell.Grazing / dry * cell.Flatness;
                        cell.Game /= dry;
                    }
                    cells[cx + cz * Size] = cell;
                }
            }
        }

        // Sea, rivers and lakes: the water map's own cells under each survey cell (every other one on fine maps).
        private void SurveyWater(WaterMap water)
        {
            if (!water.IsCreated)
            {
                for (int i = 0; i < cells.Length; i++)
                {
                    cells[i].Land = 1f;
                }
                return;
            }
            float step = water.CellSize * Mathf.Max(1, Mathf.RoundToInt(2f / water.CellSize));
            int per = Mathf.Max(1, Mathf.FloorToInt(CellSize / step));
            for (int cz = 0; cz < Size; cz++)
            {
                for (int cx = 0; cx < Size; cx++)
                {
                    int sea = 0, river = 0, lake = 0;
                    for (int j = 0; j < per; j++)
                    {
                        for (int i = 0; i < per; i++)
                        {
                            var p = new float2(Area.xMin + cx * CellSize + (i + 0.5f) * step, Area.yMin + cz * CellSize + (j + 0.5f) * step);
                            if (!water.TryCell(p, out int w) || water.Kind[w] == 0)
                            {
                                continue;
                            }
                            if (water.Kind[w] == (byte)WaterKind.River)
                            {
                                river++;
                            }
                            else if (water.IsSea(w))
                            {
                                sea++;
                            }
                            else
                            {
                                lake++;
                            }
                        }
                    }
                    float total = per * per;
                    ref SurveyCell cell = ref cells[cx + cz * Size];
                    cell.Sea = sea / total;
                    cell.FreshWater = (river + lake) / total;
                    cell.Land = 1f - cell.Sea - cell.FreshWater;
                    cell.River = river > 0;
                    cell.Coast = sea > 0 && cell.Land > 0.02f;
                }
            }
        }

        // Trees: the tree cells' own rolls and flatness rule, with the ground under every candidate from one job.
        private void SurveyTrees(BoundedMapGenerator map, TreeField trees)
        {
            float treeCell = trees.CellSize;
            int perSide = Mathf.Max(1, Mathf.RoundToInt(CellSize / treeCell));
            int stride = Mathf.Max(1, perSide / TreeCellsPerSide);
            float probe = TreeField.FlatnessProbe;
            var spots = new List<Vector2>();
            var owners = new List<int>();
            for (int cz = 0; cz < Size; cz++)
            {
                for (int cx = 0; cx < Size; cx++)
                {
                    if (cells[cx + cz * Size].Land <= 0f)
                    {
                        continue;
                    }
                    int tx0 = Mathf.FloorToInt((Area.xMin + cx * CellSize) / treeCell), tz0 = Mathf.FloorToInt((Area.yMin + cz * CellSize) / treeCell);
                    for (int tz = 0; tz < perSide; tz += stride)
                    {
                        for (int tx = 0; tx < perSide; tx += stride)
                        {
                            if (trees.TryGetSpot(new Vector2Int(tx0 + tx, tz0 + tz), out Vector2 spot))
                            {
                                spots.Add(spot);
                                owners.Add(cx + cz * Size);
                            }
                        }
                    }
                }
            }
            float[] ground = Heights(map, spots.Count * 5, i =>
            {
                Vector2 spot = spots[i / 5];
                switch (i % 5)
                {
                    case 1: return new float2(spot.x + probe, spot.y);
                    case 2: return new float2(spot.x - probe, spot.y);
                    case 3: return new float2(spot.x, spot.y + probe);
                    case 4: return new float2(spot.x, spot.y - probe);
                    default: return new float2(spot.x, spot.y);
                }
            });
            // Each tree cell looked at stands for the stride x stride it was picked from.
            int sampled = 0;
            for (int t = 0; t < perSide; t += stride)
            {
                sampled++;
            }
            float each = (float)(perSide * perSide) / (sampled * sampled);
            for (int i = 0; i < spots.Count; i++)
            {
                if (trees.IsFlatEnough(ground[i * 5], ground[i * 5 + 1], ground[i * 5 + 2], ground[i * 5 + 3], ground[i * 5 + 4]))
                {
                    cells[owners[i]].Wood += each;
                }
            }
        }

        // Ore nodes: every try the ore field would make between the ground and the deepest ore, the ground over all of a
        // band's tries from one job, then the field's own rule for which become nodes.
        private void SurveyOres(BoundedMapGenerator map, OreField oreField)
        {
            float oreCell = oreField.CellSize;
            int x0 = Mathf.FloorToInt(Area.xMin / oreCell), z0 = Mathf.FloorToInt(Area.yMin / oreCell);
            int nx = Mathf.CeilToInt(Area.xMax / oreCell) - x0, nz = Mathf.CeilToInt(Area.yMax / oreCell) - z0;
            // The ground at every ore column's corners gives the heights its nodes can lie between.
            float[] corners = Heights(map, (nx + 1) * (nz + 1), i => new float2((x0 + i % (nx + 1)) * oreCell, (z0 + i / (nx + 1)) * oreCell));
            float deepest = oreField.MaxNodeDepth;
            int limestone = -1;
            for (int i = 0; i < ores.Length; i++)
            {
                if (ores[i] != null && ores[i].name.IndexOf("limestone", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    limestone = i;
                }
            }

            const int band = 16;
            var tries = new List<OreTry>();
            var firsts = new List<int>();
            var placed = new List<int>();
            for (int bz = 0; bz < nz; bz += band)
            {
                tries.Clear();
                firsts.Clear();
                for (int z = bz; z < Mathf.Min(nz, bz + band); z++)
                {
                    for (int x = 0; x < nx; x++)
                    {
                        int c = x + z * (nx + 1);
                        float a = corners[c], b = corners[c + 1], d = corners[c + nx + 1], e = corners[c + nx + 2];
                        float low = Mathf.Min(Mathf.Min(a, b), Mathf.Min(d, e)), high = Mathf.Max(Mathf.Max(a, b), Mathf.Max(d, e));
                        // A margin for ground that dips or rises between the corners.
                        float margin = 2f + (high - low) * 0.25f;
                        int y0 = Mathf.FloorToInt((low - margin - deepest) / oreCell), y1 = Mathf.FloorToInt((high + margin) / oreCell);
                        for (int y = y0; y <= y1; y++)
                        {
                            firsts.Add(tries.Count);
                            oreField.AppendTries(new Vector3Int(x0 + x, y, z0 + z), tries);
                        }
                    }
                }
                firsts.Add(tries.Count);
                var ground = new NativeArray<float>(tries.Count, Allocator.TempJob);
                var points = new NativeArray<float2>(tries.Count, Allocator.TempJob);
                try
                {
                    for (int i = 0; i < tries.Count; i++)
                    {
                        points[i] = new float2(tries[i].Centre.x, tries[i].Centre.z);
                    }
                    map.SchedulePointHeights(points, ground).Complete();
                    placed.Clear();
                    for (int k = 0; k + 1 < firsts.Count; k++)
                    {
                        oreField.MarkPlaced(tries, firsts[k], firsts[k + 1] - firsts[k], ground, 0, placed);
                    }
                    foreach (int index in placed)
                    {
                        OreTry node = tries[index];
                        if (TryCell(new Vector2(node.Centre.x, node.Centre.z), out Vector2Int cell))
                        {
                            ore[node.Spec][cell.x + cell.y * Size]++;
                            if (node.Spec == limestone)
                            {
                                cells[cell.x + cell.y * Size].Limestone++;
                            }
                        }
                    }
                }
                finally
                {
                    ground.Dispose();
                    points.Dispose();
                }
            }
        }

        // The ground's height at a list of points, from one Burst job.
        private static float[] Heights(HeightfieldGenerator ground, int count, Func<int, float2> point)
        {
            var points = new NativeArray<float2>(count, Allocator.TempJob);
            var heights = new NativeArray<float>(count, Allocator.TempJob);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    points[i] = point(i);
                }
                ground.SchedulePointHeights(points, heights).Complete();
                return heights.ToArray();
            }
            finally
            {
                points.Dispose();
                heights.Dispose();
            }
        }
    }
}
