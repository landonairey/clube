using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Runs the streamed world's chunk jobs (K35, P3): generation, meshing and collider baking,
    /// many chunks at once on the worker threads and across frames, so loading never stalls a
    /// frame. The mechanism only: it starts a job when asked and hands back finished results;
    /// what to load, mesh or bake, and when, is <see cref="WorldStreamer"/>'s policy.
    /// </summary>
    /// <remarks>
    /// <para>Threading rules: jobs never touch managed objects. Generation writes into buffers
    /// this pipeline owns, and the main thread copies the result into a new <see cref="Chunk"/>
    /// when it takes it; meshing reads a snapshot of the chunk (<see cref="ChunkMeshInput"/>),
    /// so the chunk can be edited meanwhile. At most one job of each stage runs per chunk.
    /// Call <see cref="Poll"/> once a frame, then take what finished.</para>
    /// <para>Heightfield terrain computes each chunk column's heights once
    /// (<see cref="ColumnHeightsJob{THeight}"/>) and shares them between every chunk stacked on
    /// it; a chunk wholly above its column's highest point is known to be all air without
    /// running anything.</para>
    /// </remarks>
    public sealed class ChunkPipeline : IDisposable
    {
        // Columns kept after their last chunk was generated, in frames, then freed.
        private const int ColumnLifetimeFrames = 300;

        private readonly WorldGrid grid;
        private readonly ITerrainGenerator generator;
        private readonly OreField ores;
        private readonly ChunkFillSettings fillSettings;
        private readonly Func<Vector3Int, IVoxelStorage> createStorage;

        // Running jobs, by chunk.
        private readonly Dictionary<Vector3Int, GenerationTask> generating = new Dictionary<Vector3Int, GenerationTask>();
        private readonly Dictionary<Vector3Int, MeshTask> meshing = new Dictionary<Vector3Int, MeshTask>();
        private readonly Dictionary<Vector3Int, BakeTask> baking = new Dictionary<Vector3Int, BakeTask>();
        private readonly Dictionary<Vector2Int, ColumnHeights> columns = new Dictionary<Vector2Int, ColumnHeights>();

        // Finished work waiting to be taken, in the order it was found finished.
        private readonly Queue<GenerationTask> generated = new Queue<GenerationTask>();
        private readonly HashSet<Vector3Int> generatedCoords = new HashSet<Vector3Int>();
        private readonly Queue<MeshTask> meshed = new Queue<MeshTask>();
        private readonly HashSet<Vector3Int> meshedCoords = new HashSet<Vector3Int>();
        private readonly Queue<BakeTask> baked = new Queue<BakeTask>();

        private readonly List<Vector3Int> scratch = new List<Vector3Int>();
        private readonly List<Vector2Int> columnScratch = new List<Vector2Int>();
        private int frame;

        /// <param name="format">The density format the world's storage holds (<see cref="World.PreferredFormat"/>).</param>
        public ChunkPipeline(
            WorldGrid grid, ITerrainGenerator generator, TerrainLayers layers, OreField ores, DensityFormat format,
            Func<Vector3Int, IVoxelStorage> createStorage)
        {
            this.grid = grid;
            this.generator = generator;
            this.ores = ores;
            this.createStorage = createStorage;
            fillSettings = ChunkGenerator.SettingsFor(layers, ores, format);
        }

        /// <summary>Chunks generating now, or generated and waiting to be taken.</summary>
        public int GeneratingCount => generating.Count + generated.Count;

        /// <summary>Meshes building now, or built and waiting to be taken.</summary>
        public int MeshingCount => meshing.Count + meshed.Count;

        /// <summary>Collider shapes cooking now, or cooked and waiting to be taken.</summary>
        public int BakingCount => baking.Count + baked.Count;

        /// <summary>True from <see cref="StartGeneration"/> until the chunk is taken; false once cancelled.</summary>
        public bool IsGenerating(Vector3Int coord)
        {
            return (generating.TryGetValue(coord, out GenerationTask task) && !task.Cancelled) || generatedCoords.Contains(coord);
        }

        /// <summary>True from <see cref="StartMesh"/> until the result is taken; false once cancelled.</summary>
        public bool IsMeshing(Vector3Int coord)
        {
            return (meshing.TryGetValue(coord, out MeshTask task) && !task.Cancelled) || meshedCoords.Contains(coord);
        }

        public bool IsBaking(Vector3Int coord)
        {
            return baking.ContainsKey(coord);
        }

        /// <summary>
        /// Moves every finished job to the queues the Take methods read, and frees columns nobody
        /// has needed for a while. Call once a frame, before taking. Cheap: the results are only
        /// copied out when taken.
        /// </summary>
        public void Poll()
        {
            frame++;
            MoveFinished(generating, generated, generatedCoords);
            MoveFinished(meshing, meshed, meshedCoords);
            MoveFinished(baking, baked, null);
            FreeIdleColumns();
        }

        // ---- Generation -------------------------------------------------------------------

        /// <summary>
        /// Starts generating a chunk. One wholly above the surface is all air and is ready at
        /// once, without a job.
        /// </summary>
        public void StartGeneration(Vector3Int coord)
        {
            if (IsGenerating(coord))
            {
                return;
            }

            // Cancelled but still running (the focus went away and came back): the same chunk, so keep it.
            if (generating.TryGetValue(coord, out GenerationTask running))
            {
                running.Cancelled = false;
                return;
            }

            var task = new GenerationTask { Coord = coord, Grid = ChunkSampleGrid.ForChunk(grid, coord) };
            if (generator == null || IsAboveSurface(coord, task.Grid))
            {
                task.Air = true;
                generated.Enqueue(task);
                generatedCoords.Add(coord);
                return;
            }

            task.Output = ChunkFillOutput.Allocate(task.Grid.Length, fillSettings.Format, Allocator.Persistent);
            task.Ores = ChunkGenerator.CollectOres(ores, task.Grid, Allocator.Persistent);
            if (generator is HeightfieldGenerator heightfield)
            {
                task.Column = ColumnFor(new Vector2Int(coord.x, coord.z), task.Grid.Columns, heightfield);
                task.Column.Users++;
                task.Handle = ChunkGenerator.ScheduleFromColumns(
                    task.Column.Heights, task.Column.Range, task.Grid, fillSettings, task.Ores, task.Output, task.Column.Job);
            }
            else
            {
                task.Handle = ChunkGenerator.Schedule(generator, task.Grid, fillSettings, task.Ores, task.Output);
            }
            generating.Add(coord, task);
        }

        /// <summary>Drops a chunk's generation (it left the streamed area); its job finishes and its result is thrown away.</summary>
        public void CancelGeneration(Vector3Int coord)
        {
            if (generating.TryGetValue(coord, out GenerationTask task))
            {
                task.Cancelled = true;
            }
            if (generatedCoords.Remove(coord))
            {
                foreach (GenerationTask finished in generated)
                {
                    if (finished.Coord == coord)
                    {
                        finished.Cancelled = true;
                    }
                }
            }
        }

        /// <summary>
        /// The next generated chunk, ready to add to the world: its densities and materials are
        /// copied out of the job's buffers now, and the buffers freed.
        /// </summary>
        public bool TryTakeGenerated(out Vector3Int coord, out Chunk chunk)
        {
            while (generated.Count > 0)
            {
                GenerationTask task = generated.Dequeue();
                if (task.Cancelled)
                {
                    task.Dispose();
                    continue;
                }
                generatedCoords.Remove(task.Coord);
                coord = task.Coord;
                chunk = task.Air ? AirChunk(task.Grid) : ChunkGenerator.FromOutput(task.Grid, task.Output, createStorage);
                task.Dispose();
                return true;
            }
            coord = default;
            chunk = null;
            return false;
        }

        // ---- Meshing ----------------------------------------------------------------------

        /// <summary>
        /// Starts building a chunk's mesh from a snapshot of it, and marks it clean: an edit made
        /// while the job runs dirties it again, so it gets rebuilt after.
        /// </summary>
        public void StartMesh(Vector3Int coord, Chunk chunk, ChunkMeshSettings settings)
        {
            if (IsMeshing(coord))
            {
                throw new InvalidOperationException($"Chunk {coord} is already meshing.");
            }

            // A cancelled job of the chunk that was here before (it streamed out and back in)
            // shows another Chunk object: wait for it and throw its result away.
            if (meshing.TryGetValue(coord, out MeshTask stale))
            {
                meshing.Remove(coord);
                stale.Handle.Complete();
                stale.Data.Dispose();
            }

            Mesh.MeshDataArray data = Mesh.AllocateWritableMeshData(1);
            ChunkMeshInput input = ChunkMeshInput.Snapshot(chunk, Allocator.Persistent);
            var task = new MeshTask
            {
                Coord = coord,
                Data = data,
                SampleCount = chunk.SampleCount,
                VoxelSize = settings.VoxelSize,
                Handle = ChunkMeshJobs.Schedule(input, settings, data),
            };
            chunk.MarkClean();
            meshing.Add(coord, task);
        }

        /// <summary>The next finished mesh. Hand it to a renderer (<see cref="MeshResult.ApplyTo"/>), or <see cref="MeshResult.Discard"/> it.</summary>
        public bool TryTakeMeshed(out MeshResult result)
        {
            while (meshed.Count > 0)
            {
                MeshTask task = meshed.Dequeue();
                if (task.Cancelled)
                {
                    task.Data.Dispose();
                    continue;
                }
                meshedCoords.Remove(task.Coord);
                result = new MeshResult(task);
                return true;
            }
            result = default;
            return false;
        }

        /// <summary>
        /// Takes a chunk's mesh now, waiting for its job if it's still running (an edit needs its
        /// new mesh this frame). False when no mesh is on its way for the chunk.
        /// </summary>
        public bool TryTakeMeshNow(Vector3Int coord, out MeshResult result)
        {
            result = default;
            if (meshing.TryGetValue(coord, out MeshTask task))
            {
                meshing.Remove(coord);
                task.Handle.Complete();
            }
            else if (meshedCoords.Remove(coord))
            {
                // Finished and queued: take it out of the queue (rare, and the queue is short).
                task = null;
                int count = meshed.Count;
                for (int i = 0; i < count; i++)
                {
                    MeshTask finished = meshed.Dequeue();
                    if (task == null && finished.Coord == coord && !finished.Cancelled)
                    {
                        task = finished;
                    }
                    else
                    {
                        meshed.Enqueue(finished);
                    }
                }
            }
            else
            {
                return false;
            }

            if (task == null || task.Cancelled)
            {
                task?.Data.Dispose();
                return false;
            }
            result = new MeshResult(task);
            return true;
        }

        public void CancelMesh(Vector3Int coord)
        {
            if (meshing.TryGetValue(coord, out MeshTask task))
            {
                // Still running: it finishes and is thrown away when taken from the queue.
                task.Cancelled = true;
            }
            if (meshedCoords.Remove(coord))
            {
                foreach (MeshTask finished in meshed)
                {
                    if (finished.Coord == coord)
                    {
                        finished.Cancelled = true;
                    }
                }
            }
        }

        // ---- Colliders --------------------------------------------------------------------

        /// <summary>
        /// Starts cooking a mesh's collision shape on a worker (<see cref="Physics.BakeMesh(EntityId, bool, MeshColliderCookingOptions)"/>),
        /// so assigning it to a <see cref="MeshCollider"/> afterwards is quick (M6). Don't change
        /// the mesh while the bake runs: finish it first with <see cref="FinishBakeNow"/>.
        /// </summary>
        /// <param name="version">The mesh's version, handed back so a stale bake can be recognised.</param>
        public void StartBake(Vector3Int coord, Mesh mesh, int version, MeshColliderCookingOptions options)
        {
            if (baking.ContainsKey(coord))
            {
                return;
            }
            var task = new BakeTask
            {
                Coord = coord,
                Version = version,
                Handle = new BakeColliderJob { MeshId = mesh.GetInstanceID(), Options = options }.Schedule(),
            };
            baking.Add(coord, task);
        }

        /// <summary>The next finished bake: which chunk, and which version of its mesh was cooked.</summary>
        public bool TryTakeBaked(out Vector3Int coord, out int version)
        {
            while (baked.Count > 0)
            {
                BakeTask task = baked.Dequeue();
                if (task.Cancelled)
                {
                    continue;
                }
                coord = task.Coord;
                version = task.Version;
                return true;
            }
            coord = default;
            version = 0;
            return false;
        }

        /// <summary>
        /// Waits for a chunk's bake if one is running, so its mesh may change; returns the baked
        /// version, or -1 when none was running. A finished bake still in the queue doesn't read
        /// the mesh any more and is left there.
        /// </summary>
        public int FinishBakeNow(Vector3Int coord)
        {
            if (!baking.TryGetValue(coord, out BakeTask task))
            {
                return -1;
            }
            baking.Remove(coord);
            task.Handle.Complete();
            return task.Version;
        }

        /// <summary>Waits for and forgets any bake of the chunk's mesh: the renderer is about to show another chunk.</summary>
        public void CancelBake(Vector3Int coord)
        {
            FinishBakeNow(coord);
            foreach (BakeTask finished in baked)
            {
                if (finished.Coord == coord)
                {
                    finished.Cancelled = true;
                }
            }
        }

        /// <summary>Starts every job scheduled so far on the workers now, rather than when something waits for them.</summary>
        public void Flush()
        {
            JobHandle.ScheduleBatchedJobs();
        }

        /// <summary>Waits for every job and frees every buffer.</summary>
        public void Dispose()
        {
            foreach (GenerationTask task in generating.Values)
            {
                task.Handle.Complete();
                task.Dispose();
            }
            generating.Clear();
            foreach (GenerationTask task in generated)
            {
                task.Dispose();
            }
            generated.Clear();
            generatedCoords.Clear();

            foreach (MeshTask task in meshing.Values)
            {
                task.Handle.Complete();
                task.Data.Dispose();
            }
            meshing.Clear();
            foreach (MeshTask task in meshed)
            {
                task.Data.Dispose();
            }
            meshed.Clear();
            meshedCoords.Clear();

            foreach (BakeTask task in baking.Values)
            {
                task.Handle.Complete();
            }
            baking.Clear();
            baked.Clear();

            foreach (ColumnHeights column in columns.Values)
            {
                column.Dispose();
            }
            columns.Clear();
        }

        // Completes each finished job and queues it, in the order found.
        private void MoveFinished<TTask>(Dictionary<Vector3Int, TTask> running, Queue<TTask> finished, HashSet<Vector3Int> finishedCoords)
            where TTask : PipelineTask
        {
            scratch.Clear();
            foreach (KeyValuePair<Vector3Int, TTask> entry in running)
            {
                if (entry.Value.Handle.IsCompleted)
                {
                    scratch.Add(entry.Key);
                }
            }
            foreach (Vector3Int coord in scratch)
            {
                TTask task = running[coord];
                running.Remove(coord);
                task.Handle.Complete();
                task.OnFinished(frame);
                finished.Enqueue(task);

                // A cancelled task waits in the queue only to be freed; it isn't on its way to anyone.
                if (!task.Cancelled)
                {
                    finishedCoords?.Add(coord);
                }
            }
        }

        // A chunk above the highest surface its terrain can have there: known without sampling.
        private bool IsAboveSurface(Vector3Int coord, ChunkSampleGrid chunkGrid)
        {
            float clearance = TerrainDensity.RampHalfWidth;
            switch (generator)
            {
                case VolumeGenerator volume:
                    return chunkGrid.Bottom >= volume.SurfaceBounds.y + clearance;
                case HeightfieldGenerator _:
                    // Only once the column's heights are known; until then the fill job checks.
                    return columns.TryGetValue(new Vector2Int(coord.x, coord.z), out ColumnHeights column)
                           && column.Job.IsCompleted
                           && chunkGrid.Bottom >= column.Highest + clearance;
                default:
                    return false;
            }
        }

        private Chunk AirChunk(ChunkSampleGrid chunkGrid)
        {
            ChunkFillSummary air = ChunkFillKernel.AllAir(fillSettings.Layers);
            var sampleCount = new Vector3Int(chunkGrid.SampleCount.x, chunkGrid.SampleCount.y, chunkGrid.SampleCount.z);
            return Chunk.Uniform(sampleCount, 0f, air.Material, createStorage);
        }

        private ColumnHeights ColumnFor(Vector2Int column, ColumnBlock block, HeightfieldGenerator heightfield)
        {
            if (!columns.TryGetValue(column, out ColumnHeights heights))
            {
                heights = new ColumnHeights(block.Length);
                heights.Job = heightfield.ScheduleColumnHeights(block, heights.Heights, heights.Range);
                columns.Add(column, heights);
            }
            heights.LastUsedFrame = frame;
            return heights;
        }

        private void FreeIdleColumns()
        {
            columnScratch.Clear();
            foreach (KeyValuePair<Vector2Int, ColumnHeights> entry in columns)
            {
                if (entry.Value.Users == 0 && frame - entry.Value.LastUsedFrame > ColumnLifetimeFrames)
                {
                    columnScratch.Add(entry.Key);
                }
            }
            foreach (Vector2Int column in columnScratch)
            {
                columns[column].Dispose();
                columns.Remove(column);
            }
        }

        /// <summary>A finished mesh job's result, for the chunk's renderer.</summary>
        public readonly struct MeshResult
        {
            private readonly MeshTask task;

            internal MeshResult(MeshTask task)
            {
                this.task = task;
            }

            public Vector3Int Coord => task.Coord;

            /// <summary>Vertices in the built mesh: 0 when the chunk has no surface.</summary>
            public int VertexCount => task.Data[0].vertexCount;

            /// <summary>Hands the mesh data to <paramref name="mesh"/> (and frees it).</summary>
            public void ApplyTo(Mesh mesh)
            {
                ChunkMeshJobs.Apply(task.Data, mesh, task.SampleCount, task.VoxelSize);
            }

            /// <summary>Frees the mesh data unused.</summary>
            public void Discard()
            {
                task.Data.Dispose();
            }
        }

        internal abstract class PipelineTask
        {
            public Vector3Int Coord;
            public JobHandle Handle;
            public bool Cancelled;

            public virtual void OnFinished(int frame)
            {
            }
        }

        private sealed class GenerationTask : PipelineTask, IDisposable
        {
            public ChunkSampleGrid Grid;
            public ChunkFillOutput Output;
            public NativeArray<OreNodeData> Ores;
            public ColumnHeights Column;

            /// <summary>Known to be all air without a job: nothing allocated.</summary>
            public bool Air;

            public override void OnFinished(int frame)
            {
                if (Column != null)
                {
                    Column.Users--;
                    Column.LastUsedFrame = frame;
                    Column = null;
                }
            }

            public void Dispose()
            {
                if (Output.IsCreated)
                {
                    Output.Dispose();
                }
                if (Ores.IsCreated)
                {
                    Ores.Dispose();
                }
            }
        }

        internal sealed class MeshTask : PipelineTask
        {
            public Mesh.MeshDataArray Data;
            public Vector3Int SampleCount;
            public float VoxelSize;
        }

        private sealed class BakeTask : PipelineTask
        {
            public int Version;
        }

        // One chunk column's heights, shared by every chunk stacked on it.
        private sealed class ColumnHeights : IDisposable
        {
            public readonly NativeArray<float> Heights;
            public readonly NativeArray<float2> Range;
            public JobHandle Job;
            public int Users;
            public int LastUsedFrame;

            public ColumnHeights(int length)
            {
                Heights = new NativeArray<float>(length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                Range = new NativeArray<float2>(1, Allocator.Persistent);
            }

            /// <summary>The highest column; only read once <see cref="Job"/> is complete.</summary>
            public float Highest
            {
                get
                {
                    Job.Complete();
                    return Range[0].y;
                }
            }

            public void Dispose()
            {
                Job.Complete();
                Heights.Dispose();
                Range.Dispose();
            }
        }

        // Cooks a collision mesh on a worker thread; a plain job, since Physics isn't Burst code.
        private struct BakeColliderJob : IJob
        {
            public EntityId MeshId;
            public MeshColliderCookingOptions Options;

            public void Execute()
            {
                Physics.BakeMesh(MeshId, false, Options);
            }
        }
    }
}
