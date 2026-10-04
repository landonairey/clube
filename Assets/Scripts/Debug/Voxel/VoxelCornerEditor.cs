using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// The 8 corner densities of the lab's voxel (V2, K7, M19): read and set them, or jump
    /// to a case. The voxel is the <see cref="VoxelSelector"/>'s selection on this object
    /// (ChunkLab, WorldLab), or voxel (0,0,0) when there's no selector (VoxelLab, where the
    /// chunk is that one voxel). Writes go through the target chunk's edit path (A7), so a
    /// corner shared with neighbouring voxels, or chunks, reshapes them too.
    /// </summary>
    /// <remarks>
    /// VoxelLab edits its voxel outside Play mode too: <see cref="cornerValues"/> holds the
    /// corners then, and is applied to the chunk once it exists. With a selector, the values
    /// always come from the chunk and that field is unused.
    /// </remarks>
    public class VoxelCornerEditor : MonoBehaviour
    {
        private const float DefaultIsoLevel = 0.5f;

        [Tooltip("VoxelLab's corners (no selector): 0 = empty, 1 = solid; bottom face 0-3, top face 4-7. " +
                 "Applied when the chunk is created. Unused when a VoxelSelector picks the voxel.")]
        [SerializeField, Range(0f, 1f)]
        private float[] cornerValues = { 1f, 1f, 1f, 1f, 0f, 0f, 0f, 0f };

        private readonly float[] chunkCorners = new float[MarchingCubes.CornerCount];

        private LabChunkTarget target;
        private VoxelSelector selector;
        private bool selectorLooked;

        /// <summary>The voxel being edited, chunk-local, or null when nothing is selected.</summary>
        public Vector3Int? Voxel => Selector != null ? Selector.SelectedVoxel : Vector3Int.zero;

        /// <summary>True when there's a voxel to edit: one is selected, or VoxelLab's voxel (also outside Play mode).</summary>
        public bool HasVoxel => Voxel.HasValue && (TargetChunk != null || UsesStoredCorners);

        /// <summary>
        /// The voxel's corner densities: read from the chunk, or VoxelLab's stored corners
        /// before the chunk exists. Empty-handed (all 0) with nothing selected.
        /// </summary>
        public IReadOnlyList<float> CornerValues
        {
            get
            {
                Chunk chunk = TargetChunk;
                if (chunk == null || !Voxel.HasValue)
                {
                    return UsesStoredCorners ? cornerValues : (IReadOnlyList<float>)ClearCorners();
                }

                for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
                {
                    chunkCorners[corner] = chunk.GetDensity(SampleOf(corner));
                }
                return chunkCorners;
            }
        }

        /// <summary>Iso level of the target chunk, or its config outside Play mode, so readouts work there too.</summary>
        public float IsoLevel => HasSettings ? Target.MeshSettings.IsoLevel : DefaultIsoLevel;

        /// <summary>Edge placement of the target chunk (V3), for tools that re-run the mesher.</summary>
        public EdgePlacement EdgePlacement => HasSettings ? Target.MeshSettings.EdgePlacement : EdgePlacement.Interpolated;

        public float VoxelSize => HasSettings ? Target.MeshSettings.VoxelSize : 1f;

        /// <summary>Where the voxel's corner 0 sits, in this object's local space.</summary>
        public Vector3 VoxelOrigin => (Vector3)Voxel.GetValueOrDefault() * VoxelSize;

        /// <summary>The case index the corner values produce (V7).</summary>
        public int CaseIndex => HasVoxel ? MarchingCubes.GetCaseIndex(CornerValues, IsoLevel) : 0;

        private LabChunkTarget Target => target != null ? target : target = GetComponentInParent<LabChunkTarget>();

        // Null-safe: in the prefab asset on its own there's no chunk tools above this.
        private Chunk TargetChunk => Target != null ? Target.Chunk : null;

        private bool HasSettings => Target != null && Target.Current != null;

        private VoxelSelector Selector
        {
            get
            {
                if (!selectorLooked)
                {
                    selector = GetComponentInParent<VoxelSelector>();
                    selectorLooked = true;
                }
                return selector;
            }
        }

        /// <summary>VoxelLab: no selector, so the voxel is (0,0,0) and its corners are also kept on this component.</summary>
        public bool UsesStoredCorners => Selector == null;

        /// <summary>The density at one of the voxel's corners (0-7).</summary>
        public float GetCorner(int corner)
        {
            return CornerValues[corner];
        }

        /// <summary>Sets one corner's density (0 = empty, 1 = solid) through the chunk's edit path (A7).</summary>
        public void SetCorner(int corner, float density)
        {
            density = Mathf.Clamp01(density);
            if (UsesStoredCorners)
            {
                cornerValues[corner] = density;
            }
            if (Voxel.HasValue && Target != null)
            {
                Target.SetDensity(SampleOf(corner), density);
            }
        }

        /// <summary>How many voxels in the chunk share this corner sample (1 at a chunk corner, up to 8 inside).</summary>
        public int VoxelsSharing(int corner)
        {
            Chunk chunk = TargetChunk;
            if (chunk == null)
            {
                return 1;
            }

            Vector3Int sample = SampleOf(corner);
            Vector3Int voxelCount = chunk.VoxelCount;
            int SharedAlong(int axis) => (sample[axis] > 0 ? 1 : 0) + (sample[axis] < voxelCount[axis] ? 1 : 0);
            return SharedAlong(0) * SharedAlong(1) * SharedAlong(2);
        }

        /// <summary>
        /// Sets every corner to 1 (solid) or 0 (empty) to match a case index (V10, V20).
        /// That reproduces the case for any iso level above 0.
        /// </summary>
        public void ApplyCase(int caseIndex)
        {
            if (!HasVoxel)
            {
                return;
            }

            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                SetCorner(corner, MarchingCubes.IsCornerSolid(caseIndex, corner) ? 1f : 0f);
            }
        }

        /// <summary>Moves to the next (+1) or previous (-1) case index, wrapping 255 ↔ 0.</summary>
        public void StepCase(int delta)
        {
            int next = ((CaseIndex + delta) % MarchingCubes.CaseCount + MarchingCubes.CaseCount) % MarchingCubes.CaseCount;
            ApplyCase(next);
        }

        private void Start()
        {
            ApplyStoredCorners();
        }

        // Moved under other chunk tools: look the target and selector up again.
        private void OnTransformParentChanged()
        {
            target = null;
            selector = null;
            selectorLooked = false;
        }

        // Called by Unity whenever an Inspector value changes, including in Play mode.
        private void OnValidate()
        {
            if (cornerValues == null || cornerValues.Length != MarchingCubes.CornerCount)
            {
                System.Array.Resize(ref cornerValues, MarchingCubes.CornerCount);
            }

            ApplyStoredCorners();
        }

        // VoxelLab: write the stored corners into the chunk, once it exists.
        private void ApplyStoredCorners()
        {
            if (!UsesStoredCorners || TargetChunk == null)
            {
                return;
            }

            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                Target.SetDensity(MarchingCubes.CornerOffset(corner), cornerValues[corner]);
            }
        }

        private Vector3Int SampleOf(int corner)
        {
            return Voxel.GetValueOrDefault() + MarchingCubes.CornerOffset(corner);
        }

        private float[] ClearCorners()
        {
            System.Array.Clear(chunkCorners, 0, chunkCorners.Length);
            return chunkCorners;
        }
    }
}
