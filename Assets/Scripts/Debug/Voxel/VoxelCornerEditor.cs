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
        [Tooltip("Corner densities, 0 = empty, 1 = solid. Order: bottom face 0-3, top face 4-7.")]
        [SerializeField, Range(0f, 1f)]
        private float[] cornerValues = { 1f, 1f, 1f, 1f, 0f, 0f, 0f, 0f };

        private ChunkView chunkView;

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
