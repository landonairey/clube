using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>How a region of a <see cref="MeshRegionSet"/> is drawn: not at all, only some of its cells, or whole.</summary>
    public enum RegionDraw
    {
        None,
        Partly,
        Whole,
    }

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
        private Func<Vector3Int, RegionDraw> drawRule;
        private Func<Vector3Int, bool> drawnWhenPartly;
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
                region.Min = originOf(FirstCellOf(regionCoord));
                region.Max = region.Min + Vector3.Scale(cellSize, Span);
                region.Draw = DrawOf(regionCoord);
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

        /// <summary>
        /// How each region is drawn; null draws every one whole. A region drawn <see cref="RegionDraw.Partly"/> is merged from
        /// only the cells <paramref name="cellDrawnWhenPartly"/> accepts. A shown region whose way of drawing changed is
        /// merged again in the next <see cref="Build"/>, whatever the budget. Applies at once.
        /// </summary>
        public void SetShownRule(Func<Vector3Int, RegionDraw> rule, Func<Vector3Int, bool> cellDrawnWhenPartly = null)
        {
            drawRule = rule;
            drawnWhenPartly = cellDrawnWhenPartly;
            RefreshShown();
        }

        /// <summary>Notes that which cells a partly drawn region draws may have changed: it's merged again soon.</summary>
        public void MarkChanged(Vector3Int regionCoord)
        {
            if (regions.TryGetValue(regionCoord, out Region region) && region.Draw == RegionDraw.Partly)
            {
                dirty.Add(region);
                region.Urgent = true;
            }
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

        /// <summary>Whether a cell is drawn by the rule (whether or not it has a member).</summary>
        public bool IsCellShown(Vector3Int cell)
        {
            RegionDraw draw = DrawOf(RegionOf(cell));
            return draw == RegionDraw.Whole || (draw == RegionDraw.Partly && drawnWhenPartly != null && drawnWhenPartly(cell));
        }

        private RegionDraw DrawOf(Vector3Int regionCoord)
        {
            return drawRule?.Invoke(regionCoord) ?? RegionDraw.Whole;
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
            using FrameCost.Scope whole = FrameCost.Of("Regions: Build");
            LastBuildVertices = 0;
            if (dirty.Count == 0)
            {
                return 0;
            }
            FrameCost.Scope collecting = FrameCost.Of("Regions: collect");
            candidates.Clear();
            foreach (Region region in dirty)
            {
                float distance = HorizontalDistance(region, focus);
                if (distance <= alwaysWithin || region.Urgent || region.Draw != RegionDraw.None || distance <= buildWithin)
                {
                    // Those that must be merged sort first.
                    candidates.Add((distance <= alwaysWithin || region.Urgent ? -1f : distance, region));
                }
            }
            collecting.Dispose();
            using (FrameCost.Scope sorting = FrameCost.Of("Regions: sort"))
            {
                candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            }
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
            using (FrameCost.Scope running = FrameCost.Of("Chunk drawing: merge run"))
            {
                merge.Run();
            }
            using FrameCost.Scope finishing = FrameCost.Of("Regions: finish");
            foreach ((float _, Region region) in candidates)
            {
                if (!dirty.Contains(region))
                {
                    region.Built = true;
                    region.BuiltFor = region.Merging;
                    region.Urgent = false;
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

        // Queues the region's merge, of every cell or, drawn partly, of the cells drawn; false when its members can't be
        // merged yet (mixed vertex layouts). One not drawn at all is merged whole, ready to be.
        private bool Queue(Region region)
        {
            sources.Clear();
            offsets.Clear();
            Vector3 origin = originOf(FirstCellOf(region.Coord));
            region.Merging = region.Draw == RegionDraw.Partly ? RegionDraw.Partly : RegionDraw.Whole;
            foreach (Vector3Int cell in region.Cells)
            {
                if (region.Merging == RegionDraw.Partly && (drawnWhenPartly == null || !drawnWhenPartly(cell)))
                {
                    continue;
                }
                Mesh mesh = members[cell].Mesh;
                sources.Add(mesh);
                offsets.Add(originOf(cell) - origin);
                LastBuildVertices += mesh.vertexCount;
            }
            region.HasGeometry = sources.Count > 0;
            EnsureObject(region, origin);
            using FrameCost.Scope adding = FrameCost.Of("Chunk drawing: merge add");
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
            region.Urgent = false;
            region.HasGeometry = false;
            region.Named = false;
            region.Visible = 0;
            region.Cells.Clear();
            spare.Push(region);
        }

        private void ApplyShown(Region region)
        {
            RegionDraw draw = DrawOf(region.Coord);
            if (draw == region.Draw)
            {
                return;
            }
            region.Draw = draw;
            if (draw != RegionDraw.None && region.Built && draw != region.BuiltFor)
            {
                // Merged for the other way of drawing: it draws that until merged again, this frame.
                dirty.Add(region);
                region.Urgent = true;
            }
            ApplyEnabled(region);
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
            bool drawn = !hidden && region.Draw != RegionDraw.None && region.Built && region.HasGeometry && region.Visible > 0 && region.Renderer != null;
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
            // The bounds are kept on the region (GL778): the delegate and the vector sums ran for every dirty region every frame.
            float dx = Mathf.Max(region.Min.x - focus.x, 0f, focus.x - region.Max.x);
            float dz = Mathf.Max(region.Min.z - focus.z, 0f, focus.z - region.Max.z);
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
            public Vector3 Min, Max;
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public Material[] Materials;
            public int Visible;
            public RegionDraw Draw = RegionDraw.Whole;
            public RegionDraw BuiltFor;
            public RegionDraw Merging;
            public bool Urgent;
            public bool HasGeometry;
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
