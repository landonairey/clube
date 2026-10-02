using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// The Chapter 1 per-voxel controls for whichever voxel <see cref="VoxelSelector"/>
    /// has selected (K7): read and set its 8 corner densities, or jump to a case.
    /// Every write goes through <see cref="Chunk.SetDensity"/> (A7), and a corner is
    /// a sample shared with the neighbouring voxels, so editing it reshapes those
    /// too. The Inspector panel drives this in Play mode.
    /// </summary>
    [RequireComponent(typeof(ChunkView), typeof(VoxelSelector))]
    public class SelectedVoxelEditor : MonoBehaviour, IVoxelCaseTarget
    {
        private readonly float[] corners = new float[MarchingCubes.CornerCount];

        private ChunkView chunkView;
        private VoxelSelector selector;

        /// <summary>The voxel being edited, or null when nothing is selected.</summary>
        public Vector3Int? Voxel => Selector.SelectedVoxel;

        public bool HasSelection => Voxel != null && ChunkView.Chunk != null;

        public int CaseIndex => HasSelection ? MarchingCubes.GetCaseIndex(ReadCorners(), ChunkView.Config.IsoLevel) : 0;

        private ChunkView ChunkView => chunkView != null ? chunkView : chunkView = GetComponent<ChunkView>();

        private VoxelSelector Selector => selector != null ? selector : selector = GetComponent<VoxelSelector>();

        /// <summary>The density at one of the selected voxel's corners (0-7).</summary>
        public float GetCorner(int corner)
        {
            return ChunkView.Chunk.GetDensity(SampleOf(corner));
        }

        /// <summary>Writes one corner through the chunk's edit path (A7).</summary>
        public void SetCorner(int corner, float density)
        {
            ChunkView.Chunk.SetDensity(SampleOf(corner), density);
        }

        /// <summary>How many voxels in the chunk share this corner sample (1 at a chunk corner, up to 8 inside).</summary>
        public int VoxelsSharing(int corner)
        {
            Vector3Int sample = SampleOf(corner);
            Vector3Int voxelCount = ChunkView.Chunk.VoxelCount;
            int SharedAlong(int axis) => (sample[axis] > 0 ? 1 : 0) + (sample[axis] < voxelCount[axis] ? 1 : 0);
            return SharedAlong(0) * SharedAlong(1) * SharedAlong(2);
        }

        public void ApplyCase(int caseIndex)
        {
            if (!HasSelection)
            {
                return;
            }

            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                SetCorner(corner, MarchingCubes.IsCornerSolid(caseIndex, corner) ? 1f : 0f);
            }
        }

        public void StepCase(int delta)
        {
            int next = ((CaseIndex + delta) % MarchingCubes.CaseCount + MarchingCubes.CaseCount) % MarchingCubes.CaseCount;
            ApplyCase(next);
        }

        private Vector3Int SampleOf(int corner)
        {
            return Voxel.GetValueOrDefault() + MarchingCubes.CornerOffset(corner);
        }

        private float[] ReadCorners()
        {
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                corners[corner] = GetCorner(corner);
            }
            return corners;
        }
    }
}
