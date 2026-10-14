using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>How a <see cref="WorldStreamer"/> paces its work (P1, P3).</summary>
    public struct StreamingSettings
    {
        /// <summary>Radius in chunks of the loaded area, horizontally (M3).</summary>
        public int RenderDistance;

        /// <summary>Chunk layers stacked from chunk y = 0 (M1).</summary>
        public int HeightInChunks;

        /// <summary>Jobs of each stage running at once; 0 picks two per worker thread.</summary>
        public int MaxJobsInFlight;

        /// <summary>Main-thread time the streamer may spend a frame taking finished chunks and meshes, in milliseconds.</summary>
        public float FrameBudgetMilliseconds;

        /// <summary>
        /// The budget while the area first loads (at the start, after <see cref="WorldStreamer.UnloadAll"/>,
        /// or when the render distance grows), until everything wanted is loaded and meshed. Larger
        /// than <see cref="FrameBudgetMilliseconds"/>: with a big area, a few milliseconds of each
        /// slow frame would take minutes. 0 or less uses the frame budget.
        /// </summary>
        public float LoadingBudgetMilliseconds;

        /// <summary>Whether chunks get colliders (M6).</summary>
        public bool Colliders;

        /// <summary>How far from the focus, horizontally in metres, chunks keep a collider.</summary>
        public float ColliderRadius;

        /// <summary>Edited chunks rebuilt in the frame of the edit at most; more wait for the next frame.</summary>
        public int MaxEditsPerFrame;

        /// <summary>
        /// Labs: a fixed square of chunk columns (x, z) to load whole and keep, instead of
        /// streaming around the focus; null streams. <see cref="RenderDistance"/> is ignored then.
        /// </summary>
        public RectInt? FixedColumns;

        /// <summary>Labs: a fixed area meshes its outer faces as air, walls showing its underground (<see cref="MeshSeal"/>).</summary>
        public bool SealEdges;

        /// <summary>
        /// How far from the focus, horizontally in metres, chunks draw the overlay materials (every
        /// material after the first, like the build grid). 0 or less: every chunk.
        /// </summary>
        public float OverlayRadius;
    }

    /// <summary>Live counts from a <see cref="WorldStreamer"/>, for readouts.</summary>
    public struct StreamingStats
    {
        public int Wanted;
        public int Pending;
        public int Generating;
        public int Meshing;
        public int Baking;
        public int Renderers;
        public int Vertices;
        public int Triangles;

        /// <summary>Main-thread time the streamer took last frame, in milliseconds.</summary>
        public double FrameMilliseconds;

        /// <summary>Where <see cref="FrameMilliseconds"/> went.</summary>
        public StreamingPhases Phases;
    }

    /// <summary>Main-thread milliseconds per step of one <see cref="WorldStreamer.Update"/>: what to look at when a frame runs long.</summary>
    public struct StreamingPhases
    {
        /// <summary>Collecting finished jobs.</summary>
        public double Poll;

        /// <summary>Working out the wanted area and unloading what left it.</summary>
        public double Area;

        /// <summary>Copying generated chunks out of their buffers into the world.</summary>
        public double AddChunks;

        /// <summary>Scheduling generation jobs (buffers, ore nodes).</summary>
        public double StartGeneration;

        /// <summary>Handing finished meshes to renderers (and making renderers).</summary>
        public double ApplyMeshes;

        /// <summary>Snapshotting dirty chunks and scheduling mesh jobs.</summary>
        public double StartMeshing;

        /// <summary>Scheduling collider bakes and assigning finished ones.</summary>
        public double Colliders;

        public override string ToString()
        {
            return $"poll {Poll:0.00} · area {Area:0.00} · add {AddChunks:0.00} · generate {StartGeneration:0.00} · " +
                   $"apply {ApplyMeshes:0.00} · mesh {StartMeshing:0.00} · colliders {Colliders:0.00} ms";
        }
    }

    /// <summary>
    /// Keeps the chunks around a focus loaded, meshed and collidable (M1, M3, P1, P3): the
    /// policy over <see cref="ChunkPipeline"/>'s jobs. Every frame it works nearest first:
    /// unloads what left the area, adds finished chunks to the <see cref="World"/>, starts
    /// generating what's missing, hands finished meshes to renderers, starts meshing dirty
    /// chunks, and cooks colliders near the focus. Taking results is held to a time budget, so
    /// a burst of finished jobs spreads over a few frames instead of stalling one.
    /// </summary>
    /// <remarks>
    /// <para>Edits are the exception: <see cref="RebuildEdited"/> (late in the frame) rebuilds an
    /// edited chunk's mesh and collider at once, so digging shows in the frame it happens.</para>
    /// <para>Plain C#; <see cref="WorldView"/> owns it and calls it from Update and LateUpdate.</para>
    /// </remarks>
    public sealed class WorldStreamer : IDisposable
    {
        private readonly World world;
        private readonly ChunkPipeline pipeline;
        private readonly ChunkRendererPool renderers;
        private readonly Func<ChunkMeshSettings> meshSettings;
        private StreamingSettings settings;

        private readonly List<Vector3Int> wanted = new List<Vector3Int>();

        // The world's dirty chunks, collected each frame (World.CollectDirty).
        private readonly List<Vector3Int> dirtyScratch = new List<Vector3Int>();

        // Wanted chunks that came up before their column's heights were ready (ChunkPipeline.StartGeneration).
        private readonly List<Vector3Int> waitingForColumns = new List<Vector3Int>();
        private readonly HashSet<Vector3Int> wantedSet = new HashSet<Vector3Int>();
        private readonly HashSet<Vector3Int> meshedOnce = new HashSet<Vector3Int>();
        private readonly List<Vector3Int> scratch = new List<Vector3Int>();
        private readonly Stopwatch frameClock = new Stopwatch();
        private Vector3Int? wantedCentre;
        private int wantedDistance;
        private int loadCursor;
        // Wanted chunks that are loaded, kept up to date so PendingCount needn't scan the area.
        private int wantedLoaded;
        // Whether the area is loading in bulk (LoadingBudgetMilliseconds applies), and this frame's budget.
        private bool bulkLoading = true;
        private float budget;
        // The chunk the focus was in when far colliders were last dropped.
        private Vector3Int? collidersCentre;
        private StreamingStats stats;
        private double lapStart;

        public WorldStreamer(
            World world, ChunkPipeline pipeline, ChunkRendererPool renderers, Func<ChunkMeshSettings> meshSettings,
            StreamingSettings settings)
        {
            this.world = world;
            this.pipeline = pipeline;
            this.renderers = renderers;
            this.meshSettings = meshSettings;
            this.settings = settings;
        }

        /// <summary>Raised when a chunk's data is added to the world.</summary>
        public event Action<Vector3Int> ChunkLoaded;

        /// <summary>Raised when a chunk is unloaded.</summary>
        public event Action<Vector3Int> ChunkUnloaded;

        /// <summary>Raised after a chunk's mesh is (re)built; the renderer is null for a chunk without surface and without one.</summary>
        public event Action<Vector3Int, ChunkRenderer> ChunkMeshed;

        public StreamingStats Stats
        {
            get
            {
                StreamingStats current = stats;
                current.Wanted = wanted.Count;
                current.Pending = PendingCount;
                current.Generating = pipeline.GeneratingCount;
                current.Meshing = pipeline.MeshingCount;
                current.Baking = pipeline.BakingCount;
                current.Renderers = renderers.Active.Count;
                return current;
            }
        }

        public int RenderDistance
        {
            get => settings.RenderDistance;
            set => SetRenderDistance(value);
        }

        /// <summary>Labs: whether a fixed area seals its outer faces; call <see cref="RemeshAll"/> after changing it.</summary>
        public bool SealEdges
        {
            get => settings.SealEdges;
            set => settings.SealEdges = value;
        }

        /// <summary>Wanted chunks not loaded yet.</summary>
        public int PendingCount => wanted.Count - wantedLoaded;

        /// <summary>True while the area loads in bulk, with the larger loading budget (see <see cref="StreamingSettings.LoadingBudgetMilliseconds"/>).</summary>
        public bool IsLoading => bulkLoading;

        private int MaxJobsInFlight => settings.MaxJobsInFlight > 0 ? settings.MaxJobsInFlight : Mathf.Max(4, JobsUtility.JobWorkerCount * 2);

        /// <summary>One frame of streaming around <paramref name="focus"/> (relative to the world origin). Call from Update.</summary>
        public void Update(Vector3 focus)
        {
            frameClock.Restart();
            lapStart = 0;
            StreamingPhases phases = default;
            budget = bulkLoading && settings.LoadingBudgetMilliseconds > settings.FrameBudgetMilliseconds
                ? settings.LoadingBudgetMilliseconds
                : settings.FrameBudgetMilliseconds;

            pipeline.Poll();
            phases.Poll = Lap();

            Vector3Int centre = world.Grid.WorldToChunk(focus);
            // Chunks only leave the wanted area when it changes; between changes, AddGenerated drops
            // late arrivals, so there's no need to scan every loaded chunk each frame.
            if (UpdateWanted(centre))
            {
                UnloadUnwanted(centre);
            }
            float chunkWidth = world.Grid.ChunkWorldSize.x;
            renderers.SetOverlayFocus(centre, settings.OverlayRadius > 0f ? Mathf.CeilToInt(settings.OverlayRadius / chunkWidth) : -1);
            phases.Area = Lap();

            // Meshes first, with half the budget: they change what's on screen. Each stage takes at
            // least one result a frame, so neither can starve the other.
            ApplyMeshes(budget * 0.5);
            phases.ApplyMeshes = Lap();
            AddGenerated(budget);
            phases.AddChunks = Lap();
            StartGenerating();
            phases.StartGeneration = Lap();
            StartMeshing();
            phases.StartMeshing = Lap();
            UpdateColliders(focus);
            phases.Colliders = Lap();

            // The bulk load is over once everything wanted has been started, loaded and meshed.
            if (bulkLoading && loadCursor >= wanted.Count && waitingForColumns.Count == 0 && PendingCount == 0
                && pipeline.GeneratingCount == 0 && pipeline.MeshingCount == 0 && dirtyScratch.Count == 0)
            {
                bulkLoading = false;
            }

            pipeline.Flush();
            stats.FrameMilliseconds = frameClock.Elapsed.TotalMilliseconds;
            stats.Phases = phases;
        }

        // Milliseconds since the previous lap of this frame.
        private double Lap()
        {
            double now = frameClock.Elapsed.TotalMilliseconds;
            double lap = now - lapStart;
            lapStart = now;
            return lap;
        }

        /// <summary>
        /// Rebuilds every edited chunk's mesh now, and its collider if it has one, so an edit
        /// shows (and can be walked on) in the frame it was made. Call from LateUpdate, after
        /// the frame's edits.
        /// </summary>
        public void RebuildEdited(Vector3 focus)
        {
            scratch.Clear();
            world.CollectDirty(dirtyScratch);
            foreach (Vector3Int dirtyCoord in dirtyScratch)
            {
                if (meshedOnce.Contains(dirtyCoord))
                {
                    scratch.Add(dirtyCoord);
                    if (scratch.Count >= Mathf.Max(1, settings.MaxEditsPerFrame))
                    {
                        break;
                    }
                }
            }
            if (scratch.Count == 0)
            {
                return;
            }

            ChunkMeshSettings current = meshSettings();
            foreach (Vector3Int coord in scratch)
            {
                Chunk chunk = world.Chunks[coord];

                // A mesh already on its way shows the chunk before the edit; take it out of the way first.
                if (pipeline.TryTakeMeshNow(coord, out ChunkPipeline.MeshResult stale))
                {
                    Apply(coord, chunk, stale);
                }

                ChunkMeshSettings chunkSettings = ForChunk(current, coord);
                if (HasNoSurface(chunk, chunkSettings))
                {
                    MeshUniform(coord, chunk);
                    continue;
                }
                pipeline.StartMesh(coord, chunk, chunkSettings);
                if (pipeline.TryTakeMeshNow(coord, out ChunkPipeline.MeshResult result))
                {
                    Apply(coord, chunk, result);
                }

                if (settings.Colliders && renderers.TryGet(coord, out ChunkRenderer chunkRenderer) && chunkRenderer.Collider != null
                    && IsWithinColliderRadius(coord, focus))
                {
                    BakeNow(coord, chunkRenderer);
                }
            }
        }

        /// <summary>
        /// Rebuilds every loaded chunk's mesh over the coming frames (a meshing setting changed);
        /// the old meshes stay until their replacements arrive.
        /// </summary>
        public void RemeshAll()
        {
            meshedOnce.Clear();
            foreach (Chunk chunk in world.Chunks.Values)
            {
                chunk.MarkDirty();
            }
        }

        /// <summary>
        /// True once every chunk in the column is loaded, meshed and, near the focus, has its
        /// collider: somewhere a player can stand (M6).
        /// </summary>
        public bool IsColumnSettled(Vector2Int column)
        {
            for (int layer = 0; layer < settings.HeightInChunks; layer++)
            {
                var coord = new Vector3Int(column.x, layer, column.y);
                if (!world.TryGetChunk(coord, out Chunk chunk) || !meshedOnce.Contains(coord) || pipeline.IsMeshing(coord))
                {
                    return false;
                }
                if (settings.Colliders && renderers.TryGet(coord, out ChunkRenderer chunkRenderer) && chunkRenderer.Collider != null
                    && chunkRenderer.Mesh.vertexCount > 0 && chunkRenderer.Collider.Version != chunkRenderer.MeshVersion)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Unloads every chunk (before regenerating): renderers go back to the pool, and <see cref="ChunkUnloaded"/> is raised for each.</summary>
        public void UnloadAll()
        {
            scratch.Clear();
            scratch.AddRange(world.Chunks.Keys);
            foreach (Vector3Int coord in scratch)
            {
                pipeline.CancelMesh(coord);
                Release(coord);
                world.Unload(coord);
                ChunkUnloaded?.Invoke(coord);
            }
            meshedOnce.Clear();
            waitingForColumns.Clear();
            wantedLoaded = 0;
            bulkLoading = true;
            collidersCentre = null;
        }

        /// <summary>
        /// Waits for every job and frees their buffers. Touches no renderer and raises no event,
        /// so it's safe while the scene is torn down; <see cref="UnloadAll"/> first to pool the renderers.
        /// </summary>
        public void Dispose()
        {
            pipeline.Dispose();
        }

        // The wanted area only changes when the focus crosses into another chunk or the
        // distance changes, so it's rebuilt then, nearest chunks first.
        // True when the wanted area changed (and so chunks may have left it).
        private bool UpdateWanted(Vector3Int centre)
        {
            if (wantedCentre == centre && wantedDistance == settings.RenderDistance)
            {
                return false;
            }
            wantedCentre = centre;
            wantedDistance = settings.RenderDistance;
            StreamingArea.Collect(centre, settings.RenderDistance, settings.HeightInChunks, wanted);
            if (settings.FixedColumns.HasValue)
            {
                // A fixed area is the same circle around the focus, cut to its square of columns.
                RectInt area = settings.FixedColumns.Value;
                wanted.RemoveAll(coord => !area.Contains(new Vector2Int(coord.x, coord.z)));
            }
            wantedSet.Clear();
            wantedSet.UnionWith(wanted);
            loadCursor = 0;
            wantedLoaded = 0;
            foreach (Vector3Int coord in wanted)
            {
                if (world.IsLoaded(coord))
                {
                    wantedLoaded++;
                }
            }
            return true;
        }

        // Chunks just outside the wanted area are kept (one chunk of slack), so walking back
        // and forth across a chunk border doesn't load and unload the same chunks.
        private void UnloadUnwanted(Vector3Int centre)
        {
            int keep = (settings.RenderDistance + 1) * (settings.RenderDistance + 1);
            scratch.Clear();
            foreach (Vector3Int coord in world.Chunks.Keys)
            {
                if (!wantedSet.Contains(coord) && StreamingArea.HorizontalDistanceSquared(coord, centre) > keep)
                {
                    scratch.Add(coord);
                }
            }
            foreach (Vector3Int coord in scratch)
            {
                pipeline.CancelMesh(coord);
                Release(coord);
                meshedOnce.Remove(coord);
                world.Unload(coord);
                ChunkUnloaded?.Invoke(coord);
            }
        }

        // A chunk that finished generating after the focus moved away from it is dropped.
        private void AddGenerated(double untilMilliseconds)
        {
            int keep = (settings.RenderDistance + 1) * (settings.RenderDistance + 1);
            bool first = true;
            while ((first || Before(untilMilliseconds)) && pipeline.TryTakeGenerated(out Vector3Int coord, out Chunk chunk))
            {
                first = false;
                bool stillWanted = wantedSet.Contains(coord)
                                   || StreamingArea.HorizontalDistanceSquared(coord, wantedCentre.Value) <= keep;
                if (world.IsLoaded(coord) || !stillWanted)
                {
                    chunk.Release();
                    continue;
                }
                world.Add(coord, chunk);
                if (wantedSet.Contains(coord))
                {
                    wantedLoaded++;
                }
                ChunkLoaded?.Invoke(coord);
            }
        }

        // Nearest first, past the chunks already loaded or on their way (the cursor), up to the job
        // limit. Chunks the pipeline knows without a job (air above the surface, rock below it) don't
        // count against the limit, only against the frame budget; chunks whose column heights
        // aren't ready yet wait and go first next frame.
        private void StartGenerating()
        {
            int limit = MaxJobsInFlight;
            for (int i = 0; i < waitingForColumns.Count && pipeline.GenerationJobsRunning < limit
                            && (i < limit || Before(budget));)
            {
                Vector3Int waiting = waitingForColumns[i];
                if (!wantedSet.Contains(waiting) || world.IsLoaded(waiting) || pipeline.StartGeneration(waiting))
                {
                    waitingForColumns.RemoveAt(i);
                }
                else
                {
                    i++;
                }
            }
            // At least a job limit's worth of starts a frame, as before; more while the budget lasts.
            int started = 0;
            while (loadCursor < wanted.Count && pipeline.GenerationJobsRunning < limit
                   && (started < limit || Before(budget)))
            {
                started++;
                Vector3Int coord = wanted[loadCursor];
                loadCursor++;
                if (world.IsLoaded(coord) || pipeline.IsGenerating(coord))
                {
                    continue;
                }
                if (world.TryRestoreKept(coord, out _))
                {
                    wantedLoaded++;
                    ChunkLoaded?.Invoke(coord);
                    continue;
                }
                if (!pipeline.StartGeneration(coord))
                {
                    waitingForColumns.Add(coord);
                }
            }
        }

        private void ApplyMeshes(double untilMilliseconds)
        {
            bool first = true;
            while ((first || Before(untilMilliseconds)) && pipeline.TryTakeMeshed(out ChunkPipeline.MeshResult result))
            {
                first = false;
                if (!world.TryGetChunk(result.Coord, out Chunk chunk))
                {
                    result.Discard();
                    continue;
                }
                Apply(result.Coord, chunk, result);
            }
        }

        // Dirty chunks, nearest first, up to the job limit; a uniform chunk has no surface and needs no job.
        private void StartMeshing()
        {
            int limit = MaxJobsInFlight;
            if (pipeline.MeshingCount >= limit)
            {
                return;
            }
            ChunkMeshSettings current = meshSettings();

            // The world's dirty chunks in the wanted area, nearest first.
            world.CollectDirty(dirtyScratch);
            dirtyScratch.RemoveAll(coord => !wantedSet.Contains(coord) || pipeline.IsMeshing(coord));
            if (wantedCentre.HasValue)
            {
                Vector3Int centre = wantedCentre.Value;
                dirtyScratch.Sort((a, b) => DistanceSquared(a, centre).CompareTo(DistanceSquared(b, centre)));
            }
            foreach (Vector3Int coord in dirtyScratch)
            {
                if (pipeline.MeshingCount >= limit)
                {
                    break;
                }
                Chunk chunk = world.Chunks[coord];
                ChunkMeshSettings chunkSettings = ForChunk(current, coord);
                if (HasNoSurface(chunk, chunkSettings))
                {
                    MeshUniform(coord, chunk);
                    continue;
                }
                pipeline.StartMesh(coord, chunk, chunkSettings);
            }
        }

        // A uniform chunk has no surface, unless it's solid with a sealed face: that face is a wall.
        private static bool HasNoSurface(Chunk chunk, ChunkMeshSettings settings)
        {
            return chunk.IsUniform && (settings.Seal == MeshSeal.None || chunk.UniformDensity < settings.IsoLevel);
        }

        // A chunk's own seal: its faces on a sealed fixed area's outside, none otherwise (the
        // config's seal is for a single chunk, which seals every face).
        private ChunkMeshSettings ForChunk(ChunkMeshSettings settings, Vector3Int coord)
        {
            MeshSeal seal = this.settings.SealEdges && this.settings.FixedColumns.HasValue
                ? StreamingArea.FixedSeal(this.settings.FixedColumns.Value, coord)
                : MeshSeal.None;
            return settings.WithSeal(seal);
        }

        // Shows a finished mesh: the chunk's renderer, given one if this is its first surface.
        private void Apply(Vector3Int coord, Chunk chunk, ChunkPipeline.MeshResult result)
        {
            meshedOnce.Add(coord);
            if (!renderers.TryGet(coord, out ChunkRenderer chunkRenderer))
            {
                if (result.VertexCount == 0)
                {
                    result.Discard();
                    ChunkMeshed?.Invoke(coord, null);
                    return;
                }
                chunkRenderer = renderers.Take(world, coord, chunk, () => ForChunk(meshSettings(), coord));
            }

            // A collider bake may still be reading the old mesh.
            FinishBake(coord, chunkRenderer);
            stats.Vertices -= chunkRenderer.LastBuildStats.VertexCount;
            stats.Triangles -= chunkRenderer.LastBuildStats.TriangleCount;
            chunkRenderer.ApplyMesh(result);
            stats.Vertices += chunkRenderer.LastBuildStats.VertexCount;
            stats.Triangles += chunkRenderer.LastBuildStats.TriangleCount;
            ChunkMeshed?.Invoke(coord, chunkRenderer);
        }

        // All air or all solid: no surface. A renderer it still has (it was dug or filled out of its surface) is emptied.
        private void MeshUniform(Vector3Int coord, Chunk chunk)
        {
            chunk.MarkClean();
            meshedOnce.Add(coord);
            renderers.TryGet(coord, out ChunkRenderer chunkRenderer);
            if (chunkRenderer != null && chunkRenderer.Mesh.vertexCount > 0)
            {
                FinishBake(coord, chunkRenderer);
                stats.Vertices -= chunkRenderer.LastBuildStats.VertexCount;
                stats.Triangles -= chunkRenderer.LastBuildStats.TriangleCount;
                chunkRenderer.ClearMesh();
                chunkRenderer.Collider?.Clear();
            }
            ChunkMeshed?.Invoke(coord, chunkRenderer);
        }

        // Cooks colliders for chunks near the focus and drops those well outside, then takes finished bakes.
        private void UpdateColliders(Vector3 focus)
        {
            if (!settings.Colliders)
            {
                return;
            }

            float chunkWidth = world.Grid.ChunkWorldSize.x;
            float dropDistance = settings.ColliderRadius + chunkWidth;
            while (pipeline.TryTakeBaked(out Vector3Int coord, out int version))
            {
                // A bake that finishes after the focus moved well away isn't needed any more.
                if (renderers.TryGet(coord, out ChunkRenderer chunkRenderer) && chunkRenderer.Collider != null
                    && chunkRenderer.MeshVersion == version && HorizontalDistance(coord, focus) <= dropDistance)
                {
                    chunkRenderer.Collider.Assign(chunkRenderer.Mesh, version);
                }
            }

            // Shapes only exist near the focus, so far ones are dropped only when the focus moves to
            // another chunk; each frame only the chunks around the focus are checked, not every renderer.
            Vector3Int centre = world.Grid.WorldToChunk(focus);
            if (collidersCentre != centre)
            {
                collidersCentre = centre;
                foreach (KeyValuePair<Vector3Int, ChunkRenderer> entry in renderers.Active)
                {
                    ChunkCollider chunkCollider = entry.Value.Collider;
                    // Well outside (a chunk of slack, so the edge doesn't flicker): no shape needed.
                    if (chunkCollider != null && chunkCollider.Version >= 0 && !pipeline.IsBaking(entry.Key)
                        && HorizontalDistance(entry.Key, focus) > dropDistance)
                    {
                        chunkCollider.Clear();
                    }
                }
            }

            int reach = Mathf.CeilToInt(settings.ColliderRadius / chunkWidth) + 1;
            for (int dz = -reach; dz <= reach; dz++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    for (int layer = 0; layer < settings.HeightInChunks; layer++)
                    {
                        var coord = new Vector3Int(centre.x + dx, layer, centre.z + dz);
                        if (!renderers.TryGet(coord, out ChunkRenderer chunkRenderer) || chunkRenderer.Collider == null)
                        {
                            continue;
                        }
                        ChunkCollider chunkCollider = chunkRenderer.Collider;
                        if (HorizontalDistance(coord, focus) > settings.ColliderRadius
                            || chunkCollider.Version == chunkRenderer.MeshVersion || pipeline.IsBaking(coord))
                        {
                            continue;
                        }
                        if (chunkRenderer.Mesh.vertexCount == 0)
                        {
                            chunkCollider.Assign(chunkRenderer.Mesh, chunkRenderer.MeshVersion);
                            continue;
                        }
                        pipeline.StartBake(coord, chunkRenderer.Mesh, chunkRenderer.MeshVersion, chunkCollider.CookingOptions);
                    }
                }
            }
        }

        private void SetRenderDistance(int distance)
        {
            // A larger area loads in bulk again, with the loading budget.
            if (distance > settings.RenderDistance)
            {
                bulkLoading = true;
            }
            settings.RenderDistance = distance;
        }

        private void BakeNow(Vector3Int coord, ChunkRenderer chunkRenderer)
        {
            FinishBake(coord, chunkRenderer);
            if (chunkRenderer.Mesh.vertexCount == 0)
            {
                chunkRenderer.Collider.Assign(chunkRenderer.Mesh, chunkRenderer.MeshVersion);
                return;
            }
            pipeline.StartBake(coord, chunkRenderer.Mesh, chunkRenderer.MeshVersion, chunkRenderer.Collider.CookingOptions);
            FinishBake(coord, chunkRenderer);
        }

        // Waits for a running bake of the chunk's mesh, and keeps its shape if it's for the current mesh.
        private void FinishBake(Vector3Int coord, ChunkRenderer chunkRenderer)
        {
            int version = pipeline.FinishBakeNow(coord);
            if (version >= 0 && chunkRenderer.Collider != null && version == chunkRenderer.MeshVersion)
            {
                chunkRenderer.Collider.Assign(chunkRenderer.Mesh, version);
            }
        }

        private void Release(Vector3Int coord)
        {
            if (renderers.TryGet(coord, out ChunkRenderer chunkRenderer))
            {
                pipeline.CancelBake(coord);
                stats.Vertices -= chunkRenderer.LastBuildStats.VertexCount;
                stats.Triangles -= chunkRenderer.LastBuildStats.TriangleCount;
                renderers.Release(coord);
            }
        }

        private bool IsWithinColliderRadius(Vector3Int coord, Vector3 focus)
        {
            return HorizontalDistance(coord, focus) <= settings.ColliderRadius;
        }

        // From the focus to the chunk's nearest point, horizontally.
        private float HorizontalDistance(Vector3Int coord, Vector3 focus)
        {
            Vector3 min = world.Grid.ChunkOrigin(coord);
            Vector3 max = min + world.Grid.ChunkWorldSize;
            float dx = Mathf.Max(min.x - focus.x, 0f, focus.x - max.x);
            float dz = Mathf.Max(min.z - focus.z, 0f, focus.z - max.z);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static int DistanceSquared(Vector3Int a, Vector3Int b)
        {
            Vector3Int d = a - b;
            return d.x * d.x + d.y * d.y + d.z * d.z;
        }

        // Whether this frame's streaming has run for less than the given time so far.
        private bool Before(double milliseconds)
        {
            return frameClock.Elapsed.TotalMilliseconds < milliseconds;
        }
    }
}
