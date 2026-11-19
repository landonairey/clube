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

        /// <summary>
        /// The budget while the bulk load has moved on past the drawn chunks (<see cref="DetailDistance"/>) to the hidden
        /// ones beyond, which the coarse terrain stands in for (GL324): the game plays smoothly while they load. 0 or less
        /// keeps the loading budget to the end.
        /// </summary>
        public float FarLoadingBudgetMilliseconds;

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

        /// <summary>
        /// Radius in chunks, horizontally, within which loaded chunks are drawn (P18); past it they stay
        /// loaded (for edits, colliders, the player) but hidden, and something coarser draws the ground
        /// there. Columns holding an edited chunk are always drawn. 0: every loaded chunk is drawn.
        /// </summary>
        public int DetailDistance;

        /// <summary>
        /// Layers the height map proves all air or all rock are neither generated, stored nor meshed until something writes
        /// to them (GL773). Streamed heightfield worlds only: a fixed area loads whole.
        /// </summary>
        public bool SparseChunks;
    }

    /// <summary>Live counts from a <see cref="WorldStreamer"/>, for readouts.</summary>
    public struct StreamingStats
    {
        public int Wanted;
        public int Pending;
        public int Generating;
        public int Meshing;
        public int Baking;
        /// <summary>Chunks with a surface, each with its own mesh (not a renderer since GL776).</summary>
        public int Renderers;

        /// <summary>Regions the chunks are drawn by (GL776): those holding a surface, those drawing now, and those waiting to be merged again.</summary>
        public int Regions;
        public int RegionsDrawn;
        public int RegionsWaiting;
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

        /// <summary>Proving chunk columns' sky and deep rock uniform (GL773).</summary>
        public double Prove;

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
            return $"poll {Poll:0.00} · area {Area:0.00} · add {AddChunks:0.00} · prove {Prove:0.00} · generate {StartGeneration:0.00} · " +
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

        // The wanted columns (x, z), nearest first; every layer of each is wanted. Kept per column, not
        // per chunk: at long render distances the chunks run to hundreds of thousands, and the area
        // is redone every time the focus crosses a chunk border.
        private readonly List<Vector2Int> wantedColumns = new List<Vector2Int>();
        // The columns left behind, still to be unloaded (GL771).
        private const double PlayingUnloadMilliseconds = 2.0;
        private const double EditRebuildMilliseconds = 8.0;
        private readonly List<Vector2Int> unloading = new List<Vector2Int>();
        private int unloadingNext;
        private int unloadingLayer;

        // The dirty chunks waiting for a mesh, held ones parked (GL774), and those this frame looks at.
        private readonly DirtyChunks dirtyChunks = new DirtyChunks();
        // Chunks whose collider was given a shape (some since cleared): the ones a move may have left far away.
        private readonly HashSet<Vector3Int> shaped = new HashSet<Vector3Int>();
        private readonly List<Vector3Int> dirtyScratch = new List<Vector3Int>();
        // Whether the last look found a dirty chunk that may be meshed (StartMeshing).
        private bool hasChunksToMesh;
        // Whether the last look left dirty chunks unlooked at, the frame's starts being used up.
        private bool meshingBehind;
        // Dirty chunks allowed to wait before generation pauses (GL759).
        private const int MostMeshBacklog = 600;

        // Wanted chunks that came up before their column's heights were ready (ChunkPipeline.StartGeneration).
        private readonly List<Vector3Int> waitingForColumns = new List<Vector3Int>();
        private readonly HashSet<Vector3Int> meshedOnce = new HashSet<Vector3Int>();
        private readonly List<Vector3Int> scratch = new List<Vector3Int>();
        private readonly Stopwatch frameClock = new Stopwatch();
        private Vector3Int? wantedCentre;
        private int wantedDistance;
        // The next wanted column to start, and the next layer in it.
        private int loadCursor;
        private int loadLayer;
        // Wanted chunks that are loaded, kept up to date so PendingCount needn't scan the area.
        private int wantedLoaded;
        // Whether the area is loading in bulk (LoadingBudgetMilliseconds applies), and this frame's budget.
        private bool bulkLoading = true;
        private float budget;
        // The chunk the focus was in when far colliders were last dropped.
        private Vector3Int? collidersCentre;
        // Set when a mesh changes or a bake finishes: the chunks round the focus are checked for bakes only then, or when
        // the focus enters another chunk (checking every frame cost 0.5 ms with nothing to do, GL560).
        private bool collidersDirty = true;
        private float collidersRadius = -1f;
        private Vector3 collidersFocus;
        private StreamingStats stats;
        private double lapStart;

        // The column the detail distance is measured from (P18), and a number that changes whenever
        // which columns are drawn could have, unique across streamers.
        private Vector2Int drawCentre;
        private bool hasDrawCentre;
        private static int drawVersions;

        public WorldStreamer(
            World world, ChunkPipeline pipeline, ChunkRendererPool renderers, Func<ChunkMeshSettings> meshSettings,
            StreamingSettings settings)
        {
            this.world = world;
            this.pipeline = pipeline;
            this.renderers = renderers;
            this.meshSettings = meshSettings;
            this.settings = settings;
            DrawVersion = ++drawVersions;
            renderers.SetDrawRule(IsRegionWithin, RegionHasEdits, world.HasEditsIn);
            world.ChunkMaterialised += NoteMaterialised;
            world.ColumnEdited += NoteColumnEdited;
            world.ProvenLayersChanged += RaiseProvenLayersChanged;
        }

        /// <summary>How many of a column's layers are proven uniform changed (<see cref="World.ProvenLayersIn"/>).</summary>
        public event Action<Vector2Int> ProvenLayersChanged;

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
                current.Wanted = WantedCount;
                current.Pending = PendingCount;
                current.Generating = pipeline.GeneratingCount;
                current.Meshing = pipeline.MeshingCount;
                current.Baking = pipeline.BakingCount;
                current.Renderers = renderers.Active.Count;
                current.Regions = renderers.RegionCount;
                current.RegionsDrawn = renderers.DrawnRegionCount;
                current.RegionsWaiting = renderers.DirtyRegionCount;
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

        /// <summary>
        /// GL653: true for a chunk column something still has to write into (<see cref="IColumnHold"/>). Its chunks load,
        /// but those with a surface aren't meshed for the first time until it lets go, so they first appear as written;
        /// null for none. The focus's own column and those touching it are never held: there is always ground to stand on.
        /// </summary>
        public Func<Vector2Int, bool> Hold { get; set; }

        /// <summary>True when <see cref="Hold"/> keeps the column from being shown now.</summary>
        /// <summary>
        /// Set while the load is waited for behind a curtain (<see cref="WorldView.Hurry"/>): nothing is watched, so a bulk
        /// load takes twice its loading budget of each frame.
        /// </summary>
        public bool Hurry { get; set; }

        private const float HurryBudgetScale = 2f;

        public bool IsColumnHeld(Vector2Int column)
        {
            if (Hold == null || !wantedCentre.HasValue)
            {
                return false;
            }
            Vector3Int centre = wantedCentre.Value;
            return (Mathf.Abs(column.x - centre.x) > 1 || Mathf.Abs(column.y - centre.z) > 1) && Hold(column);
        }

        // A chunk never shown yet, with a surface, in a held column.
        private bool IsHeld(Vector3Int coord)
        {
            return Hold != null && !meshedOnce.Contains(coord) && IsColumnHeld(new Vector2Int(coord.x, coord.z))
                   && world.TryPeekChunk(coord, out Chunk chunk) && !chunk.IsUniform;
        }

        /// <summary>Wanted chunks not loaded yet.</summary>
        public int PendingCount => WantedCount - wantedLoaded;

        private int WantedCount => wantedColumns.Count * settings.HeightInChunks;

        /// <summary>True while the area loads in bulk, with the larger loading budget (see <see cref="StreamingSettings.LoadingBudgetMilliseconds"/>).</summary>
        public bool IsLoading => bulkLoading;

        /// <summary>
        /// True while the bulk load is still on the chunks that will be drawn. False once only the hidden ones past the
        /// detail distance are left (GL759): at a long render distance those take minutes, with the game being played.
        /// </summary>
        public bool IsLoadingDrawnArea => bulkLoading && !IsPlaying && !IsLoadingHiddenOnly;

        // GL770: a load curtain has lifted and is not back: someone is walking about, and every frame counts. At a long
        // render distance the bulk load is never over, so it can't be what says the game is being played.
        private bool IsPlaying => curtainLifted && !Hurry;

        // Starting jobs keeps to the frame's budget: the game is being played, or the bulk load is on to hidden chunks (GL759).
        private bool RationsStarts => IsPlaying || (bulkLoading && !Hurry && IsLoadingHiddenOnly);

        // Starts a frame may make past its budget: the fewest while playing, a worker's share when rationed, else the whole job limit.
        private int StartsPastBudget => IsPlaying ? PlayingStartsPastBudget
            : RationsStarts ? Mathf.Max(4, JobsUtility.JobWorkerCount * JobsPerWorker) : MaxJobsInFlight;

        // While playing: milliseconds a frame for streaming, and the job starts a frame makes however late it is (GL770).
        private const float PlayingBudgetMilliseconds = 5f;
        private const double PlayingAddShare = 0.7;
        private const int PlayingStartsPastBudget = 2;

        // The layers under and over a column's ground that load first while playing, the columns round the player that always
        // do, and the columns' ground layers remembered.
        private const int GroundLayersBelow = 3, GroundLayersAbove = 6, MinimumGroundReach = 8, MostGroundLayersKept = 20000;

        // Chunks a road or settlement piece is waiting for: they load before anything else, for its ground is held until then.
        private readonly Queue<Vector3Int> requested = new Queue<Vector3Int>();
        private readonly HashSet<Vector3Int> requestedSet = new HashSet<Vector3Int>();

        /// <summary>Loads these chunks ahead of the rest (GL770): something near the player can't be shown without them.</summary>
        public void Request(List<Vector3Int> chunks)
        {
            foreach (Vector3Int coord in chunks)
            {
                if (requestedSet.Add(coord))
                {
                    requested.Enqueue(coord);
                }
            }
        }
        private readonly Dictionary<Vector2Int, int> groundLayers = new Dictionary<Vector2Int, int>();
        private int groundCursor;

        private bool curtainLifted;
        private bool wasHurrying;

        // True once the bulk load has started every drawn column and is on to the hidden ones past the detail distance.
        private bool IsLoadingHiddenOnly =>
            settings.FarLoadingBudgetMilliseconds > 0f && settings.DetailDistance > 0 && hasDrawCentre
            && (loadCursor >= wantedColumns.Count || !IsWithin(wantedColumns[loadCursor], drawCentre, DrawRadius + 1));

        // Jobs in flight per worker thread: two while playing, so a frame's results are few; more while the area loads in
        // bulk (GL653), when frames are long and a worker gets through several fills in one. Measured at the MapGen start:
        // with two, the workers idled most of each frame and the load ran at some 700 chunks a frame.
        private const int JobsPerWorker = 2;
        private const int BulkJobsPerWorker = 8;

        private int MaxJobsInFlight => settings.MaxJobsInFlight > 0
            ? settings.MaxJobsInFlight
            : Mathf.Max(4, JobsUtility.JobWorkerCount * (bulkLoading && !IsPlaying ? BulkJobsPerWorker : JobsPerWorker));

        /// <summary>One frame of streaming around <paramref name="focus"/> (relative to the world origin). Call from Update.</summary>
        public void Update(Vector3 focus)
        {
            frameClock.Restart();
            lapStart = 0;
            StreamingPhases phases = default;
            curtainLifted |= wasHurrying && !Hurry;
            wasHurrying = Hurry;
            budget = IsPlaying ? Mathf.Max(PlayingBudgetMilliseconds, settings.FrameBudgetMilliseconds)
                : bulkLoading && Hurry ? Mathf.Max(settings.LoadingBudgetMilliseconds * HurryBudgetScale, settings.FrameBudgetMilliseconds)
                : bulkLoading && settings.LoadingBudgetMilliseconds > settings.FrameBudgetMilliseconds
                ? (IsLoadingHiddenOnly ? Mathf.Max(settings.FarLoadingBudgetMilliseconds, settings.FrameBudgetMilliseconds) : settings.LoadingBudgetMilliseconds)
                : settings.FrameBudgetMilliseconds;

            using (FrameCost.Of("Stream: poll"))
            {
                pipeline.Poll();
                TakeMaterialised();
            }
            phases.Poll = Lap();

            Vector3Int centre = world.Grid.WorldToChunk(focus);
            // Chunks only leave the wanted area when it changes; between changes, AddGenerated drops
            // late arrivals, so there's no need to scan every loaded chunk each frame.
            using (FrameCost.Of("Stream: wanted and unload"))
            {
                if (UpdateWanted(centre))
                {
                    UnloadUnwanted(centre);
                }
                UnloadSome(centre);
            }
            float chunkWidth = world.Grid.ChunkWorldSize.x;
            renderers.SetOverlayFocus(centre, settings.OverlayRadius > 0f ? Mathf.CeilToInt(settings.OverlayRadius / chunkWidth) : -1);
            UpdateDrawRadius(centre.y);
            UpdateDrawCentre(new Vector2Int(centre.x, centre.z));
            phases.Area = Lap();

            // Meshes first, with half the budget: they change what's on screen. Each stage takes at
            // least one result a frame, so neither can starve the other.
            using (FrameCost.Of("Stream: apply meshes"))
            {
                ApplyMeshes(budget * 0.5);
            }
            phases.ApplyMeshes = Lap();
            // While playing some of the budget is left for starting what is nearest.
            using (FrameCost.Of("Stream: add chunks"))
            {
                AddGenerated(IsPlaying ? budget * PlayingAddShare : budget);
            }
            phases.AddChunks = Lap();
            using (FrameCost.Of("Stream: prove ahead"))
            {
                ProveAhead();
            }
            phases.Prove = Lap();
            using (FrameCost.Of("Stream: start generating"))
            {
                StartGenerating();
            }
            phases.StartGeneration = Lap();
            using (FrameCost.Of("Stream: start meshing"))
            {
                StartMeshing();
            }
            phases.StartMeshing = Lap();
            using (FrameCost.Of("Stream: colliders"))
            {
                UpdateColliders(focus);
            }
            phases.Colliders = Lap();

            // The bulk load is over once everything wanted has been started, loaded and meshed.
            if (bulkLoading && loadCursor >= wantedColumns.Count && waitingForColumns.Count == 0 && PendingCount == 0
                && pipeline.GeneratingCount == 0 && pipeline.MeshingCount == 0 && !hasChunksToMesh)
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
        /// the frame's edits. Then merges the drawn regions whose chunks changed this frame or before (GL776).
        /// </summary>
        public void RebuildEdited(Vector3 focus)
        {
            using (FrameCost.Scope edited = FrameCost.Of("Chunk drawing: edited chunks"))
            {
                RebuildEditedChunks(focus);
            }
            using FrameCost.Scope regionCost = FrameCost.Of("Chunk drawing: regions");
            BuildRegions(focus);
        }

        // Regions round the focus are merged whatever the budget; then the nearest drawn ones, and those just past the
        // detail distance (so they're ready when the focus walks up), to a budget: bulk loading gets ten times as much.
        private const int RegionVertexBudget = 300000;
        private const int LoadingRegionVertexBudget = 3000000;

        private void BuildRegions(Vector3 focus)
        {
            float chunkWidth = world.Grid.ChunkWorldSize.x;
            float buildWithin = settings.DetailDistance > 0
                ? (Mathf.Max(DrawRadius, 0) + 2 * Mathf.Max(renderers.RegionSpan.x, renderers.RegionSpan.z) + 1) * chunkWidth
                : float.PositiveInfinity;
            renderers.BuildRegions(focus, buildWithin, IsLoadingDrawnArea ? LoadingRegionVertexBudget : RegionVertexBudget);
        }

        private void RebuildEditedChunks(Vector3 focus)
        {
            using (FrameCost.Scope taking = FrameCost.Of("Chunk drawing: take materialised"))
            {
                TakeMaterialised();
            }
            scratch.Clear();
            // A chunk shown once and dirty again was edited. Held chunks were never shown, so the parked ones needn't be looked at.
            using (FrameCost.Scope taking = FrameCost.Of("Chunk drawing: take dirty"))
            {
                dirtyChunks.Take(world);
            }
            foreach (Vector3Int dirtyCoord in dirtyChunks.Waiting)
            {
                if (meshedOnce.Contains(dirtyCoord) && world.TryPeekChunk(dirtyCoord, out Chunk dirtyChunk) && dirtyChunk.IsDirty)
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
            // GL771: what doesn't fit in the time stays dirty for the next frame.
            long began = System.Diagnostics.Stopwatch.GetTimestamp();
            long most = (long)(EditRebuildMilliseconds * 0.001 * System.Diagnostics.Stopwatch.Frequency);
            foreach (Vector3Int coord in scratch)
            {
                if (IsPlaying && System.Diagnostics.Stopwatch.GetTimestamp() - began > most)
                {
                    break;
                }
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
        public bool IsColumnSettled(Vector2Int column, bool colliders = true)
        {
            if (IsColumnHeld(column))
            {
                return false;
            }
            for (int layer = 0; layer < settings.HeightInChunks; layer++)
            {
                var coord = new Vector3Int(column.x, layer, column.y);
                if (!world.IsLoaded(coord) || !IsMeshed(coord) || pipeline.IsMeshing(coord))
                {
                    return false;
                }
                if (colliders && settings.Colliders && renderers.TryGet(coord, out ChunkRenderer chunkRenderer) && chunkRenderer.Collider != null
                    && chunkRenderer.Mesh.vertexCount > 0 && chunkRenderer.Collider.Version != chunkRenderer.MeshVersion)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>How far a column has got, for load reports: its layers loaded, those meshed (or known to have no surface), and whether it is held.</summary>
        public void ColumnState(Vector2Int column, out int loaded, out int meshed, out bool held)
        {
            loaded = meshed = 0;
            for (int layer = 0; layer < settings.HeightInChunks; layer++)
            {
                var coord = new Vector3Int(column.x, layer, column.y);
                loaded += world.IsLoaded(coord) ? 1 : 0;
                meshed += IsMeshed(coord) && !pipeline.IsMeshing(coord) ? 1 : 0;
            }
            held = IsColumnHeld(column);
        }

        // Meshed, or proven to have nothing to mesh and not changed since.
        private bool IsMeshed(Vector3Int coord)
        {
            return meshedOnce.Contains(coord)
                   || (world.IsProvenUniform(coord) && (!world.TryPeekChunk(coord, out Chunk chunk) || !chunk.IsDirty));
        }

        /// <summary>Unloads every chunk (before regenerating): renderers go back to the pool, and <see cref="ChunkUnloaded"/> is raised for each.</summary>
        public void UnloadAll()
        {
            world.ForgetAllProofs();
            proving.Clear();
            proveCursor = 0;
            materialised.Clear();
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
            dirtyChunks.Clear();
            waitingForColumns.Clear();
            wantedLoaded = 0;
            bulkLoading = true;
            collidersCentre = null;
            collidersDirty = true;
        }

        /// <summary>
        /// Waits for every job and frees their buffers. Touches no renderer and raises no event,
        /// so it's safe while the scene is torn down; <see cref="UnloadAll"/> first to pool the renderers.
        /// </summary>
        public void Dispose()
        {
            world.ChunkMaterialised -= NoteMaterialised;
            world.ColumnEdited -= NoteColumnEdited;
            world.ProvenLayersChanged -= RaiseProvenLayersChanged;
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
            StreamingArea.CollectColumns(new Vector2Int(centre.x, centre.z), settings.RenderDistance, wantedColumns);
            if (settings.FixedColumns.HasValue)
            {
                // A fixed area is the same circle around the focus, cut to its square of columns.
                RectInt area = settings.FixedColumns.Value;
                wantedColumns.RemoveAll(column => !area.Contains(column));
            }
            loadCursor = 0;
            loadLayer = 0;
            groundCursor = 0;
            proveCursor = 0;
            wantedLoaded = 0;
            int layers = settings.HeightInChunks;
            foreach (Vector2Int column in wantedColumns)
            {
                wantedLoaded += Mathf.Min(layers, world.LoadedLayersIn(column));
            }
            return true;
        }

        // Whether the chunk is in the wanted area: a column in the circle (and the fixed area), any layer.
        private bool IsWanted(Vector3Int coord)
        {
            if (!wantedCentre.HasValue || coord.y < 0 || coord.y >= settings.HeightInChunks)
            {
                return false;
            }
            var column = new Vector2Int(coord.x, coord.z);
            Vector3Int centre = wantedCentre.Value;
            return StreamingArea.IsColumnInRange(column, new Vector2Int(centre.x, centre.z), wantedDistance)
                   && (!settings.FixedColumns.HasValue || settings.FixedColumns.Value.Contains(column));
        }

        // Chunks just outside the wanted area are kept (one chunk of slack), so walking back
        // and forth across a chunk border doesn't load and unload the same chunks. Looks over the
        // loaded columns, not every loaded chunk.
        private void UnloadUnwanted(Vector3Int centre)
        {
            int keep = (settings.RenderDistance + 1) * (settings.RenderDistance + 1);
            unloading.Clear();
            unloadingNext = 0;
            unloadingLayer = 0;
            foreach (Vector2Int column in world.LoadedColumns.Keys)
            {
                int dx = column.x - centre.x;
                int dz = column.y - centre.z;
                if (dx * dx + dz * dz > keep)
                {
                    unloading.Add(column);
                }
            }
        }

        // GL771: the columns left behind go a few milliseconds a frame while playing: a whole edge of the loaded circle at
        // once was up to 50 ms each time the player crossed into a new column. A column come back into range stays.
        private void UnloadSome(Vector3Int centre)
        {
            if (unloadingNext >= unloading.Count)
            {
                return;
            }
            int keep = (settings.RenderDistance + 1) * (settings.RenderDistance + 1);
            double until = IsPlaying ? frameClock.Elapsed.TotalMilliseconds + PlayingUnloadMilliseconds : double.PositiveInfinity;
            for (; unloadingNext < unloading.Count; unloadingNext++, unloadingLayer = 0)
            {
                Vector2Int column = unloading[unloadingNext];
                int dx = column.x - centre.x;
                int dz = column.y - centre.z;
                if (dx * dx + dz * dz <= keep)
                {
                    continue;
                }
                world.ForgetColumnProof(column);
                for (; unloadingLayer < settings.HeightInChunks && world.LoadedLayersIn(column) > 0; unloadingLayer++)
                {
                    var coord = new Vector3Int(column.x, unloadingLayer, column.y);
                    if (!world.IsLoaded(coord))
                    {
                        continue;
                    }
                    pipeline.CancelMesh(coord);
                    Release(coord);
                    meshedOnce.Remove(coord);
                    world.Unload(coord);
                    ChunkUnloaded?.Invoke(coord);
                    if (!Before(until))
                    {
                        unloadingLayer++;
                        return;
                    }
                }
            }
            unloading.Clear();
            unloadingNext = 0;
        }

        // A chunk that finished generating after the focus moved away from it is dropped.
        private void AddGenerated(double untilMilliseconds)
        {
            int keep = (settings.RenderDistance + 1) * (settings.RenderDistance + 1);
            bool first = true;
            while ((first || Before(untilMilliseconds)) && pipeline.TryTakeGenerated(out Vector3Int coord, out Chunk chunk))
            {
                first = false;
                bool wanted = IsWanted(coord);
                bool stillWanted = wanted || StreamingArea.HorizontalDistanceSquared(coord, wantedCentre.Value) <= keep;
                if (world.IsLoaded(coord) || !stillWanted)
                {
                    chunk.Release();
                    continue;
                }
                world.Add(coord, chunk);
                if (wanted)
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
            int pastBudget = StartsPastBudget;
            for (int i = 0; i < waitingForColumns.Count && pipeline.GenerationJobsRunning < limit
                            && (i < pastBudget || Before(budget));)
            {
                Vector3Int waiting = waitingForColumns[i];
                if (!IsWanted(waiting) || world.IsLoaded(waiting) || TryStartGeneration(waiting))
                {
                    waitingForColumns.RemoveAt(i);
                }
                else
                {
                    i++;
                }
            }
            // GL759: while rationed, no new chunks while meshing is behind: every waiting chunk is looked at each frame, so a
            // pile of them would slow the frame more and more. Chunks held for roads don't count: they need their neighbours.
            if (RationsStarts && meshingBehind && dirtyScratch.Count > MostMeshBacklog)
            {
                return;
            }
            int started = 0;
            while (requested.Count > 0 && pipeline.GenerationJobsRunning < limit && (started < pastBudget || Before(budget)))
            {
                Vector3Int coord = requested.Dequeue();
                requestedSet.Remove(coord);
                if (IsWanted(coord))
                {
                    started += StartChunk(coord) ? 1 : 0;
                }
            }
            bool groundDone = !IsPlaying || StartGroundFirst(limit, pastBudget, ref started);
            if (!groundDone)
            {
                return;
            }
            // At least a job limit's worth of starts a frame, as before; more while the budget lasts.
            // A column with every layer loaded is passed over in one step, so moving the area (which puts
            // the cursor back to the nearest column) costs a look per column, not per chunk.
            int layers = settings.HeightInChunks;
            while (loadCursor < wantedColumns.Count && pipeline.GenerationJobsRunning < limit
                   && (started < pastBudget || Before(budget)))
            {
                Vector2Int column = wantedColumns[loadCursor];
                if (loadLayer == 0 && world.LoadedLayersIn(column) >= layers)
                {
                    loadCursor++;
                    continue;
                }
                // Nearest first: a column not proven yet is waited for, or all its layers would start.
                if (ProvesColumns && !world.IsColumnProven(column) && !TryProve(column))
                {
                    break;
                }
                var coord = new Vector3Int(column.x, loadLayer, column.y);
                if (++loadLayer >= layers)
                {
                    loadLayer = 0;
                    loadCursor++;
                }
                if (world.IsLoaded(coord))
                {
                    continue;
                }
                started++;
                StartChunk(coord);
            }
        }

        // ---- Sparse chunks (GL773) ---------------------------------------------------------

        // Columns whose heights are being worked out, and the most at a time; the time a frame may spend proving, while playing
        // and while the area loads.
        private const int MostColumnsProving = 256;
        private const double PlayingProveMilliseconds = 1.0, LoadingProveMilliseconds = 6.0;

        private readonly List<Vector2Int> proving = new List<Vector2Int>();
        private readonly List<Vector3Int> materialised = new List<Vector3Int>();
        private int proveCursor;

        private bool ProvesColumns => settings.SparseChunks && !settings.FixedColumns.HasValue && pipeline.CanProveColumns;

        // Proves the wanted columns nearest first, ahead of generating them: until a column is proven all its layers count as waiting.
        private void ProveAhead()
        {
            if (!ProvesColumns)
            {
                return;
            }
            double until = frameClock.Elapsed.TotalMilliseconds + (IsPlaying ? PlayingProveMilliseconds : LoadingProveMilliseconds);
            for (int i = proving.Count - 1; i >= 0 && Before(until); i--)
            {
                Vector2Int column = proving[i];
                if (world.IsColumnProven(column) || !IsColumnWanted(column) || TryProve(column))
                {
                    proving[i] = proving[proving.Count - 1];
                    proving.RemoveAt(proving.Count - 1);
                }
            }
            while (proveCursor < wantedColumns.Count && proving.Count < MostColumnsProving && Before(until))
            {
                Vector2Int column = wantedColumns[proveCursor++];
                if (!world.IsColumnProven(column) && !TryProve(column))
                {
                    proving.Add(column);
                }
            }
        }

        // False while the column's heights aren't ready.
        private bool TryProve(Vector2Int column)
        {
            bool proved = pipeline.TryProveColumn(column, settings.HeightInChunks, out UniformLayers layers);
            if (!proved)
            {
                return false;
            }
            int added = world.ProveColumn(column, layers, settings.HeightInChunks);
            if (IsColumnWanted(column))
            {
                wantedLoaded += added;
            }
            return true;
        }

        private bool IsColumnWanted(Vector2Int column)
        {
            return IsWanted(new Vector3Int(column.x, 0, column.y));
        }

        // Starts a chunk's generation, proving its column first; true when nothing more is needed now.
        private bool TryStartGeneration(Vector3Int coord)
        {
            var column = new Vector2Int(coord.x, coord.z);
            if (ProvesColumns && !world.IsColumnProven(column))
            {
                if (!TryProve(column))
                {
                    return false;
                }
                if (world.IsLoaded(coord))
                {
                    return true;
                }
            }
            return pipeline.StartGeneration(coord);
        }

        // Raised in the middle of an edit, so it is only noted down here.
        private void NoteMaterialised(Vector3Int coord)
        {
            materialised.Add(coord);
        }

        private void RaiseProvenLayersChanged(Vector2Int column)
        {
            ProvenLayersChanged?.Invoke(column);
        }

        // A proven layer given a chunk was shown already, as nothing: an edit to it rebuilds in the frame of the edit, like any
        // shown chunk's. In a held column it waits to be meshed with the others, written.
        private void TakeMaterialised()
        {
            foreach (Vector3Int coord in materialised)
            {
                if (world.TryPeekChunk(coord, out _) && !IsColumnHeld(new Vector2Int(coord.x, coord.z)))
                {
                    meshedOnce.Add(coord);
                }
            }
            materialised.Clear();
        }

        // Starts a chunk unless it is loaded or on its way; true when there was something to start.
        private bool StartChunk(Vector3Int coord)
        {
            if (world.IsLoaded(coord) || pipeline.IsGenerating(coord))
            {
                return false;
            }
            if (world.TryRestoreKept(coord, out _))
            {
                wantedLoaded++;
                ChunkLoaded?.Invoke(coord);
                return true;
            }
            if (!TryStartGeneration(coord))
            {
                waitingForColumns.Add(coord);
            }
            return true;
        }

        // GL770: while playing, the layers round the ground of the drawn columns come first, nearest first: a column's 104
        // layers are mostly sky and deep rock nobody sees, and loading them bottom up left the ground ahead of a walker missing.
        // True once every drawn column's ground is on its way, so the rest of the layers may follow.
        private bool StartGroundFirst(int limit, int pastBudget, ref int started)
        {
            int reach = (settings.DetailDistance > 0 ? Mathf.Max(DrawRadius, MinimumGroundReach) : wantedDistance) + 1;
            Vector3Int centre = wantedCentre.Value;
            while (groundCursor < wantedColumns.Count)
            {
                if (pipeline.GenerationJobsRunning >= limit || (started >= pastBudget && !Before(budget)))
                {
                    return false;
                }
                Vector2Int column = wantedColumns[groundCursor];
                int dx = column.x - centre.x, dz = column.y - centre.z;
                if (dx * dx + dz * dz > reach * reach || !TryGroundLayer(column, out int ground))
                {
                    // The columns go out from the middle: the rest are further still.
                    groundCursor = wantedColumns.Count;
                    return true;
                }
                groundCursor++;
                int top = Mathf.Min(settings.HeightInChunks - 1, ground + GroundLayersAbove);
                for (int y = Mathf.Max(0, ground - GroundLayersBelow); y <= top; y++)
                {
                    started += StartChunk(new Vector3Int(column.x, y, column.y)) ? 1 : 0;
                }
            }
            return true;
        }

        private bool TryGroundLayer(Vector2Int column, out int layer)
        {
            if (groundLayers.TryGetValue(column, out layer))
            {
                return true;
            }
            if (!pipeline.TryGroundLayer(column, out layer))
            {
                return false;
            }
            if (groundLayers.Count >= MostGroundLayersKept)
            {
                groundLayers.Clear();
            }
            groundLayers[column] = layer;
            return true;
        }

        private void ApplyMeshes(double untilMilliseconds)
        {
            bool first = true;
            while ((first || Before(untilMilliseconds)) && pipeline.TryTakeMeshed(out ChunkPipeline.MeshResult result))
            {
                first = false;
                if (!world.TryPeekChunk(result.Coord, out Chunk chunk))
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

            // The world's dirty chunks in the wanted area, nearest first. GL759: at a long render distance tens of thousands
            // wait, and a frame starts only a few: a heap orders just those, where sorting them all took 20 ms a frame.
            hasChunksToMesh = false;
            if (!wantedCentre.HasValue)
            {
                return;
            }
            dirtyChunks.Take(world);
            dirtyChunks.LookAtParked(world, IsColumnHeld);
            dirtyChunks.CollectWaiting(world, dirtyScratch);
            var nearest = new NearestChunkHeap(dirtyScratch, wantedCentre.Value);
            bool rationed = RationsStarts;
            int pastBudget = StartsPastBudget;
            int started = 0;
            while (pipeline.MeshingCount < limit && (!rationed || started < pastBudget || Before(budget))
                   && nearest.TryTake(out Vector3Int coord))
            {
                bool skip = !IsWanted(coord) || pipeline.IsMeshing(coord);
                bool held = !skip && IsHeld(coord);
                if (held)
                {
                    dirtyChunks.Park(coord);
                }
                if (skip || held)
                {
                    continue;
                }
                hasChunksToMesh = true;
                started++;
                Chunk chunk = world.Chunks[coord];
                ChunkMeshSettings chunkSettings = ForChunk(current, coord);
                if (HasNoSurface(chunk, chunkSettings))
                {
                    MeshUniform(coord, chunk);
                    continue;
                }
                pipeline.StartMesh(coord, chunk, chunkSettings);
            }
            meshingBehind = nearest.Remaining > 0;
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
            NoteMeshChanged(coord);
            stats.Vertices += chunkRenderer.LastBuildStats.VertexCount;
            stats.Triangles += chunkRenderer.LastBuildStats.TriangleCount;
            if (settings.DetailDistance > 0 && world.IsEdited(coord))
            {
                // An edited column is drawn whole at any distance: its other layers too.
                renderers.RefreshColumn(new Vector2Int(coord.x, coord.z), settings.HeightInChunks);
            }
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
                NoteMeshChanged(coord);
            }
            ChunkMeshed?.Invoke(coord, chunkRenderer);
        }

        // GL774: only a mesh within reach of the last look's focus can need a bake before the focus moves (which looks again):
        // while the far area streams in, meshes land every frame, and each used to set off a look over the columns round the focus.
        private void NoteMeshChanged(Vector3Int coord)
        {
            if (HorizontalDistance(coord, collidersFocus) <= settings.ColliderRadius + 2f * world.Grid.ChunkWorldSize.x)
            {
                collidersDirty = true;
            }
        }

        // Cooks colliders for chunks near the focus and drops those well outside, then takes finished bakes.
        private void UpdateColliders(Vector3 focus)
        {
            if (!settings.Colliders)
            {
                collidersDirty = true;
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
                    AssignShape(coord, chunkRenderer, version);
                }
                else
                {
                    // A stale bake: the chunk may need another.
                    collidersDirty = true;
                }
            }

            // Shapes only exist near the focus, so far ones are dropped only when the focus moves to
            // another chunk; each frame only the chunks around the focus are checked, not every renderer.
            Vector3Int centre = world.Grid.WorldToChunk(focus);
            if (collidersCentre != centre)
            {
                collidersCentre = centre;
                // GL774: only the chunks given a shape, not every renderer (30,000 of them at a long render distance).
                scratch.Clear();
                scratch.AddRange(shaped);
                foreach (Vector3Int coord in scratch)
                {
                    ChunkCollider chunkCollider = renderers.TryGet(coord, out ChunkRenderer chunkRenderer) ? chunkRenderer.Collider : null;
                    if (chunkCollider == null || chunkCollider.Version < 0)
                    {
                        shaped.Remove(coord);
                    }
                    // Well outside (a chunk of slack, so the edge doesn't flicker): no shape needed.
                    else if (!pipeline.IsBaking(coord) && HorizontalDistance(coord, focus) > dropDistance)
                    {
                        chunkCollider.Clear();
                        shaped.Remove(coord);
                    }
                }
                collidersDirty = true;
            }

            // Nothing changed and the focus has hardly moved: no chunk can need a bake it didn't need last time.
            if (!collidersDirty && collidersRadius == settings.ColliderRadius && (focus - collidersFocus).sqrMagnitude < 1f)
            {
                return;
            }
            collidersDirty = false;
            collidersRadius = settings.ColliderRadius;
            collidersFocus = focus;
            int reach = Mathf.CeilToInt(settings.ColliderRadius / chunkWidth) + 1;
            for (int dz = -reach; dz <= reach; dz++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    // Only the layers with a renderer: asking every layer of every column was 23,000 lookups a look. By
                    // index: a foreach over the read-only list boxed an enumerator for every column.
                    IReadOnlyList<int> rendered = renderers.LayersIn(new Vector2Int(centre.x + dx, centre.z + dz));
                    for (int i = 0; i < rendered.Count; i++)
                    {
                        var coord = new Vector3Int(centre.x + dx, rendered[i], centre.z + dz);
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
                            AssignShape(coord, chunkRenderer, chunkRenderer.MeshVersion);
                            continue;
                        }
                        pipeline.StartBake(coord, chunkRenderer.Mesh, chunkRenderer.MeshVersion, chunkCollider.CookingOptions);
                    }
                }
            }
        }

        /// <summary>
        /// Radius in chunks within which loaded chunks are drawn (P18, see
        /// <see cref="StreamingSettings.DetailDistance"/>); 0 draws every one.
        /// </summary>
        public int DetailDistance
        {
            get => settings.DetailDistance;
            set
            {
                value = Mathf.Max(0, value);
                if (value == settings.DetailDistance)
                {
                    return;
                }
                settings.DetailDistance = value;
                DrawVersion = ++drawVersions;
                renderers.RefreshDrawn();
            }
        }

        /// <summary>Changes whenever which columns are drawn may have (<see cref="IsColumnDrawn"/>); never repeats.</summary>
        public int DrawVersion { get; private set; }

        /// <summary>
        /// True when the world draws the column's chunks (x, z): every column with no detail distance, otherwise those of
        /// the regions (<see cref="ChunkRendererPool.RegionSpan"/> columns square, GL776) that reach within it of the focus
        /// column (whole regions, since each draws as one renderer), and any column holding an edited chunk.
        /// </summary>
        public bool IsColumnDrawn(Vector2Int column)
        {
            return IsRegionWithin(column) || world.HasEditsIn(column);
        }

        // Whether the nearest column of the column's region lies within the draw radius of the focus column; every region
        // with no detail distance.
        private bool IsRegionWithin(Vector2Int column)
        {
            if (settings.DetailDistance <= 0 || !hasDrawCentre)
            {
                return true;
            }
            Vector2Int first = renderers.RegionColumn(column);
            Vector3Int span = renderers.RegionSpan;
            var nearest = new Vector2Int(
                Mathf.Clamp(drawCentre.x, first.x, first.x + span.x - 1),
                Mathf.Clamp(drawCentre.y, first.y, first.y + span.z - 1));
            return IsWithin(nearest, drawCentre, DrawRadius);
        }

        // Regions (by first column) known to hold edits or not, until a column gains its first edit.
        private readonly Dictionary<Vector2Int, bool> regionEdits = new Dictionary<Vector2Int, bool>();
        private int regionEditsVersion = -1;

        // Keeps the cache current through new edits (roads and settlements write new columns all the time): only a clear
        // of the world's edits, or an edit missed, empties it.
        private void NoteColumnEdited(Vector2Int column)
        {
            if (regionEditsVersion == world.EditedColumnsVersion - 1)
            {
                regionEdits[renderers.RegionColumn(column)] = true;
                regionEditsVersion = world.EditedColumnsVersion;
            }
        }

        private bool RegionHasEdits(Vector2Int first)
        {
            if (regionEditsVersion != world.EditedColumnsVersion)
            {
                regionEdits.Clear();
                regionEditsVersion = world.EditedColumnsVersion;
            }
            if (regionEdits.TryGetValue(first, out bool edited))
            {
                return edited;
            }
            Vector3Int span = renderers.RegionSpan;
            for (int z = 0; z < span.z && !edited; z++)
            {
                for (int x = 0; x < span.x && !edited; x++)
                {
                    edited = world.HasEditsIn(new Vector2Int(first.x + x, first.y + z));
                }
            }
            regionEdits.Add(first, edited);
            return edited;
        }

        private static bool IsWithin(Vector2Int column, Vector2Int centre, int distance)
        {
            if (distance < 0)
            {
                return false;
            }
            int dx = column.x - centre.x;
            int dz = column.y - centre.y;
            return dx * dx + dz * dz <= distance * distance;
        }


        private int drawRadius = -2;

        /// <summary>
        /// The detail distance shrunk by the focus's height over the world's top (GL323): the chunks within it in three
        /// dimensions are drawn, so seen from far above, where a chunk is a pixel or two, none is and the coarse terrain
        /// stands in (from 2 km up the world drew 10,000 chunk renderers, 5.5 million triangles). Below 0, none.
        /// </summary>
        public int DrawRadius => drawRadius < -1 ? settings.DetailDistance : drawRadius;

        private void UpdateDrawRadius(int focusLayer)
        {
            int distance = settings.DetailDistance;
            int above = Mathf.Max(0, focusLayer - settings.HeightInChunks);
            int radius = above >= distance ? -1 : Mathf.FloorToInt(Mathf.Sqrt(distance * distance - above * above));
            if (radius == drawRadius)
            {
                return;
            }
            drawRadius = radius;
            if (distance > 0 && hasDrawCentre)
            {
                DrawVersion = ++drawVersions;
                renderers.RefreshDrawn();
            }
        }
        // The focus column moved: switch the regions that crossed the detail distance (a look at every region: a few thousand).
        private void UpdateDrawCentre(Vector2Int column)
        {
            if (hasDrawCentre && column == drawCentre)
            {
                return;
            }
            drawCentre = column;
            hasDrawCentre = true;
            if (settings.DetailDistance <= 0)
            {
                return;
            }
            DrawVersion = ++drawVersions;
            renderers.RefreshDrawn();
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
                AssignShape(coord, chunkRenderer, chunkRenderer.MeshVersion);
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
                AssignShape(coord, chunkRenderer, version);
            }
        }

        // Gives the chunk's collider the shape of its mesh's version, and notes it among those with a shape.
        private void AssignShape(Vector3Int coord, ChunkRenderer chunkRenderer, int version)
        {
            chunkRenderer.Collider.Assign(chunkRenderer.Mesh, version);
            shaped.Add(coord);
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
            float dx = Mathf.Max(Mathf.Max(min.x - focus.x, 0f), focus.x - max.x);
            float dz = Mathf.Max(Mathf.Max(min.z - focus.z, 0f), focus.z - max.z);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // Whether this frame's streaming has run for less than the given time so far.
        private bool Before(double milliseconds)
        {
            return frameClock.Elapsed.TotalMilliseconds < milliseconds;
        }
    }
}
