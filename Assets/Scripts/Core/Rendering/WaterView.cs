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

        [Tooltip("Most chunk water meshes rebuilt in a frame.")]
        [SerializeField, Range(1, 32)]
        private int chunksPerFrame = 6;

        [Tooltip("Most far water tiles rebuilt in a frame.")]
        [SerializeField, Range(1, 16)]
        private int tilesPerFrame = 4;

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
        private readonly Dictionary<Vector3Int, GameObject> chunkMeshes = new Dictionary<Vector3Int, GameObject>();
        private readonly HashSet<Vector3Int> dirtyChunks = new HashSet<Vector3Int>();
        private readonly List<Vector3Int> changed = new List<Vector3Int>();
        private readonly List<int> rebuilding = new List<int>();
        private readonly List<Vector3Int> rebuildingChunks = new List<Vector3Int>();
        private readonly SheetMesh sheet = new SheetMesh();
        private readonly WaterMesher mesher = new WaterMesher();
        private float nextFlowStep;

        /// <summary>Triangles drawn, for tests and the lab.</summary>
        public int TriangleCount { get; private set; }

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
            ITerrainGenerator generator = worldView.Generator;
            if (generator != builtFor)
            {
                builtFor = generator;
                Reset(generator is HeightfieldGenerator heightfield ? heightfield.Water : default);
            }
            World world = worldView.World;
            if (water.IsCreated && world != null && (Live == null || Live.World != world))
            {
                StartLive(world);
            }
            if (Live != null && Time.time >= nextFlowStep)
            {
                nextFlowStep = Time.time + flowStepSeconds;
                Live.Step();
            }
            if (world != null)
            {
                changed.Clear();
                world.CollectWaterChanged(changed);
                foreach (Vector3Int coord in changed)
                {
                    dirtyChunks.Add(coord);
                }
            }
            if (water.IsCreated && worldView.DrawVersion != drawVersion)
            {
                drawVersion = worldView.DrawVersion;
                MarkAllTiles();
            }
            RebuildChunks();
            RebuildTiles();
            foreach (GameObject tile in tiles.Values)
            {
                if (tile.activeSelf != show)
                {
                    tile.SetActive(show);
                }
            }
            foreach (GameObject chunk in chunkMeshes.Values)
            {
                if (chunk.activeSelf != show)
                {
                    chunk.SetActive(show);
                }
            }
        }

        private void OnDestroy()
        {
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
            tiles.Clear();
            chunkMeshes.Clear();
            dirtyTiles.Clear();
            dirtyChunks.Clear();
            TriangleCount = 0;
        }

        private void DestroyMeshObject(GameObject go)
        {
            if (go == null)
            {
                return;
            }
            Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
            TriangleCount -= mesh != null ? mesh.triangles.Length / 3 : 0;
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
            for (int i = 0; i < from.Length; i++)
            {
                Recompute(i);
            }
            tilesAcross = (size.x + Tile - 1) / Tile;
            MarkAllTiles();
        }

        private void MarkAllTiles()
        {
            if (!water.IsCreated)
            {
                return;
            }
            int tilesDown = (water.Size.y + Tile - 1) / Tile;
            for (int t = 0; t < tilesAcross * tilesDown; t++)
            {
                dirtyTiles.Add(t);
            }
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
                dirtyChunks.Add(coord);
            }
            foreach (Vector3Int step in Faces)
            {
                if (world.TryGetChunk(coord + step, out Chunk next) && next.HasWater)
                {
                    dirtyChunks.Add(coord + step);
                }
            }
            MarkTileOf(coord);
        }

        private void OnChunkUnloaded(Vector3Int coord)
        {
            dirtyChunks.Remove(coord);
            if (chunkMeshes.TryGetValue(coord, out GameObject go))
            {
                DestroyMeshObject(go);
                chunkMeshes.Remove(coord);
            }
            MarkTileOf(coord);
        }

        private void MarkTileOf(Vector3Int coord)
        {
            if (!water.IsCreated || worldView.World == null)
            {
                return;
            }
            Vector3 middle = worldView.World.Grid.ChunkOrigin(coord) + worldView.World.Grid.ChunkWorldSize * 0.5f;
            if (water.TryCell(new float2(middle.x, middle.z), out int cell))
            {
                dirtyTiles.Add(cell % water.Size.x / Tile + cell / water.Size.x / Tile * tilesAcross);
            }
        }

        private void RebuildChunks()
        {
            World world = worldView.World;
            if (dirtyChunks.Count == 0 || world == null)
            {
                return;
            }
            rebuildingChunks.Clear();
            foreach (Vector3Int coord in dirtyChunks)
            {
                rebuildingChunks.Add(coord);
                if (rebuildingChunks.Count >= chunksPerFrame)
                {
                    break;
                }
            }
            foreach (Vector3Int coord in rebuildingChunks)
            {
                dirtyChunks.Remove(coord);
                BuildChunk(world, coord);
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
            mesh.SetTriangles(mesher.Triangles, 0);
            mesh.RecalculateNormals();
            GameObject go = MakeMeshObject("Water chunk", mesh);
            go.transform.position = world.Grid.ChunkOrigin(coord);
            chunkMeshes[coord] = go;
            TriangleCount += mesher.Triangles.Count / 3;
        }

        // What a far cell draws: its own water when wet, else the lowest water beside it, so the surface runs into the
        // banks and the ground hides its edge.
        private void Recompute(int i)
        {
            int2 size = water.Size;
            from[i] = -1;
            if (water.Kind[i] != 0)
            {
                from[i] = i;
                level[i] = water.Surface[i];
                return;
            }
            int x = i % size.x, z = i / size.x;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, nz = z + dz;
                    if (nx < 0 || nz < 0 || nx >= size.x || nz >= size.y)
                    {
                        continue;
                    }
                    int j = nx + nz * size.x;
                    if (water.Kind[j] != 0 && (from[i] < 0 || water.Surface[j] < level[i]))
                    {
                        from[i] = j;
                        level[i] = water.Surface[j];
                    }
                }
            }
        }

        private void RebuildTiles()
        {
            if (dirtyTiles.Count == 0 || !water.IsCreated || worldView.World == null)
            {
                return;
            }
            rebuilding.Clear();
            foreach (int tile in dirtyTiles)
            {
                rebuilding.Add(tile);
                if (rebuilding.Count >= tilesPerFrame && tiles.Count > 0)
                {
                    break;
                }
            }
            foreach (int tile in rebuilding)
            {
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
            Vector3 chunkSize = world.Grid.ChunkWorldSize;
            for (int z = tz; z < Mathf.Min(tz + Tile, size.y); z++)
            {
                for (int x = tx; x < Mathf.Min(tx + Tile, size.x); x++)
                {
                    int i = x + z * size.x;
                    if (from[i] < 0)
                    {
                        continue;
                    }
                    // Where the world draws its chunks, their own water is drawn.
                    float cx = water.Origin.x + (x + 0.5f) * water.CellSize, cz = water.Origin.y + (z + 0.5f) * water.CellSize;
                    var column = new Vector2Int(Mathf.FloorToInt(cx / chunkSize.x), Mathf.FloorToInt(cz / chunkSize.z));
                    if (world.LoadedColumns.ContainsKey(column) && worldView.IsColumnDrawn(column))
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
                tiles[tile] = MakeMeshObject("Far water tile", mesh);
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

        private GameObject MakeMeshObject(string name, Mesh mesh)
        {
            // Room for the ripples above and below the water.
            Bounds bounds = mesh.bounds;
            bounds.Expand(new Vector3(0f, 0.3f, 0f));
            mesh.bounds = bounds;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }
    }
}
