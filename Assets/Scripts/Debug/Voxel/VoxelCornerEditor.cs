using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Inspector sliders for the 8 corner densities of voxel (0,0,0) in the
    /// chunk (V2). In <c>VoxelLab</c> the chunk is 1x1x1, so this is the whole
    /// voxel. Writes go through <see cref="Chunk.SetDensity"/>, the same edit
    /// path the in-game brush will use (A7).
    /// </summary>
    [RequireComponent(typeof(ChunkView))]
    public class VoxelCornerEditor : MonoBehaviour
    {
        private const float DefaultIsoLevel = 0.5f;

        [Tooltip("Corner densities, 0 = empty, 1 = solid. Order: bottom face 0-3, top face 4-7.")]
        [SerializeField, Range(0f, 1f)]
        private float[] cornerValues = { 1f, 1f, 1f, 1f, 0f, 0f, 0f, 0f };

        private ChunkView chunkView;

        public IReadOnlyList<float> CornerValues => cornerValues;

        /// <summary>Iso level from the chunk's config, so readouts also work outside Play mode.</summary>
        public float IsoLevel
        {
            get
            {
                WorldConfig config = GetComponent<ChunkView>().Config;
                return config != null ? config.IsoLevel : DefaultIsoLevel;
            }
        }

        public float VoxelSize
        {
            get
            {
                WorldConfig config = GetComponent<ChunkView>().Config;
                return config != null ? config.VoxelSize : 1f;
            }
        }

        /// <summary>The case index the current corner values produce (V7).</summary>
        public int CaseIndex => MarchingCubes.GetCaseIndex(cornerValues, IsoLevel);

        /// <summary>
        /// Sets every corner to 1 (solid) or 0 (empty) to match a case index (V10, V20).
        /// That reproduces the case for any iso level above 0.
        /// </summary>
        public void ApplyCase(int caseIndex)
        {
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                cornerValues[corner] = MarchingCubes.IsCornerSolid(caseIndex, corner) ? 1f : 0f;
            }

            ApplyCorners();
        }

        /// <summary>Moves to the next (+1) or previous (-1) case index, wrapping 255 ↔ 0.</summary>
        public void StepCase(int delta)
        {
            int next = ((CaseIndex + delta) % MarchingCubes.CaseCount + MarchingCubes.CaseCount) % MarchingCubes.CaseCount;
            ApplyCase(next);
        }

        private void Awake()
        {
            chunkView = GetComponent<ChunkView>();
        }

        private void Start()
        {
            ApplyCorners();
        }

        // Called by Unity whenever an Inspector value changes, including in Play mode.
        private void OnValidate()
        {
            if (cornerValues == null || cornerValues.Length != MarchingCubes.CornerCount)
            {
                System.Array.Resize(ref cornerValues, MarchingCubes.CornerCount);
            }

            ApplyCorners();
        }

        private void ApplyCorners()
        {
            Chunk chunk = chunkView != null ? chunkView.Chunk : null;
            if (chunk == null)
            {
                return;
            }

            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                chunk.SetDensity(MarchingCubes.CornerOffset(corner), cornerValues[corner]);
            }
        }
    }
}
