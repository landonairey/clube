using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Placeholder terrain for <c>ChunkLab</c> until the 2C generators (K9) exist:
    /// fills the chunk with a sphere of density centred in it, fading from solid
    /// to empty across its surface, so there is a surface to look at. Writes go
    /// through <see cref="Chunk.SetDensity"/> (A7). Remove once K9 lands.
    /// </summary>
    [RequireComponent(typeof(ChunkView))]
    public class ChunkTestFill : MonoBehaviour
    {
        [Tooltip("Sphere radius as a fraction of the chunk's smallest side.")]
        [SerializeField, Range(0f, 1f)]
        private float radius = 0.4f;

        [Tooltip("Distance, in voxels, over which density fades from 1 to 0 across the sphere's surface.")]
        [SerializeField, Min(0.01f)]
        private float falloff = 1f;

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

            Vector3Int samples = chunk.SampleCount;
            Vector3 centre = (Vector3)chunk.VoxelCount * 0.5f;
            float sphereRadius = radius * Mathf.Min(chunk.VoxelCount.x, Mathf.Min(chunk.VoxelCount.y, chunk.VoxelCount.z));

            for (int z = 0; z < samples.z; z++)
            {
                for (int y = 0; y < samples.y; y++)
                {
                    for (int x = 0; x < samples.x; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        float distance = Vector3.Distance(sample, centre);

                        // 0.5 exactly on the sphere's surface, so the default iso level traces it.
                        float density = Mathf.Clamp01(0.5f + (sphereRadius - distance) / (2f * falloff));
                        chunk.SetDensity(sample, density);
                    }
                }
            }
        }
    }
}
