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

        private WorldView worldView;
        private ITerrainGenerator builtFor;
        private readonly List<GameObject> tiles = new List<GameObject>();

        /// <summary>Triangles drawn, for tests and the lab.</summary>
        public int TriangleCount { get; private set; }

        public bool Show
        {
            get => show;
            set => show = value;
        }

        private void Awake()
        {
            worldView = GetComponent<WorldView>();
        }

        private void LateUpdate()
        {
            ITerrainGenerator generator = worldView.Generator;
            if (generator != builtFor)
            {
                builtFor = generator;
                Rebuild(generator is HeightfieldGenerator heightfield ? heightfield.Water : default);
            }
            foreach (GameObject tile in tiles)
            {
                if (tile.activeSelf != show)
                {
                    tile.SetActive(show);
                }
            }
        }

        private void OnDestroy()
        {
            Clear();
        }

        private void Clear()
        {
            foreach (GameObject tile in tiles)
            {
                if (tile != null)
                {
                    Destroy(tile.GetComponent<MeshFilter>().sharedMesh);
                    Destroy(tile);
                }
            }
            tiles.Clear();
            TriangleCount = 0;
        }

        private void Rebuild(WaterMap water)
        {
            Clear();
            if (!water.IsCreated || material == null)
            {
                return;
            }
            int2 size = water.Size;
            // Each drawn cell's water height and the wet cell it takes its look from (-1: not drawn).
            var level = new float[size.x * size.y];
            var from = new int[size.x * size.y];
            for (int z = 0; z < size.y; z++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    int i = x + z * size.x;
                    from[i] = -1;
                    if (water.Kind[i] != 0)
                    {
                        from[i] = i;
                        level[i] = water.Surface[i];
                        continue;
                    }
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
            }

            var mesh = new TileMesh();
            for (int tz = 0; tz < size.y; tz += Tile)
            {
                for (int tx = 0; tx < size.x; tx += Tile)
                {
                    mesh.Clear();
                    for (int z = tz; z < Mathf.Min(tz + Tile, size.y); z++)
                    {
                        for (int x = tx; x < Mathf.Min(tx + Tile, size.x); x++)
                        {
                            if (from[x + z * size.x] >= 0)
                            {
                                AddCell(water, level, from, x, z, mesh);
                            }
                        }
                    }
                    if (mesh.Triangles.Count > 0)
                    {
                        tiles.Add(MakeTile(mesh));
                        TriangleCount += mesh.Triangles.Count / 3;
                    }
                }
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
