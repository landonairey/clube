using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Draws the chunk's bounds, optionally the wireframe of every voxel, and in
    /// Play mode a sphere on every density sample shaded by its value, black = 0
    /// to white = 1 (V6). Reads the chunk's public data only (A4). Editor
    /// visualisation only.
    /// </summary>
    [RequireComponent(typeof(ChunkView))]
    public class ChunkGizmos : MonoBehaviour
    {
        /// <summary>
        /// Above this many samples the spheres are suppressed. Unity's gizmo buffer
        /// holds about 38 million vertices, shared by every gizmo, and one sphere is
        /// several hundred; 64³ chunks overflowed it. This leaves plenty of margin.
        /// </summary>
        public const int MaxSampleSpheres = 20000;

        [Tooltip("Draw a sphere on every density sample. Suppressed automatically above 20,000 samples " +
                 "(about a 26³ chunk), and slow well before that.")]
        [SerializeField]
        private bool showSamples = true;

        // Capped at half a voxel: any larger and neighbouring corner spheres overlap.
        [Tooltip("Sphere radius as a fraction of the voxel size (at most half an edge).")]
        [SerializeField, Range(0f, 0.5f)]
        private float cornerRadius = 0.05f;

        [Tooltip("Outline every sample sphere, so dark (empty) samples stay visible against a dark background.")]
        [SerializeField]
        private Color sampleOutlineColor = new Color(0.75f, 0.75f, 0.75f, 0.5f);

        [Tooltip("Draw the grid lines between voxels, showing every voxel's wireframe.")]
        [SerializeField]
        private bool showVoxelGrid;

        [SerializeField]
        private Color voxelGridColor = new Color(1f, 1f, 1f, 0.12f);

        [SerializeField]
        private Color outlineColor = new Color(1f, 1f, 1f, 0.3f);

        /// <summary>True when sample spheres are wanted but the chunk has too many samples to draw them.</summary>
        public bool AreSamplesSuppressed
        {
            get
            {
                Chunk chunk = GetComponent<ChunkView>().Chunk;
                return showSamples && chunk != null && SampleTotal(chunk) > MaxSampleSpheres;
            }
        }

        private void OnDrawGizmos()
        {
            if (!enabled)
            {
                return;
            }

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

            if (showVoxelGrid)
            {
                DrawVoxelGrid(voxelCount, voxelSize);
            }

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

        // Lines through every sample row along each axis, which together outline every voxel.
        private void DrawVoxelGrid(Vector3Int voxelCount, float voxelSize)
        {
            Gizmos.color = voxelGridColor;
            Vector3 size = (Vector3)voxelCount * voxelSize;

            for (int a = 0; a <= voxelCount.y; a++)
            {
                for (int b = 0; b <= voxelCount.z; b++)
                {
                    var start = new Vector3(0f, a, b) * voxelSize;
                    Gizmos.DrawLine(start, start + Vector3.right * size.x);
                }
            }
            for (int a = 0; a <= voxelCount.x; a++)
            {
                for (int b = 0; b <= voxelCount.z; b++)
                {
                    var start = new Vector3(a, 0f, b) * voxelSize;
                    Gizmos.DrawLine(start, start + Vector3.up * size.y);
                }
            }
            for (int a = 0; a <= voxelCount.x; a++)
            {
                for (int b = 0; b <= voxelCount.y; b++)
                {
                    var start = new Vector3(a, b, 0f) * voxelSize;
                    Gizmos.DrawLine(start, start + Vector3.forward * size.z);
                }
            }
        }

        private void DrawSamples(Chunk chunk, float voxelSize)
        {
            float radius = cornerRadius * voxelSize;
            Vector3Int sampleCount = chunk.SampleCount;
            for (int z = 0; z < sampleCount.z; z++)
            {
                for (int y = 0; y < sampleCount.y; y++)
                {
                    for (int x = 0; x < sampleCount.x; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        Vector3 position = (Vector3)sample * voxelSize;

                        Gizmos.color = Color.Lerp(Color.black, Color.white, chunk.GetDensity(sample));
                        Gizmos.DrawSphere(position, radius);
                        Gizmos.color = sampleOutlineColor;
                        Gizmos.DrawWireSphere(position, radius);
                    }
                }
            }
        }
    }
}
