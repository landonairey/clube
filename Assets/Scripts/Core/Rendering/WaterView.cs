using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Core
{
    /// <summary>
    /// The rivers and lakes (GL309, GL315) of the <see cref="WorldView"/>'s ground (<see cref="HeightfieldGenerator.Water"/>).
    /// Water lives in the voxel data: each chunk that loads gets the map's water in its samples (<see cref="WaterFill"/>),
    /// <see cref="Live"/> moves it (water falls down drops and runs downhill into dug holes; placed ground replaces it), and
    /// every chunk holding water gets a mesh of its surface (<see cref="WaterMesher"/>: marching cubes, the terrain's own
    /// facets), rebuilt when its water changes. Past the chunks the world draws, the map's water is drawn as sheets over
    /// the coarse terrain, in two layers (GL626): within <see cref="farDetailMetres"/> of the viewer, tiles of two
    /// triangles per map cell (<see cref="WaterSheetMesh.AddCell"/>), which ripple; past it, a few big meshes built once
    /// (<see cref="CoarseWaterMesher"/>: level water as large sheets, single cells only along shores and rivers). The
    /// shader draws the first inside a square round the viewer and the second outside it, so nothing is rebuilt as the
    /// viewer moves but the handful of tiles entering the square. All are drawn in the <c>Clube/Water</c> shader.
    /// </summary>
    [RequireComponent(typeof(WorldView))]
    public sealed class WaterView : MonoBehaviour
    {
        private const int Tile = 32;
        // Cells along a coarse region, the largest single sheet (512 m of 2 m cells).
        private const int Region = 256;

        private static readonly Vector3Int[] Faces =
        {
            Vector3Int.up, Vector3Int.down, Vector3Int.right, Vector3Int.left, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        [SerializeField]
        private Material material;

        [SerializeField]
        private bool show = true;

        [Tooltip("Seconds between steps of the live water (GL313, GL315): water moves a sample a step.")]
        [SerializeField, Range(0.02f, 0.5f)]
        private float flowStepSeconds = 0.06f;

        [Tooltip("Milliseconds a flow step may take; a big spill moves more slowly rather than stalling the frame.")]
        [SerializeField, Range(0.5f, 10f)]
        private float flowBudgetMs = 2.5f;

        [Tooltip("The gentlest ground water runs down, as fall over distance (0.25 is about 14°); gentler ground holds puddles (GL332).")]
        [SerializeField, Range(0.05f, 1f)]
        private float minFlowSlope = 0.25f;

        [Tooltip("Milliseconds a frame may spend building water meshes (chunks nearest first, then far tiles); at least one is built.")]
        [SerializeField, Range(0.25f, 8f)]
        private float buildBudgetMs = 1.5f;

        [Tooltip("Draw the far water sheets (the map's rivers and lakes beyond the loaded chunks). Off for a small fixed area such as the title diorama, so no water floats outside it (GL536).")]
        [SerializeField]
        private bool drawFarWater = true;

        [Tooltip("Far water within this many metres of the viewer keeps two triangles per map cell and its ripples; past it, level water is drawn as big sheets (GL626). The chunks' own water, nearer still, is not affected.")]
        [SerializeField, Range(0f, 1024f)]
        private float farDetailMetres = 256f;

        private WorldView worldView;
        private ITerrainGenerator builtFor;
        private WaterMap water;
        private World filledWorld;
        private int tilesAcross;
        // Far water past the detail square: one mesh per region of the map, built once.
        private CoarseWaterMesher coarse;
        private readonly List<GameObject> regions = new List<GameObject>();
        private readonly List<int> dirtyRegions = new List<int>();
        private readonly List<int> droppedTiles = new List<int>();
        private Vector2Int detailTile;
        private bool detailTilesKnown;
        private Material detailMaterial;
        private Material coarseMaterial;
        private int drawVersion = -1;
        private readonly Dictionary<int, GameObject> tiles = new Dictionary<int, GameObject>();
        private readonly HashSet<int> dirtyTiles = new HashSet<int>();
        // Far tiles holding any water to draw.
        private readonly HashSet<int> wetTiles = new HashSet<int>();
        private readonly Dictionary<Vector3Int, GameObject> chunkMeshes = new Dictionary<Vector3Int, GameObject>();
        private readonly HashSet<Vector3Int> dirtyChunks = new HashSet<Vector3Int>();
        // Loaded chunks holding water.
        private readonly HashSet<Vector3Int> wetChunks = new HashSet<Vector3Int>();
        private readonly System.Diagnostics.Stopwatch budget = new System.Diagnostics.Stopwatch();
        private readonly System.Diagnostics.Stopwatch frameTimer = new System.Diagnostics.Stopwatch();
        private readonly List<Vector3Int> changed = new List<Vector3Int>();
        private readonly WaterSheetMesh sheet = new WaterSheetMesh();
        private readonly WaterMesher mesher = new WaterMesher();
        private float nextFlowStep;
        // Every water mesh hangs under this, so Show switches one object.
        private GameObject holder;

        /// <summary>Triangles drawn, for tests and the lab.</summary>
        public int TriangleCount { get; private set; }

        /// <summary>Milliseconds the last LateUpdate took (flow, fills, mesh builds), for timing.</summary>
        public float LastUpdateMilliseconds { get; private set; }

        /// <summary>Chunk water meshes and far water meshes (detailed tiles, coarse regions) waiting to be built.</summary>
        public int PendingChunks => dirtyChunks.Count;

        public int PendingTiles => dirtyTiles.Count + dirtyRegions.Count;

        /// <summary>Triangles in the far water's detailed tiles, its coarse regions, and the sea past the map.</summary>
        public int DetailTriangleCount => Triangles(tiles.Values);

        public int CoarseTriangleCount => Triangles(regions);

        private static int Triangles(IEnumerable<GameObject> objects)
        {
            int count = 0;
            foreach (GameObject go in objects)
            {
                count += (int)go.GetComponent<MeshFilter>().sharedMesh.GetIndexCount(0) / 3;
            }
            return count;
        }

        /// <summary>Chunks with a water mesh.</summary>
        public int ChunkMeshCount => chunkMeshes.Count;

        /// <summary>The water that moves (GL313, GL315); null until the world and its water exist.</summary>
        public LiveWater Live { get; private set; }

        /// <summary>The map of rivers and lakes the water came from.</summary>
        public WaterMap Map => water;

        public bool Show
        {
            get => show;
            set => show = value;
        }

        private void Awake()
        {
            worldView = GetComponent<WorldView>();
        }

        private void OnEnable()
        {
            worldView ??= GetComponent<WorldView>();
            worldView.ChunkLoaded += OnChunkLoaded;
            worldView.ChunkUnloaded += OnChunkUnloaded;
        }

        private void OnDisable()
        {
            worldView.ChunkLoaded -= OnChunkLoaded;
            worldView.ChunkUnloaded -= OnChunkUnloaded;
        }

        private void LateUpdate()
        {
            frameTimer.Restart();
            ITerrainGenerator generator = worldView.Generator;
            if (generator != builtFor)
            {
                builtFor = generator;
                Reset(generator is HeightfieldGenerator heightfield ? heightfield.Water : default);
                BuildOuterSea(generator as BoundedMapGenerator);
            }
            World world = worldView.World;
            if (water.IsCreated && world != null && (Live == null || Live.World != world))
            {
                StartLive(world);
            }
            if (Live != null && Time.time >= nextFlowStep)
            {
                nextFlowStep = Time.time + flowStepSeconds;
                if (worldView.Focus != null)
                {
                    Live.Focus = world.Grid.WorldToNearestSample(transform.InverseTransformPoint(worldView.Focus.position));
                }
                Live.MinFlowSlope = minFlowSlope;
                WaterGeneration settings = worldView.Config.Terrain.Water;
                Live.SoilWater = settings.SoilWater;
                Live.HoldSoilWaterFull = settings.HoldSoilWaterFull;
                Live.Step(budgetMs: flowBudgetMs);
            }
            if (world != null)
            {
                changed.Clear();
                world.CollectWaterChanged(changed);
                foreach (Vector3Int coord in changed)
                {
                    dirtyChunks.Add(coord);
                    if (world.TryGetChunk(coord, out Chunk chunk) && chunk.HasWater)
                    {
                        wetChunks.Add(coord);
                    }
                }
            }
            if (water.IsCreated && world != null && worldView.DrawVersion != drawVersion)
            {
                drawVersion = worldView.DrawVersion;
                RecheckDrawnChunks(world);
            }
            budget.Restart();
            RebuildChunks();
            UpdateDetailTiles();
            RebuildRegions();
            UpdateFarClip(world);
            if (holder != null && holder.activeSelf != show)
            {
                holder.SetActive(show);
            }
            LastUpdateMilliseconds = (float)frameTimer.Elapsed.TotalMilliseconds;
        }


        private static readonly int CoverageMaskId = Shader.PropertyToID("_CoverageMask");
        private static readonly int CoverageId = Shader.PropertyToID("_Coverage");
        private static readonly int ClipCoveredId = Shader.PropertyToID("_ClipCovered");
        private static readonly int DetailId = Shader.PropertyToID("_Detail");
        private ChunkCoverageMask coverage;
        private Material farMaterial;

        // The far sheets are clipped per pixel wherever the world draws a chunk column in full (GL323), like the far
        // terrain: there the chunks' own water is drawn. So the sheets are built once and never stand in near the player.
        private void UpdateFarClip(World world)
        {
            if (world == null || farMaterial == null)
            {
                return;
            }
            coverage ??= new ChunkCoverageMask(worldView, WorldView.MaxRenderDistance);
            Vector3Int focusChunk = worldView.FocusChunk;
            coverage.Update(new Vector2Int(focusChunk.x, focusChunk.z));
            float chunkWidth = world.Grid.ChunkWorldSize.x;
            Vector3 origin = worldView.transform.position;
            var area = new Vector4(
                origin.x + coverage.Origin.x * chunkWidth, origin.z + coverage.Origin.y * chunkWidth, chunkWidth, coverage.Size);
            // The detail square, in world space (the sheets hang under this object, which is not turned or scaled).
            Vector2 centre = DetailCentre();
            Vector3 middle = transform.TransformPoint(new Vector3(centre.x, 0f, centre.y));
            foreach (Material far in new[] { farMaterial, detailMaterial, coarseMaterial })
            {
                far.SetTexture(CoverageMaskId, coverage.Texture);
                far.SetVector(CoverageId, area);
            }
            detailMaterial.SetVector(DetailId, new Vector4(middle.x, middle.z, farDetailMetres, 1f));
            coarseMaterial.SetVector(DetailId, new Vector4(middle.x, middle.z, farDetailMetres, 2f));
        }

        private void OnDestroy()
        {
            coverage?.Dispose();
            foreach (Material far in new[] { farMaterial, detailMaterial, coarseMaterial })
            {
                if (far != null)
                {
                    Destroy(far);
                }
            }
            if (Live != null)
            {
                Live.World.DensityChanged -= OnDensityChanged;
                Live.Dispose();
            }
            Clear();
        }

        private void Clear()
        {
            foreach (GameObject tile in tiles.Values)
            {
                DestroyMeshObject(tile);
            }
            foreach (GameObject chunk in chunkMeshes.Values)
            {
                DestroyMeshObject(chunk);
            }
            foreach (GameObject region in regions)
            {
                DestroyMeshObject(region);
            }
            DestroyMeshObject(outerSea);
            outerSea = null;
            regions.Clear();
            dirtyRegions.Clear();
            coarse = null;
            detailTilesKnown = false;
            tiles.Clear();
            chunkMeshes.Clear();
            dirtyTiles.Clear();
            dirtyChunks.Clear();
            wetChunks.Clear();
            TriangleCount = 0;
        }

        private void DestroyMeshObject(GameObject go)
        {
            if (go == null)
            {
                return;
            }
            Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
            TriangleCount -= mesh != null ? (int)mesh.GetIndexCount(0) / 3 : 0;
            Destroy(mesh);
            Destroy(go);
        }

        private void Reset(WaterMap map)
        {
            Clear();
            if (Live != null)
            {
                Live.World.DensityChanged -= OnDensityChanged;
                Live.Dispose();
            }
            Live = null;
            filledWorld = null;
            water = map;
            if (!water.IsCreated || material == null)
            {
                water = default;
                return;
            }
            int2 size = water.Size;
            tilesAcross = (size.x + Tile - 1) / Tile;
            if (farMaterial == null)
            {
                farMaterial = new Material(material) { name = material.name + " (far)" };
                farMaterial.SetFloat(ClipCoveredId, 1f);
                // The sea past the map: level like the coarse sheets it meets, and drawn everywhere.
                farMaterial.SetVector(DetailId, new Vector4(0f, 0f, 0f, 3f));
                detailMaterial = new Material(farMaterial) { name = material.name + " (far, detailed)" };
                coarseMaterial = new Material(farMaterial) { name = material.name + " (far, coarse)" };
            }
            wetTiles.Clear();
            if (!drawFarWater)
            {
                return;
            }
            for (int i = 0; i < water.Kind.Length; i++)
            {
                if (water.Kind[i] != (byte)WaterKind.Dry)
                {
                    wetTiles.Add(i % size.x / Tile + i / size.x / Tile * tilesAcross);
                }
            }
            coarse = new CoarseWaterMesher(water, Region);
            for (int region = 0; region < coarse.RegionCount; region++)
            {
                if (coarse.HasWater(region))
                {
                    dirtyRegions.Add(region);
                }
            }
        }

        private GameObject outerSea;

        // The sea past the water map (GL600): a bounded map's ocean goes on to the horizon as one flat mesh at sea level,
        // in cells that grow with the distance, wherever the map's shape says the low ground is sea (not past a void
        // cliff, a desert or a ridge). The water map's own cells draw the sea inside it.
        private void BuildOuterSea(BoundedMapGenerator map)
        {
            if (map == null || !water.IsCreated || !water.HasSea || !drawFarWater || farMaterial == null)
            {
                return;
            }
            const float firstStep = 256f, fineReach = 4096f, reach = 40000f;
            var offsets = new List<float> { 0f };
            for (float d = 0f, step = firstStep; d < reach; step = d >= fineReach ? step * 2f : step)
            {
                d += step;
                offsets.Add(d);
            }
            float2 min = water.Origin, max = water.Origin + new float2(water.Size.x, water.Size.y) * water.CellSize;
            // Breakpoints along an axis: outwards from the water map's two edges, the map itself one span in the middle.
            List<float> Axis(float low, float high)
            {
                var points = new List<float>();
                for (int i = offsets.Count - 1; i >= 0; i--)
                {
                    points.Add(low - offsets[i]);
                }
                foreach (float offset in offsets)
                {
                    points.Add(high + offset);
                }
                return points;
            }
            List<float> xs = Axis(min.x, max.x), zs = Axis(min.y, max.y);
            int middle = offsets.Count - 1;
            MapShape shape = map.Layout.Shape;
            float y = water.SeaLevel;
            sheet.Clear();
            for (int z = 0; z + 1 < zs.Count; z++)
            {
                for (int x = 0; x + 1 < xs.Count; x++)
                {
                    if (x == middle && z == middle)
                    {
                        continue;
                    }
                    float x0 = xs[x], x1 = xs[x + 1], z0 = zs[z], z1 = zs[z + 1];
                    // Judged at the cell's point nearest the map, so the sea starts where the water map's sea stops.
                    float px = Mathf.Clamp(0f, x0, x1), pz = Mathf.Clamp(0f, z0, z1);
                    if (!shape.IsSeaSide(px, pz))
                    {
                        continue;
                    }
                    Vector3 a = new Vector3(x0, y, z0), b = new Vector3(x1, y, z0), c = new Vector3(x1, y, z1), d = new Vector3(x0, y, z1);
                    sheet.Add(a, d, c, Vector4.zero, Vector4.zero, Vector4.zero, Vector2.zero, 2f);
                    sheet.Add(a, c, b, Vector4.zero, Vector4.zero, Vector4.zero, Vector2.zero, 2f);
                }
            }
            if (sheet.Triangles.Count == 0)
            {
                return;
            }
            outerSea = MakeMeshObject("Outer sea", SheetToMesh("Outer sea"), farMaterial);
        }

        // The live water starts on a world whose chunks may already be loaded: they get their water first.
        private void StartLive(World world)
        {
            if (Live != null)
            {
                Live.World.DensityChanged -= OnDensityChanged;
                Live.Dispose();
            }
            Live = new LiveWater(world, water, worldView.Config.IsoLevel);
            world.DensityChanged += OnDensityChanged;
            if (filledWorld != world)
            {
                filledWorld = world;
                foreach (Vector3Int coord in new List<Vector3Int>(world.Chunks.Keys))
                {
                    OnChunkLoaded(coord);
                }
            }
        }

        // Digging or building beside water changes where the surface meets the ground.
        private void OnDensityChanged(Vector3Int sample)
        {
            World world = worldView.World;
            if (world != null && world.TryGetOwner(sample, out Chunk chunk, out _) && chunk.HasWater)
            {
                dirtyChunks.Add(world.Grid.VoxelToChunk(sample));
            }
        }

        // A chunk that loads gets the map's water; it and the neighbours whose edge it touches are drawn again.
        private void OnChunkLoaded(Vector3Int coord)
        {
            World world = worldView.World;
            if (world == null || !water.IsCreated || Live == null)
            {
                // Chunks loaded before the live water starts are filled when it does.
                return;
            }
            WaterFill.Fill(world, coord, water, worldView.Config.IsoLevel);
            if (world.IsEdited(coord))
            {
                Live.WakeChunk(coord);
            }
            if (world.TryGetChunk(coord, out Chunk chunk) && chunk.HasWater)
            {
                wetChunks.Add(coord);
                dirtyChunks.Add(coord);
            }
            foreach (Vector3Int step in Faces)
            {
                if (world.TryGetChunk(coord + step, out Chunk next) && next.HasWater)
                {
                    dirtyChunks.Add(coord + step);
                }
            }

        }

        private void OnChunkUnloaded(Vector3Int coord)
        {
            dirtyChunks.Remove(coord);
            wetChunks.Remove(coord);
            if (chunkMeshes.TryGetValue(coord, out GameObject go))
            {
                DestroyMeshObject(go);
                chunkMeshes.Remove(coord);
            }

        }

        // Builds the dirty chunks' water nearest the focus first, until the frame's budget is spent. Chunks in columns
        // the world doesn't draw get no mesh: the far sheets show their water.
        private void RebuildChunks()
        {
            World world = worldView.World;
            if (dirtyChunks.Count == 0 || world == null)
            {
                return;
            }
            Vector3 focus = worldView.Focus != null ? worldView.Focus.position : transform.position;
            Vector3Int focusChunk = world.Grid.WorldToChunk(transform.InverseTransformPoint(focus));
            int built = 0;
            while (dirtyChunks.Count > 0 && (built == 0 || budget.Elapsed.TotalMilliseconds < buildBudgetMs))
            {
                Vector3Int nearest = default;
                int best = int.MaxValue;
                foreach (Vector3Int coord in dirtyChunks)
                {
                    Vector3Int d = coord - focusChunk;
                    int distance = d.x * d.x + d.y * d.y + d.z * d.z;
                    if (distance < best)
                    {
                        best = distance;
                        nearest = coord;
                    }
                }
                dirtyChunks.Remove(nearest);
                if (!IsDrawn(nearest))
                {
                    DropChunkMesh(nearest);
                    continue;
                }
                BuildChunk(world, nearest);
                built++;
            }
        }

        private bool IsDrawn(Vector3Int coord)
        {
            return worldView.IsColumnDrawn(new Vector2Int(coord.x, coord.z));
        }

        private void DropChunkMesh(Vector3Int coord)
        {
            if (chunkMeshes.TryGetValue(coord, out GameObject go))
            {
                DestroyMeshObject(go);
                chunkMeshes.Remove(coord);
            }
        }

        // The drawn area moved: wet chunks coming into it get meshes, those leaving it lose theirs.
        private void RecheckDrawnChunks(World world)
        {
            foreach (Vector3Int coord in wetChunks)
            {
                bool drawn = IsDrawn(coord);
                if (drawn && !chunkMeshes.ContainsKey(coord))
                {
                    dirtyChunks.Add(coord);
                }
                else if (!drawn)
                {
                    DropChunkMesh(coord);
                    dirtyChunks.Remove(coord);
                }
            }
        }

        private void BuildChunk(World world, Vector3Int coord)
        {
            if (chunkMeshes.TryGetValue(coord, out GameObject old))
            {
                DestroyMeshObject(old);
                chunkMeshes.Remove(coord);
            }
            if (!world.IsLoaded(coord) || !mesher.Build(world, coord, worldView.Config.IsoLevel, water))
            {
                return;
            }
            var mesh = new Mesh { name = "Water", indexFormat = mesher.Vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(mesher.Vertices);
            mesh.SetUVs(0, mesher.Flows);
            mesh.SetUVs(1, mesher.Looks);
            mesh.SetUVs(2, mesher.Ripples);
            mesh.SetUVs(3, mesher.Banks);
            mesh.SetTriangles(mesher.Triangles, 0);
            mesh.RecalculateNormals();
            GameObject go = MakeMeshObject("Water chunk", mesh, material);
            go.transform.position = world.Grid.ChunkOrigin(coord);
            chunkMeshes[coord] = go;
            TriangleCount += mesher.Triangles.Count / 3;
        }

        // Far water, coarse (GL626): the whole map's water in a few big meshes, built once (CoarseWaterMesher). They
        // take what's left of the frame's budget after the chunks and the detailed tiles (one at least).
        private void RebuildRegions()
        {
            bool first = dirtyChunks.Count == 0 && dirtyTiles.Count == 0;
            while (dirtyRegions.Count > 0 && (budget.Elapsed.TotalMilliseconds < buildBudgetMs || first))
            {
                first = false;
                int region = dirtyRegions[dirtyRegions.Count - 1];
                dirtyRegions.RemoveAt(dirtyRegions.Count - 1);
                sheet.Clear();
                coarse.Build(region, sheet);
                if (sheet.Triangles.Count > 0)
                {
                    regions.Add(MakeMeshObject("Far water region", SheetToMesh("Far water (coarse)"), coarseMaterial));
                }
            }
        }

        // Far water, detailed: the tiles within the detail square round the viewer (and a tile's width past it, so a
        // tile is there before the square reaches it) are two triangles per cell and ripple; the rest are dropped.
        private void UpdateDetailTiles()
        {
            if (!water.IsCreated || !drawFarWater || worldView.World == null || coarse == null)
            {
                return;
            }
            float tileWidth = Tile * water.CellSize;
            Vector2 centre = DetailCentre();
            float2 local = (new float2(centre.x, centre.y) - water.Origin) / tileWidth;
            var at = new Vector2Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y));
            if (at != detailTile || !detailTilesKnown)
            {
                detailTile = at;
                detailTilesKnown = true;
                int reach = Mathf.CeilToInt(farDetailMetres / tileWidth) + 1;
                int tilesDeep = (water.Size.y + Tile - 1) / Tile;
                dirtyTiles.Clear();
                for (int z = Mathf.Max(0, at.y - reach); z <= Mathf.Min(tilesDeep - 1, at.y + reach); z++)
                {
                    for (int x = Mathf.Max(0, at.x - reach); x <= Mathf.Min(tilesAcross - 1, at.x + reach); x++)
                    {
                        int tile = x + z * tilesAcross;
                        if (wetTiles.Contains(tile) && !tiles.ContainsKey(tile))
                        {
                            dirtyTiles.Add(tile);
                        }
                    }
                }
                droppedTiles.Clear();
                foreach (int tile in tiles.Keys)
                {
                    if (Mathf.Abs(tile % tilesAcross - at.x) > reach + 1 || Mathf.Abs(tile / tilesAcross - at.y) > reach + 1)
                    {
                        droppedTiles.Add(tile);
                    }
                }
                foreach (int tile in droppedTiles)
                {
                    DestroyMeshObject(tiles[tile]);
                    tiles.Remove(tile);
                }
            }
            bool first = dirtyChunks.Count == 0;
            while (dirtyTiles.Count > 0 && (budget.Elapsed.TotalMilliseconds < buildBudgetMs || first))
            {
                first = false;
                int tile = -1;
                foreach (int dirty in dirtyTiles)
                {
                    tile = dirty;
                    break;
                }
                dirtyTiles.Remove(tile);
                BuildTile(tile);
            }
        }

        // The middle of the detail square, in the water map's space: under the viewer.
        private Vector2 DetailCentre()
        {
            Vector3 focus = worldView.Focus != null ? transform.InverseTransformPoint(worldView.Focus.position) : Vector3.zero;
            return new Vector2(focus.x, focus.z);
        }

        private void BuildTile(int tile)
        {
            int2 size = water.Size;
            int tx = tile % tilesAcross * Tile, tz = tile / tilesAcross * Tile;
            sheet.Clear();
            for (int z = tz; z < Mathf.Min(tz + Tile, size.y); z++)
            {
                for (int x = tx; x < Mathf.Min(tx + Tile, size.x); x++)
                {
                    if (water.Kind[x + z * size.x] != (byte)WaterKind.Dry)
                    {
                        sheet.AddCell(water, x, z);
                    }
                }
            }
            if (sheet.Triangles.Count > 0)
            {
                tiles[tile] = MakeMeshObject("Far water tile", SheetToMesh("Far water"), detailMaterial);
            }
        }

        private Mesh SheetToMesh(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = sheet.Vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(sheet.Vertices);
            mesh.SetUVs(0, sheet.Flows);
            mesh.SetUVs(1, sheet.Looks);
            mesh.SetUVs(2, sheet.Ripples);
            mesh.SetTriangles(sheet.Triangles, 0);
            mesh.RecalculateNormals();
            // The sheets are never read back: keep the one copy on the graphics card.
            mesh.UploadMeshData(true);
            TriangleCount += sheet.Triangles.Count / 3;
            return mesh;
        }

        private GameObject MakeMeshObject(string name, Mesh mesh, Material look)
        {
            // Room for the ripples above and below the water.
            Bounds bounds = mesh.bounds;
            bounds.Expand(new Vector3(0f, 0.3f, 0f));
            mesh.bounds = bounds;
            var go = new GameObject(name);
            if (holder == null)
            {
                holder = new GameObject("Water");
                holder.transform.SetParent(transform, false);
            }
            go.transform.SetParent(holder.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = look;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }
    }
}
