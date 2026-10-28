using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The far terrain's runtime (P7): keeps every clipmap level (<see cref="FarTerrainGrid"/>)
    /// centred on the focus, rebuilding a level in jobs when its grid moves or the finer
    /// level inside it does (that moves its hole). The levels a move changes are built as one
    /// batch and applied in the same frame, so the rings never gap or overlap while they
    /// catch up. Heights come from the world's generator (<see cref="HeightfieldGenerator.ScheduleColumnHeightsParallel"/>),
    /// so the far terrain is the same shape as the voxel ground, sampled coarser.
    /// </summary>
    /// <remarks>
    /// Owns only native buffers and job handles; the meshes it fills belong to the caller
    /// (<see cref="FarTerrainView"/>), which keeps the GameObjects.
    /// </remarks>
    public sealed class FarTerrainBuilder : IDisposable
    {
        private readonly HeightfieldGenerator generator;
        private readonly float baseSpacing;
        private readonly int quads;
        private readonly float sink;
        private readonly int firstLevel;
        private readonly Level[] levels;
        private readonly FarTerrainGrid[] wanted;
        private bool batchInFlight;
        private float batchStarted;

        private sealed class Level
        {
            public NativeArray<float> Heights;
            public Mesh.MeshDataArray Data;
            public JobHandle Job;
            public bool Building;
            public bool HasShown;
            public FarTerrainGrid Shown;
            public int4 ShownHole;
            public FarTerrainGrid Next;
            public int4 NextHole;
        }

        /// <param name="range">How far from the focus the coarsest level reaches, in metres.</param>
        /// <param name="baseSpacing">Metres between the finest level's vertices.</param>
        /// <param name="quads">Quads per side of every level: a multiple of 4, at most 252 (16-bit indices).</param>
        /// <param name="sink">Metres the far terrain sits below the true surface.</param>
        /// <param name="firstLevel">The finest level built: levels inside it are left to nearer terrain (the coarse voxel terrain, P7c).</param>
        public FarTerrainBuilder(HeightfieldGenerator generator, float range, float baseSpacing, int quads, float sink, int firstLevel = 0)
        {
            this.generator = generator ?? throw new ArgumentNullException(nameof(generator));
            this.baseSpacing = Mathf.Max(0.25f, baseSpacing);
            this.quads = Mathf.Clamp(quads / 4 * 4, 8, 252);
            this.sink = sink;
            int total = FarTerrainGrid.LevelsFor(range, this.baseSpacing, this.quads);
            this.firstLevel = Mathf.Clamp(firstLevel, 0, total - 1);
            int count = total - this.firstLevel;
            levels = new Level[count];
            wanted = new FarTerrainGrid[count];
            int bordered = (this.quads + 3) * (this.quads + 3);
            for (int i = 0; i < count; i++)
            {
                levels[i] = new Level
                {
                    Heights = new NativeArray<float>(bordered, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
                };
            }
        }

        public int LevelCount => levels.Length;

        /// <summary>The clipmap level the first built level is.</summary>
        public int FirstLevel => firstLevel;

        public int Quads => quads;

        /// <summary>How far the coarsest level reaches from its centre, in metres.</summary>
        public float Reach => levels.Length == 0 ? 0f : FarTerrainGrid.Around(float2.zero, firstLevel + levels.Length - 1, baseSpacing, quads).HalfExtent;

        /// <summary>Milliseconds from the last batch's start until it was applied (worker time, not main-thread time).</summary>
        public float LastBatchMilliseconds { get; private set; }

        /// <summary>Levels rebuilt in the last batch.</summary>
        public int LastBatchLevels { get; private set; }

        /// <summary>The grid a level shows, once it has been built.</summary>
        public bool TryGetShown(int level, out FarTerrainGrid grid)
        {
            grid = levels[level].Shown;
            return levels[level].HasShown;
        }

        /// <summary>
        /// Call once a frame with the focus (world-view space). Applies a finished batch into
        /// <paramref name="meshes"/> (one per level) and starts the next if the focus has moved far
        /// enough to shift any level. Returns true when meshes changed.
        /// </summary>
        public bool Update(Vector3 focus, Mesh[] meshes)
        {
            bool applied = false;
            if (batchInFlight && AllDone())
            {
                ApplyBatch(meshes);
                applied = true;
            }
            if (!batchInFlight)
            {
                StartBatch(new float2(focus.x, focus.z));
            }
            return applied;
        }

        /// <summary>Waits for and applies anything in flight (e.g. before the first frame shows).</summary>
        public void CompleteNow(Mesh[] meshes)
        {
            if (batchInFlight)
            {
                ApplyBatch(meshes);
            }
        }

        public void Dispose()
        {
            foreach (Level level in levels)
            {
                level.Job.Complete();
                if (level.Building)
                {
                    level.Data.Dispose();
                    level.Building = false;
                }
                if (level.Heights.IsCreated)
                {
                    level.Heights.Dispose();
                }
            }
        }

        private bool AllDone()
        {
            foreach (Level level in levels)
            {
                if (level.Building && !level.Job.IsCompleted)
                {
                    return false;
                }
            }
            return true;
        }

        private void StartBatch(float2 focus)
        {
            for (int i = 0; i < levels.Length; i++)
            {
                wanted[i] = FarTerrainGrid.Around(focus, firstLevel + i, baseSpacing, quads);
            }

            int started = 0;
            for (int i = 0; i < levels.Length; i++)
            {
                Level level = levels[i];
                int4 hole = i == 0 ? int4.zero : wanted[i].Hole(wanted[i - 1]);
                if (level.HasShown && level.Shown.Equals(wanted[i]) && math.all(level.ShownHole == hole))
                {
                    continue;
                }
                level.Next = wanted[i];
                level.NextHole = hole;
                level.Data = Mesh.AllocateWritableMeshData(1);
                JobHandle heights = generator.ScheduleColumnHeightsParallel(FarTerrainMeshJob.BorderedBlock(wanted[i]), level.Heights);
                level.Job = new FarTerrainMeshJob
                {
                    Heights = level.Heights,
                    Quads = quads,
                    Spacing = wanted[i].Spacing,
                    First = wanted[i].First,
                    Hole = hole,
                    StitchEdges = i < levels.Length - 1,
                    Sink = sink,
                    Output = level.Data[0],
                }.Schedule(heights);
                level.Building = true;
                started++;
            }
            if (started > 0)
            {
                batchInFlight = true;
                batchStarted = Time.realtimeSinceStartup;
                LastBatchLevels = started;
                JobHandle.ScheduleBatchedJobs();
            }
        }

        private void ApplyBatch(Mesh[] meshes)
        {
            for (int i = 0; i < levels.Length; i++)
            {
                Level level = levels[i];
                if (!level.Building)
                {
                    continue;
                }
                level.Job.Complete();
                Mesh mesh = meshes[i];
                Mesh.ApplyAndDisposeWritableMeshData(
                    level.Data, mesh,
                    UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds | UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices);
                mesh.bounds = mesh.GetSubMesh(0).bounds;
                level.Building = false;
                level.HasShown = true;
                level.Shown = level.Next;
                level.ShownHole = level.NextHole;
            }
            batchInFlight = false;
            LastBatchMilliseconds = (Time.realtimeSinceStartup - batchStarted) * 1000f;
        }
    }
}
