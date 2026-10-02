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
        // Unity's gizmo buffer holds about 38 million vertices, shared by every gizmo, and one
        // sphere is several hundred; 64³ chunks overflowed it. This leaves plenty of margin.
        private const int MaxSampleSpheres = 20000;

        [Tooltip("Draw a sphere on every density sample. Skipped automatically above 20,000 samples (about a 26³ chunk), " +
                 "and slow well before that.")]
        [SerializeField]
        private bool showSamples = true;

        // Capped at half a voxel: any larger and neighbouring corner spheres overlap.
        [Tooltip("Sphere radius as a fraction of the voxel size (at most half an edge).")]
        [SerializeField, Range(0f, 0.5f)]
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

            // Step-through draws the samples itself, coloured by step.
            if (chunk != null && showSamples && !StepThroughMode.IsOn(this) && SampleTotal(chunk) <= MaxSampleSpheres)
            {
                DrawSamples(chunk, voxelSize);
            }
        }

        private static int SampleTotal(Chunk chunk)
        {
            Vector3Int count = chunk.SampleCount;
            return count.x * count.y * count.z;
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
