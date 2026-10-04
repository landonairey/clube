using System.Diagnostics;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Measures the solid volume inside the target chunk after it rebuilds, both ways
    /// from Chapter 1: the approximate mean-density sum (V11) and the exact sum of
    /// tetrahedra (V12), via <see cref="ChunkVolume"/>. <see cref="ChunkStatsHud"/> and
    /// WorldLab's panel show the results. The exact sum takes milliseconds on bigger
    /// chunks, so <see cref="calculate"/> can switch the measurement off, and while a
    /// brush keeps rebuilding the chunk it's measured at most a few times a second.
    /// </summary>
    [RequireComponent(typeof(LabChunkTarget))]
    public class ChunkVolumeStats : MonoBehaviour
    {
        [Tooltip("Measure the volume after every rebuild. Off skips the work (the exact sum costs a few ms on bigger chunks) and hides the HUD line.")]
        [SerializeField]
        private bool calculate = true;

        [Tooltip("Seconds between measurements while the chunk keeps rebuilding (e.g. holding the brush).")]
        [SerializeField, Min(0f)]
        private float minInterval = 0.25f;

        private LabChunkTarget target;
        private bool measureRequested;
        private float nextMeasureTime;

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
            target = GetComponent<LabChunkTarget>();
        }

        private void OnEnable()
        {
            target.MeshRebuilt += OnMeshRebuilt;
            target.Changed += OnTargetChanged;
            RequestMeasure(immediately: true);
        }

        private void OnDisable()
        {
            target.MeshRebuilt -= OnMeshRebuilt;
            target.Changed -= OnTargetChanged;
            HasMeasurement = false;
        }

        // Toggling in the Inspector applies at once instead of waiting for the next rebuild.
        private void OnValidate()
        {
            if (target != null && isActiveAndEnabled)
            {
                RequestMeasure(immediately: true);
            }
        }

        private void LateUpdate()
        {
            if (measureRequested && Time.unscaledTime >= nextMeasureTime)
            {
                MeasureIfWanted();
            }
        }

        private void OnMeshRebuilt(Mesh mesh)
        {
            RequestMeasure(immediately: false);
        }

        // Another chunk's figures don't apply: measure the new one straight away.
        private void OnTargetChanged()
        {
            HasMeasurement = false;
            RequestMeasure(immediately: true);
        }

        private void RequestMeasure(bool immediately)
        {
            measureRequested = true;
            if (immediately)
            {
                nextMeasureTime = 0f;
            }
        }

        private void MeasureIfWanted()
        {
            measureRequested = false;
            if (calculate && target.Chunk != null)
            {
                Measure();
                nextMeasureTime = Time.unscaledTime + minInterval;
            }
            else
            {
                HasMeasurement = false;
            }
        }

        private void Measure()
        {
            Chunk chunk = target.Chunk;
            ChunkMeshSettings settings = target.MeshSettings;
            float size = settings.VoxelSize;

            var stopwatch = Stopwatch.StartNew();
            Approximate = ChunkVolume.Approximate(chunk, size);
            Exact = ChunkVolume.Exact(chunk, settings.IsoLevel, EdgeVertexPlacers.For(settings.EdgePlacement), size);
            Milliseconds = stopwatch.Elapsed.TotalMilliseconds;

            Vector3Int count = chunk.VoxelCount;
            ChunkCapacity = count.x * count.y * count.z * size * size * size;
            HasMeasurement = true;
        }
    }
}
