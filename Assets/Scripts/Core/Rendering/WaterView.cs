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
    /// facets), rebuilt when its water changes. Past the chunks the world draws, the map's water is drawn as flat sheets of
    /// two triangles per metre cell over the coarse terrain, each wet cell's ring of dry neighbours too, at the lowest water
    /// beside it, so the far banks hide the sheets' edges. Both are drawn in the <c>Clube/Water</c> shader.
    /// </summary>
    [RequireComponent(typeof(WorldView))]
    public sealed class WaterView : MonoBehaviour
    {
        private const int Tile = 32;

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

        private WorldView worldView;
        private ITerrainGenerator builtFor;
        private WaterMap water;
        private World filledWorld;
        // Far sheets, per map cell: the water height drawn there, and the wet cell it takes its look from (-1: not drawn).
        private float[] level;
        private int[] from;
        private int tilesAcross;
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
        private readonly SheetMesh sheet = new SheetMesh();
        private readonly WaterMesher mesher = new WaterMesher();
        private float nextFlowStep;
        // Every water mesh hangs under this, so Show switches one object.
        private GameObject holder;

        /// <summary>Triangles drawn, for tests and the lab.</summary>
        public int TriangleCount { get; private set; }

        /// <summary>Milliseconds the last LateUpdate took (flow, fills, mesh builds), for timing.</summary>
        public float LastUpdateMilliseconds { get; private set; }

        /// <summary>Chunk water meshes and far tiles waiting to be built.</summary>
        public int PendingChunks => dirtyChunks.Count;

        public int PendingTiles => dirtyTiles.Count;

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
            RebuildTiles();
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
            farMaterial.SetTexture(CoverageMaskId, coverage.Texture);
            farMaterial.SetVector(CoverageId, new Vector4(
                origin.x + coverage.Origin.x * chunkWidth, origin.z + coverage.Origin.y * chunkWidth, chunkWidth, coverage.Size));
        }
        private void OnDestroy()
        {
            coverage?.Dispose();
            if (farMaterial != null)
            {
                Destroy(farMaterial);
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
            DestroyMeshObject(outerSea);
            outerSea = null;
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
            level = new float[size.x * size.y];
            from = new int[size.x * size.y];
            tilesAcross = (size.x + Tile - 1) / Tile;
            if (farMaterial == null)
            {
                farMaterial = new Material(material) { name = material.name + " (far)" };
                farMaterial.SetFloat(ClipCoveredId, 1f);
            }
            wetTiles.Clear();
            for (int i = 0; i < from.Length; i++)
            {
                Recompute(i);
                if (from[i] >= 0)
                {
                    wetTiles.Add(i % size.x / Tile + i / size.x / Tile * tilesAcross);
                }
            }
            MarkAllTiles();
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
            var mesh = new Mesh { name = "Outer sea", indexFormat = sheet.Vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(sheet.Vertices);
            mesh.SetUVs(0, sheet.Flows);
            mesh.SetUVs(1, sheet.Looks);
            mesh.SetUVs(2, sheet.Ripples);
            mesh.SetTriangles(sheet.Triangles, 0);
            mesh.RecalculateNormals();
            outerSea = MakeMeshObject("Outer sea", mesh, farMaterial);
            TriangleCount += sheet.Triangles.Count / 3;
        }

        private void MarkAllTiles()
        {
            if (!water.IsCreated || !drawFarWater)
            {
                return;
            }
            dirtyTiles.UnionWith(wetTiles);
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

        // What a far cell draws: its own water when wet, nothing when dry (GL323: the dry ring drawn at the lowest water
        // beside it stood as a skirt under the banks).
        private void Recompute(int i)
        {
            from[i] = -1;
            if (water.Kind[i] != 0)
            {
                from[i] = i;
                level[i] = water.Surface[i];
                return;
            }
        }

        // Far tiles take what's left of the frame's budget (one at least once the chunks are all built).
        private void RebuildTiles()
        {
            if (dirtyTiles.Count == 0 || !water.IsCreated || !drawFarWater || worldView.World == null)
            {
                return;
            }
            bool first = true;
            while (dirtyTiles.Count > 0 && (budget.Elapsed.TotalMilliseconds < buildBudgetMs || (first && dirtyChunks.Count == 0)))
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

        private void BuildTile(int tile)
        {
            int2 size = water.Size;
            int tx = tile % tilesAcross * Tile, tz = tile / tilesAcross * Tile;
            sheet.Clear();
            World world = worldView.World;
            for (int z = tz; z < Mathf.Min(tz + Tile, size.y); z++)
            {
                for (int x = tx; x < Mathf.Min(tx + Tile, size.x); x++)
                {
                    int i = x + z * size.x;
                    if (from[i] < 0)
                    {
                        continue;
                    }
                    AddCell(x, z);
                }
            }
            if (tiles.TryGetValue(tile, out GameObject old))
            {
                DestroyMeshObject(old);
                tiles.Remove(tile);
            }
            if (sheet.Triangles.Count > 0)
            {
                var mesh = new Mesh { name = "Far water", indexFormat = sheet.Vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(sheet.Vertices);
                mesh.SetUVs(0, sheet.Flows);
                mesh.SetUVs(1, sheet.Looks);
                mesh.SetUVs(2, sheet.Ripples);
                mesh.SetTriangles(sheet.Triangles, 0);
                mesh.RecalculateNormals();
                tiles[tile] = MakeMeshObject("Far water tile", mesh, farMaterial);
                TriangleCount += sheet.Triangles.Count / 3;
            }
        }

        // Two triangles over a cell, the diagonal alternating like a checkerboard, so the facets don't line up. Corners
        // are shared, at the average height of the cells round them, so the sheet never cracks.
        private void AddCell(int x, int z)
        {
            int2 size = water.Size;
            float cell = water.CellSize;
            Vector3 At(int cx, int cz, out Vector4 ripple)
            {
                float sum = 0f, river = 0f;
                var flow = Vector2.zero;
                int count = 0;
                for (int dz = -1; dz <= 0; dz++)
                {
                    for (int dx = -1; dx <= 0; dx++)
                    {
                        int nx = cx + dx, nz = cz + dz;
                        int n = nx + nz * size.x;
                        if (nx >= 0 && nz >= 0 && nx < size.x && nz < size.y && from[n] >= 0)
                        {
                            sum += level[n];
                            float2 f = water.Flow[from[n]];
                            flow += new Vector2(f.x, f.y);
                            river += water.Kind[from[n]] == (byte)WaterKind.River ? 1f : 0f;
                            count++;
                        }
                    }
                }
                count = Mathf.Max(1, count);
                ripple = new Vector4(flow.x / count, flow.y / count, river / count, 0f);
                return new Vector3(water.Origin.x + cx * cell, sum / count, water.Origin.y + cz * cell);
            }

            Vector3 a = At(x, z, out Vector4 ra), b = At(x + 1, z, out Vector4 rb);
            Vector3 c = At(x + 1, z + 1, out Vector4 rc), d = At(x, z + 1, out Vector4 rd);
            int source = from[x + z * size.x];
            float2 riverFlow = water.Flow[source];
            bool river = water.Kind[source] == (byte)WaterKind.River;
            Vector2 flow = river && math.lengthsq(riverFlow) > 1e-6f ? new Vector2(riverFlow.x, riverFlow.y).normalized * 0.4f : Vector2.zero;
            float kind = river ? 1f : 2f;
            if (((x + z) & 1) == 0)
            {
                sheet.Add(a, d, c, ra, rd, rc, flow, kind);
                sheet.Add(a, c, b, ra, rc, rb, flow, kind);
            }
            else
            {
                sheet.Add(a, d, b, ra, rd, rb, flow, kind);
                sheet.Add(b, d, c, rb, rd, rc, flow, kind);
            }
        }

        // A far tile's vertex streams while it's built, in the same layout as a chunk's water (WaterMesher).
        private sealed class SheetMesh
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector2> Flows = new List<Vector2>();
            public readonly List<Vector4> Looks = new List<Vector4>();
            public readonly List<Vector4> Ripples = new List<Vector4>();
            public readonly List<int> Triangles = new List<int>();

            public void Clear()
            {
                Vertices.Clear();
                Flows.Clear();
                Looks.Clear();
                Ripples.Clear();
                Triangles.Clear();
            }

            public void Add(Vector3 p, Vector3 q, Vector3 r, Vector4 rp, Vector4 rq, Vector4 rr, Vector2 flow, float kind)
            {
                Vector3 centre = (p + q + r) / 3f;
                var look = new Vector4(centre.x, centre.y, centre.z, kind);
                Add(p, rp, flow, look);
                Add(q, rq, flow, look);
                Add(r, rr, flow, look);
            }

            private void Add(Vector3 position, Vector4 ripple, Vector2 flow, Vector4 look)
            {
                Triangles.Add(Vertices.Count);
                Vertices.Add(position);
                Flows.Add(flow);
                Looks.Add(look);
                Ripples.Add(ripple);
            }
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
