using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Step log of one mesh build (V15, A11). Pass it to <see cref="ChunkMesher.Build"/>
    /// and the real algorithm logs each step as it runs; playback then replays the
    /// log and never re-runs the algorithm itself.
    /// </summary>
    /// <remarks>
    /// Recording only exists in the Editor, development builds and the lab demo
    /// build (<c>CLUBE_LAB_BUILD</c>; A5): in a release build the record calls
    /// compile out and the log stays empty.
    /// When no recorder is passed, the mesher does no extra work.
    /// </remarks>
    public sealed class MeshingRecorder
    {
        private const string EditorSymbol = "UNITY_EDITOR";
        private const string DevelopmentSymbol = "DEVELOPMENT_BUILD";

        // Set only by the lab demo build (DemoBuild), so its step-through works; the game never sets it.
        private const string LabBuildSymbol = "CLUBE_LAB_BUILD";

        private readonly List<RecordedVoxel> voxels = new List<RecordedVoxel>();
        private readonly List<MeshingStep> steps = new List<MeshingStep>();

        private float[] densities = new float[0];

        /// <summary>
        /// True when recording is compiled into this build (the Editor, a development build,
        /// or the lab demo build). Elsewhere the log always stays empty.
        /// </summary>
        public static bool IsAvailable =>
#if UNITY_EDITOR || DEVELOPMENT_BUILD || CLUBE_LAB_BUILD
            true;
#else
            false;
#endif

        /// <summary>The settings the recorded build used (iso level, voxel size, variants).</summary>
        public ChunkMeshSettings Settings { get; private set; }

        /// <summary>Size of the recorded density field, in samples.</summary>
        public Vector3Int SampleCount { get; private set; }

        /// <summary>Every voxel in build order, each with what the build saw and produced for it.</summary>
        public IReadOnlyList<RecordedVoxel> Voxels => voxels;

        /// <summary>Every step in build order.</summary>
        public IReadOnlyList<MeshingStep> Steps => steps;

        private int CurrentVoxel => voxels.Count - 1;

        /// <summary>The recorded density at a sample, as the build read it.</summary>
        public float GetDensity(Vector3Int sample)
        {
            return densities[sample.x + SampleCount.x * (sample.y + SampleCount.y * sample.z)];
        }

        /// <summary>Drops the log, e.g. when the chunk it recorded is no longer shown.</summary>
        public void Clear()
        {
            voxels.Clear();
            steps.Clear();
        }

        /// <summary>Starts a new log with the chunk's density field as its first step.</summary>
        [Conditional(EditorSymbol), Conditional(DevelopmentSymbol), Conditional(LabBuildSymbol)]
        internal void Begin(ChunkMeshSettings settings, Chunk chunk)
        {
            Settings = settings;
            voxels.Clear();
            steps.Clear();

            SampleCount = chunk.SampleCount;
            int total = SampleCount.x * SampleCount.y * SampleCount.z;
            if (densities.Length != total)
            {
                densities = new float[total];
            }

            for (int z = 0; z < SampleCount.z; z++)
            {
                for (int y = 0; y < SampleCount.y; y++)
                {
                    for (int x = 0; x < SampleCount.x; x++)
                    {
                        densities[x + SampleCount.x * (y + SampleCount.y * z)] = chunk.GetDensity(new Vector3Int(x, y, z));
                    }
                }
            }

            steps.Add(new MeshingStep(MeshingStepType.DensityField, voxelIndex: -1));
        }

        [Conditional(EditorSymbol), Conditional(DevelopmentSymbol), Conditional(LabBuildSymbol)]
        internal void BeginVoxel(Vector3Int voxel, Vector3 origin, IReadOnlyList<float> cornerValues)
        {
            voxels.Add(new RecordedVoxel(voxel, origin, cornerValues));
        }

        [Conditional(EditorSymbol), Conditional(DevelopmentSymbol), Conditional(LabBuildSymbol)]
        internal void RecordCase(int caseIndex, int crossedEdgeMask)
        {
            RecordedVoxel voxel = voxels[CurrentVoxel];
            voxel.CaseIndex = caseIndex;
            voxel.CrossedEdgeMask = crossedEdgeMask;

            steps.Add(new MeshingStep(MeshingStepType.SampleCorners, CurrentVoxel));
            steps.Add(new MeshingStep(MeshingStepType.CaseIndex, CurrentVoxel));
            steps.Add(new MeshingStep(MeshingStepType.EdgeTable, CurrentVoxel));
        }

        [Conditional(EditorSymbol), Conditional(DevelopmentSymbol), Conditional(LabBuildSymbol)]
        internal void RecordVertex(int edge, Vector3 position)
        {
            voxels[CurrentVoxel].SetEdgeVertex(edge, position);
            steps.Add(new MeshingStep(MeshingStepType.Interpolate, CurrentVoxel, edge));
        }

        [Conditional(EditorSymbol), Conditional(DevelopmentSymbol), Conditional(LabBuildSymbol)]
        internal void RecordTriangle(int edgeA, int edgeB, int edgeC)
        {
            steps.Add(new MeshingStep(
                MeshingStepType.Triangle, CurrentVoxel, triangle: new EdgeTriangle(edgeA, edgeB, edgeC)));
        }

        /// <summary>Closes the log with the normals step, after the last voxel (K18).</summary>
        [Conditional(EditorSymbol), Conditional(DevelopmentSymbol), Conditional(LabBuildSymbol)]
        internal void End()
        {
            steps.Add(new MeshingStep(MeshingStepType.Normals, voxelIndex: -1));
        }
    }
}
