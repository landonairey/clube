using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The coarse voxel terrain's runtime (P7c): rings of chunks around the focus, each level's
    /// voxels twice the size of the level inside it (<see cref="TerrainLodRing"/>), generated and
    /// meshed by the same jobs as the world's own chunks, one <see cref="ChunkPipeline"/> per level.
    /// Only heightfield terrain: each column's height range says which chunk layers hold its surface.
    /// </summary>
    /// <remarks>
    /// <para>A level keeps showing the square it has until every chunk of the square it now wants is
    /// built, then swaps in one frame (<see cref="ISink"/>), so a moving focus never opens holes.
    /// Levels are visited finest first, so the near rings fill in first.</para>
    /// <para>Nothing is kept but the meshes: each chunk's samples go back to the pool once its mesh
    /// job has its snapshot. Edits don't reach these chunks; the world's own chunks draw over them.</para>
    /// </remarks>
    public sealed class TerrainLodBuilder : IDisposable
    {
        /// <summary>Where the builder's chunks are drawn (<see cref="TerrainLodView"/>).</summary>
        public interface ISink
        {
            void Show(int level, Vector3Int coord, Mesh mesh);

            void Hide(int level, Vector3Int coord);
        }

        private readonly Level[] levels;
        private readonly ISink sink;
        private readonly int halfChunks;
        private readonly List<Vector3Int> scratch = new List<Vector3Int>();

        private enum State
        {
            Waiting,
            Generating,
            Meshing,
            Ready,
        }

        private sealed class Entry
        {
            public State State;
            public Mesh Mesh;
            public bool Visible;
            public bool Wanted;
            public bool Queued;
        }

        private sealed class Level
        {
            public int Index;
            public ChunkPipeline Pipeline;
            public ChunkMeshSettings MeshSettings;
            public float ChunkWidth;
            public bool HasTarget;
            public TerrainLodRing Target;
            public bool HasShown;
            public TerrainLodRing Shown;
            public bool Switched;
            public int Unready;
            public readonly Dictionary<Vector3Int, Entry> Entries = new Dictionary<Vector3Int, Entry>();
            public readonly List<Vector2Int> PendingColumns = new List<Vector2Int>();
            public readonly Queue<Vector3Int> ToStart = new Queue<Vector3Int>();
        }

        /// <param name="worldGrid">The world's own grid: level l has its chunk size and 2^l times its voxel size.</param>
        /// <param name="fill">The world's fill settings; each level widens them (<see cref="ChunkFillSettings.ForLod"/>).</param>
        /// <param name="mesh">The world's mesh settings; each level gets its own voxel size, the Burst mesher and no seal.</param>
        /// <param name="firstLevel">The finest level built (1: voxels twice the world's).</param>
        public TerrainLodBuilder(
            HeightfieldGenerator generator, WorldGrid worldGrid, ChunkFillSettings fill, ChunkMeshSettings mesh,
            Func<Vector3Int, IVoxelStorage> createStorage,
            int firstLevel, int levelCount, int halfChunks, ISink sink)
        {
            this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
            this.halfChunks = Mathf.Max(2, halfChunks);
            FirstLevel = Mathf.Max(1, firstLevel);
            levels = new Level[Mathf.Max(0, levelCount)];
            for (int i = 0; i < levels.Length; i++)
            {
                int index = FirstLevel + i;
                float scale = TerrainLodRing.ScaleOf(index);
                var grid = new WorldGrid(worldGrid.ChunkSize, worldGrid.VoxelSize * scale);
                levels[i] = new Level
                {
                    Index = index,
                    Pipeline = new ChunkPipeline(grid, generator, fill.ForLod(scale), null, createStorage),
                    MeshSettings = new ChunkMeshSettings(
                        mesh.IsoLevel, grid.VoxelSize, mesh.EdgePlacement, mesh.Shading, MesherBackend.Burst, mesh.MaterialDisplay, MeshSeal.None),
                    ChunkWidth = grid.ChunkWorldSize.x,
                };
            }
        }

        /// <summary>The finest level's index (its voxels are 2^index times the world's).</summary>
        public int FirstLevel { get; }

        public int LevelCount => levels.Length;

        /// <summary>Generation jobs allowed at once, over every level.</summary>
        public int MaxJobs { get; set; } = 48;

        /// <summary>Jobs started per frame.</summary>
        public int MaxStartsPerFrame { get; set; } = 48;

        /// <summary>Finished jobs taken per frame (each copies a chunk on the main thread).</summary>
        public int MaxTakesPerFrame { get; set; } = 32;

        /// <summary>The grid of the level at <paramref name="i"/> (0 = <see cref="FirstLevel"/>).</summary>
        public WorldGrid GridOf(int i)
        {
            return levels[i].Pipeline.Grid;
        }

        /// <summary>The square level <paramref name="i"/> shows, once it shows one.</summary>
        public bool TryGetShown(int i, out TerrainLodRing ring)
        {
            ring = levels[i].Shown;
            return levels[i].HasShown;
        }

        /// <summary>Chunks level <paramref name="i"/> draws, and chunks it still waits for.</summary>
        public void CountOf(int i, out int drawn, out int waiting)
        {
            drawn = 0;
            foreach (Entry entry in levels[i].Entries.Values)
            {
                if (entry.Visible && entry.Mesh != null)
                {
                    drawn++;
                }
            }
            waiting = levels[i].Switched ? 0 : levels[i].Unready + levels[i].PendingColumns.Count;
        }

        /// <summary>True once every level shows the square around the focus it was last given.</summary>
        public bool IsSettled
        {
            get
            {
                foreach (Level level in levels)
                {
                    if (!level.Switched)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        /// <summary>Call once a frame with the focus (world-view space).</summary>
        public void Update(Vector3 focus)
        {
            var flat = new Vector2(focus.x, focus.z);
            int starts = MaxStartsPerFrame;
            int takes = MaxTakesPerFrame;
            foreach (Level level in levels)
            {
                level.Pipeline.Poll();
                TerrainLodRing wanted = TerrainLodRing.Around(flat, level.Index, halfChunks, level.ChunkWidth);
                if (!level.HasTarget || !wanted.Equals(level.Target))
                {
                    Retarget(level, wanted, flat);
                }
                ResolveColumns(level);
                StartJobs(level, ref starts);
                TakeResults(level, ref takes);
                if (!level.Switched && level.PendingColumns.Count == 0 && level.Unready == 0)
                {
                    Switch(level);
                }
            }
            if (levels.Length > 0)
            {
                levels[0].Pipeline.Flush();
            }
        }

        public void Dispose()
        {
            foreach (Level level in levels)
            {
                foreach (Entry entry in level.Entries.Values)
                {
                    DestroyMesh(entry);
                }
                level.Entries.Clear();
                level.Pipeline.Dispose();
            }
        }

        // A new square: forget what it doesn't need (keeping what's on screen), list its columns nearest first.
        private void Retarget(Level level, TerrainLodRing wanted, Vector2 focus)
        {
            level.Target = wanted;
            level.HasTarget = true;
            level.Switched = false;
            level.Unready = 0;
            level.ToStart.Clear();

            scratch.Clear();
            foreach (KeyValuePair<Vector3Int, Entry> pair in level.Entries)
            {
                pair.Value.Wanted = false;
                pair.Value.Queued = false;
                if (!pair.Value.Visible && !wanted.Builds(new Vector2Int(pair.Key.x, pair.Key.z)))
                {
                    scratch.Add(pair.Key);
                }
            }
            foreach (Vector3Int coord in scratch)
            {
                Drop(level, coord);
            }

            level.PendingColumns.Clear();
            Vector2Int first = wanted.First;
            for (int z = 0; z < wanted.Width; z++)
            {
                for (int x = 0; x < wanted.Width; x++)
                {
                    var column = new Vector2Int(first.x + x, first.y + z);
                    if (wanted.Builds(column))
                    {
                        level.PendingColumns.Add(column);
                    }
                }
            }
            float width = level.ChunkWidth;
            level.PendingColumns.Sort((a, b) =>
                (((Vector2)a + Vector2.one * 0.5f) * width - focus).sqrMagnitude.CompareTo((((Vector2)b + Vector2.one * 0.5f) * width - focus).sqrMagnitude));
        }

        // Columns whose heights are known become the chunks that hold their surface.
        private void ResolveColumns(Level level)
        {
            float height = level.Pipeline.Grid.ChunkWorldSize.y;
            float margin = level.Pipeline.Grid.VoxelSize * 2f;
            int kept = 0;
            for (int i = 0; i < level.PendingColumns.Count; i++)
            {
                Vector2Int column = level.PendingColumns[i];
                if (!level.Pipeline.TryGetColumnRange(column, out float lowest, out float highest))
                {
                    level.PendingColumns[kept++] = column;
                    continue;
                }
                int bottom = Mathf.FloorToInt((lowest - margin) / height);
                int top = Mathf.FloorToInt((highest + margin) / height);
                for (int y = bottom; y <= top; y++)
                {
                    var coord = new Vector3Int(column.x, y, column.y);
                    if (!level.Entries.TryGetValue(coord, out Entry entry))
                    {
                        entry = new Entry { State = State.Waiting };
                        level.Entries.Add(coord, entry);
                    }
                    if (!entry.Wanted)
                    {
                        entry.Wanted = true;
                        if (entry.State != State.Ready)
                        {
                            level.Unready++;
                        }
                    }
                    if (entry.State == State.Waiting && !entry.Queued)
                    {
                        entry.Queued = true;
                        level.ToStart.Enqueue(coord);
                    }
                }
            }
            level.PendingColumns.RemoveRange(kept, level.PendingColumns.Count - kept);
        }

        private void StartJobs(Level level, ref int starts)
        {
            int tries = level.ToStart.Count;
            while (starts > 0 && tries-- > 0 && level.ToStart.Count > 0 && JobsRunning() < MaxJobs)
            {
                Vector3Int coord = level.ToStart.Dequeue();
                if (!level.Entries.TryGetValue(coord, out Entry entry) || entry.State != State.Waiting)
                {
                    continue;
                }
                entry.Queued = false;
                if (!entry.Wanted)
                {
                    if (!entry.Visible)
                    {
                        Drop(level, coord);
                    }
                    continue;
                }
                if (level.Pipeline.StartGeneration(coord))
                {
                    entry.State = State.Generating;
                    starts--;
                }
                else
                {
                    // Its column's heights are still on their way.
                    entry.Queued = true;
                    level.ToStart.Enqueue(coord);
                }
            }
        }

        private void TakeResults(Level level, ref int takes)
        {
            while (takes > 0 && level.Pipeline.TryTakeGenerated(out Vector3Int coord, out Chunk chunk))
            {
                takes--;
                if (!level.Entries.TryGetValue(coord, out Entry entry) || entry.State != State.Generating)
                {
                    chunk.Release();
                    continue;
                }
                if (chunk.IsUniform)
                {
                    chunk.Release();
                    SetReady(level, entry, null);
                    continue;
                }
                level.Pipeline.StartMesh(coord, chunk, level.MeshSettings);
                chunk.Release();
                entry.State = State.Meshing;
            }
            while (takes > 0 && level.Pipeline.TryTakeMeshed(out ChunkPipeline.MeshResult result))
            {
                takes--;
                if (!level.Entries.TryGetValue(result.Coord, out Entry entry) || entry.State != State.Meshing)
                {
                    result.Discard();
                    continue;
                }
                if (result.VertexCount == 0)
                {
                    result.Discard();
                    SetReady(level, entry, null);
                    continue;
                }
                Mesh mesh = entry.Mesh != null ? entry.Mesh : new Mesh { name = $"LOD {level.Index} chunk {result.Coord}" };
                result.ApplyTo(mesh);
                SetReady(level, entry, mesh);
            }
        }

        private static void SetReady(Level level, Entry entry, Mesh mesh)
        {
            entry.State = State.Ready;
            entry.Mesh = mesh;
            if (entry.Wanted)
            {
                level.Unready--;
            }
        }

        // Every chunk of the new square is built: show them and drop the old square's leftovers together.
        private void Switch(Level level)
        {
            scratch.Clear();
            foreach (KeyValuePair<Vector3Int, Entry> pair in level.Entries)
            {
                Entry entry = pair.Value;
                if (entry.Wanted)
                {
                    if (entry.Mesh != null && !entry.Visible)
                    {
                        sink.Show(level.Index, pair.Key, entry.Mesh);
                        entry.Visible = true;
                    }
                }
                else
                {
                    scratch.Add(pair.Key);
                }
            }
            foreach (Vector3Int coord in scratch)
            {
                Drop(level, coord);
            }
            level.Shown = level.Target;
            level.HasShown = true;
            level.Switched = true;
        }

        private void Drop(Level level, Vector3Int coord)
        {
            Entry entry = level.Entries[coord];
            if (entry.Visible)
            {
                sink.Hide(level.Index, coord);
            }
            if (entry.State == State.Generating)
            {
                level.Pipeline.CancelGeneration(coord);
            }
            else if (entry.State == State.Meshing)
            {
                level.Pipeline.CancelMesh(coord);
            }
            DestroyMesh(entry);
            level.Entries.Remove(coord);
        }

        private int JobsRunning()
        {
            int running = 0;
            foreach (Level level in levels)
            {
                running += level.Pipeline.GenerationJobsRunning + level.Pipeline.MeshingCount;
            }
            return running;
        }

        private static void DestroyMesh(Entry entry)
        {
            if (entry.Mesh == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(entry.Mesh);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(entry.Mesh);
            }
            entry.Mesh = null;
        }
    }
}
