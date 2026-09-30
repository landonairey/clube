using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Draws the chunk's bounds and, in Play mode, a sphere on every density
    /// sample shaded by its value, black = 0 to white = 1 (V6). Reads the chunk's
    /// public data only (A4). Editor visualisation only.
    /// </summary>
    [RequireComponent(typeof(ChunkView))]
    public class ChunkGizmos : MonoBehaviour
    {
        [Tooltip("Sphere radius as a fraction of the voxel size.")]
        [SerializeField, Min(0f)]
        private float cornerRadius = 0.05f;

        [SerializeField]
        private Color outlineColor = new Color(1f, 1f, 1f, 0.3f);

        private void OnDrawGizmos()
        {
            var view = GetComponent<ChunkView>();
            WorldConfig config = view.Config;
            if (config == null)
            {
                return;
            }

            // Before Play mode there is no chunk yet, so size the outline from the config.
            Chunk chunk = view.Chunk;
            Vector3Int voxelCount = chunk != null ? chunk.VoxelCount : config.ChunkSize;
            float voxelSize = config.VoxelSize;

            Gizmos.matrix = transform.localToWorldMatrix;

            Vector3 size = (Vector3)voxelCount * voxelSize;
            Gizmos.color = outlineColor;
            Gizmos.DrawWireCube(size * 0.5f, size);

            if (chunk != null)
            {
                DrawSamples(chunk, voxelSize);
            }
        }

        private void DrawSamples(Chunk chunk, float voxelSize)
        {
            Vector3Int sampleCount = chunk.SampleCount;
            for (int z = 0; z < sampleCount.z; z++)
            {
                for (int y = 0; y < sampleCount.y; y++)
                {
                    for (int x = 0; x < sampleCount.x; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        Gizmos.color = Color.Lerp(Color.black, Color.white, chunk.GetDensity(sample));
                        Gizmos.DrawSphere((Vector3)sample * voxelSize, cornerRadius * voxelSize);
                    }
                }
            }
        }
    }
}
