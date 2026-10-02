using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Test shapes for <c>ChunkLab</c>, independent of the terrain generators (K9):
    /// a ball of dirt in the middle of the chunk, or a chunk filled solid with a
    /// shaft carved from top to bottom. Densities fade from 1 to 0 over one voxel
    /// across each surface, so the default iso level traces the shape smoothly.
    /// Writes go through <see cref="Chunk.SetDensity"/> (A7).
    /// </summary>
    [RequireComponent(typeof(ChunkView))]
    public class ChunkTestFill : MonoBehaviour
    {
        /// <summary>The shapes this tool can fill the chunk with.</summary>
        public enum Shape
        {
            /// <summary>A ball centred in the chunk, empty around it.</summary>
            Ball,

            /// <summary>Solid everywhere except a vertical shaft through the centre.</summary>
            SolidWithShaft,
        }

        [SerializeField]
        private Shape shape = Shape.Ball;

        [Tooltip("Ball or shaft radius, as a fraction of the chunk's smallest horizontal or overall side.")]
        [SerializeField, Range(0f, 1f)]
        private float radius = 0.4f;

        private ChunkView chunkView;

        private void Awake()
        {
            chunkView = GetComponent<ChunkView>();
        }

        private void OnEnable()
        {
            chunkView.ChunkCreated += Fill;
        }

        private void OnDisable()
        {
            chunkView.ChunkCreated -= Fill;
        }

        private void Start()
        {
            Fill(chunkView.Chunk);
        }

        // Called by Unity whenever an Inspector value changes, including in Play mode.
        private void OnValidate()
        {
            if (chunkView != null && chunkView.Chunk != null)
            {
                Fill(chunkView.Chunk);
            }
        }

        private void Fill(Chunk chunk)
        {
            if (chunk == null)
            {
                return;
            }

            Vector3Int voxels = chunk.VoxelCount;
            Vector3 centre = (Vector3)voxels * 0.5f;
            float ballRadius = radius * Mathf.Min(voxels.x, Mathf.Min(voxels.y, voxels.z));
            float shaftRadius = radius * Mathf.Min(voxels.x, voxels.z);

            Vector3Int samples = chunk.SampleCount;
            for (int z = 0; z < samples.z; z++)
            {
                for (int y = 0; y < samples.y; y++)
                {
                    for (int x = 0; x < samples.x; x++)
                    {
                        var sample = new Vector3Int(x, y, z);

                        // Signed distance inside the solid: positive inside, negative outside.
                        float inside = shape == Shape.Ball
                            ? ballRadius - Vector3.Distance(sample, centre)
                            : Vector2.Distance(new Vector2(x, z), new Vector2(centre.x, centre.z)) - shaftRadius;

                        // 0.5 exactly on the surface, reaching 0 and 1 one voxel either side.
                        chunk.SetDensity(sample, Mathf.Clamp01(0.5f + inside * 0.5f));
                    }
                }
            }
        }
    }
}
