using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Owns one <see cref="Chunk"/> and keeps its mesh in sync: whenever the chunk
    /// is dirty (an edit, or a config change), the mesh is rebuilt once at the end
    /// of the frame (G1). The chunk's sample (0,0,0) sits at this transform's origin.
    /// The single-chunk labs use it; a world of chunks uses <see cref="WorldView"/>.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ChunkView : MonoBehaviour, IEditableTerrain, IRenderedChunk
    {
        [SerializeField]
        private WorldConfig config;

        private ChunkMeshBuilder meshBuilder;

        // Play mode works on a copy, so Inspector tweaks during Play revert on
        // exit like scene values do, instead of being saved into the asset.
        private WorldConfig runtimeConfig;

        /// <summary>
        /// Raised after the mesh is rebuilt, with normals and bounds already set.
        /// Lab tools hook in here (A4) rather than the core calling into them.
        /// </summary>
        public event Action<Mesh> MeshRebuilt;

        /// <summary>
        /// Raised when the chunk is replaced by a new, empty one because the
        /// config's chunk size changed (K1). Whatever filled the old chunk refills this one.
        /// </summary>
        /// <remarks>Not raised when the chunk is first created in Awake.</remarks>
        public event Action<Chunk> ChunkChanged;

        /// <summary>The chunk this view renders. Created in Awake; null outside Play mode.</summary>
        public Chunk Chunk { get; private set; }

        /// <summary>Counts and timings from the last mesh build (K4).</summary>
        public ChunkMeshStats LastBuildStats => meshBuilder != null ? meshBuilder.LastStats : default;

        /// <summary>The config in use: the Play mode copy while playing, otherwise the assigned asset.</summary>
        public WorldConfig Config => runtimeConfig != null ? runtimeConfig : config;

        /// <summary>True while <see cref="Config"/> is a Play mode copy whose edits are discarded on exit.</summary>
        public bool IsUsingRuntimeConfig => runtimeConfig != null;

        public bool IsReady => Chunk != null;

        public Transform Transform => transform;

        public ChunkMeshSettings MeshSettings => Config.MeshSettings;

        public Mesh Mesh => meshBuilder?.Mesh;

        public Renderer Renderer => GetComponent<MeshRenderer>();

        /// <summary>Writes a sample through <see cref="Chunk.SetDensity"/> (A7).</summary>
        public void SetDensity(Vector3Int sample, float density)
        {
            Chunk?.SetDensity(sample, density);
        }

        /// <summary>Where a world-space ray first meets the chunk's surface (<see cref="SurfaceRaycast"/>).</summary>
        public bool Raycast(Ray worldRay, out Vector3 worldPoint)
        {
            worldPoint = default;
            if (Chunk == null)
            {
                return false;
            }

            var localRay = new Ray(
                transform.InverseTransformPoint(worldRay.origin),
                transform.InverseTransformDirection(worldRay.direction));
            if (!SurfaceRaycast.Cast(localRay, Chunk, Config.MeshSettings, out _, out Vector3 localPoint))
            {
                return false;
            }
            worldPoint = transform.TransformPoint(localPoint);
            return true;
        }

        public float VoxelSize => Config.VoxelSize;

        /// <summary>Applies a brush around a world-space centre through <see cref="TerrainBrush"/> (A7).</summary>
        public BrushResult ApplyBrush(Vector3 worldCentre, BrushSettings brush, BrushOperation operation)
        {
            return Chunk != null
                ? TerrainBrush.Apply(Chunk, transform.InverseTransformPoint(worldCentre), Config.VoxelSize, brush, operation)
                : default;
        }

        private void Awake()
        {
            if (config == null)
            {
                UnityEngine.Debug.LogError($"{nameof(ChunkView)} on '{name}' has no {nameof(WorldConfig)} assigned.", this);
                enabled = false;
                return;
            }

            runtimeConfig = Instantiate(config);
            runtimeConfig.name = $"{config.name} (Play mode copy)";

            Chunk = new Chunk(Config.ChunkSize);
            meshBuilder = new ChunkMeshBuilder("Chunk");
            GetComponent<MeshFilter>().sharedMesh = meshBuilder.Mesh;
        }

        private void OnEnable()
        {
            if (Config != null)
            {
                Config.Changed += OnConfigChanged;
            }
        }

        private void OnDisable()
        {
            if (Config != null)
            {
                Config.Changed -= OnConfigChanged;
            }
        }

        private void LateUpdate()
        {
            if (Chunk.IsDirty)
            {
                meshBuilder.Build(Chunk, Config.MeshSettings);
                MeshRebuilt?.Invoke(meshBuilder.Mesh);
            }
        }

        private void OnDestroy()
        {
            meshBuilder?.Dispose();
            if (runtimeConfig != null)
            {
                Destroy(runtimeConfig);
            }
        }

        private void OnConfigChanged()
        {
            if (Chunk == null)
            {
                return;
            }

            // A new size means new storage: the old densities can't be kept (K1).
            if (Chunk.VoxelCount != Config.ChunkSize)
            {
                Chunk = new Chunk(Config.ChunkSize);
                ChunkChanged?.Invoke(Chunk);
            }

            Chunk.MarkDirty();
        }
    }
}
