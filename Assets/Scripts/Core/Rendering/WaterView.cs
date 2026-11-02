using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Core
{
    /// <summary>
    /// Draws the rivers and lakes (GL309) of the <see cref="WorldView"/>'s ground (<see cref="HeightfieldGenerator.Water"/>):
    /// a low-poly surface of flat triangles, two per metre cell, in tiles. Corners are shared, at the water's height there,
    /// so rivers slope down their course and lakes lie level; every wet cell's ring of dry neighbours is drawn too, at the
    /// water's height, at the lowest water beside it, so the surface runs into the banks and the ground hides its edge. Each triangle carries its centre,
    /// the river's flow, whether it's river or lake and a random number for the <c>Clube/Water</c> shader's shimmer. The
    /// water stands still in the data: digging doesn't move it.
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

            var vertices = new List<Vector3>();
            var flows = new List<Vector2>();
            var centres = new List<Vector2>();
            var kinds = new List<Vector2>();
            var triangles = new List<int>();
            for (int tz = 0; tz < size.y; tz += Tile)
            {
                for (int tx = 0; tx < size.x; tx += Tile)
                {
                    vertices.Clear();
                    flows.Clear();
                    centres.Clear();
                    kinds.Clear();
                    triangles.Clear();
                    for (int z = tz; z < Mathf.Min(tz + Tile, size.y); z++)
                    {
                        for (int x = tx; x < Mathf.Min(tx + Tile, size.x); x++)
                        {
                            int i = x + z * size.x;
                            if (from[i] < 0)
                            {
                                continue;
                            }
                            AddCell(water, level, from, x, z, vertices, flows, centres, kinds, triangles);
                        }
                    }
                    if (triangles.Count > 0)
                    {
                        tiles.Add(MakeTile(vertices, flows, centres, kinds, triangles));
                        TriangleCount += triangles.Count / 3;
                    }
                }
            }
        }

        // Two triangles over a cell, the diagonal alternating like a checkerboard, so the facets don't line up.
        private static void AddCell(WaterMap water, float[] level, int[] from, int x, int z, List<Vector3> vertices, List<Vector2> flows,
            List<Vector2> centres, List<Vector2> kinds, List<int> triangles)
        {
            int2 size = water.Size;
            float cell = water.CellSize;
            Vector3 Corner(int cx, int cz)
            {
                float sum = 0f;
                int count = 0;
                for (int dz = -1; dz <= 0; dz++)
                {
                    for (int dx = -1; dx <= 0; dx++)
                    {
                        int nx = cx + dx, nz = cz + dz;
                        if (nx >= 0 && nz >= 0 && nx < size.x && nz < size.y && from[nx + nz * size.x] >= 0)
                        {
                            sum += level[nx + nz * size.x];
                            count++;
                        }
                    }
                }
                return new Vector3(water.Origin.x + cx * cell, sum / Mathf.Max(1, count), water.Origin.y + cz * cell);
            }

            Vector3 a = Corner(x, z), b = Corner(x + 1, z), c = Corner(x + 1, z + 1), d = Corner(x, z + 1);
            int source = from[x + z * size.x];
            float2 flow = water.Flow[source];
            float kind = water.Kind[source];
            if (((x + z) & 1) == 0)
            {
                AddTriangle(a, d, c, flow, kind, x, z, 0, vertices, flows, centres, kinds, triangles);
                AddTriangle(a, c, b, flow, kind, x, z, 1, vertices, flows, centres, kinds, triangles);
            }
            else
            {
                AddTriangle(a, d, b, flow, kind, x, z, 0, vertices, flows, centres, kinds, triangles);
                AddTriangle(b, d, c, flow, kind, x, z, 1, vertices, flows, centres, kinds, triangles);
            }
        }

        private static void AddTriangle(Vector3 p, Vector3 q, Vector3 r, float2 flow, float kind, int x, int z, int half,
            List<Vector3> vertices, List<Vector2> flows, List<Vector2> centres, List<Vector2> kinds, List<int> triangles)
        {
            Vector3 centre = (p + q + r) / 3f;
            float random = VoxelHash.Uniform(0, x, half, z, 0x57415445);
            foreach (Vector3 corner in new[] { p, q, r })
            {
                triangles.Add(vertices.Count);
                vertices.Add(corner);
                flows.Add(new Vector2(flow.x, flow.y));
                centres.Add(new Vector2(centre.x, centre.z));
                kinds.Add(new Vector2(kind, random));
            }
        }

        private GameObject MakeTile(List<Vector3> vertices, List<Vector2> flows, List<Vector2> centres, List<Vector2> kinds, List<int> triangles)
        {
            var mesh = new Mesh { name = "Water", indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, flows);
            mesh.SetUVs(1, centres);
            mesh.SetUVs(2, kinds);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
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
