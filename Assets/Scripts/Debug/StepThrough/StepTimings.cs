using System;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Seconds each kind of step takes to play at speed 1 (V17), and how long a whole
    /// voxel or Z slice takes when playback steps by those (K19).
    /// </summary>
    [Serializable]
    public class StepTimings
    {
        // At 0.8 s a voxel, a 4×4×4 chunk plays through in about a minute (2F).
        [Tooltip("Stepping by voxel: seconds per voxel. Its sub-steps share this time in proportion to their own durations.")]
        [SerializeField, Min(0f)]
        private float voxel = 0.8f;

        [Tooltip("Stepping by Z slice: seconds per slice.")]
        [SerializeField, Min(0f)]
        private float slice = 3f;

        [Tooltip("Density samples appear one at a time.")]
        [SerializeField, Min(0f)]
        private float densityField = 1.5f;

        [Tooltip("Corners are sampled and turn solid or empty one at a time.")]
        [SerializeField, Min(0f)]
        private float sampleCorners = 2f;

        [Tooltip("The case index is built one bit (corner) at a time.")]
        [SerializeField, Min(0f)]
        private float caseIndex = 2.5f;

        [Tooltip("Crossed edges light up one at a time.")]
        [SerializeField, Min(0f)]
        private float edgeTable = 1.5f;

        [Tooltip("Per vertex: it slides along its edge to the crossing.")]
        [SerializeField, Min(0f)]
        private float interpolate = 0.8f;

        [Tooltip("Per triangle: its outline is drawn in winding order, then it fills.")]
        [SerializeField, Min(0f)]
        private float triangle = 1f;

        [Tooltip("Once, at the end: a normal grows from every triangle, then the finished mesh is shaded.")]
        [SerializeField, Min(0f)]
        private float normals = 2f;

        public float Voxel => voxel;

        public float Slice => slice;

        public float For(MeshingStepType type)
        {
            switch (type)
            {
                case MeshingStepType.DensityField: return densityField;
                case MeshingStepType.SampleCorners: return sampleCorners;
                case MeshingStepType.CaseIndex: return caseIndex;
                case MeshingStepType.EdgeTable: return edgeTable;
                case MeshingStepType.Interpolate: return interpolate;
                case MeshingStepType.Triangle: return triangle;
                case MeshingStepType.Normals: return normals;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }
}
