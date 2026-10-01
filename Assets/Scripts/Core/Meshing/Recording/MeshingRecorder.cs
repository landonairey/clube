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
    /// Recording only exists in the Editor and development builds (A5): in a
    /// release build the record calls compile out and the log stays empty.
    /// When no recorder is passed, the mesher does no extra work.
    /// </remarks>
    public sealed class MeshingRecorder
    {
        private const string EditorSymbol = "UNITY_EDITOR";
        private const string DevelopmentSymbol = "DEVELOPMENT_BUILD";

        private readonly List<RecordedVoxel> voxels = new List<RecordedVoxel>();
        private readonly List<MeshingStep> steps = new List<MeshingStep>();

        /// <summary>The settings the recorded build used (iso level, voxel size, variants).</summary>
        public ChunkMeshSettings Settings { get; private set; }

        /// <summary>Every voxel in build order, each with what the build saw and produced for it.</summary>
        public IReadOnlyList<RecordedVoxel> Voxels => voxels;

        /// <summary>Every step in build order.</summary>
        public IReadOnlyList<MeshingStep> Steps => steps;

        private int CurrentVoxel => voxels.Count - 1;

        [Conditional(EditorSymbol), Conditional(DevelopmentSymbol)]
        internal void Begin(ChunkMeshSettings settings)
        {
            Settings = settings;
            voxels.Clear();
            steps.Clear();
        }

        [Conditional(EditorSymbol), Conditional(DevelopmentSymbol)]
        internal void BeginVoxel(Vector3Int voxel, Vector3 origin, IReadOnlyList<float> cornerValues)
        {
            voxels.Add(new RecordedVoxel(voxel, origin, cornerValues));
            steps.Add(new MeshingStep(MeshingStepType.ReadCorners, CurrentVoxel));
        }

        [Conditional(EditorSymbol), Conditional(DevelopmentSymbol)]
        internal void RecordCase(int caseIndex, int crossedEdgeMask)
        {
            RecordedVoxel voxel = voxels[CurrentVoxel];
            voxel.CaseIndex = caseIndex;
            voxel.CrossedEdgeMask = crossedEdgeMask;

            steps.Add(new MeshingStep(MeshingStepType.Classify, CurrentVoxel));
            steps.Add(new MeshingStep(MeshingStepType.CaseIndex, CurrentVoxel));
            steps.Add(new MeshingStep(MeshingStepType.EdgeTable, CurrentVoxel));
        }

        [Conditional(EditorSymbol), Conditional(DevelopmentSymbol)]
        internal void RecordVertex(int edge, Vector3 position)
        {
            voxels[CurrentVoxel].SetEdgeVertex(edge, position);
            steps.Add(new MeshingStep(MeshingStepType.Interpolate, CurrentVoxel, edge));
        }

        [Conditional(EditorSymbol), Conditional(DevelopmentSymbol)]
        internal void RecordTriangle(int edgeA, int edgeB, int edgeC)
        {
            steps.Add(new MeshingStep(
                MeshingStepType.Triangle, CurrentVoxel, triangle: new EdgeTriangle(edgeA, edgeB, edgeC)));
        }
    }
}
