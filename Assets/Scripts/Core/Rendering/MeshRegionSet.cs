using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Draws many small meshes on a grid (members: a world's chunks, a coarse terrain level's chunks) as a few big ones
    /// (GL776): the members of each block of <see cref="Span"/> grid cells (a region) are merged into one mesh with one
    /// renderer, so a pass costs a draw call per region instead of one per member, and the engine culls a few hundred
    /// renderers instead of tens of thousands. The members' own meshes stay as they are (colliders, flora and the lab
    /// tools read them); a region is merged again (<see cref="MeshMerge"/>) when one of its members changes, nearest
    /// first within a budget (<see cref="Build"/>), and until then draws what it had. Unity objects only: the owner
    /// says which members there are, which regions are shown and with which materials.
    /// </summary>
    public sealed class MeshRegionSet : IDisposable
    {
        private readonly Transform parent;
        private readonly string name;
        private readonly Func<Vector3Int, Vector3> originOf;
        private readonly Vector3 cellSize;
        private readonly Action<MeshRenderer> setUp;
        private readonly Dictionary<Vector3Int, Member> members = new Dictionary<Vector3Int, Member>();
        private readonly Dictionary<Vector3Int, Region> regions = new Dictionary<Vector3Int, Region>();
        private readonly HashSet<Region> dirty = new HashSet<Region>();
        private readonly Stack<Region> spare = new Stack<Region>();
        private readonly List<(float Distance, Region Region)> candidates = new List<(float, Region)>();
        private readonly List<Mesh> sources = new List<Mesh>();
        private readonly List<Vector3> offsets = new List<Vector3>();
        private readonly MeshMerge merge = new MeshMerge();
        private Func<Vector3Int, bool> isShown;
        private Func<Vector3Int, Material[]> materialsFor;
        private bool hidden;

        /// <param name="parent">Region renderers sit under it; member origins are in its space.</param>
        /// <param name="name">The region objects' name, followed by the region's coordinate.</param>
        /// <param name="span">Grid cells per region along each axis.</param>
        /// <param name="cellSize">One grid cell's size, in the parent's space.</param>
        /// <param name="originOf">Where a cell's mesh origin lies, in the parent's space.</param>
        /// <param name="setUp">Sets up each new region renderer (shadows, probes); null keeps Unity's defaults.</param>
        public MeshRegionSet(Transform parent, string name, Vector3Int span, Vector3 cellSize, Func<Vector3Int, Vector3> originOf, Action<MeshRenderer> setUp = null)
        {
            this.parent = parent;
            this.name = name;
            Span = Vector3Int.Max(span, Vector3Int.one);
            this.cellSize = cellSize;
            this.originOf = originOf;
            this.setUp = setUp;
        }

        /// <summary>Grid cells per region along each axis.</summary>
        public Vector3Int Span { get; }

        /// <summary>Regions with at least one member.</summary>
        public int RegionCount => regions.Count;

        /// <summary>Regions drawn now: shown, built, and holding a visible member.</summary>
        public int DrawnCount { get; private set; }

        /// <summary>Regions whose members changed since they were last merged.</summary>
        public int DirtyCount => dirty.Count;

        /// <summary>Members merged in the last <see cref="Build"/>'s regions, a measure of its work.</summary>
        public int LastBuildVertices { get; private set; }

        /// <summary>Hides every region at once (a lab's X-ray), without forgetting anything.</summary>
        public bool Hidden
        {
            get => hidden;
            set
            {
                if (hidden != value)
                {
                    hidden = value;
                    foreach (Region region in regions.Values)
                    {
                        ApplyEnabled(region);
                    }
                }
            }
        }

        /// <summary>The region holding a grid cell.</summary>
        public Vector3Int RegionOf(Vector3Int cell)
        {
            return new Vector3Int(FloorDiv(cell.x, Span.x), FloorDiv(cell.y, Span.y), FloorDiv(cell.z, Span.z));
        }

        /// <summary>The region's first grid cell.</summary>
        public Vector3Int FirstCellOf(Vector3Int region)
        {
            return Vector3Int.Scale(region, Span);
        }

        /// <summary>Adds a member, or notes that its mesh changed: its region is merged again. An empty mesh is removed.</summary>
        public void Set(Vector3Int cell, Mesh mesh, bool visible = true)
        {
            if (mesh == null || mesh.vertexCount == 0)
            {
                Remove(cell);
                return;
            }
            Vector3Int regionCoord = RegionOf(cell);
            if (!regions.TryGetValue(regionCoord, out Region region))
            {
                region = spare.Count > 0 ? spare.Pop() : new Region();
                region.Coord = regionCoord;
                region.Shown = isShown == null || isShown(regionCoord);
                regions.Add(regionCoord, region);
            }
            if (members.TryGetValue(cell, out Member member))
            {
                if (member.Visible != visible)
                {
                    region.Visible += visible ? 1 : -1;
                }
            }
            else
            {
                region.Cells.Add(cell);
                region.Visible += visible ? 1 : 0;
            }
            members[cell] = new Member { Mesh = mesh, Visible = visible };
            dirty.Add(region);
            ApplyEnabled(region);
        }

        /// <summary>Removes a member; its region is merged again, or let go once it has none.</summary>
        public void Remove(Vector3Int cell)
        {
            if (!members.TryGetValue(cell, out Member member))
            {
                return;
            }
            members.Remove(cell);
            Region region = regions[RegionOf(cell)];
            region.Cells.Remove(cell);
            region.Visible -= member.Visible ? 1 : 0;
            if (region.Cells.Count == 0)
            {
                Release(region);
                return;
            }
            dirty.Add(region);
            ApplyEnabled(region);
        }

        /// <summary>Whether the member counts towards drawing its region; a region with none visible is switched off. No merge.</summary>
        public void SetVisible(Vector3Int cell, bool visible)
        {
            if (!members.TryGetValue(cell, out Member member) || member.Visible == visible)
            {
                return;
            }
            members[cell] = new Member { Mesh = member.Mesh, Visible = visible };
            Region region = regions[RegionOf(cell)];
            region.Visible += visible ? 1 : -1;
            ApplyEnabled(region);
        }

        public bool Contains(Vector3Int cell)
        {
            return members.ContainsKey(cell);
        }

        /// <summary>Which regions are drawn; null draws every one. Applies at once.</summary>
        public void SetShownRule(Func<Vector3Int, bool> rule)
        {
            isShown = rule;
            RefreshShown();
        }

        /// <summary>Applies the shown rule to every region again.</summary>
        public void RefreshShown()
        {
            foreach (Region region in regions.Values)
            {
                ApplyShown(region);
            }
        }

        /// <summary>Applies the shown rule to one region again.</summary>
        public void RefreshShown(Vector3Int regionCoord)
        {
            if (regions.TryGetValue(regionCoord, out Region region))
            {
                ApplyShown(region);
            }
        }

        /// <summary>Whether the region is drawn by the rule (whether or not it has members).</summary>
        public bool IsShown(Vector3Int regionCoord)
        {
            return isShown == null || isShown(regionCoord);
        }

        /// <summary>The materials each region draws with; applies at once, and to regions made later.</summary>
        public void SetMaterialRule(Func<Vector3Int, Material[]> rule)
        {
            materialsFor = rule;
            RefreshMaterials();
        }

        /// <summary>Gives every region its materials again (they're only set when they change).</summary>
        public void RefreshMaterials()
        {
            foreach (Region region in regions.Values)
            {
                ApplyMaterials(region);
            }
        }

        /// <summary>
        /// Merges changed regions again: all within <paramref name="alwaysWithin"/> of the focus (horizontally, in the parent's
        /// space), then the nearest of those shown or within <paramref name="buildWithin"/> until about
        /// <paramref name="vertexBudget"/> vertices are copied (at least one). Others wait, drawing what they had. Returns the
        /// regions merged.
        /// </summary>
        public int Build(Vector3 focus, float alwaysWithin, float buildWithin, int vertexBudget)
        {
            LastBuildVertices = 0;
            if (dirty.Count == 0)
            {
                return 0;
            }
            candidates.Clear();
            foreach (Region region in dirty)
            {
                float distance = HorizontalDistance(region, focus);
                if (distance <= alwaysWithin || region.Shown || distance <= buildWithin)
                {
                    // Those that must be merged sort first.
                    candidates.Add((distance <= alwaysWithin ? -1f : distance, region));
                }
            }
            candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            // The budget counts only the regions past those that must be merged, and always lets one through.
            int merged = 0;
            int spent = 0;
            bool budgeted = false;
            foreach ((float distance, Region region) in candidates)
            {
                if (distance >= 0f && budgeted && spent >= vertexBudget)
                {
                    break;
                }
                int before = LastBuildVertices;
                if (Queue(region))
                {
                    dirty.Remove(region);
                    merged++;
                    if (distance >= 0f)
                    {
                        spent += LastBuildVertices - before;
                        budgeted = true;
                    }
                }
            }
            merge.Run();
            foreach ((float _, Region region) in candidates)
            {
                if (!dirty.Contains(region))
                {
                    region.Built = true;
                    ApplyEnabled(region);
                }
            }
            candidates.Clear();
            return merged;
        }

        /// <summary>Destroys every region object; the set can be used again after.</summary>
        public void Dispose()
        {
            foreach (Region region in regions.Values)
            {
                region.Destroy();
            }
            foreach (Region region in spare)
            {
                region.Destroy();
            }
            regions.Clear();
            spare.Clear();
            members.Clear();
            dirty.Clear();
            DrawnCount = 0;
        }

        // Queues the region's merge; false when its members can't be merged yet (mixed vertex layouts).
        private bool Queue(Region region)
        {
            sources.Clear();
            offsets.Clear();
            Vector3 origin = originOf(FirstCellOf(region.Coord));
            foreach (Vector3Int cell in region.Cells)
            {
                Mesh mesh = members[cell].Mesh;
                sources.Add(mesh);
                offsets.Add(originOf(cell) - origin);
                LastBuildVertices += mesh.vertexCount;
            }
            EnsureObject(region, origin);
            return merge.Add(region.Mesh, sources, offsets);
        }

        private void EnsureObject(Region region, Vector3 origin)
        {
            if (region.Renderer == null)
            {
                var regionObject = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                regionObject.transform.SetParent(parent, false);
                region.Renderer = regionObject.GetComponent<MeshRenderer>();
                region.Mesh = new Mesh { name = name };
                region.Mesh.MarkDynamic();
                regionObject.GetComponent<MeshFilter>().sharedMesh = region.Mesh;
                setUp?.Invoke(region.Renderer);
                region.Renderer.enabled = false;
                region.Materials = null;
            }
            region.Renderer.transform.localPosition = origin;
            if (!region.Named)
            {
                region.Renderer.name = $"{name} {region.Coord.x}, {region.Coord.y}, {region.Coord.z}";
                region.Named = true;
            }
            ApplyMaterials(region);
        }

        private void Release(Region region)
        {
            regions.Remove(region.Coord);
            dirty.Remove(region);
            if (region.Renderer != null)
            {
                if (region.Drawn)
                {
                    DrawnCount--;
                }
                region.Renderer.enabled = false;
                region.Mesh.Clear();
            }
            region.Drawn = false;
            region.Built = false;
            region.Named = false;
            region.Visible = 0;
            region.Cells.Clear();
            spare.Push(region);
        }

        private void ApplyShown(Region region)
        {
            bool shown = isShown == null || isShown(region.Coord);
            if (shown != region.Shown)
            {
                region.Shown = shown;
                ApplyEnabled(region);
            }
        }

        private void ApplyMaterials(Region region)
        {
            if (region.Renderer == null)
            {
                return;
            }
            Material[] materials = materialsFor?.Invoke(region.Coord);
            if (materials != null && materials != region.Materials)
            {
                region.Renderer.sharedMaterials = materials;
                region.Materials = materials;
            }
        }

        private void ApplyEnabled(Region region)
        {
            bool drawn = !hidden && region.Shown && region.Built && region.Visible > 0 && region.Renderer != null;
            if (drawn == region.Drawn)
            {
                return;
            }
            region.Drawn = drawn;
            region.Renderer.enabled = drawn;
            DrawnCount += drawn ? 1 : -1;
        }

        // From the focus to the region's nearest point, horizontally.
        private float HorizontalDistance(Region region, Vector3 focus)
        {
            Vector3 min = originOf(FirstCellOf(region.Coord));
            Vector3 max = min + Vector3.Scale(cellSize, Span);
            float dx = Mathf.Max(min.x - focus.x, 0f, focus.x - max.x);
            float dz = Mathf.Max(min.z - focus.z, 0f, focus.z - max.z);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static int FloorDiv(int value, int divisor)
        {
            return value >= 0 ? value / divisor : -((-value + divisor - 1) / divisor);
        }

        private struct Member
        {
            public Mesh Mesh;
            public bool Visible;
        }

        private sealed class Region
        {
            public readonly List<Vector3Int> Cells = new List<Vector3Int>();
            public Vector3Int Coord;
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public Material[] Materials;
            public int Visible;
            public bool Shown = true;
            public bool Built;
            public bool Drawn;
            public bool Named;

            public void Destroy()
            {
                if (Renderer != null)
                {
                    DestroyObject(Renderer.gameObject);
                }
                if (Mesh != null)
                {
                    DestroyObject(Mesh);
                }
                Renderer = null;
                Mesh = null;
            }

            // Worlds also stream outside Play mode (benchmarks, tests), where only DestroyImmediate works.
            private static void DestroyObject(UnityEngine.Object target)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(target);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }
        }
    }
}
