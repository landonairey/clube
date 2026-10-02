using System;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>Seconds each kind of step takes to play at speed 1 (V17).</summary>
    [Serializable]
    public class StepTimings
    {
        [SerializeField, Min(0f)]
        private float readCorners = 1.5f;

        [Tooltip("Corners light up one at a time.")]
        [SerializeField, Min(0f)]
        private float classify = 2f;

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
                case MeshingStepType.ReadCorners: return readCorners;
                case MeshingStepType.Classify: return classify;
                case MeshingStepType.CaseIndex: return caseIndex;
                case MeshingStepType.EdgeTable: return edgeTable;
                case MeshingStepType.Interpolate: return interpolate;
                case MeshingStepType.Triangle: return triangle;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }
}
