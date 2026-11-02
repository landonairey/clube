using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Core
{
    /// <summary>
    /// Draws the rivers and lakes (GL309) of the <see cref="WorldView"/>'s ground (<see cref="HeightfieldGenerator.Water"/>):
    /// a low-poly surface of flat triangles, two per metre cell, in tiles (the <c>Clube/Water</c> shader cuts each into four
    /// for the drawing, GL312). Corners are shared, at the water's height there, so rivers slope down their course and lakes
    /// lie level; every wet cell's ring of dry neighbours is drawn too, at the lowest water beside it, so the surface runs
    /// into the banks and the ground hides its edge. Each triangle carries the river's flow and whether it's river or lake
    /// for the shader's shimmer, and each corner the flow there for its ripples, which move the surface in the drawing only.
    /// The water stands still in the data: digging doesn't move it.
    /// </summary>
    [RequireComponent(typeof(WorldView))]
    public sealed class WaterView : MonoBehaviour
    {
        private const int Tile = 32;

        [SerializeField]
        private Material material;

        [SerializeField]
        private bool show = true;

        [Tooltip("Seconds between steps of the live water (GL313): water runs into a dug hole a sample a step.")]
        [SerializeField, Range(0.02f, 0.5f)]
        private float flowStepSeconds = 0.08f;

        [Tooltip("Most water tiles rebuilt in a frame after the water changed.")]
        [SerializeField, Range(1, 16)]
        private int tilesPerFrame = 4;

        private WorldView worldView;
        private ITerrainGenerator builtFor;
        private WaterMap water;
        // Per map cell: the water height drawn there, and the wet cell it takes its look from (-1: not drawn).
        private float[] level;
        private int[] from;
        private int tilesAcross;
        private readonly Dictionary<int, GameObject> tiles = new Dictionary<int, GameObject>();
        private readonly HashSet<int> dirty = new HashSet<int>();
        private readonly List<int> rebuilding = new List<int>();
        private readonly TileMesh mesh = new TileMesh();
        private float nextFlowStep;

        /// <summary>Triangles drawn, for tests and the lab.</summary>
        public int TriangleCount { get; private set; }

        /// <summary>The water that answers digging and building (GL313); null until the world and its water exist.</summary>
        public LiveWater Live { get; private set; }

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
        }

        private void OnDisable()
        {
            worldView.ChunkLoaded -= OnChunkLoaded;
        }

        private void LateUpdate()
        {
            ITerrainGenerator generator = worldView.Generator;
            if (generator != builtFor)
            {
                builtFor = generator;
                Reset(generator is HeightfieldGenerator heightfield ? heightfield.Water : default);
            }
            if (water.IsCreated && worldView.World != null && (Live == null || Live.World != worldView.World))
            {
                StartLive();
            }
            if (Live != null && Time.time >= nextFlowStep)
            {
                nextFlowStep = Time.time + flowStepSeconds;
                Live.Step();
            }
            RebuildDirty();
            foreach (GameObject tile in tiles.Values)
            {
                if (tile.activeSelf != show)
                {
                    tile.SetActive(show);
                }
            }
        }

        private void OnDestroy()
        {
            Live?.Dispose();
            Clear();
        }

        private void Clear()
        {
            foreach (GameObject tile in tiles.Values)
            {
                if (tile != null)
                {
                    Destroy(tile.GetComponent<MeshFilter>().sharedMesh);
                    Destroy(tile);
                }
            }
            tiles.Clear();
            dirty.Clear();
            TriangleCount = 0;
        }

        private void Reset(WaterMap map)
        {
            Clear();
            Live?.Dispose();
            Live = null;
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
            int tilesDown = (size.y + Tile - 1) / Tile;
            for (int t = 0; t < tilesAcross * tilesDown; t++)
            {
                dirty.Add(t);
            }
        }

        private void StartLive()
        {
            Live?.Dispose();
            Live = new LiveWater(worldView.World, water, worldView.Config.IsoLevel);
            Live.CellChanged += OnCellChanged;
        }

        // A chunk loaded with edits near water (a saved game, or one streamed back in) gets its flooded holes back.
        private void OnChunkLoaded(Vector3Int coord)
        {
            if (Live != null && worldView.World.IsEdited(coord))
            {
                Live.WakeChunk(coord);
            }
        }

        // A cell's water changed: what it and the cells round it draw, and the tiles holding them.
        private void OnCellChanged(int cell)
        {
            if (cell < 0)
            {
                return;
            }
            int2 size = water.Size;
            int x = cell % size.x, z = cell / size.x;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, nz = z + dz;
                    if (nx >= 0 && nz >= 0 && nx < size.x && nz < size.y)
                    {
                        Recompute(nx + nz * size.x);
                        dirty.Add(nx / Tile + nz / Tile * tilesAcross);
                    }
                }
            }
        }

        // What a cell draws: its own water when wet (and not built over), else the lowest water beside it, so the surface
        // runs into the banks and the ground hides its edge.
        private void Recompute(int i)
        {
            int2 size = water.Size;
            from[i] = -1;
            if (IsWater(i))
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
                    if (IsWater(j) && (from[i] < 0 || water.Surface[j] < level[i]))
                    {
                        from[i] = j;
                        level[i] = water.Surface[j];
                    }
                }
            }
        }

        private bool IsWater(int cell)
        {
            return water.Kind[cell] != 0 && (Live == null || !Live.IsFilled(cell));
        }

        private void RebuildDirty()
        {
            if (dirty.Count == 0 || !water.IsCreated)
            {
                return;
            }
            rebuilding.Clear();
            foreach (int tile in dirty)
            {
                rebuilding.Add(tile);
                if (rebuilding.Count >= tilesPerFrame && tiles.Count > 0)
                {
                    break;
                }
            }
            foreach (int tile in rebuilding)
            {
                dirty.Remove(tile);
                BuildTile(tile);
            }
        }

        private void BuildTile(int tile)
        {
            int2 size = water.Size;
            int tx = tile % tilesAcross * Tile, tz = tile / tilesAcross * Tile;
            mesh.Clear();
            columns.Clear();
            for (int z = tz; z < Mathf.Min(tz + Tile, size.y); z++)
            {
                for (int x = tx; x < Mathf.Min(tx + Tile, size.x); x++)
                {
                    int i = x + z * size.x;
                    if (from[i] >= 0)
                    {
                        AddCell(water, level, from, x, z, mesh);
                    }
                    else if (Live != null)
                    {
                        GatherFlooded(i);
                    }
                }
            }
            EmitFlooded(mesh);
            if (tiles.TryGetValue(tile, out GameObject old))
            {
                TriangleCount -= old.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3;
                Destroy(old.GetComponent<MeshFilter>().sharedMesh);
                Destroy(old);
                tiles.Remove(tile);
            }
            if (mesh.Triangles.Count > 0)
            {
                tiles[tile] = MakeTile(mesh);
                TriangleCount += mesh.Triangles.Count / 3;
            }
        }

        // Water that came into a cell the map has dry (GL313): a square per sample column, at the top of its water, and one
        // more sample round each, so the surface runs into the sides of the hole.
        private readonly Dictionary<Vector2Int, float> columns = new Dictionary<Vector2Int, float>();

        private void GatherFlooded(int cell)
        {
            IReadOnlyCollection<Vector3Int> samples = Live.FloodedIn(cell);
            if (samples == null || samples.Count == 0)
            {
                return;
            }
            foreach (Vector3Int sample in samples)
            {
                float top = Live.Flooded[sample];
                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        var column = new Vector2Int(sample.x + dx, sample.z + dz);
                        bool own = dx == 0 && dz == 0;
                        if (!columns.TryGetValue(column, out float drawn) || (own ? top > drawn : top < drawn))
                        {
                            columns[column] = top;
                        }
                    }
                }
            }
        }

        private void EmitFlooded(TileMesh into)
        {
            if (Live == null || columns.Count == 0)
            {
                return;
            }
            float voxel = Live.World.Grid.VoxelSize;
            foreach (KeyValuePair<Vector2Int, float> column in columns)
            {
                float x0 = (column.Key.x - 0.5f) * voxel, z0 = (column.Key.y - 0.5f) * voxel;
                int under = Live.CellOf(new Vector3(x0 + voxel * 0.5f, 0f, z0 + voxel * 0.5f));
                if (under >= 0 && from[under] >= 0)
                {
                    continue;
                }
                Corner Point(float x, float z) => new Corner { Position = new Vector3(x, column.Value, z), Ripple = Vector4.zero };
                Corner a = Point(x0, z0), b = Point(x0 + voxel, z0), c = Point(x0 + voxel, z0 + voxel), d = Point(x0, z0 + voxel);
                var look = new Vector3(0f, 0f, (float)WaterKind.Lake);
                into.Add(a, d, c, look);
                into.Add(a, c, b, look);
            }
        }

        // A corner of the water: where it is, and for the shader's ripples the flow there and how much of it is river
        // (both averaged over the cells round it, so neighbouring triangles ripple alike and the surface never cracks).
        private struct Corner
        {
            public Vector3 Position;
            public Vector4 Ripple;
        }

        // Two triangles over a cell, the diagonal alternating like a checkerboard, so the facets don't line up.
        private static void AddCell(WaterMap water, float[] level, int[] from, int x, int z, TileMesh mesh)
        {
            int2 size = water.Size;
            float cell = water.CellSize;
            Corner At(int cx, int cz)
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
                return new Corner
                {
                    Position = new Vector3(water.Origin.x + cx * cell, sum / count, water.Origin.y + cz * cell),
                    Ripple = new Vector4(flow.x / count, flow.y / count, river / count, 0f),
                };
            }

            Corner a = At(x, z), b = At(x + 1, z), c = At(x + 1, z + 1), d = At(x, z + 1);
            int source = from[x + z * size.x];
            float2 flow = water.Flow[source];
            var look = new Vector3(flow.x, flow.y, water.Kind[source]);
            // The shader finds these same two triangles from the position, and cuts each into four (GL312).
            if (((x + z) & 1) == 0)
            {
                mesh.Add(a, d, c, look);
                mesh.Add(a, c, b, look);
            }
            else
            {
                mesh.Add(a, d, b, look);
                mesh.Add(b, d, c, look);
            }
        }

        // One tile's vertex streams while it's built. Every triangle has its own three vertices, so its flow and kind are its own.
        private sealed class TileMesh
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            /// <summary>UV0: the triangle's river flow (x, z), for the shimmer's direction.</summary>
            public readonly List<Vector2> Flows = new List<Vector2>();
            /// <summary>UV2: the triangle's kind (1 river, 2 lake).</summary>
            public readonly List<Vector2> Kinds = new List<Vector2>();
            /// <summary>UV3: the corner's flow (x, z) and river share, for the ripples.</summary>
            public readonly List<Vector4> Ripples = new List<Vector4>();
            public readonly List<int> Triangles = new List<int>();

            public void Clear()
            {
                Vertices.Clear();
                Flows.Clear();
                Kinds.Clear();
                Ripples.Clear();
                Triangles.Clear();
            }

            /// <param name="look">The triangle's flow (x, y) and kind (z).</param>
            public void Add(Corner p, Corner q, Corner r, Vector3 look)
            {
                Add(p, look);
                Add(q, look);
                Add(r, look);
            }

            private void Add(Corner corner, Vector3 look)
            {
                Triangles.Add(Vertices.Count);
                Vertices.Add(corner.Position);
                Ripples.Add(corner.Ripple);
                Flows.Add(new Vector2(look.x, look.y));
                Kinds.Add(new Vector2(look.z, 0f));
            }
        }

        private GameObject MakeTile(TileMesh built)
        {
            var mesh = new Mesh { name = "Water", indexFormat = built.Vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(built.Vertices);
            mesh.SetUVs(0, built.Flows);
            mesh.SetUVs(2, built.Kinds);
            mesh.SetUVs(3, built.Ripples);
            mesh.SetTriangles(built.Triangles, 0);
            mesh.RecalculateNormals();
            // Room for the ripples above and below the water.
            Bounds bounds = mesh.bounds;
            bounds.Expand(new Vector3(0f, 0.5f, 0f));
            mesh.bounds = bounds;
            var tile = new GameObject("Water tile");
            tile.transform.SetParent(transform, false);
            tile.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = tile.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return tile;
        }
    }
}
