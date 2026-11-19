using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Makes, reuses and keeps track of a world's chunk objects (M1, P2): one <see cref="ChunkRenderer"/> per loaded chunk
    /// that has a surface, holding its mesh and collider. Chunks that are all air or all solid never get one, which is most
    /// of a world. The chunks are drawn by region (GL776): <see cref="MeshRegionSet"/> merges the meshes of each block of
    /// <see cref="RegionSpan"/> chunks into one renderer, so the engine draws and culls a few hundred renderers instead of
    /// tens of thousands. Unity objects only; what to show comes from the <see cref="WorldStreamer"/>.
    /// </summary>
    public sealed class ChunkRendererPool : IDisposable
    {
        /// <summary>Chunks per region along each axis: 16 m at 4 m chunks.</summary>
        public static readonly Vector3Int DefaultRegionSpan = new Vector3Int(4, 4, 4);

        // A changed region this close to the focus (metres, horizontally) is merged in the same frame, whatever the budget:
        // digging, felling and building show at once.
        private const float AlwaysMergeWithin = 24f;

        private readonly Transform parent;
        private readonly bool withColliders;
        private readonly Vector3Int regionSpan;
        private readonly Dictionary<Vector3Int, ChunkRenderer> active = new Dictionary<Vector3Int, ChunkRenderer>();
        private readonly Stack<ChunkRenderer> idle = new Stack<ChunkRenderer>();

        // The layers of each column (x, z) that have a renderer (GL773): a column has a few, among a hundred layers mostly
        // proven air or rock, so looks round the focus ask only about these.
        private readonly Dictionary<Vector2Int, List<int>> layersByColumn = new Dictionary<Vector2Int, List<int>>();
        private readonly Stack<List<int>> spareLayerLists = new Stack<List<int>>();
        private Material[] materials;
        private Material[] farMaterials;
        private MeshRegionSet regions;
        private WorldGrid grid;
        private bool hidden;

        // Materials after the first are overlays (the build grid, M22): a second pass over every
        // triangle, so only regions within the overlay radius of the focus draw them.
        private Vector3Int overlayCentre;
        private int overlayRadius = -1;

        // Which chunks are drawn (P18), by the region's first column (x, z): regions within the detail distance whole, those
        // past it only their edited columns (and not at all without any). Null draws every one. Chunks not drawn stay loaded.
        private Func<Vector2Int, bool> isRegionWithin;
        private Func<Vector2Int, bool> regionHasEdits;
        private Func<Vector2Int, bool> isColumnEdited;

        /// <param name="parent">Chunk objects and region renderers sit under it, at their origin in its space.</param>
        /// <param name="withColliders">Give every chunk a <see cref="ChunkCollider"/> (M6).</param>
        /// <param name="regionSpan">Chunks per drawn region along each axis; <see cref="DefaultRegionSpan"/> when left out.</param>
        public ChunkRendererPool(Transform parent, bool withColliders, Material[] materials, Vector3Int? regionSpan = null)
        {
            this.parent = parent;
            this.withColliders = withColliders;
            this.regionSpan = Vector3Int.Max(regionSpan ?? DefaultRegionSpan, Vector3Int.one);
            SetMaterialArrays(materials);
        }

        /// <summary>Every chunk shown, by coordinate.</summary>
        public IReadOnlyDictionary<Vector3Int, ChunkRenderer> Active => active;

        /// <summary>Chunks per drawn region along each axis (GL776).</summary>
        public Vector3Int RegionSpan => regionSpan;

        /// <summary>Regions holding at least one chunk's surface.</summary>
        public int RegionCount => regions?.RegionCount ?? 0;

        /// <summary>Region renderers drawing now.</summary>
        public int DrawnRegionCount => regions?.DrawnCount ?? 0;

        /// <summary>Regions changed since they were last merged.</summary>
        public int DirtyRegionCount => regions?.DirtyCount ?? 0;

        /// <summary>The materials chunks near the focus draw with (the surface first, then overlays).</summary>
        public Material[] Materials => materials;

        /// <summary>Stops drawing every chunk (a lab's X-ray), keeping everything loaded.</summary>
        public bool Hidden
        {
            get => hidden;
            set
            {
                hidden = value;
                if (regions != null)
                {
                    regions.Hidden = value;
                }
            }
        }

        public bool TryGet(Vector3Int coord, out ChunkRenderer chunkRenderer)
        {
            return active.TryGetValue(coord, out chunkRenderer);
        }

        /// <summary>The layers of a column (x, z) that have a renderer, in no order.</summary>
        public IReadOnlyList<int> LayersIn(Vector2Int column)
        {
            return layersByColumn.TryGetValue(column, out List<int> layers) ? layers : (IReadOnlyList<int>)Array.Empty<int>();
        }

        /// <summary>The first column (x, z) of the region holding a column: every column of a region is drawn alike.</summary>
        public Vector2Int RegionColumn(Vector2Int column)
        {
            return new Vector2Int(FloorDiv(column.x, regionSpan.x) * regionSpan.x, FloorDiv(column.y, regionSpan.z) * regionSpan.z);
        }

        /// <summary>Whether a chunk at the coordinate would be drawn now (the draw rule; false while hidden).</summary>
        public bool IsDrawn(Vector3Int coord)
        {
            if (hidden)
            {
                return false;
            }
            var column = new Vector2Int(coord.x, coord.z);
            RegionDraw draw = DrawOf(RegionColumn(column));
            return draw == RegionDraw.Whole || (draw == RegionDraw.Partly && isColumnEdited != null && isColumnEdited(column));
        }

        /// <summary>A chunk object for the chunk at its origin: an idle one if there is one, otherwise a new one.</summary>
        public ChunkRenderer Take(World world, Vector3Int coord, Chunk chunk, Func<ChunkMeshSettings> meshSettings)
        {
            EnsureRegions(world.Grid);
            ChunkRenderer chunkRenderer = idle.Count > 0 ? idle.Pop() : Create();
            chunkRenderer.transform.localPosition = world.Grid.ChunkOrigin(coord);
            chunkRenderer.Show(world, coord, chunk, meshSettings);
            active.Add(coord, chunkRenderer);
            var column = new Vector2Int(coord.x, coord.z);
            if (!layersByColumn.TryGetValue(column, out List<int> layers))
            {
                layers = spareLayerLists.Count > 0 ? spareLayerLists.Pop() : new List<int>();
                layersByColumn.Add(column, layers);
            }
            layers.Add(coord.y);
            return chunkRenderer;
        }

        /// <summary>Stops showing a chunk; its object waits for another.</summary>
        public void Release(Vector3Int coord)
        {
            if (!active.TryGetValue(coord, out ChunkRenderer chunkRenderer))
            {
                return;
            }
            active.Remove(coord);
            var column = new Vector2Int(coord.x, coord.z);
            if (layersByColumn.TryGetValue(column, out List<int> layers))
            {
                layers.Remove(coord.y);
                if (layers.Count == 0)
                {
                    layersByColumn.Remove(column);
                    spareLayerLists.Push(layers);
                }
            }
            regions?.Remove(coord);
            chunkRenderer.Hide();
            idle.Push(chunkRenderer);
        }

        /// <summary>
        /// Merges the regions whose chunks changed (GL776): those near the focus always, then the nearest of those drawn or
        /// within <paramref name="buildWithin"/> (metres) until about <paramref name="vertexBudget"/> vertices are copied.
        /// Call once a frame, after the frame's meshes are applied. Returns the regions merged.
        /// </summary>
        public int BuildRegions(Vector3 focus, float buildWithin, int vertexBudget)
        {
            return regions?.Build(focus, AlwaysMergeWithin, buildWithin, vertexBudget) ?? 0;
        }

        /// <summary>The materials chunks draw with (the terrain material first, M15).</summary>
        public void SetMaterials(Material[] newMaterials)
        {
            SetMaterialArrays(newMaterials);
            regions?.RefreshMaterials();
        }

        /// <summary>
        /// Overlays (every material after the first) only on regions within <paramref name="radius"/>
        /// chunks of <paramref name="centre"/>, horizontally; a negative radius puts them on every region.
        /// Cheap when nothing changed, so call it every frame.
        /// </summary>
        public void SetOverlayFocus(Vector3Int centre, int radius)
        {
            if (centre == overlayCentre && radius == overlayRadius)
            {
                return;
            }
            overlayCentre = centre;
            overlayRadius = radius;
            regions?.RefreshMaterials();
        }

        /// <summary>
        /// Which chunks are drawn (P18): every chunk of a region within the detail distance (<paramref name="regionWithin"/>,
        /// asked with the region's first column), and past it only those in edited columns (<paramref name="columnEdited"/>;
        /// <paramref name="regionEdited"/> says, by first column, whether a region has any, so the others aren't looked at).
        /// The others stay loaded, not drawn. A null <paramref name="regionWithin"/> draws every chunk. Applies to every
        /// region now and to those made later.
        /// </summary>
        public void SetDrawRule(Func<Vector2Int, bool> regionWithin, Func<Vector2Int, bool> regionEdited, Func<Vector2Int, bool> columnEdited)
        {
            isRegionWithin = regionWithin;
            regionHasEdits = regionEdited;
            isColumnEdited = columnEdited;
            RefreshDrawn();
        }

        /// <summary>Applies the draw rule to every region, e.g. after it changed everywhere.</summary>
        public void RefreshDrawn()
        {
            regions?.RefreshShown();
        }

        /// <summary>Applies the draw rule to the regions holding the column's layers 0 to <paramref name="height"/> - 1.</summary>
        public void RefreshColumn(Vector2Int column, int height)
        {
            if (regions == null)
            {
                return;
            }
            Vector2Int first = RegionColumn(column);
            int regionX = first.x / regionSpan.x;
            int regionZ = first.y / regionSpan.z;
            for (int y = 0; y * regionSpan.y < height; y++)
            {
                var region = new Vector3Int(regionX, y, regionZ);
                regions.RefreshShown(region);
                // Drawn partly, its newly edited column joins it.
                regions.MarkChanged(region);
            }
        }

        /// <summary>Destroys the region renderers (the chunk objects are left to their parent).</summary>
        public void Dispose()
        {
            regions?.Dispose();
        }

        private void EnsureRegions(WorldGrid worldGrid)
        {
            if (regions != null && grid.ChunkWorldSize == worldGrid.ChunkWorldSize)
            {
                return;
            }
            regions?.Dispose();
            grid = worldGrid;
            regions = new MeshRegionSet(parent, "Chunk region", regionSpan, grid.ChunkWorldSize, grid.ChunkOrigin);
            regions.SetShownRule(region => DrawOf(new Vector2Int(region.x * regionSpan.x, region.z * regionSpan.z)),
                cell => isColumnEdited != null && isColumnEdited(new Vector2Int(cell.x, cell.z)));
            regions.SetMaterialRule(MaterialsFor);
            regions.Hidden = hidden;
        }

        // How the region starting at the column is drawn.
        private RegionDraw DrawOf(Vector2Int firstColumn)
        {
            if (isRegionWithin == null || isRegionWithin(firstColumn))
            {
                return RegionDraw.Whole;
            }
            return regionHasEdits != null && regionHasEdits(firstColumn) ? RegionDraw.Partly : RegionDraw.None;
        }

        private Material[] MaterialsFor(Vector3Int region)
        {
            return IsWithinOverlayRadius(region) ? materials : farMaterials;
        }

        // The chunk's surface goes into its region, or leaves it when it has none or is hidden.
        private void OnSurfaceChanged(ChunkRenderer chunkRenderer)
        {
            if (regions == null || !active.TryGetValue(chunkRenderer.Coord, out ChunkRenderer current) || current != chunkRenderer)
            {
                return;
            }
            if (chunkRenderer.SurfaceHidden)
            {
                regions.Remove(chunkRenderer.Coord);
            }
            else
            {
                regions.Set(chunkRenderer.Coord, chunkRenderer.Mesh);
            }
        }

        // Whether the region's nearest column lies within the overlay radius of its centre.
        private bool IsWithinOverlayRadius(Vector3Int region)
        {
            if (overlayRadius < 0)
            {
                return true;
            }
            int x0 = region.x * regionSpan.x;
            int z0 = region.z * regionSpan.z;
            int dx = Mathf.Max(x0 - overlayCentre.x, 0, overlayCentre.x - (x0 + regionSpan.x - 1));
            int dz = Mathf.Max(z0 - overlayCentre.z, 0, overlayCentre.z - (z0 + regionSpan.z - 1));
            return dx * dx + dz * dz <= overlayRadius * overlayRadius;
        }

        private void SetMaterialArrays(Material[] newMaterials)
        {
            materials = newMaterials;
            farMaterials = newMaterials != null && newMaterials.Length > 1 ? new[] { newMaterials[0] } : newMaterials;
        }

        private ChunkRenderer Create()
        {
            var chunkObject = new GameObject("Chunk", typeof(ChunkRenderer));
            chunkObject.transform.SetParent(parent, false);
            if (withColliders)
            {
                chunkObject.AddComponent<ChunkCollider>();
            }
            var chunkRenderer = chunkObject.GetComponent<ChunkRenderer>();
            chunkRenderer.SetUp(() => materials != null && materials.Length > 0 ? materials[0] : null);
            chunkRenderer.SurfaceChanged += OnSurfaceChanged;
            return chunkRenderer;
        }

        private static int FloorDiv(int value, int divisor)
        {
            return value >= 0 ? value / divisor : -((-value + divisor - 1) / divisor);
        }
    }
}
