using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// When each part of a step appears as the step plays from progress 0 to 1.
    /// Shared by the 3D markers (<see cref="StepThroughVisuals"/>) and the labels
    /// (<see cref="StepThroughLab"/>) so the two always agree.
    /// </summary>
    public static class StepReveal
    {
        /// <summary>How many of <paramref name="count"/> items have appeared, one per equal slice of the step.</summary>
        public static int Revealed(float progress, int count)
        {
            return progress >= 1f ? count : Mathf.FloorToInt(progress * count);
        }

        /// <summary>How many corners (from c0) have been sampled and shown as solid or empty.</summary>
        public static int CornersSampled(MeshingStepType type, float progress)
        {
            switch (type)
            {
                case MeshingStepType.DensityField: return 0;
                case MeshingStepType.SampleCorners: return Revealed(progress, MarchingCubes.CornerCount);
                default: return MarchingCubes.CornerCount;
            }
        }

        /// <summary>How many case index bits (from bit 0 = c0) are known.</summary>
        public static int CaseBitsKnown(MeshingStepType type, float progress)
        {
            if (type < MeshingStepType.CaseIndex)
            {
                return 0;
            }
            if (type > MeshingStepType.CaseIndex || progress >= 1f)
            {
                return MarchingCubes.CornerCount;
            }
            return Mathf.Min(Mathf.FloorToInt(progress * MarchingCubes.CornerCount) + 1, MarchingCubes.CornerCount);
        }

        /// <summary>
        /// True while the case index is built and this corner's bit has been added as a 1.
        /// Empty corners add nothing, so they are never highlighted.
        /// </summary>
        public static bool IsBitHighlighted(RecordedVoxel voxel, int corner, MeshingStepType type, float progress)
        {
            return type == MeshingStepType.CaseIndex
                   && MarchingCubes.IsCornerSolid(voxel.CaseIndex, corner)
                   && corner < CaseBitsKnown(type, progress);
        }

        /// <summary>True once a crossed edge has lit up; during the edge table step they light one at a time.</summary>
        public static bool IsEdgeLit(RecordedVoxel voxel, int edge, MeshingStepType type, float progress)
        {
            if ((voxel.CrossedEdgeMask & (1 << edge)) == 0 || type < MeshingStepType.EdgeTable)
            {
                return false;
            }
            if (type > MeshingStepType.EdgeTable)
            {
                return true;
            }

            int rank = CountBits(voxel.CrossedEdgeMask & ((1 << edge) - 1));
            return rank < Revealed(progress, CountBits(voxel.CrossedEdgeMask));
        }

        private static int CountBits(int mask)
        {
            int count = 0;
            for (; mask != 0; mask &= mask - 1)
            {
                count++;
            }
            return count;
        }
    }
}
