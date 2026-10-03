using System.Diagnostics;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Measures the solid volume inside the chunk after every rebuild, both ways
    /// from Chapter 1: the approximate mean-density sum (V11) and the exact sum of
    /// tetrahedra (V12), via <see cref="ChunkVolume"/>. <see cref="ChunkStatsHud"/>
    /// shows the results. The exact sum takes milliseconds on bigger chunks, so
    /// <see cref="calculate"/> can switch the measurement off.
    /// </summary>
    [RequireComponent(typeof(ChunkView))]
    public class ChunkVolumeStats : MonoBehaviour
    {
        [Tooltip("Measure the volume after every rebuild. Off skips the work (the exact sum costs a few ms on bigger chunks) and hides the HUD line.")]
        [SerializeField]
        private bool calculate = true;

        private ChunkView chunkView;

        public bool HasMeasurement { get; private set; }

        /// <summary>Approximate solid volume (V11), world units³.</summary>
        public float Approximate { get; private set; }

        /// <summary>Exact solid volume of the Marching Cubes surface (V12), world units³.</summary>
        public float Exact { get; private set; }

        /// <summary>The whole chunk's volume, for percentages.</summary>
        public float ChunkCapacity { get; private set; }

        /// <summary>How long the last measurement took, both methods together.</summary>
        public double Milliseconds { get; private set; }

        private void Awake()
        {
            chunkView = GetComponent<ChunkView>();
        }

        private void OnEnable()
        {
            chunkView.MeshRebuilt += OnMeshRebuilt;
            MeasureIfWanted();
        }

        private void OnDisable()
        {
            chunkView.MeshRebuilt -= OnMeshRebuilt;
            HasMeasurement = false;
        }

        // Toggling in the Inspector applies at once instead of waiting for the next rebuild.
        private void OnValidate()
        {
            if (chunkView != null && isActiveAndEnabled)
            {
                MeasureIfWanted();
            }
        }

        private void OnMeshRebuilt(Mesh mesh)
        {
            MeasureIfWanted();
        }

        private void MeasureIfWanted()
        {
            if (calculate && chunkView.Chunk != null)
            {
                Measure();
            }
            else
            {
                HasMeasurement = false;
            }
        }

        private void Measure()
        {
            Chunk chunk = chunkView.Chunk;
            WorldConfig config = chunkView.Config;
            float size = config.VoxelSize;

            var stopwatch = Stopwatch.StartNew();
            Approximate = ChunkVolume.Approximate(chunk, size);
            Exact = ChunkVolume.Exact(chunk, config.IsoLevel, EdgeVertexPlacers.For(config.EdgePlacement), size);
            Milliseconds = stopwatch.Elapsed.TotalMilliseconds;

            Vector3Int count = chunk.VoxelCount;
            ChunkCapacity = count.x * count.y * count.z * size * size * size;
            HasMeasurement = true;
        }
    }
}
