using System;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>Seconds each kind of step takes to play at speed 1 (V17).</summary>
    [Serializable]
    public class StepTimings
    {
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
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }
}
