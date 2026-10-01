using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// What a recorded mesh build saw and produced for one voxel (V15): enough
    /// for playback to redraw any step without re-running the algorithm (A11).
    /// </summary>
    public sealed class RecordedVoxel
    {
        private readonly float[] cornerValues = new float[MarchingCubes.CornerCount];
        private readonly Vector3[] edgeVertices = new Vector3[MarchingCubes.EdgeCount];

        internal RecordedVoxel(Vector3Int voxel, Vector3 origin, IReadOnlyList<float> corners)
        {
            Voxel = voxel;
            Origin = origin;
            for (int corner = 0; corner < cornerValues.Length; corner++)
            {
                cornerValues[corner] = corners[corner];
            }
        }

        /// <summary>The voxel's coordinate in the chunk.</summary>
        public Vector3Int Voxel { get; }

        /// <summary>Chunk-local position of corner 0.</summary>
        public Vector3 Origin { get; }

        /// <summary>The 8 corner densities, ordered as in <see cref="MarchingCubesTables.CornerOffsets"/>.</summary>
        public IReadOnlyList<float> CornerValues => cornerValues;

        public int CaseIndex { get; internal set; }

        /// <summary>Bit <c>e</c> set when the surface crosses edge <c>e</c>.</summary>
        public int CrossedEdgeMask { get; internal set; }

        /// <summary>
        /// Chunk-local vertex position per edge (0-11). Only edges in
        /// <see cref="CrossedEdgeMask"/> hold a vertex.
        /// </summary>
        public IReadOnlyList<Vector3> EdgeVertices => edgeVertices;

        internal void SetEdgeVertex(int edge, Vector3 position)
        {
            edgeVertices[edge] = position;
        }
    }
}
